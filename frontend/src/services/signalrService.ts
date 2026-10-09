import * as signalR from "@microsoft/signalr";
import type { SystemUsage } from "../models/systemUsage";
import type { SubscriptionVideo } from "../models/youtubeUploads";

let connection: signalR.HubConnection | null = null;

export async function startMetricsConnection(
    onMetricsReceived: (data: SystemUsage) => void
): Promise<void> {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5185/metricsHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMetrics", (data: SystemUsage) => {
        onMetricsReceived(data);
    });

    while (true) {
        try {
            await connection.start();

            console.log("[SignalR] Metrics connected.");
            break;
        } catch (error) {
            console.log(
                "[SignalR] Backend not ready. Retrying in 2 seconds..."
            );

            await new Promise((resolve) =>
                setTimeout(resolve, 2000)
            );
        }
    }
}

export async function refreshYouTubeUploads(): Promise<void> {
    const response = await fetch("http://localhost:5185/api/youtube/refresh", {
        method: "POST",
    });

    if (!response.ok) {
        throw new Error("Failed to refresh YouTube uploads.");
    }
}

export async function getYouTubeUploads(): Promise<SubscriptionVideo[]> {
    const response = await fetch(
        "http://localhost:5185/api/youtube/uploads"
    );

    if (!response.ok) {
        throw new Error("Failed to get YouTube uploads.");
    }

    return await response.json();
}

export async function setCurrentState(state: string): Promise<void> {
    const response = await fetch("http://localhost:5185/api/state/update", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify({ state }),
    });

    if (!response.ok) {
        throw new Error("Failed to update state.");
    }
}