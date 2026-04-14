import { useEffect, useState } from "react";
import MetricTile from "./components/MetricTile";
import { startMetricsConnection } from "./services/signalrService";
import type { SystemUsage } from "./models/systemUsage";

function App() {
  const [usage, setUsage] = useState<SystemUsage>({
    cpu: 0,
    gpu: 0,
    ram: 0,
    networkIn: 0,
    networkOut: 0,
  });

  useEffect(() => {
    const connect = async (): Promise<void> => {
      await startMetricsConnection((data) => {
        setUsage(data);
      });
    };

    connect();
  }, []);

  return (
    <div style={{ padding: "20px", backgroundColor: "#111827", minHeight: "100vh" }}>
      <h1 style={{ color: "white" }}>Dashboard</h1>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" }}>
        <MetricTile title="CPU" value={`${usage.cpu.toFixed(1)}%`} />
        <MetricTile title="GPU" value={`${usage.gpu.toFixed(1)}%`} />
      </div>
    </div>
  );
}

export default App;