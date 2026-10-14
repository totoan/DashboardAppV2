# Wearable connection, browsing, and Chrome playback

The wearable feature lives under `backend/wearable` and `frontend/src/wearable`.
Only registration in `Program.cs` and mounting the panel in `App.tsx` sit outside
these folders. The existing dashboard layout and services continue to work.

## What this stage does

- Pair a wearable or Windows preview with the backend.
- Show backend and wearable connection status on the dashboard.
- Send a test from either side and receive an acknowledgement from the other.
- Reconnect automatically when the backend becomes available again.

YouTube browsing and selected-tab playback controls are now available on the
wearable. Arbitrary PC commands are not accepted.

## Structure

```text
backend/wearable/
    models/WearableMessage.cs       Shared message fields
    services/WearableSettings.cs    Load/create local pairing settings
    services/WearableServer.cs      Dedicated WebSocket listener
    services/WearableConnections.cs Connected clients and test routing
    local/settings.json            Generated locally; ignored by Git

frontend/src/wearable/
    models/wearableMessage.ts       Message and UI state types
    services/wearableConnection.ts  Pairing, reconnecting, and test messages
    components/WearablePanel.tsx    Dashboard connection panel
    wearable.css                   Styles scoped to this panel
```

This uses native WebSockets so Qt can connect directly without a Python SignalR
client. The dashboard's existing SignalR metrics connection is independent.
The listener has its own port, 5186, and exposes only `/wearable/health` and
`/wearable/socket`. The existing dashboard APIs remain on their existing port.

## Test on the Windows PC

1. Stop and restart your dashboard backend through your normal development
   workflow. If using CMD directly:

   ```bat
   cd /d N:\Projects\VSCodeProjects\DashboardAppV2\backend
   dotnet run
   ```

2. Start/reload the frontend as usual. From a separate CMD window:

   ```bat
   cd /d N:\Projects\VSCodeProjects\DashboardAppV2\frontend
   npm run dev
   ```

3. The first backend start generates `wearable/local/settings.json`. Open it
   locally to copy the `PairingKey`:

   ```bat
   notepad wearable\local\settings.json
   ```

   Do not change or share the key unnecessarily. To rotate it, replace it with
   a new random key of at least 32 characters, restart the backend, and update
   both clients. Port and allowed browser origins are in this file too.

4. In the dashboard's **Wearable** panel, use
   `ws://localhost:5186/wearable/socket`, paste the key, and click **Connect**.
   The browser remembers the key in this dashboard origin's local storage;
   **Forget pairing** removes it. Default permitted origins are the Vite page
   at `http://localhost:5173` and `http://127.0.0.1:5173`. If your dashboard uses
   another origin, add that exact origin to `AllowedOrigins` and restart.

5. Open the wearable preview:

   ```bat
   cd /d N:\Projects\PiApp
   preview.cmd
   ```

6. Open **PC Dashboard**, use the same localhost address/key, and select
   **Save and connect**. Test in both directions using **Test dashboard**
   on the wearable and **Test wearable** on the dashboard. Each receiver
   displays the incoming test and replies automatically.

7. Restart the backend and confirm the clients reconnect. Close the dashboard
   page and confirm the wearable distinguishes the missing page from a
   disconnected backend. A timed-out test is not replayed after reconnecting.

## Connect the Pi

Deploy the wearable app using PiApp's `deploy.cmd`. On the Pi, update requirements
because Qt WebSockets requires the Addons package:

```sh
cd ~/Wearable
.venv/bin/python -m pip install -r requirements.txt
cp config/dashboard.example.json config/dashboard.json
nano config/dashboard.json
```

Use your PC's LAN address instead of localhost, copy the same pairing key, and
keep `device_name` as `Pi5 Wearable`. The file is read at app startup. You can
also edit the connection from the app when a keyboard is attached; a touch
keyboard is not included in this first stage.

On Windows, find the PC's address with `ipconfig`. Allow inbound TCP port 5186
on your private LAN. If needed, run this in an Administrator CMD:

```bat
netsh advfirewall firewall add rule name="Dashboard Wearable" dir=in action=allow protocol=TCP localport=5186 profile=private remoteip=localsubnet
```

Reboot the Pi to use the new handler. Normal deployments preserve its local
`config/dashboard.json`; the Windows pairing file is never included in exports.
Only one wearable/preview should be connected for the two-way acknowledgement tests. If multiple
clients of a role connect, the backend routes a test to one of them.

The connection is intended for a trusted home LAN and uses plain `ws://` by
default. There is no internet hosting or TLS setup in this stage. No firewall
rules are created automatically.

## Protocol

Messages are JSON objects. The first message is `pair`, with `role`, `name`, and
`key`. The key is sent inside that message, not in a URL. Successful pairing
returns `welcome`, followed by `status` with both role connection flags.
Tests use `test` and a UUID `requestId`; the recipient returns `ack` with that
ID. The backend validates the recipient before forwarding its reply. `ping`
receives `pong`. YouTube browsing uses `request` with an operation and UUID `requestId`, and returns
`result` with `data`, or `error` with a readable message. Unknown commands are rejected; arbitrary PC commands are not
accepted. Clients time out tests after five seconds and reconnect after three
seconds when disconnected. Invalid keys stop reconnecting until corrected.


## YouTube browsing stage

