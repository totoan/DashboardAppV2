using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardBackend.Wearable.Models;

namespace DashboardBackend.Wearable.Services;

// Each socket has one send lock: WebSocket sends must not overlap.
public sealed class WearablePeer(string role, string name, WebSocket socket)
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Role { get; } = role;
    public string Name { get; } = name;
    public WebSocket Socket { get; } = socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public async Task SendAsync(WearableMessage message, CancellationToken cancellation)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, WearableConnections.JsonOptions);
        await _sendLock.WaitAsync(cancellation);

        try
        {
            if (Socket.State == WebSocketState.Open)
            {
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
}

public sealed class WearableConnections(WearableYouTubeService youtube)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, WearablePeer> _peers = new();
    private readonly ConcurrentDictionary<string, PendingTest> _pending = new();

    private readonly ConcurrentDictionary<string, PendingPlayback> _playbackRequests = new();
    private string? _playerId;
    private YouTubePlaybackState _playback = new();

    private sealed record PendingPlayback(string SenderId, string PlayerId, TaskCompletionSource<WearableMessage> Reply);

    private sealed record PendingTest(string SenderId, string RecipientId, DateTime Expires);

    public async Task AddAsync(WearablePeer peer, CancellationToken cancellation)
    {
        if (peer.Role == "player" && Interlocked.CompareExchange(ref _playerId, peer.Id, null) is not null)
        {
            await peer.Socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation,
                "Another Chrome controller is already connected.", cancellation);
            return;
        }

        _peers[peer.Id] = peer;
        await peer.SendAsync(new WearableMessage
        {
            Type = "welcome",
            Message = "Paired with the dashboard backend."
        }, cancellation);

        await peer.SendAsync(new WearableMessage
        {
            Type = "playbackState",
            Data = Volatile.Read(ref _playback)
        }, cancellation);

        await BroadcastStatusAsync(cancellation);
    }

    public async Task RemoveAsync(WearablePeer peer, CancellationToken cancellation)
    {
        _peers.TryRemove(peer.Id, out _);

        foreach (var entry in _playbackRequests)
        {
            if (entry.Value.PlayerId == peer.Id || entry.Value.SenderId == peer.Id)
            {
                if (_playbackRequests.TryRemove(entry.Key, out var pending))
                {
                    pending.Reply.TrySetResult(new WearableMessage
                    {
                        Type = "error",
                        Message = "Chrome controller disconnected."
                    });
                }
            }
        }

        if (Interlocked.CompareExchange(ref _playerId, null, peer.Id) == peer.Id)
        {
            Volatile.Write(ref _playback, new YouTubePlaybackState());
            await BroadcastPlaybackAsync(cancellation);
        }

        foreach (var entry in _pending)
        {
            if (entry.Value.SenderId == peer.Id || entry.Value.RecipientId == peer.Id)
            {
                _pending.TryRemove(entry.Key, out _);
            }
        }

        await BroadcastStatusAsync(cancellation);
    }

    public async Task HandleAsync(WearablePeer sender, WearableMessage message, CancellationToken cancellation)
    {
        RemoveExpiredTests();

        switch (message.Type)
        {
            case "ping":
                await sender.SendAsync(new WearableMessage { Type = "pong" }, cancellation);
                break;

            case "playbackState":
                if (sender.Id == _playerId)
                {
                    Volatile.Write(ref _playback, YouTubePlaybackState.Read(message.Data));
                    await BroadcastPlaybackAsync(cancellation);
                }
                break;

            case "playbackReply":
                if (sender.Id == _playerId
                    && _playbackRequests.TryGetValue(message.RequestId, out var playbackRequest)
                    && playbackRequest.PlayerId == sender.Id)
                {
                    playbackRequest.Reply.TrySetResult(message);
                }
                break;

            case "request":
                await RespondToRequestAsync(sender, message, cancellation);
                break;

            case "test":
                await ForwardTestAsync(sender, message, cancellation);
                break;

            case "ack":
                if (_pending.TryGetValue(message.RequestId, out var pending)
                    && pending.RecipientId == sender.Id
                    && _pending.TryRemove(message.RequestId, out _)
                    && _peers.TryGetValue(pending.SenderId, out var recipient))
                {
                    await recipient.SendAsync(new WearableMessage
                    {
                        Type = "ack",
                        RequestId = message.RequestId,
                        Message = $"Acknowledged by {sender.Name}."
                    }, cancellation);
                }
                break;

            default:
                await sender.SendAsync(new WearableMessage
                {
                    Type = "error",
                    Message = "Unknown message type."
                }, cancellation);
                break;
        }
    }

    private async Task RespondToRequestAsync(WearablePeer sender, WearableMessage request, CancellationToken cancellation)
    {
        if (!Guid.TryParse(request.RequestId, out _))
        {
            await sender.SendAsync(new WearableMessage
            {
                Type = "error",
                RequestId = request.RequestId,
                Message = "Invalid request ID."
            }, cancellation);
            return;
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(25));
            if (request.Operation is "youtube.play" or "youtube.pause" or "youtube.resume"
                or "youtube.next" or "youtube.playbackState"
                or "youtube.volume" or "youtube.seek" or "youtube.fullscreen")
            {
                await ControlPlaybackAsync(sender, request, deadline.Token);
                return;
            }

            var page = await youtube.BrowseAsync(request, deadline.Token);
            await sender.SendAsync(new WearableMessage
            {
                Type = "result",
                RequestId = request.RequestId,
                Data = page
            }, cancellation);
        }
        catch (Exception error) when (
            error is ArgumentException or InvalidOperationException
                or HttpRequestException or OperationCanceledException or JsonException)
        {
            if (!cancellation.IsCancellationRequested)
            {
                string reason = error is ArgumentException or InvalidOperationException
                    ? error.Message
                    : "YouTube is unavailable. Please retry.";

                await sender.SendAsync(new WearableMessage
                {
                    Type = "error",
                    RequestId = request.RequestId,
                    Message = reason
                }, cancellation);
            }
        }
    }

    private async Task ControlPlaybackAsync(WearablePeer sender, WearableMessage request, CancellationToken cancellation)
    {
        if (sender.Role == "player")
        {
            throw new ArgumentException("The Chrome controller cannot issue remote playback requests.");
        }

        if (request.Operation == "youtube.playbackState")
        {
            await sender.SendAsync(new WearableMessage
            {
                Type = "result",
                RequestId = request.RequestId,
                Data = Volatile.Read(ref _playback)
            }, cancellation);
            return;
        }

        if (request.Operation == "youtube.play"
            && (!Regex.IsMatch(request.VideoId, @"^[A-Za-z0-9_-]{11}$")
                || (!string.IsNullOrEmpty(request.PlaylistId)
                    && !Regex.IsMatch(request.PlaylistId, @"^[A-Za-z0-9_-]{10,100}$"))
                || request.Position is < 0 or > 100000))
        {
            throw new ArgumentException("Invalid video or playlist selection.");
        }

        if (request.Operation == "youtube.volume" && request.Volume is not (>= 0 and <= 100))
        {
            throw new ArgumentException("Volume must be between 0 and 100.");
        }
        if (request.Operation == "youtube.seek"
            && (request.SeekTime is null || !double.IsFinite(request.SeekTime.Value) || request.SeekTime < 0
                || !Regex.IsMatch(request.VideoId, @"^[A-Za-z0-9_-]{11}$")))
        {
            throw new ArgumentException("Choose a valid video time.");
        }
        if (request.Operation == "youtube.fullscreen" && request.Fullscreen is null)
        {
            throw new ArgumentException("Specify whether fullscreen should be enabled.");
        }

        string? playerId = _playerId;
        if (playerId is null || !_peers.TryGetValue(playerId, out var player))
        {
            throw new InvalidOperationException("Connect the Chrome controller and select a YouTube tab first.");
        }

        string forwardedId = Guid.NewGuid().ToString();
        var completion = new TaskCompletionSource<WearableMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_playbackRequests.Count >= 20)
        {
            throw new InvalidOperationException("The Chrome controller is busy. Please retry.");
        }

        _playbackRequests[forwardedId] = new PendingPlayback(sender.Id, playerId, completion);
        try
        {
            await player.SendAsync(new WearableMessage
            {
                Type = "playbackCommand",
                RequestId = forwardedId,
                Operation = request.Operation,
                VideoId = request.VideoId,
                PlaylistId = request.PlaylistId,
                Position = request.Position,
                Volume = request.Volume,
                SeekTime = request.SeekTime,
                Fullscreen = request.Fullscreen
            }, cancellation);

            WearableMessage reply;
            try
            {
                reply = await completion.Task.WaitAsync(TimeSpan.FromSeconds(18), cancellation);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("Chrome did not confirm the command. Check the selected YouTube tab.");
            }

            if (!string.IsNullOrEmpty(reply.Message))
            {
                throw new InvalidOperationException(reply.Message.Length > 300 ? reply.Message[..300] : reply.Message);
            }

            var state = YouTubePlaybackState.Read(reply.Data);
            Volatile.Write(ref _playback, state);
            await BroadcastPlaybackAsync(cancellation);
            await sender.SendAsync(new WearableMessage
            {
                Type = "result",
                RequestId = request.RequestId,
                Data = state
            }, cancellation);
        }
        finally
        {
            _playbackRequests.TryRemove(forwardedId, out _);
        }
    }

    private async Task BroadcastPlaybackAsync(CancellationToken cancellation)
    {
        var message = new WearableMessage
        {
            Type = "playbackState",
            Data = Volatile.Read(ref _playback)
        };

        foreach (var peer in _peers.Values.Where(peer => peer.Role != "player"))
        {
            try
            {
                await peer.SendAsync(message, cancellation);
            }
            catch (WebSocketException)
            {
                peer.Socket.Abort();
            }
        }
    }

    private async Task ForwardTestAsync(WearablePeer sender, WearableMessage message, CancellationToken cancellation)
    {
        string targetRole = sender.Role == "wearable" ? "dashboard" : "wearable";
        var recipient = _peers.Values.FirstOrDefault(peer => peer.Role == targetRole);

        if (!Guid.TryParse(message.RequestId, out _) || message.Message.Length > 200)
        {
            await sender.SendAsync(new WearableMessage
            {
                Type = "error",
                RequestId = message.RequestId,
                Message = "Invalid test message."
            }, cancellation);
            return;
        }

        if (recipient is null)
        {
            await sender.SendAsync(new WearableMessage
            {
                Type = "error",
                RequestId = message.RequestId,
                Message = $"The {targetRole} is disconnected."
            }, cancellation);
            return;
        }

        // Cap outstanding requests so an unresponsive client cannot grow this indefinitely.
        if (_pending.Count >= 100 || !_pending.TryAdd(message.RequestId,
            new PendingTest(sender.Id, recipient.Id, DateTime.UtcNow.AddSeconds(15))))
        {
            await sender.SendAsync(new WearableMessage
            {
                Type = "error",
                RequestId = message.RequestId,
                Message = "Please retry the test."
            }, cancellation);
            return;
        }

        await recipient.SendAsync(new WearableMessage
        {
            Type = "test",
            RequestId = message.RequestId,
            Message = $"Test received from {sender.Name}."
        }, cancellation);
    }

    private void RemoveExpiredTests()
    {
        foreach (var entry in _pending)
        {
            if (entry.Value.Expires < DateTime.UtcNow)
            {
                _pending.TryRemove(entry.Key, out _);
            }
        }
    }

    private async Task BroadcastStatusAsync(CancellationToken cancellation)
    {
        var status = new WearableMessage
        {
            Type = "status",
            WearableConnected = _peers.Values.Any(peer => peer.Role == "wearable"),
            DashboardConnected = _peers.Values.Any(peer => peer.Role == "dashboard"),
            PlayerConnected = _playerId is not null
        };

        foreach (var peer in _peers.Values)
        {
            try
            {
                await peer.SendAsync(status, cancellation);
            }
            catch (WebSocketException)
            {
                peer.Socket.Abort();
            }
        }
    }
}
