"""Read one temporary WAV, return JSON, and never log its recognized text."""
import json
from pathlib import Path
import sys
import wave

from faster_whisper import WhisperModel


def transcribe(path):
    with wave.open(str(path), 'rb') as audio:
        if audio.getnchannels() != 1 or audio.getsampwidth() != 2:
            raise ValueError('Expected mono PCM audio')
        if audio.getnframes() / audio.getframerate() > 121:
            raise ValueError('Recording exceeds duration limit')
    folder = Path(__file__).resolve().parent.parent / 'local/speech/models'
    model = WhisperModel('small.en', device='cpu', compute_type='int8', download_root=str(folder), local_files_only=True)
    segments, info = model.transcribe(str(path), language='en', beam_size=5,
                                      vad_filter=True, condition_on_previous_text=False)
    return ' '.join(segment.text.strip() for segment in segments).strip()[:6000]


if __name__ == '__main__':
    try:
        print(json.dumps({'text': transcribe(Path(sys.argv[1]))}))
    except Exception:
        print(json.dumps({'error': 'PC transcription unavailable'}))
        sys.exit(1)
