const status = document.getElementById("status");
const message = document.getElementById("message");
const tabs = document.getElementById("tabs");
const address = document.getElementById("address");
const key = document.getElementById("key");

async function request(data) {
    const reply = await chrome.runtime.sendMessage(data);
    if (!reply?.ok) throw new Error(reply?.error || "Controller unavailable. Reload the extension.");
    status.textContent = reply.status;
    return reply;
}

async function refreshTabs() {
    const state = await request({ type: "wearable.popup.status" });
    const available = await chrome.tabs.query({ url: "https://www.youtube.com/*" });
    tabs.replaceChildren();
    for (const tab of available) {
        const option = document.createElement("option");
        option.value = String(tab.id);
        option.textContent = tab.title || "YouTube";
        option.selected = tab.id === state.selectedTabId;
        tabs.append(option);
    }
    document.getElementById("select").disabled = available.length === 0;
    if (!available.length) message.textContent = "Open a YouTube tab first, then refresh this list.";
}

function action(id, callback) {
    document.getElementById(id).addEventListener("click", async () => {
        message.textContent = "";
        try {
            await callback();
        } catch (error) {
            message.textContent = error.message;
        }
    });
}

action("connect", () => request({ type: "wearable.popup.connect", address: address.value, key: key.value }));
action("forget", async () => {
    await request({ type: "wearable.popup.forget" });
    key.value = "";
});
action("refresh", refreshTabs);
action("select", async () => {
    await request({ type: "wearable.popup.select", tabId: Number(tabs.value) });
    message.textContent = "This tab is now controlled by the wearable.";
});

const saved = await chrome.storage.local.get(["address", "pairingKey"]);
address.value = saved.address || "ws://localhost:5186/wearable/socket";
key.value = saved.pairingKey || "";
await refreshTabs().catch(error => { message.textContent = error.message; });
setInterval(() => { void request({ type: "wearable.popup.status" }).catch(() => {}); }, 1000);
