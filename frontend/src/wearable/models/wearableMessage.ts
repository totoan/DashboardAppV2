export interface WearableMessage {
    type: string;
    requestId?: string;
    message?: string;
    wearableConnected?: boolean;
    dashboardConnected?: boolean;
    playerConnected?: boolean;
    data?: {
        playerConnected?: boolean;
        title?: string;
        message?: string;
    };
}

export interface WearableState {
    connected: boolean;
    wearableConnected: boolean;
    playerConnected: boolean;
    playbackTitle: string;
    status: string;
    lastMessage: string;
    testPending: boolean;
}