The wearable's YouTube page has Now Playing, Subscriptions, and Search tabs.
Subscriptions and Search work, and Now Playing now receives live state and
controls through the Chrome extension.

### Additional files

- `models/YouTubeBrowseModels.cs`: common list format for videos, channels, and playlists.
- `services/YouTubeUploadCache.cs`: completed upload snapshot shared by both listeners.
- `services/WearableYouTubeService.cs`: validated, read-only Google requests and page caching.

The original upload service now includes `VideoId` and `ChannelId` in each record.
The existing 15-minute refresh updates `YouTubeUploadCache`; the dashboard's
`/api/youtube/uploads` endpoint still returns the same list, with these extra IDs.
No second subscription-refresh job was added. Existing limits are preserved:
the first 50 subscriptions, five uploads per channel, newest 20 overall.

### Try it

1. Restart the backend, then run PiApp's `preview.cmd`.
2. Open **YouTube** on the wearable. Its Subscriptions tab reads the PC cache.
3. Select an upload and choose **Creator playlists**. Select a playlist and choose
   **Open playlist**. Use **Back** and **Load more** to navigate.
4. Open Search, enter text or use the on-screen **Keyboard**, then submit Search.
   Video, channel, and playlist results are labelled. Creator and playlist actions
   work here too.
5. Disconnect/restart the backend and confirm that the last loaded list stays
   visible and browsing works again after reconnecting.

The uploads timestamp represents the backend's last successful Google refresh.
The wearable reads that cache every minute while its uploads list is visible;
**Reload list** reads it immediately without making a Google request. The existing
dashboard **YT Refresh** button can still force a Google refresh when desired.

Search and playlist pages are cached for five minutes on the PC. Search is sent
only on submit, with 20 results per page and explicit pagination. These requests
use the existing dashboard access token and read-only authorization; credentials
are never sent to the wearable. If sign-in expires, refresh YouTube on the PC
dashboard and retry. Quota/API failures produce readable errors. Thumbnails load
asynchronously on the wearable from official YouTube image hosts.

### New request operations

| Operation | Fields | Result |
|---|---|---|
| `youtube.uploads` | none | Cached uploads, refresh timestamp, readiness flag |
| `youtube.search` | `query`, optional `pageToken` | Videos, channels, playlists |
| `youtube.playlists` | `channelId`, optional `pageToken` | Creator's available playlists |
| `youtube.playlistVideos` | `playlistId`, optional `pageToken` | Ordered videos with playlist ID and position |

Playlist owner and video creator can differ; playlist-video records use the
video-owner fields when supplied by Google. Deleted/private placeholder entries
are omitted. This context is also used when sending a playlist video to Chrome.


## Chrome playback controller

The controller lives in the project's `wearable/chrome-extension` folder, with
its own README and separate files for connection handling, tab selection,
protocol validation, and YouTube page controls. It runs in the same Chrome
profile as the YouTube tab and pairs with the existing backend key.

Follow `wearable/chrome-extension/README.md` from the project root to install
it through Chrome's **Load unpacked** workflow. Restart the backend before
pairing it. The dashboard panel shows controller connection status and the
current playback title. The wearable's saved pairing file remains unchanged.

### Playback protocol

- A `player` role identifies the single Chrome controller. Chrome-extension
  origins are allowed at the wearable endpoint, but still require the pairing key.
- `playbackState` from that controller is validated and forwarded to dashboard
  and wearable clients. The state includes title, creator, elapsed/duration,
  selected-tab readiness, paused state, playlist ID, Next/seek availability,
  YouTube volume/mute, and fullscreen state.
- Wearable `request` operations `youtube.play`, `youtube.pause`, `youtube.resume`,
  `youtube.next`, `youtube.volume`, `youtube.seek`, and `youtube.fullscreen`
  are forwarded as `playbackCommand` to that controller only.
- The backend replaces the request ID for forwarding, validates the replying
  controller, and returns `result` or a readable `error` to the original sender.
- `youtube.play` includes `videoId`, optional `playlistId`, and optional zero-based
  `position`. Only validated IDs are forwarded; arbitrary URLs or scripts are not.
- `youtube.playbackState` returns the latest state without contacting Google.
- Closing the controller clears its availability and fails outstanding commands.
  Commands time out and are never queued for replay after reconnecting.

Playback uses the normal YouTube page rather than an embedded player. Creator
playlist context is retained. Outside a playlist, Next follows YouTube's suggested
next video; within one, Next must stay in that playlist. Chrome autoplay and
YouTube sign-in/consent screens may require an initial action in Chrome.

After backend/extension changes, restart the backend, reload the extension in
`chrome://extensions`, and reload the selected YouTube tab. You do not need to
re-enter the saved pairing key. Select the desired tab again if Chrome was fully
closed or that tab was removed.

### Media control fields

| Operation | Required fields | Behaviour |
|---|---|---|
| `youtube.volume` | integer `volume`, 0–100 | Sets YouTube video volume and unmutes it |
| `youtube.seek` | finite nonnegative `seekTime`, current `videoId` | Seeks within valid ranges; preserves pause |
| `youtube.fullscreen` | boolean `fullscreen` | Expands/restores selected Chrome window and player |

The backend validates these fields before forwarding. Seeking includes the
expected video ID so a delayed command cannot seek a different video. Manual
PC changes flow back through the same two-second playback-state updates.
