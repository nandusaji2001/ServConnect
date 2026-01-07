@echo off
echo ============================================
echo   Elder Wellness Model Training
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

echo.
echo ============================================
echo   Training Random Forest Models...
echo ============================================
echo.

python train_wellness_model.py

echo.
pause
