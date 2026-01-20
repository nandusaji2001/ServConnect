@echo off
echo Starting Item Matching API (S-BERT)...
echo.

cd /d "%~dp0"

if exist venv\Scripts\activate.bat (
    call venv\Scripts\activate.bat
) else (
    echo Virtual environment not found. Creating one...
    python -m venv venv
    call venv\Scripts\activate.bat
    pip install -r requirements.txt
    pip install sentence-transformers
)

echo.
echo Starting API on port 5003...
python item_matching_api.py

pause
