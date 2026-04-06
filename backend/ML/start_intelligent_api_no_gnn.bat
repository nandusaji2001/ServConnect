@echo off
echo ========================================
echo Starting Intelligent Moderation API
echo WITHOUT GNN Training (to avoid errors)
echo ========================================
echo.
echo This will start the API on port 5051
echo GNN features will be disabled temporarily
echo.

REM Set environment variable to skip GNN training
set SKIP_GNN_TRAINING=1

REM Activate virtual environment if it exists
if exist venv\Scripts\activate.bat (
    echo Activating virtual environment...
    call venv\Scripts\activate.bat
)

echo Starting API...
python intelligent_moderation_api.py

pause
