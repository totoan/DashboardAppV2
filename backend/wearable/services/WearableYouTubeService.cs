using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardBackend.Wearable.Models;

namespace DashboardBackend.Wearable.Services;

public sealed class WearableYouTubeService : IDisposable
{
    private readonly YouTubeUploadCache _uploads;
    private readonly Func<string?> _getAccessToken;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly Dictionary<string, CachedPage> _pages = new();

    private sealed record CachedPage(YouTubeBrowsePage Page, DateTimeOffset Expires);

    public WearableYouTubeService(
        YouTubeUploadCache uploads,
        Func<string?> getAccessToken,
        HttpClient? client = null)
    {
        _uploads = uploads;
        _getAccessToken = getAccessToken;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<YouTubeBrowsePage> BrowseAsync(WearableMessage request, CancellationToken cancellation)
    {
        if (request.Operation == "youtube.uploads")
        {
            return ReadUploads();
        }

        // Validate before taking a lock or making any Google request.
        var parameters = GetParameters(request);
        string endpoint = request.Operation switch
        {
            "youtube.search" => "search",
            "youtube.playlists" => "playlists",
            "youtube.playlistVideos" => "playlistItems",
            _ => throw new ArgumentException("Unknown YouTube request.")
        };

        string query = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        string cacheKey = endpoint + "?" + query;

        await _requestLock.WaitAsync(cancellation);
        try
        {
            RemoveExpiredPages();
            if (_pages.TryGetValue(cacheKey, out var saved))
            {
                return saved.Page;
            }

            string? token = _getAccessToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Sign in to YouTube on the PC dashboard first.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/youtube/v3/" + cacheKey);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _client.SendAsync(message, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await DescribeErrorAsync(response, cancellation));
            }

            string json = await response.Content.ReadAsStringAsync(cancellation);
            var page = ParsePage(json, request.Operation, request.PlaylistId);

            if (_pages.Count >= 64)
            {
                _pages.Clear();
            }

            _pages[cacheKey] = new CachedPage(page, DateTimeOffset.UtcNow.AddMinutes(5));
            return page;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private YouTubeBrowsePage ReadUploads()
    {
        var snapshot = _uploads.Read();
        return new YouTubeBrowsePage
        {
            Ready = snapshot.UpdatedAt is not null,
            UpdatedAt = snapshot.UpdatedAt,
            Items = snapshot.Videos.Select(video => new YouTubeBrowseItem
            {
                Id = video.VideoId,
                Title = WebUtility.HtmlDecode(video.Title),
                ChannelId = video.ChannelId,
                ChannelTitle = WebUtility.HtmlDecode(video.ChannelTitle),
                ThumbnailUrl = video.ThumbnailUrl,
                PublishedAt = video.PublishedAt.ToUniversalTime().ToString("O")
            }).ToArray()
        };
    }

    private static Dictionary<string, string> GetParameters(WearableMessage request)
    {
        var parameters = new Dictionary<string, string>
        {
            ["part"] = "snippet",
            ["maxResults"] = "20"
        };

        if (request.PageToken.Length > 200)
        {
            throw new ArgumentException("Invalid page token.");
        }

        if (!string.IsNullOrEmpty(request.PageToken))
        {
            parameters["pageToken"] = request.PageToken;
        }

        switch (request.Operation)
        {
            case "youtube.search":
                string query = request.Query.Trim();
                if (query.Length is < 1 or > 100)
                {
                    throw new ArgumentException("Enter a search of 1 to 100 characters.");
                }
                parameters["q"] = query;
                parameters["type"] = "video,channel,playlist";
                break;

            case "youtube.playlists":
                if (!Regex.IsMatch(request.ChannelId, @"^UC[A-Za-z0-9_-]{22}$"))
                {
                    throw new ArgumentException("This item does not have a valid creator channel.");
                }
                parameters["channelId"] = request.ChannelId;
                break;

            case "youtube.playlistVideos":
                if (!Regex.IsMatch(request.PlaylistId, @"^[A-Za-z0-9_-]{10,100}$"))
                {
                    throw new ArgumentException("Invalid playlist.");
                }
                parameters["playlistId"] = request.PlaylistId;
                break;

            default:
                throw new ArgumentException("Unknown YouTube request.");
        }

        return parameters;
    }

    private static YouTubeBrowsePage ParsePage(string json, string operation, string playlistId)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = new List<YouTubeBrowseItem>();

        if (root.TryGetProperty("items", out var entries))
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("snippet", out var snippet))
                {
                    continue;
                }

                var item = new YouTubeBrowseItem
                {
                    Title = WebUtility.HtmlDecode(Text(snippet, "title")),
                    ChannelId = Text(snippet, "channelId"),
                    ChannelTitle = WebUtility.HtmlDecode(Text(snippet, "channelTitle")),
                    PublishedAt = Text(snippet, "publishedAt"),
                    ThumbnailUrl = Thumbnail(snippet)
                };

                if (operation == "youtube.search" && entry.TryGetProperty("id", out var id))
                {
                    item.Kind = Text(id, "kind").Replace("youtube#", "");
                    item.Id = Text(id, item.Kind + "Id");
                    if (item.Kind == "channel") item.ChannelId = item.Id;
                    if (item.Kind == "playlist") item.PlaylistId = item.Id;
                }
                else if (operation == "youtube.playlists")
                {
                    item.Kind = "playlist";
                    item.Id = Text(entry, "id");
                    item.PlaylistId = item.Id;
                }
                else if (operation == "youtube.playlistVideos")
                {
                    if (snippet.TryGetProperty("resourceId", out var resource))
                    {
                        item.Id = Text(resource, "videoId");
                    }

                    item.PlaylistId = playlistId;
                    string owner = Text(snippet, "videoOwnerChannelId");
                    if (!string.IsNullOrEmpty(owner)) item.ChannelId = owner;
                    string ownerName = Text(snippet, "videoOwnerChannelTitle");
                    if (!string.IsNullOrEmpty(ownerName)) item.ChannelTitle = ownerName;
                    if (snippet.TryGetProperty("position", out var position) && position.TryGetInt32(out int index))
                    {
                        item.Position = index;
                    }

                    // Unavailable videos cannot be selected or used to browse a creator.
                    if (item.Title is "Private video" or "Deleted video") continue;
                }

                if (!string.IsNullOrEmpty(item.Id) && item.Kind is "video" or "channel" or "playlist")
                {
                    items.Add(item);
                }
            }
        }

        return new YouTubeBrowsePage
        {
            Items = items,
            NextPageToken = Text(root, "nextPageToken")
        };
    }

    private static string Text(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
    }

    private static string Thumbnail(JsonElement snippet)
    {
        if (snippet.TryGetProperty("thumbnails", out var thumbnails))
        {
            foreach (string size in new[] { "medium", "default", "high" })
            {
                if (thumbnails.TryGetProperty(size, out var thumbnail))
                {
                    return Text(thumbnail, "url");
                }
            }
        }
        return "";
    }

    private static async Task<string> DescribeErrorAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "YouTube sign-in expired. Refresh YouTube on the PC dashboard and retry.";
        }

        string json = await response.Content.ReadAsStringAsync(cancellation);
        if (json.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase))
        {
            return "The YouTube API quota has been reached. Try again after it resets.";
        }

        return "YouTube could not return this list. Try again later.";
    }

    private void RemoveExpiredPages()
    {
        foreach (var entry in _pages.Where(pair => pair.Value.Expires < DateTimeOffset.UtcNow).ToArray())
        {
            _pages.Remove(entry.Key);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _requestLock.Dispose();
    }
}
