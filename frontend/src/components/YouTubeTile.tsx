import type { SubscriptionVideo } from "../models/youtubeUploads";

type YouTubeTileProps = {
  uploads: SubscriptionVideo[];
  onRefresh: () => Promise<void>;
};

function YouTubeTile({ uploads, onRefresh }: YouTubeTileProps) {
  return (
    <section
        className="yt-panel-wrapper"
        style={{
            margin: "16px",
            maxHeight: "300px",
            display: "grid",
            gridTemplateRows: "1fr auto",
        }}
    >
        <div
            className="youtube-panel"
            style={{
                textAlign: "left",
                whiteSpace: "nowrap",
                overflowY: "auto",
                overflowX: "hidden",
                border: "2px solid Red",
                borderRadius: "10px",
                padding: "3px"
            }}
        >
        {uploads.map((video) => (
            <div
                key={`${video.channelTitle}-${video.publishedAt}-${video.title}`}
                style={{
                display: "flex",
                gap: "12px",
                marginBottom: "5px",
                color: "white",
                }}
            >
                <img
                src={video.thumbnailUrl}
                alt={video.title}
                style={{ width: "120px", borderRadius: "8px" }}
                />

                <div>
                <div>{video.title}</div>
                <div>{video.channelTitle}</div>
                <div>{new Date(video.publishedAt).toLocaleString()}</div>
                </div>
            </div>
            ))}
        </div>

        <div>
            <button onClick={onRefresh}>YT Refresh</button>
        </div>
    </section>
  );
}

export default YouTubeTile;