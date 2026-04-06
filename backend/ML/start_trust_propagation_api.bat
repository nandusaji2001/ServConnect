@echo off
echo Starting Trust Propagation API...
echo.
echo API will be available at: http://localhost:8006
echo Documentation at: http://localhost:8006/docs
echo.

cd /d "%~dp0"
python trust_propagation_api.py

pause
