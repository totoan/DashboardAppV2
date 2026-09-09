import { useEffect, useState } from "react";
import DigitalClock from "./components/DigitalClock";
import Calendar from "./components/Calendar";
import MetricTile from "./components/MetricTile";
import NetworkTile from "./components/NetworkTile";
import StorageTile from "./components/StorageTile";
import YouTubeTile from "./components/YouTubeTile";
import ImageSlideshow from "./components/ImageSlideshow";

import { startMetricsConnection, refreshYouTubeUploads } from "./services/signalrService";

import type { SystemUsage } from "./models/systemUsage";
import type { SubscriptionVideo } from "./models/youtubeUploads";

type LeftPanel = "metrics" | "images"

function App() {
  const [usage, setUsage] = useState<SystemUsage>({
    cpu: 0,
    gpu: 0,
    ram: 0,
    networkIn: 0,
    networkOut: 0,
    storage: []
  });

  const [uploads, setUploads] = useState<SubscriptionVideo[]>([]);

  const [LeftPanel, setLeftPanel] = useState<LeftPanel>("images");

  useEffect(() => {
    const connect = async (): Promise<void> => {
      await startMetricsConnection(
        (metrics) => {
        setUsage(metrics);
        },
        (videos) => {
        setUploads(videos);
        }
      );
    };

    connect();
  }, []);

  const handleRefreshYouTube = async (): Promise<void> => {
    try {
      await refreshYouTubeUploads();
    } catch (error) {
      console.error(error);
    }
  };

  return (
    <div className="window" style={{ padding: "20px", backgroundColor: "#1b1616", minHeight: "100vh" }}>

      <div className="title-bar" style={{ display: "grid", gridTemplateColumns: "1fr 1.5fr 1fr" }}>
        <DigitalClock/>
        <h1 style={{ color: "white" }}>Dashboard</h1>
        <Calendar/>
      </div>
      
      <div className="main-layout" style={{ display: "grid", gridTemplateColumns: "45px 1.5fr 1fr", gap: "16px" }}>

        {/* LEFT TAB RAIL */}
        <div style={{display: "flex", flexDirection: "column", gap: "8px", alignItems: "center"}}>
          {/* TAB BUTTONS */}
          <button
            onClick={() => setLeftPanel("metrics")}
            style={{width: "40px", height: "110px", cursor: "pointer", backgroundColor: LeftPanel === "metrics" ? "#444" : "#242020", color: "white", border: "1px solid #666", borderRadius: "6px", writingMode: "vertical-rl", transform: "rotate(180deg)"}}>
              Metrics
            </button>
          <button
            onClick={() => setLeftPanel("images")}
            style={{width: "40px", height: "110px", cursor: "pointer", backgroundColor: LeftPanel === "images" ? "#444" : "#242020", color: "white", border: "1px solid #666", borderRadius: "6px", writingMode: "vertical-rl", transform: "rotate(180deg)"}}>
            Images
          </button>
        </div>
        
        {/* LEFT SIDE */}
        <section>
          {/* SWITCHABLE LEFT PANEL */}
          <div style={{display: LeftPanel === "metrics" ? "block" : "none"}}>
            <div className="metrics-panel">
              <div style={{ display: "grid", gridTemplateRows: "1fr 1fr" }}>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" }}>
                  <MetricTile title="CPU" value={`${usage.cpu.toFixed(1)}%`} />
                  <MetricTile title="GPU" value={`${usage.gpu.toFixed(1)}%`} />
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1.2fr 1.5fr", gap: "16px", margin: "16px"}}>
                  <MetricTile title="Memory" value={`${usage.ram.toFixed(1)}%`} />
                  <NetworkTile title="Network" valueOut={`${(usage.networkOut / 1000).toFixed(1)} kB/s`} valueIn={`${(usage.networkIn / 1000).toFixed(1)} kB/s`} />
                  <StorageTile title="Storage" drives={usage.storage} />
                </div>
              </div>
            </div>
          </div>
          <div style={{display: LeftPanel === "images" ? "block" : "none"}}>
            <ImageSlideshow />
          </div>
        </section>

        {/* RIGHT SIDE - ALWAYS VISIBLE */}
        <YouTubeTile uploads={uploads} onRefresh={handleRefreshYouTube}/>
      </div>
    </div>
  );
}

export default App;