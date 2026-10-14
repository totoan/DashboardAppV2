namespace DashboardBackend.Wearable.Models;

// One list format covers uploads, search results, creator playlists, and their videos.
public sealed class YouTubeBrowseItem
{
    public string Kind { get; set; } = "video";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string ChannelTitle { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string PlaylistId { get; set; } = "";
    public int? Position { get; set; }
}

public sealed class YouTubeBrowsePage
{
    public IReadOnlyList<YouTubeBrowseItem> Items { get; set; } = [];
    public string NextPageToken { get; set; } = "";
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool Ready { get; set; } = true;
}
