using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Diagnostics;

namespace DashboardApp;

public class YouTubeService
{
    private readonly HttpClient _client;

    public YouTubeService(string accessToken)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<string> GetSubscriptionsAsync()
    {
        string url = "https://www.googleapis.com/youtube/v3/subscriptions" +
                     "?part=snippet" +
                     "&mine=true" +
                     "&maxResults=50";
        
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetChannelDetailsAsync()
    {
        string json = await GetSubscriptionsAsync();
        var data = JsonSerializer.Deserialize<SubscriptionsResponse>(json);

        if (data == null)
            throw new Exception("SubscriptionsResponse deserialized to null.");

        if (data.items == null || data.items.Count == 0)
            throw new Exception("SubscriptionsResponse.items was empy.");

        List<string> channelIds = new List<string>();

        foreach (var item in data.items)
        {
            string channelId = item.snippet.resourceId.channelId;

            if (!string.IsNullOrWhiteSpace(channelId))
            {
                channelIds.Add(channelId);
            }
        }

        if (channelIds.Count == 0)
            Debug.WriteLine("No ChannelIds were found from subscriptions.");

        string joinedIds = string.Join(",", channelIds);
        string url = "https://www.googleapis.com/youtube/v3/channels" +
        "?part=contentDetails" +
        $"&id={joinedIds}";

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<SubscriptionVideo>> GetUploadsAsync()
    {
        string json = await GetChannelDetailsAsync();
        var data = JsonSerializer.Deserialize<ChannelResponse>(json);

        if (data == null)
            throw new Exception("ChannelResponse deserialized to null.");

        if (data.items == null || data.items.Count == 0)
            throw new Exception("ChannelResponse.items was empty.");
        
        List<string> playlistIds = new List<string>();

        if (data != null)
        {
            foreach (var item in data.items)
            {
                string playlistId = item.contentDetails.relatedPlaylists.uploads;

                if (!string.IsNullOrWhiteSpace(playlistId))
                {
                    playlistIds.Add(playlistId);
                }
            }
        }
        if (playlistIds.Count == 0)
            Debug.WriteLine("No uploads playlistIds were found.");
        
        List<SubscriptionVideo> subVideos = new List<SubscriptionVideo>();
        
        foreach (string id in playlistIds)
        {
            string url = "https://www.googleapis.com/youtube/v3/playlistItems" +
                         "?part=snippet" +
                        $"&playlistId={id}" +
                         "&maxResults=5";
            
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            string playlistJson = await response.Content.ReadAsStringAsync();
            var playlistData = JsonSerializer.Deserialize<PlaylistItemsResponse>(playlistJson);

            if (playlistData == null)
                throw new Exception("PlaylistItemsResponse deserialized to null.");

            if (playlistData.items == null)
                throw new Exception($"playlistData.items was null for playlist {id}");

            if (playlistData != null)
                foreach (var item in playlistData.items)
                {
                    subVideos.Add(new SubscriptionVideo
                    {
                        Title = item.snippet.title,
                        ChannelTitle = item.snippet.channelTitle,
                        ThumbnailUrl = item.snippet.thumbnails.medium.url,
                        PublishedAt = item.snippet.publishedAt
                    });
                }
        }
        
        subVideos = subVideos.OrderByDescending(v => v.PublishedAt).ToList();
        subVideos = subVideos.Take(20).ToList();

        return subVideos;
    }
}