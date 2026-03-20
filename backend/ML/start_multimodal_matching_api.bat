@echo off
echo ================================================
echo   Starting Multimodal Item Matching API
echo   (CLIP + GNN + SBERT)
echo ================================================

cd /d "%~dp0"

REM Activate virtual environment
if exist "venv\Scripts\activate.bat" (
    call venv\Scripts\activate.bat
) else (
    echo Virtual environment not found. Creating...
    python -m venv venv
    call venv\Scripts\activate.bat
    pip install -r requirements.txt
)

REM Set environment variable for port
set MULTIMODAL_MATCHING_PORT=5003

echo.
echo Starting Multimodal Item Matching API on port %MULTIMODAL_MATCHING_PORT%...
echo.

python multimodal_item_matching_api.py

pause
