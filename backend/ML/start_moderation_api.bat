@echo off
echo ========================================
echo Content Moderation API Startup Script
echo ========================================

cd /d "%~dp0"

REM Check if virtual environment exists
if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
)

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Install dependencies
echo Installing dependencies...
pip install -r requirements.txt -q

REM Check if model exists
if not exist "models\toxic_classifier.pkl" (
    echo Training model...
    python train_model.py
)

REM Start the API
echo Starting Content Moderation API on port 5050...
python content_moderation_api.py
