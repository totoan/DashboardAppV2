using System.Text.Json;
using System.Text.RegularExpressions;

namespace DashboardBackend.Wearable.Models;

// Only these fields can cross from the Chrome controller to the wearable UI.
public sealed class YouTubePlaybackState
{
    public bool PlayerConnected { get; set; }
    public bool TabSelected { get; set; }
    public bool Ready { get; set; }
    public string VideoId { get; set; } = "";
    public string Title { get; set; } = "";
    public string ChannelTitle { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public bool Paused { get; set; } = true;
    public bool CanNext { get; set; }
    public int Volume { get; set; } = 100;
    public bool Muted { get; set; }
    public bool CanSeek { get; set; }
    public bool Fullscreen { get; set; }
    public bool IsAd { get; set; }
    public string Message { get; set; } = "Chrome controller disconnected.";

    public static YouTubePlaybackState Read(object? data)
    {
        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Invalid playback state.");
        }

        var state = element.Deserialize<YouTubePlaybackState>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Missing playback state.");

        state.PlayerConnected = true;
        state.VideoId = Limited(state.VideoId, 20);
        state.Title = Limited(state.Title, 300);
        state.ChannelTitle = Limited(state.ChannelTitle, 150);
        state.PlaylistId = Limited(state.PlaylistId, 100);
        state.Message = Limited(state.Message, 300);
        state.CurrentTime = double.IsFinite(state.CurrentTime) ? Math.Max(0, state.CurrentTime) : 0;
        state.Duration = double.IsFinite(state.Duration) ? Math.Max(0, state.Duration) : 0;
        state.Volume = Math.Clamp(state.Volume, 0, 100);
        state.CanSeek = state.TabSelected && state.Ready && !state.IsAd && state.Duration > 0 && state.CanSeek;
        state.Ready = state.TabSelected && state.Ready;
        state.CanNext = state.Ready && !state.IsAd && state.CanNext;

        // Derive the thumbnail from the video ID instead of trusting an arbitrary URL.
        state.ThumbnailUrl = Regex.IsMatch(state.VideoId, @"^[A-Za-z0-9_-]{11}$")
            ? $"https://i.ytimg.com/vi/{state.VideoId}/mqdefault.jpg"
            : "";

        return state;
    }

    private static string Limited(string? value, int maximum)
    {
        string text = value ?? "";
        return text.Length > maximum ? text[..maximum] : text;
    }
}
