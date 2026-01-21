@echo off
echo ================================================
echo   Starting All ML APIs
echo ================================================
echo.
echo   - Content Moderation API (Port 5050)
echo   - Elder Wellness API (Port 5002)
echo   - Item Matching API (Port 5003)
echo.
echo ================================================

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
pip install sentence-transformers -q

REM Check and train models if needed
echo.
echo Checking models...

if not exist "models\toxic_classifier.pkl" (
    echo Training Content Moderation model...
    python train_model.py
)

if not exist "models\wellness_models.pkl" (
    echo Training Wellness model...
    python train_wellness_model.py
)

echo.
echo ================================================
echo   Starting all APIs in separate windows...
echo ================================================
echo.

REM Start each API in a new window
start "Content Moderation API - Port 5050" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python content_moderation_api.py"
timeout /t 2 /nobreak > nul

start "Elder Wellness API - Port 5002" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python elder_wellness_api.py"
timeout /t 2 /nobreak > nul

start "Item Matching API - Port 5003" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python item_matching_api.py"

echo.
echo ================================================
echo   All APIs started!
echo ================================================
echo.
echo   Content Moderation API: http://localhost:5050
echo   Elder Wellness API:     http://localhost:5002
echo   Item Matching API:      http://localhost:5003
echo.
echo   Each API is running in its own window.
echo   Close this window or press any key to exit.
echo ================================================
pause
