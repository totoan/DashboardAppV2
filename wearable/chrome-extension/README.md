# Pi Wearable YouTube Controller

This unpacked Chrome extension controls one selected YouTube tab through the
DashboardAppV2 wearable backend. It uses the existing pairing key and port 5186.
YouTube account credentials stay in Chrome/the dashboard; no Google API key is
needed in this extension.

## Install and test on your PC

1. Restart the updated dashboard backend and reload the dashboard frontend.
2. Open `chrome://extensions` in your normal Chrome profile.
3. Enable **Developer mode**, choose **Load unpacked**, and select:

   ```text
   N:\Projects\VSCodeProjects\DashboardAppV2\wearable\chrome-extension
   ```

4. Open the extension from Chrome's extensions menu (pin it for easy access).
5. Keep the address as `ws://localhost:5186/wearable/socket`, paste the same
   `PairingKey` from `backend/wearable/local/settings.json`, and click
   **Save and connect**. Wait for **Connected to dashboard backend**.
6. Open YouTube in a tab. In the popup, press **Refresh tabs**, choose that tab,
   and select **Use selected tab**. This remains the target when another tab,
   such as your dashboard, is active. No new playback tabs are created.
7. In CMD, launch the wearable preview:

   ```bat
   cd /d N:\Projects\PiApp
   preview.cmd
   ```

8. Open YouTube on the wearable, select a video from Subscriptions, Search,
   or a creator playlist, and press **Play on PC**. The selected Chrome tab
   loads that video; after confirmation the wearable opens **Now Playing**.
9. Test **Pause**, **Play**, and **Next**, then pause/seek/change a video
   manually in Chrome and confirm that the wearable updates.

If Chrome blocks starting playback, press Play once directly in the YouTube
tab and retry from the wearable. The app reports the failed command instead
of claiming the video started. Normal YouTube sign-in/consent/restriction
screens still need to be handled in Chrome.

## Remembered setup

The address and pairing key are saved in `chrome.storage.local`. You do not
need to re-enter them when Chrome or the backend restarts. **Forget key** removes
the saved key. The selected tab is stored in `chrome.storage.session`, so it
survives extension-worker restarts but is cleared when Chrome fully exits.
Select the desired tab again after a full Chrome restart; this avoids targeting
an unrelated tab whose numeric ID has been reused.

Closing the selected tab or navigating it away from YouTube disables controls.
Select another YouTube tab in the popup to resume. Only one Chrome controller
can be paired to the backend at a time (for example, one Chrome profile).

## Playlist behaviour

Choosing a playlist video preserves its playlist ID and its zero-based API
position, converting that position to the watch URL's one-based index.
**Next** uses YouTube's existing next control. Within a playlist it checks
that the next target still belongs to that playlist; outside a playlist it
uses YouTube's suggested next video. If Next is unavailable it is disabled.
There is no automatic ad-skipping or custom queue in this version.

## Files

```text
chrome-extension/
    manifest.json              Chrome permissions and entry points
    background.js              Worker startup, status, and popup actions
    services/connection.js     Pairing, reconnecting, and command replies
    services/youtube_tabs.js   Selected tab, video loading, and routing
    services/fullscreen.js     Chrome window fullscreen and restoration
    models/protocol.js         URL validation and playlist index conversion
    youtube_content.js        Read/control the real YouTube video element
    popup.html / popup.js      PC setup and tab selector
    popup.css                 Popup styles
```

The extension has access to `https://www.youtube.com/*` and uses Storage,
Alarms, and Scripting. It does not request access to all websites, cookies,
browser history, or remote debugging. Backend authorization still requires
the pairing key, including for Chrome-extension origins.

Playback state is read about every two seconds; the worker sends a heartbeat
every ten seconds while paired. A Chrome alarm can wake it to reconnect if
the backend is temporarily unavailable. Commands are not replayed after
disconnecting, and a navigation failure never causes Next to be clicked twice.

After editing extension code, press **Reload** on its card in
`chrome://extensions`, then reload the selected YouTube page. Saved pairing
details remain; select the tab again if necessary.

This is a normal YouTube-page controller, so changes to YouTube's page markup
may require updates to `youtube_content.js`. Automated tests exercise the
actual extension modules with a simulated YouTube document; installation
and playback in your real Chrome profile are the final device-specific check.

## Volume, seeking, and fullscreen

The wearable's **Now Playing** tab includes two touch sliders. Tap or drag
the position slider to preview a time, then release to seek. A paused video
stays paused. Seeking is disabled for ads and media without a finite seekable
timeline. If the video changes while you drag, the old seek is cancelled.

The volume slider changes the YouTube video element's volume from 0–100%,
and unmutes it when adjusted. It does not change Windows or other apps' volume.
Manual YouTube volume and mute changes are reflected on the wearable.

**Fullscreen** activates the selected tab, brings its Chrome window forward,
and expands its YouTube player to fill the screen. The controller combines
Chrome window fullscreen with a page style because a remote command cannot
supply the trusted click required by the page's native fullscreen API.
**Exit fullscreen** restores the previous normal or maximized window state.
F11 can also exit; the controller then removes its expanded-player style.
Closing or deselecting a controlled tab also restores the managed window.

To update an existing installation, restart the backend, reload the extension
at `chrome://extensions`, and reload its selected YouTube tab. Run PiApp's
`preview.cmd` in CMD to test the new controls before deploying to the Pi.
No new Pi dependencies or Chrome permissions are required.
