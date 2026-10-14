import type { WearableMessage, WearableState } from "../models/wearableMessage";

// A connection instance belongs to one mounted dashboard panel.
export class WearableConnection {
    private socket: WebSocket | null = null;
    private retryTimer: ReturnType<typeof setTimeout> | null = null;
    private testTimer: ReturnType<typeof setTimeout> | null = null;
    private connectionTimer: ReturnType<typeof setTimeout> | null = null;
    private pendingRequest = "";
    private stopped = true;
    private state: WearableState = {
        connected: false,
        wearableConnected: false,
        playerConnected: false,
        playbackTitle: "",
        status: "Not paired",
        lastMessage: "",
        testPending: false
    };

    private readonly address: string;
    private readonly key: string;
    private readonly onState: (state: WearableState) => void;

    constructor(address: string, key: string, onState: (state: WearableState) => void) {
        this.address = address;
        this.key = key;
        this.onState = onState;
    }

    start(): void {
        this.stopped = false;
        this.connect();
    }

    stop(): void {
        this.stopped = true;
        this.clearTimers();
        this.socket?.close();
        this.socket = null;
    }

    sendTest(): void {
        if (!this.state.connected || !this.state.wearableConnected || this.state.testPending) {
            return;
        }

        this.pendingRequest = crypto.randomUUID();
        this.send({ type: "test", requestId: this.pendingRequest, message: "Hello from the dashboard." });
        this.update({ testPending: true, lastMessage: "Waiting for wearable acknowledgement…" });

        this.testTimer = setTimeout(() => {
            this.pendingRequest = "";
            this.update({ testPending: false, lastMessage: "No acknowledgement received. Try again." });
        }, 5000);
    }

    private connect(): void {
        if (this.stopped) {
            return;
        }

        this.update({ connected: false, wearableConnected: false, status: "Connecting…" });
        let socket: WebSocket;

        try {
            const url = new URL(this.address);
            if (!["ws:", "wss:"].includes(url.protocol)) {
                throw new Error("Use a ws:// or wss:// address.");
            }
            socket = new WebSocket(url);
        } catch {
            this.stopped = true;
            this.update({ status: "Invalid or blocked WebSocket address." });
            return;
        }

        this.socket = socket;
        this.connectionTimer = setTimeout(() => socket.close(), 8000);

        socket.onopen = () => {
            socket.send(JSON.stringify({
                type: "pair",
                role: "dashboard",
                name: "PC dashboard",
                key: this.key
            }));
        };

        socket.onmessage = event => {
            if (this.stopped || this.socket !== socket) {
                return;
            }

            try {
                const message = JSON.parse(event.data) as WearableMessage;
                this.receive(message);
            } catch {
                this.update({ lastMessage: "An invalid message was ignored." });
            }
        };

        socket.onclose = event => {
            if (this.stopped || this.socket !== socket) {
                return;
            }

            this.clearTimers();
            this.pendingRequest = "";
            this.update({ connected: false, wearableConnected: false, playerConnected: false, playbackTitle: "", testPending: false });

            if (event.code === 1008) {
                this.stopped = true;
                this.update({ status: "Pairing rejected. Check the key and reconnect." });
            } else {
                this.update({ status: "Backend disconnected — retrying…" });
                this.retryTimer = setTimeout(() => this.connect(), 3000);
            }
        };

        // The close event handles reporting and reconnecting after network errors.
        socket.onerror = () => {};
    }

    private receive(message: WearableMessage): void {
        switch (message.type) {
            case "welcome":
                if (this.connectionTimer) {
                    clearTimeout(this.connectionTimer);
                }
                this.update({ connected: true, status: "Backend connected" });
                break;

            case "status":
                this.update({
                    wearableConnected: Boolean(message.wearableConnected),
                    playerConnected: Boolean(message.playerConnected)
                });
                break;

            case "playbackState":
                this.update({
                    playerConnected: Boolean(message.data?.playerConnected),
                    playbackTitle: message.data?.title || message.data?.message || ""
                });
                break;

            case "test":
                this.update({ lastMessage: message.message ?? "Wearable test received." });
                this.send({ type: "ack", requestId: message.requestId });
                break;

            case "ack":
            case "error":
                if (message.requestId === this.pendingRequest) {
                    if (this.testTimer) {
                        clearTimeout(this.testTimer);
                    }
                    this.pendingRequest = "";
                    this.update({ testPending: false, lastMessage: message.message ?? message.type });
                }
                break;
        }
    }

    private send(message: WearableMessage): void {
        if (this.socket?.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify(message));
        }
    }

    private update(change: Partial<WearableState>): void {
        this.state = { ...this.state, ...change };
        this.onState({ ...this.state });
    }

    private clearTimers(): void {
        if (this.retryTimer) clearTimeout(this.retryTimer);
        if (this.testTimer) clearTimeout(this.testTimer);
        if (this.connectionTimer) clearTimeout(this.connectionTimer);
    }
}
