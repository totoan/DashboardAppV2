import { FullscreenController } from "./fullscreen.js";
import { disconnectedState, isYouTubeAddress, videoAddress } from "../models/protocol.js";

export class YouTubeTabs {
    constructor(onSelectionLost) {
        this.selectedTabId = null;
        this.onSelectionLost = onSelectionLost;
        this.fullscreen = new FullscreenController((message, tabId) => this.sendToTab(message, tabId));
    }

    async restore() {
        await this.fullscreen.restore();
        const saved = await chrome.storage.session.get("selectedTabId");
        if (Number.isInteger(saved.selectedTabId)) {
            this.selectedTabId = saved.selectedTabId;
            await this.getSelectedTab();
        }
    }

    async select(tabId) {
        const tab = await chrome.tabs.get(tabId);
        if (!isYouTubeAddress(tab.url)) {
            throw new Error("Select an open YouTube tab.");
        }
        if (this.selectedTabId !== tabId) await this.fullscreen.exitManaged();
        this.selectedTabId = tabId;
        await chrome.storage.session.set({ selectedTabId: tabId });
        return this.state();
    }

    async clear() {
        await this.fullscreen.exitManaged();
        this.selectedTabId = null;
        await chrome.storage.session.remove("selectedTabId");
        this.onSelectionLost?.();
    }

    async getSelectedTab() {
        if (this.selectedTabId === null) return null;

        try {
            const tab = await chrome.tabs.get(this.selectedTabId);
            if (isYouTubeAddress(tab.url)) return tab;
        } catch {
            // Closing or replacing a tab invalidates the selection.
        }

        await this.clear();
        return null;
    }

    async sendToTab(message, expectedTabId = null) {
        const tab = await this.getSelectedTab();
        if (!tab) throw new Error("Choose a YouTube tab in the Chrome controller first.");
        if (expectedTabId !== null && tab.id !== expectedTabId) {
            throw new Error("The selected YouTube tab changed. Please retry.");
        }

        try {
            return await chrome.tabs.sendMessage(tab.id, message);
        } catch (error) {
            // A closed message port can mean the command already caused navigation.
            // Only retry when there was no receiving script to execute it at all.
            if (!String(error.message).includes("Receiving end does not exist")) throw error;
            // Existing tabs predate extension installation, so inject once if needed.
            await chrome.scripting.executeScript({
                target: { tabId: tab.id },
                files: ["youtube_content.js"]
            });
            return chrome.tabs.sendMessage(tab.id, message);
        }
    }

    async state() {
        const tab = await this.getSelectedTab();
        if (!tab) return disconnectedState();

        try {
            const result = await this.sendToTab({ type: "wearable.getState" });
            if (result?.state) return await this.fullscreen.observe(tab, result.state);
        } catch {
            // Navigation briefly replaces the content script.
        }

        return { ...disconnectedState("YouTube tab is loading…"), tabSelected: true };
    }

    async execute(command, signal) {
        const selected = await this.getSelectedTab();
        if (!selected) {
            throw new Error("Choose a YouTube tab in the Chrome controller first.");
        }
        signal?.throwIfAborted();

        if (command.operation === "youtube.play") {
            const target = videoAddress(command);
            const tab = selected;
            const current = new URL(tab.url);
            const desired = new URL(target);

            if (current.pathname !== "/watch"
                || current.searchParams.get("v") !== desired.searchParams.get("v")
                || current.searchParams.get("list") !== desired.searchParams.get("list")
                || (desired.searchParams.has("index")
                    && current.searchParams.get("index") !== desired.searchParams.get("index"))) {
                await chrome.tabs.update(tab.id, { url: target });
            }

            await this.waitForVideo(command.videoId, command.playlistId || "", tab.id, signal);
            signal?.throwIfAborted();
            return this.control("resume", tab.id);
        }

        if (command.operation === "youtube.fullscreen") {
            if (typeof command.fullscreen !== "boolean") throw new Error("Invalid fullscreen request.");
            await this.fullscreen.set(selected, command.fullscreen);
            return this.state();
        }
        if (command.operation === "youtube.volume") {
            return this.control("volume", selected.id, { volume: command.volume });
        }
        if (command.operation === "youtube.seek") {
            return this.control("seek", selected.id, { seekTime: command.seekTime, videoId: command.videoId });
        }

        const actions = {
            "youtube.pause": "pause",
            "youtube.resume": "resume",
            "youtube.next": "next"
        };
        const action = actions[command.operation];
        if (!action) throw new Error("Unsupported playback command.");
        signal?.throwIfAborted();
        try {
            return await this.control(action, selected.id);
        } catch (error) {
            if (action !== "next") throw error;

            // A full navigation destroys the old content script before it can reply.
            // Observe the new page; never click Next a second time to recover.
            const deadline = Date.now() + 6000;
            while (Date.now() < deadline) {
                signal?.throwIfAborted();
                const tab = await this.getSelectedTab();
                if (!tab || tab.id !== selected.id) throw error;
                if (tab.url === selected.url) throw error;
                const state = await this.state();
                const originalPlaylist = new URL(selected.url).searchParams.get("list") || "";
                if (state.ready && state.playlistId === originalPlaylist) return state;
                await new Promise(resolve => setTimeout(resolve, 250));
            }
            throw error;
        }
    }

    async waitForVideo(videoId, playlistId, tabId, signal) {
        const deadline = Date.now() + 12000;
        while (Date.now() < deadline) {
            signal?.throwIfAborted();
            if (this.selectedTabId !== tabId) throw new Error("The selected YouTube tab changed. Please retry.");
            const state = await this.state();
            if (state.ready && state.videoId === videoId && state.playlistId === playlistId) return;
            await new Promise(resolve => setTimeout(resolve, 250));
        }
        throw new Error("The video did not finish loading. Check the selected YouTube tab.");
    }

    async control(action, tabId, parameters = {}) {
        const result = await this.sendToTab({ type: "wearable.control", action, ...parameters }, tabId);
        if (!result?.ok) throw new Error(result?.error || "YouTube could not confirm the command.");
        const tab = await this.getSelectedTab();
        return tab ? this.fullscreen.observe(tab, result.state) : result.state;
    }
}
