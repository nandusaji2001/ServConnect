@echo off
echo ============================================
echo   Elder Wellness Prediction API Launcher
echo ============================================
echo.

cd /d "%~dp0"

REM Check if virtual environment exists
if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
)

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Install requirements
echo Installing requirements...
pip install -r requirements.txt

REM Check if models exist, if not train them
if not exist "models\wellness_models.pkl" (
    echo.
    echo ============================================
    echo   Models not found! Training models first...
    echo ============================================
    python train_wellness_model.py
    echo.
)

REM Start the API
echo.
echo ============================================
echo   Starting Wellness API on port 5002...
echo ============================================
echo.
python elder_wellness_api.py

pause
