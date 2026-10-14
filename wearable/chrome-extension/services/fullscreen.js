// Remote fullscreen uses Chrome's window API and a viewport-sized YouTube player.
// This works without requiring a fresh trusted click inside the YouTube page.
export class FullscreenController {
    constructor(sendToTab) {
        this.sendToTab = sendToTab;
        this.managed = null;
        this.lastWindowState = new Map();
    }

    async restore() {
        const saved = await chrome.storage.session.get("managedFullscreen");
        const record = saved.managedFullscreen;
        if (record && Number.isInteger(record.tabId) && Number.isInteger(record.windowId)) {
            this.managed = record;
        }
    }

    async observe(tab, state) {
        const window = await chrome.windows.get(tab.windowId);
        if (window.state === "normal" || window.state === "maximized") {
            this.lastWindowState.set(tab.windowId, window.state);
        }

        if (this.managed?.tabId === tab.id) {
            if (window.state !== "fullscreen") {
                // F11/Escape or a manual window change can exit remote fullscreen.
                await this.sendToTab({ type: "wearable.control", action: "exitFullscreen" }, tab.id);
                await this.forget();
                state.fullscreen = false;
            } else if (state.ready && !state.playerExpanded) {
                // A new video navigation replaces the old content script/styles.
                const reply = await this.sendToTab({ type: "wearable.control", action: "enterFullscreen" }, tab.id);
                if (reply?.ok) state = reply.state;
            }
        }

        return { ...state, fullscreen: Boolean(state.fullscreen || window.state === "fullscreen") };
    }

    async set(tab, enabled) {
        if (enabled) {
            const window = await chrome.windows.get(tab.windowId);
            const previousState = this.managed?.previousState
                || this.lastWindowState.get(tab.windowId)
                || (window.state === "maximized" ? "maximized" : "normal");

            this.managed = { tabId: tab.id, windowId: tab.windowId, previousState };
            await chrome.storage.session.set({ managedFullscreen: this.managed });

            try {
                await chrome.tabs.update(tab.id, { active: true });
                await chrome.windows.update(tab.windowId, { state: "fullscreen", focused: true });
                const reply = await this.sendToTab({ type: "wearable.control", action: "enterFullscreen" }, tab.id);
                if (!reply?.ok) throw new Error(reply?.error || "YouTube could not enter fullscreen.");
            } catch (error) {
                await this.exitManaged();
                throw error;
            }
        } else {
            const reply = await this.sendToTab({ type: "wearable.control", action: "exitFullscreen" }, tab.id);
            if (!reply?.ok) throw new Error(reply?.error || "YouTube could not exit fullscreen.");
            if (this.managed) {
                await this.exitManaged();
            } else {
                const window = await chrome.windows.get(tab.windowId);
                if (window.state === "fullscreen") {
                    await chrome.windows.update(tab.windowId, {
                        state: this.lastWindowState.get(tab.windowId) || "normal"
                    });
                }
            }
        }
    }

    async exitManaged() {
        const record = this.managed;
        if (!record) return;

        // Use the recorded tab directly: it may have been closed or deselected.
        await chrome.tabs.sendMessage(record.tabId, {
            type: "wearable.control",
            action: "exitFullscreen"
        }).catch(() => {});

        try {
            const window = await chrome.windows.get(record.windowId);
            if (window.state === "fullscreen") {
                await chrome.windows.update(record.windowId, { state: record.previousState });
            }
        } catch {
            // Closing a Chrome window removes its fullscreen state too.
        }
        await this.forget();
    }

    async forget() {
        this.managed = null;
        await chrome.storage.session.remove("managedFullscreen");
    }
}
