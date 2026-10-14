export function isYouTubeAddress(address) {
    try {
        const url = new URL(address);
        return url.protocol === "https:" && url.hostname === "www.youtube.com";
    } catch {
        return false;
    }
}

export function videoAddress(command) {
    if (!/^[A-Za-z0-9_-]{11}$/.test(command.videoId ?? "")) {
        throw new Error("Invalid video selection.");
    }

    const url = new URL("https://www.youtube.com/watch");
    url.searchParams.set("v", command.videoId);

    if (command.playlistId) {
        if (!/^[A-Za-z0-9_-]{10,100}$/.test(command.playlistId)) {
            throw new Error("Invalid playlist selection.");
        }
        url.searchParams.set("list", command.playlistId);

        if (command.position !== null && command.position !== undefined) {
            if (!Number.isInteger(command.position) || command.position < 0 || command.position > 100000) {
                throw new Error("Invalid playlist position.");
            }
            // The API uses zero-based positions; YouTube watch URLs use one-based indices.
            url.searchParams.set("index", String(command.position + 1));
        }
    }

    return url.href;
}

export function disconnectedState(message = "Choose a YouTube tab in the Chrome controller.") {
    return {
        tabSelected: false,
        ready: false,
        videoId: "",
        title: "",
        channelTitle: "",
        playlistId: "",
        currentTime: 0,
        duration: 0,
        paused: true,
        canNext: false,
        isAd: false,
        volume: 100,
        muted: false,
        canSeek: false,
        fullscreen: false,
        message
    };
}
