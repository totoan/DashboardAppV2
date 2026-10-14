# Wearable PC speech

This service receives recordings from the paired Pi on the existing wearable
listener and uses Whisper small.en on the PC CPU. The Pi keeps offline Vosk
recognition running for live previews and automatic fallback.

From DashboardAppV2 in CMD:

```bat
backend\wearable\speech\setup.cmd
```

Python 3.12 is required. Restart the dashboard backend after installation.
No dashboard frontend or Chrome extension reinstall is needed. On the wearable,
use its existing PC pairing and select a microphone in Settings → Microphone.
Tap Speak, speak normally, then Stop. Wait for recognition and review the text.

Implementation files:
- speech/setup.cmd: isolated Python environment and model setup.
- speech/prepare_model.py: one-time model download (approximately 500 MB).
- speech/transcribe.py: CPU int8 English recognition; no cloud calls.
- services/WearableSpeechService.cs: one request at a time, timeout/cancellation,
  temporary WAV cleanup.
- services/WearableServer.cs: authenticated POST /wearable/speech on the existing
  wearable listener. Uses the same pairing key as WebSocket control.

All environments, models, and temporary audio live under ignored wearable/local.
Audio files are removed after requests; recognized text is returned to the Pi
without being logged by the recognizer. Cancel closes the Pi request and stops
the PC recognition process. The Pi falls back if the service is unavailable,
busy, or takes longer than the 60-second server deadline. No extra Pi packages
are needed for PC speech beyond the existing offline voice dependencies.

Dependencies pin PyAV below version 19 because its changed open() API is
incompatible with faster-whisper 1.2.1. The setup script preserves this pin.

References: [faster-whisper](https://github.com/SYSTRAN/faster-whisper),
[Vosk](https://alphacephei.com/vosk/).
