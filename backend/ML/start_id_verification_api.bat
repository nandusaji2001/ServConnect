@echo off
echo Starting ID Verification API...
echo.
cd /d "%~dp0"

REM Activate virtual environment if it exists
if exist "venv\Scripts\activate.bat" (
    call venv\Scripts\activate.bat
)

REM Check if required packages are installed
python -c "import easyocr" 2>nul
if errorlevel 1 (
    echo Installing required packages...
    pip install easyocr Pillow flask flask-cors
)

echo.
echo Starting ID Verification API on http://localhost:5004
echo.
python id_verification_api.py

pause
