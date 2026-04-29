import { useEffect, useState } from "react";
import MetricTile from "./components/MetricTile";
import NetworkTile from "./components/NetworkTile";
import StorageTile from "./components/StorageTile";
import StateSelector from "./components/StateSelector";
import YouTubeTile from "./components/YouTubeTile";

import { startMetricsConnection, refreshYouTubeUploads } from "./services/signalrService";

import type { SystemUsage } from "./models/systemUsage";
import type { SubscriptionVideo } from "./models/youtubeUploads";

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
    <div className="window" style={{ padding: "20px", backgroundColor: "#1b1616", minHeight: "100vh"}}>
      <h1 style={{ color: "white" }}>Dashboard</h1>
      <div className="main-layout" style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr" }}>
        
        <section className="metrics-panel">
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
        </section>

        <YouTubeTile uploads={uploads} onRefresh={handleRefreshYouTube}/>
      </div>
      <div>
        <StateSelector />
      </div>
    </div>
  );
}

export default App;