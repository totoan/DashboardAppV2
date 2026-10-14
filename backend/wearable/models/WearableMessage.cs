namespace DashboardBackend.Wearable.Models;

// These messages are shared by the dashboard and the Python wearable client.
public sealed class WearableMessage
{
    public string Type { get; set; } = "";
    public string Key { get; set; } = "";
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Message { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Query { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public string PageToken { get; set; } = "";
    public string VideoId { get; set; } = "";
    public int? Volume { get; set; }
    public double? SeekTime { get; set; }
    public bool? Fullscreen { get; set; }
    public int? Position { get; set; }
    public bool PlayerConnected { get; set; }
    public object? Data { get; set; }
    public bool WearableConnected { get; set; }
    public bool DashboardConnected { get; set; }
}
