namespace DashboardApp;


// -- Subscriptions --
public class SubscriptionsResponse
{
    public List<SubscriptionItems> items { get; set; } = new();
}

public class SubscriptionItems
{
    public Snippet snippet { get; set; } = new();
}

public class Snippet
{
    public string title { get; set; } = "";
    public ResourceId resourceId { get; set; } = new();
}

public class ResourceId
{
    public string channelId { get; set; } = "";
}


// -- Channels --
public class ChannelResponse
{
    public List<ChannelItem> items { get; set; } = new();
}

public class ChannelItem
{
    public ContentDetails contentDetails { get; set; } = new();
}

public class ContentDetails
{
    public RelatedPlaylists relatedPlaylists { get; set; } = new();
}

public class RelatedPlaylists
{
    public string uploads { get; set; } = "";
}

// -- Playlist Items --
public class SubscriptionVideo
{
    public string Title { get; set; } = "";
    public string ChannelTitle { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public DateTime PublishedAt { get; set; }
}

public class PlaylistItemsResponse
{
    public List<PlaylistItem> items { get; set; } = new();
}

public class PlaylistItem
{
    public PlaylistSnippet snippet { get; set; } = new();
}

public class PlaylistSnippet
{
    public string title { get; set; } = "";
    public string channelTitle { get; set; } = "";
    public DateTime publishedAt { get; set; }
    public PlaylistResourceId resourceId { get; set; } = new();
    public PlaylistThumbnails thumbnails { get; set; } = new();
}

public class PlaylistResourceId
{
    public string videoId { get; set; } = "";
}

public class PlaylistThumbnails
{
    public Thumbnail medium { get; set; } = new();
}

public class Thumbnail
{
    public string url { get; set; } = "";
}