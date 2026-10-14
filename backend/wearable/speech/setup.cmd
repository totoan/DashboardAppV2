@echo off
setlocal
set "SPEECH_ROOT=%~dp0..\local\speech"
set "SPEECH_PYTHON=%SPEECH_ROOT%\venv\Scripts\python.exe"
if not exist "%SPEECH_PYTHON%" (
    py -3.12 -m venv "%SPEECH_ROOT%\venv"
    if errorlevel 1 exit /b 1
)
"%SPEECH_PYTHON%" -m pip install -r "%~dp0requirements.txt"
if errorlevel 1 exit /b 1
"%SPEECH_PYTHON%" "%~dp0prepare_model.py"
exit /b %errorlevel%
