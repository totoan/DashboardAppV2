(() => {
    if (globalThis.__piWearableController) return;
    globalThis.__piWearableController = true;

    function nextButton() {
        const button = document.querySelector(".ytp-next-button");
        if (!button || button.disabled || button.getAttribute("aria-disabled") === "true") return null;

        const current = new URL(location.href);
        const playlist = current.searchParams.get("list");
        if (playlist) {
            try {
                const next = new URL(button.href, location.href);
                if (next.hostname !== "www.youtube.com" || next.searchParams.get("list") !== playlist) return null;
                if (next.searchParams.get("v") === current.searchParams.get("v")
                    && next.searchParams.get("index") === current.searchParams.get("index")) return null;
            } catch {
                return null;
            }
        }
        return button;
    }

    function expandPlayer(enabled) {
        const styleId = "pi-wearable-fullscreen";
        document.getElementById(styleId)?.remove();
        document.documentElement.classList.toggle("pi-wearable-expanded", enabled);
        if (enabled) {
            const style = document.createElement("style");
            style.id = styleId;
            style.textContent = `
                html.pi-wearable-expanded body { overflow: hidden !important; }
                html.pi-wearable-expanded #movie_player {
                    position: fixed !important;
                    top: 0 !important; left: 0 !important;
                    width: 100vw !important; height: 100vh !important;
                    z-index: 2147483646 !important;
                    background: black !important;
                }
                html.pi-wearable-expanded .html5-video-container {
                    width: 100% !important; height: 100% !important;
                }
                html.pi-wearable-expanded video.html5-main-video {
                    position: absolute !important;
                    top: 0 !important; left: 0 !important;
                    width: 100% !important; height: 100% !important;
                    object-fit: contain !important;
                }
                html.pi-wearable-expanded .ytp-chrome-bottom {
                    width: calc(100% - 24px) !important;
                    left: 12px !important;
                }
            `;
            document.documentElement.append(style);
        }
        window.dispatchEvent(new Event("resize"));
    }

    async function seekVideo(video, seconds) {
        if (!Number.isFinite(seconds) || seconds < 0 || !snapshot().canSeek) {
            throw new Error("This video cannot seek to that time.");
        }

        // Some media has gaps or a non-zero timeline start. Choose a valid range.
        let target = Math.min(seconds, video.duration);
        let nearest = video.seekable.start(0);
        for (let index = 0; index < video.seekable.length; index++) {
            const start = video.seekable.start(index);
            const end = video.seekable.end(index);
            if (target >= start && target <= end) {
                nearest = target;
                break;
            }
            if (Math.abs(start - target) < Math.abs(nearest - target)) nearest = start;
            if (Math.abs(end - target) < Math.abs(nearest - target)) nearest = end;
        }
        target = nearest;

        await new Promise((resolve, reject) => {
            const finish = error => {
                clearTimeout(deadline);
                video.removeEventListener("seeked", onSeeked);
                if (error) reject(error); else resolve();
            };
            const onSeeked = () => finish();
            const deadline = setTimeout(() => finish(new Error("YouTube did not confirm seeking. Try again.")), 6000);
            video.addEventListener("seeked", onSeeked, { once: true });
            try {
                video.currentTime = target;
                if (!video.seeking && Math.abs(video.currentTime - target) < 1) finish();
            } catch (error) {
                finish(error);
            }
        });
    }

    function snapshot() {
        const url = new URL(location.href);
        const video = document.querySelector("video.html5-main-video");
        const videoId = url.pathname === "/watch" ? url.searchParams.get("v") || "" : "";
        const ready = Boolean(video && videoId && video.readyState >= 1);
        const title = document.querySelector("ytd-watch-metadata h1 yt-formatted-string")?.textContent?.trim()
            || document.querySelector('meta[name="title"]')?.content || "";
        const creator = document.querySelector("ytd-video-owner-renderer #channel-name a")?.textContent?.trim() || "";
        const isAd = Boolean(document.querySelector("#movie_player.ad-showing"));

        return {
            tabSelected: true,
            ready,
            videoId,
            title,
            channelTitle: creator,
            playlistId: url.searchParams.get("list") || "",
            currentTime: Number.isFinite(video?.currentTime) ? Math.floor(video.currentTime) : 0,
            duration: Number.isFinite(video?.duration) ? Math.floor(video.duration) : 0,
            paused: video?.paused ?? true,
            canNext: ready && !isAd && Boolean(nextButton()),
            isAd,
            volume: Math.round((video?.volume ?? 1) * 100),
            muted: video?.muted ?? false,
            canSeek: ready && !isAd && Number.isFinite(video.duration)
                && video.duration > 0 && video.seekable?.length > 0,
            fullscreen: Boolean(document.fullscreenElement)
                || document.documentElement.classList.contains("pi-wearable-expanded"),
            playerExpanded: document.documentElement.classList.contains("pi-wearable-expanded"),
            message: ready ? (isAd ? "Advertisement playing" : "") : "Choose a video or send one from the wearable."
        };
    }

    async function control(action, parameters) {
        if (action === "exitFullscreen") {
            if (document.fullscreenElement) await document.exitFullscreen();
            expandPlayer(false);
            return snapshot();
        }
        const video = document.querySelector("video.html5-main-video");
        if (!video || !snapshot().ready) throw new Error("No ready YouTube video in the selected tab.");

        if (action === "enterFullscreen") {
            expandPlayer(true);
        } else if (action === "volume") {
            if (!Number.isInteger(parameters.volume) || parameters.volume < 0 || parameters.volume > 100) {
                throw new Error("Volume must be between 0 and 100.");
            }
            video.volume = parameters.volume / 100;
            video.muted = false;
        } else if (action === "seek") {
            if (parameters.videoId !== snapshot().videoId) {
                throw new Error("The video changed. Choose a time again.");
            }
            await seekVideo(video, parameters.seekTime);
        } else if (action === "pause") {
            video.pause();
        } else if (action === "resume") {
            let deadline;
            try {
                await Promise.race([
                    video.play(),
                    new Promise((resolve, reject) => {
                        deadline = setTimeout(() => reject(new Error("Playback confirmation timed out.")), 4000);
                    })
                ]);
            } catch (error) {
                if (error.name === "NotAllowedError") {
                    throw new Error("Chrome blocked playback. Press Play once in YouTube, then retry.");
                }
                throw new Error("YouTube could not start playback. Check the selected tab and retry.");
            } finally {
                clearTimeout(deadline);
            }
        } else if (action === "next") {
            const next = nextButton();
            if (!next || snapshot().isAd) throw new Error("Next is not available for this video.");
            const originalAddress = location.href;
            next.click();
            const deadline = Date.now() + 10000;
            while (Date.now() < deadline) {
                if (location.href !== originalAddress && snapshot().ready) return snapshot();
                await new Promise(resolve => setTimeout(resolve, 200));
            }
            throw new Error("YouTube did not advance. Check the selected tab.");
        } else {
            throw new Error("Unsupported player action.");
        }

        return snapshot();
    }

    chrome.runtime.onMessage.addListener((message, sender, respond) => {
        if (sender.id !== chrome.runtime.id) return;
        if (message?.type === "wearable.getState") {
            respond({ ok: true, state: snapshot() });
            return;
        }
        if (message?.type === "wearable.control") {
            void control(message.action, message)
                .then(state => respond({ ok: true, state }))
                .catch(error => respond({ ok: false, error: error.message }));
            return true;
        }
    });
})();
