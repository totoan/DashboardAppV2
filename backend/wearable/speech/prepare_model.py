"""Download a stronger English model once; transcription itself stays local."""
from pathlib import Path
import os
os.environ['HF_HUB_DISABLE_IMPLICIT_TOKEN'] = '1'
from faster_whisper import WhisperModel

folder = Path(__file__).resolve().parent.parent / 'local/speech'
folder.mkdir(parents=True, exist_ok=True)
print('Preparing the small English Whisper model (roughly 500 MB)...', flush=True)
WhisperModel('small.en', device='cpu', compute_type='int8', download_root=str(folder / 'models'))
print('PC speech model ready. Restart the dashboard backend after setup.')
