export class ControllerConnection {
    constructor(onCommand, onStatus, onPaired) {
        this.onCommand = onCommand;
        this.onStatus = onStatus;
        this.onPaired = onPaired;
        this.socket = null;
        this.retryTimer = null;
        this.heartbeatTimer = null;
        this.deadlineTimer = null;
        this.enabled = false;
        this.paired = false;
        this.commandBusy = false;
        this.commandAbort = null;
    }

    configure(address, key) {
        this.stop();
        this.address = address;
        this.key = key;
        this.enabled = Boolean(key);
        if (this.enabled) this.connect();
    }

    connect() {
        if (!this.enabled || this.socket) return;
        this.onStatus("Connecting to dashboard backend…");

        try {
            const url = new URL(this.address);
            if (!["ws:", "wss:"].includes(url.protocol) || url.pathname !== "/wearable/socket"
                || url.username || url.password || url.search || url.hash) {
                throw new Error("Invalid backend address.");
            }
            this.socket = new WebSocket(url);
        } catch {
            this.enabled = false;
            this.onStatus("Use ws://localhost:5186/wearable/socket.");
            return;
        }

        const socket = this.socket;
        this.deadlineTimer = setTimeout(() => socket.close(), 8000);
        socket.onopen = () => socket.send(JSON.stringify({
            type: "pair",
            role: "player",
            name: "Chrome YouTube controller",
            key: this.key
        }));

        socket.onmessage = event => {
            if (socket !== this.socket) return;
            let message;
            try {
                message = JSON.parse(event.data);
            } catch {
                return;
            }

            if (message?.type === "welcome") {
                this.paired = true;
                clearTimeout(this.deadlineTimer);
                this.onStatus("Connected to dashboard backend");
                // WebSocket traffic keeps a Manifest V3 worker alive in Chrome 116+.
                this.heartbeatTimer = setInterval(() => this.send({ type: "ping" }), 10000);
                this.onPaired();
            } else if (message?.type === "playbackCommand" && this.paired) {
                void this.execute(message, socket);
            }
        };

        socket.onclose = event => {
            if (socket !== this.socket) return;
            this.socket = null;
            this.paired = false;
            this.commandAbort?.abort();
            clearInterval(this.heartbeatTimer);
            clearTimeout(this.deadlineTimer);

            if (!this.enabled) return;
            if (event.code === 1008) {
                this.enabled = false;
                this.onStatus(event.reason || "Pairing rejected. Check the key.");
            } else {
                this.onStatus("Backend disconnected — reconnecting…");
                this.retryTimer = setTimeout(() => this.connect(), 3000);
            }
        };
        socket.onerror = () => {};
    }

    async execute(command, socket) {
        if (this.commandBusy) {
            this.send({ type: "playbackReply", requestId: command.requestId, message: "Chrome is still handling the previous command." });
            return;
        }
        this.commandBusy = true;
        this.commandAbort = new AbortController();
        try {
            const state = await this.onCommand(command, this.commandAbort.signal);
            if (socket === this.socket && this.paired) {
                this.send({ type: "playbackReply", requestId: command.requestId, data: state });
            }
        } catch (error) {
            if (socket === this.socket && this.paired) {
                this.send({ type: "playbackReply", requestId: command.requestId, message: String(error.message || "Playback failed.").slice(0, 300) });
            }
        } finally {
            this.commandBusy = false;
            this.commandAbort = null;
        }
    }

    send(message) {
        if (this.socket?.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify(message));
        }
    }

    stop() {
        this.enabled = false;
        this.paired = false;
        this.commandAbort?.abort();
        clearTimeout(this.retryTimer);
        clearTimeout(this.deadlineTimer);
        clearInterval(this.heartbeatTimer);
        const socket = this.socket;
        this.socket = null;
        socket?.close();
    }
}
