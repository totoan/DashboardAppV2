import { ControllerConnection } from "./services/connection.js";
import { YouTubeTabs } from "./services/youtube_tabs.js";

let status = "Not paired";
let lastState = null;
let publishing = false;
let ready;
const tabs = new YouTubeTabs(() => { lastState = null; });
const connection = new ControllerConnection(
    (command, signal) => tabs.execute(command, signal),
    text => { status = text; },
    () => { lastState = null; void publishState(); }
);

async function initialize() {
    await tabs.restore();
    const saved = await chrome.storage.local.get(["address", "pairingKey"]);
    connection.configure(saved.address || "ws://localhost:5186/wearable/socket", saved.pairingKey || "");
    await chrome.alarms.create("wearable-reconnect", { periodInMinutes: 0.5 });
}

async function publishState() {
    if (!connection.paired || publishing || connection.commandBusy) return;
    publishing = true;
    try {
        const state = await tabs.state();
        const signature = JSON.stringify(state);
        if (signature !== lastState) {
            connection.send({ type: "playbackState", data: state });
            lastState = signature;
        }
    } finally {
        publishing = false;
    }
}

chrome.alarms.onAlarm.addListener(async alarm => {
    if (alarm.name !== "wearable-reconnect") return;
    await ready;
    connection.connect();
});

chrome.tabs.onRemoved.addListener(async tabId => {
    await ready;
    if (tabId === tabs.selectedTabId) {
        await tabs.clear();
        await publishState();
    }
});

chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (sender.id !== chrome.runtime.id || !message?.type?.startsWith("wearable.popup.")) return;

    void (async () => {
        await ready;
        if (message.type === "wearable.popup.connect") {
            const address = String(message.address || "").trim();
            const pairingKey = String(message.key || "").trim();
            if (pairingKey.length < 32 || pairingKey.length > 256) throw new Error("Enter the backend pairing key.");
            await chrome.storage.local.set({ address, pairingKey });
            connection.configure(address, pairingKey);
        } else if (message.type === "wearable.popup.select") {
            await tabs.select(Number(message.tabId));
            lastState = null;
            await publishState();
        } else if (message.type === "wearable.popup.forget") {
            connection.stop();
            await chrome.storage.local.remove("pairingKey");
            status = "Not paired";
        } else if (message.type !== "wearable.popup.status") {
            throw new Error("Unknown controller action.");
        }

        respond({ ok: true, status, selectedTabId: tabs.selectedTabId, paired: connection.paired });
    })().catch(error => respond({ ok: false, error: error.message }));

    return true;
});

// Read the real tab periodically, including changes made manually on the PC.
setInterval(() => { void publishState().catch(() => {}); }, 2000);
ready = initialize();
