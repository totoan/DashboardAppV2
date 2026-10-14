using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardBackend.Wearable.Models;

namespace DashboardBackend.Wearable.Services;

// This dedicated listener exposes only wearable routes, not the dashboard APIs.
public sealed class WearableServer(IHostEnvironment environment, ILogger<WearableServer> logger, WearableYouTubeService youtube) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = WearableSettings.Load(environment.ContentRootPath);
        var connections = new WearableConnections(youtube);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = environment.ContentRootPath
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{settings.Port}");
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 24 * 1024 * 1024);
        await using var server = builder.Build();
        var sockets = new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(15),
            KeepAliveTimeout = TimeSpan.FromSeconds(10)
        };

        // Origins are checked at the wearable endpoint. Chrome extension IDs
        // are installation-specific; those clients must still provide the key.

        server.UseWebSockets(sockets);
        server.MapGet("/wearable/health", () => Results.Ok(new { service = "wearable", version = 1 }));
        server.Map("/wearable/socket", context => ConnectAsync(context, settings, connections));
        var speech = new WearableSpeechService(environment.ContentRootPath);
        server.MapPost("/wearable/speech", async (HttpContext context) =>
        {
            string authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal)
                || !KeyMatches(authorization[7..], settings.PairingKey))
                return Results.Unauthorized();
            if (context.Request.ContentType != "audio/wav"
                || context.Request.ContentLength is null or <= 44 or > 25165824)
                return Results.BadRequest(new { error = "Expected a bounded WAV recording." });
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, context.RequestAborted);
            byte[] audio = body.ToArray();
            if (audio.Length <= 44 || !audio.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                || !audio.AsSpan(8, 4).SequenceEqual("WAVE"u8))
                return Results.BadRequest(new { error = "Invalid WAV recording." });
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            string? text = await speech.TranscribeAsync(audio, deadline.Token);
            return text is null ? Results.StatusCode(503) : Results.Ok(new { text, engine = "pc-whisper" });
        });

        logger.LogInformation("Wearable listener uses port {Port}. Pairing settings: wearable/local/settings.json", settings.Port);
        await server.StartAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal dashboard shutdown.
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    private static async Task ConnectAsync(HttpContext context, WearableSettings settings, WearableConnections connections)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string origin = context.Request.Headers.Origin.ToString();
        bool extensionOrigin = Regex.IsMatch(origin, @"^chrome-extension://[a-p]{32}$");
        if (!string.IsNullOrEmpty(origin) && !extensionOrigin
            && !settings.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        WearablePeer? peer = null;

        try
        {
            using var pairingDeadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            pairingDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            var pair = await ReceiveAsync(socket, pairingDeadline.Token);

            if (pair is null || pair.Type != "pair"
                || pair.Role is not ("wearable" or "dashboard" or "player")
                || pair.Name.Length is < 1 or > 60
                || !KeyMatches(pair.Key, settings.PairingKey))
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Pairing rejected.", context.RequestAborted);
                return;
            }

            peer = new WearablePeer(pair.Role, pair.Name, socket);
            await connections.AddAsync(peer, context.RequestAborted);

            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveAsync(socket, context.RequestAborted);
                if (message is null)
                {
                    break;
                }

                await connections.HandleAsync(peer, message, context.RequestAborted);
            }

            if (socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Disconnected.", context.RequestAborted);
            }
        }
        catch (Exception error) when (
            error is WebSocketException or OperationCanceledException
                or JsonException or InvalidDataException or ArgumentException)
        {
            socket.Abort();
        }
        finally
        {
            if (peer is not null)
            {
                using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    await connections.RemoveAsync(peer, cleanupDeadline.Token);
                }
                catch (OperationCanceledException)
                {
                    // A stalled peer must not prevent this request from ending.
                }
            }
        }
    }

    private static bool KeyMatches(string supplied, string expected)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected)
        );
    }

    private static async Task<WearableMessage?> ReceiveAsync(WebSocket socket, CancellationToken cancellation)
    {
        byte[] buffer = new byte[8192];
        int length = 0;
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), cancellation);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            length += result.Count;
            if (result.MessageType != WebSocketMessageType.Text || length >= buffer.Length)
            {
                throw new InvalidDataException("Only small JSON text messages are accepted.");
            }
        }
        while (!result.EndOfMessage);

        var message = JsonSerializer.Deserialize<WearableMessage>(buffer.AsSpan(0, length), WearableConnections.JsonOptions)
            ?? throw new InvalidDataException("Empty message.");

        // JSON can contain explicit nulls even for non-nullable C# properties.
        message.Type ??= "";
        message.Key ??= "";
        message.Role ??= "";
        message.Name ??= "";
        message.RequestId ??= "";
        message.Message ??= "";
        message.Operation ??= "";
        message.Query ??= "";
        message.ChannelId ??= "";
        message.PlaylistId ??= "";
        message.PageToken ??= "";
        message.VideoId ??= "";
        return message;
    }
}
