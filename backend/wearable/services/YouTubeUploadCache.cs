using DashboardApp;

namespace DashboardBackend.Wearable.Services;

// The dashboard's existing 15-minute refresh publishes a snapshot here.
// Readers on either listener get the same completed refresh, never a partial list.
public sealed class YouTubeUploadCache
{
    public sealed record Snapshot(IReadOnlyList<SubscriptionVideo> Videos, DateTimeOffset? UpdatedAt);
    private Snapshot _snapshot = new([], null);

    public Snapshot Read()
    {
        return Volatile.Read(ref _snapshot);
    }

    public void Update(IEnumerable<SubscriptionVideo> videos)
    {
        var snapshot = new Snapshot(videos.ToArray(), DateTimeOffset.UtcNow);
        Volatile.Write(ref _snapshot, snapshot);
    }
}
