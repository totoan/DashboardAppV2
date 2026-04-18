import * as signalR from "@microsoft/signalr";
import type { SystemUsage } from "../models/systemUsage";
import type { SubscriptionVideo } from "../models/youtubeUploads";

let connection: signalR.HubConnection | null = null;

export async function startMetricsConnection(
    onMetricsReceived: (data: SystemUsage) => void,
    onUploadsReceived: (data: SubscriptionVideo[]) => void,
): Promise<void> {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5185/metricsHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMetrics", (data: SystemUsage) => {
        onMetricsReceived(data);
    });

    connection.on("ReceiveUploads", (data: SubscriptionVideo[]) => {
        onUploadsReceived(data);
    });

    await connection.start();
}

export async function refreshYouTubeUploads(): Promise<void> {
    const response = await fetch("http://localhost:5185/api/youtube/refresh", {
        method: "POST",
    });

    if (!response.ok) {
        throw new Error("Failed to refresh YouTube uploads.");
    }
}