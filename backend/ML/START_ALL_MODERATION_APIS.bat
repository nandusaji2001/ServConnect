@echo off
echo ========================================
echo Starting All Content Moderation APIs
echo ========================================
echo.

echo This will start:
echo 1. Intelligent Moderation API (port 8002) - Content detection
echo 2. Trust Propagation API (port 8006) - Trust score propagation
echo.
echo Press Ctrl+C in each window to stop the APIs
echo.
pause

echo Starting Intelligent Moderation API...
start "Intelligent Moderation API" cmd /k "cd /d %~dp0 && python intelligent_moderation_api.py"

timeout /t 3 /nobreak >nul

echo Starting Trust Propagation API...
start "Trust Propagation API" cmd /k "cd /d %~dp0 && python trust_propagation_api.py"

echo.
echo ========================================
echo All APIs Started!
echo ========================================
echo.
echo Intelligent Moderation API: http://localhost:8002
echo Trust Propagation API: http://localhost:8006
echo.
echo Check the opened windows for API status
echo.
pause
