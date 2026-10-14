import { useEffect, useRef, useState } from "react";
import { WearableConnection } from "../services/wearableConnection";
import type { WearableState } from "../models/wearableMessage";
import "../wearable.css";

const defaultAddress = "ws://localhost:5186/wearable/socket";

function WearablePanel() {
    const [address, setAddress] = useState(() => localStorage.getItem("wearable.address") ?? defaultAddress);
    const [key, setKey] = useState(() => localStorage.getItem("wearable.key") ?? "");
    const [state, setState] = useState<WearableState>({
        connected: false,
        wearableConnected: false,
        playerConnected: false,
        playbackTitle: "",
        status: "Not paired",
        lastMessage: "",
        testPending: false
    });

    const connection = useRef<WearableConnection | null>(null);

    useEffect(() => {
        // Reuse the pairing details after a dashboard reload.
        const savedKey = localStorage.getItem("wearable.key");
        const savedAddress = localStorage.getItem("wearable.address") ?? defaultAddress;

        if (savedKey) {
            const client = new WearableConnection(savedAddress, savedKey, setState);
            connection.current = client;
            client.start();
        }

        return () => connection.current?.stop();
    }, []);

    const connect = () => {
        connection.current?.stop();
        const client = new WearableConnection(address.trim(), key.trim(), setState);
        localStorage.setItem("wearable.address", address.trim());
        localStorage.setItem("wearable.key", key.trim());
        connection.current = client;
        client.start();
    };

    const disconnect = () => {
        connection.current?.stop();
        localStorage.removeItem("wearable.key");
        setKey("");
        setState({ connected: false, wearableConnected: false, playerConnected: false, playbackTitle: "", status: "Not paired", lastMessage: "", testPending: false });
    };

    return (
        <section className="wearable-panel" aria-label="Wearable connection">
            <h2>Wearable</h2>
            <div className="wearable-status" role="status">
                <span>{state.status}</span>
                <strong>{state.wearableConnected ? "Wearable connected" : "Wearable disconnected"}</strong>
                <span>{state.playerConnected ? "Chrome controller connected" : "Chrome controller disconnected"}</span>
            </div>

            {state.playbackTitle && <p>{state.playbackTitle}</p>}

            <div className="wearable-settings">
                <label>
                    Backend address
                    <input value={address} onChange={event => setAddress(event.target.value)} />
                </label>
                <label>
                    Pairing key
                    <input type="password" value={key} onChange={event => setKey(event.target.value)} autoComplete="off" />
                </label>
                <button onClick={connect} disabled={!key.trim()}>Connect</button>
                <button onClick={disconnect}>Forget pairing</button>
                <button
                    onClick={() => connection.current?.sendTest()}
                    disabled={!state.connected || !state.wearableConnected || state.testPending}
                >
                    {state.testPending ? "Waiting…" : "Test wearable"}
                </button>
            </div>

            <p className="wearable-message" aria-live="polite">{state.lastMessage || "Pair both sides to test the connection."}</p>
        </section>
    );
}

export default WearablePanel;
