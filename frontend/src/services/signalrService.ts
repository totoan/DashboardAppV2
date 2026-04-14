import * as signalR from "@microsoft/signalr";
import type { SystemUsage } from "../models/systemUsage";

let connection: signalR.HubConnection | null = null;

export async function startMetricsConnection(
    onMetricsRecevied: (data: SystemUsage) => void
): Promise<void> {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5185/metricsHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMetrics", (data: SystemUsage) => {
        onMetricsRecevied(data);
    });

    await connection.start();
}