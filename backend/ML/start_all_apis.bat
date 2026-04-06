@echo off
echo ================================================
echo   Starting All ML APIs
echo ================================================
echo.
echo   - Content Moderation API (Port 5050) [Legacy]
echo   - Intelligent Moderation API (Port 5051) [NEW: BERT+CLIP+GNN]
echo   - Elder Wellness API (Port 5002)
echo   - Multimodal Item Matching API (Port 5003) [CLIP+GNN]
echo   - ID Verification API (Port 5004)
echo   - Depression Prediction API (Port 5007)
echo   - OCR Text Extraction API (Port 5008) [NEW: EasyOCR]
echo   - Trust Propagation API (Port 8006) [GNN Social Graph]
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
pip install easyocr Pillow -q
pip install transformers torch-geometric -q

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

if not exist "models\depression_model.pkl" (
    echo Training Depression model...
    python train_depression_model.py
)

echo.
echo ================================================
echo   Starting all APIs in separate windows...
echo ================================================
echo.

REM Start each API in a new window
start "Content Moderation API - Port 5050 [Legacy]" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python content_moderation_api.py"
timeout /t 2 /nobreak > nul

start "Intelligent Moderation API - Port 5051 [BERT+CLIP+GNN]" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python intelligent_moderation_api.py"
timeout /t 2 /nobreak > nul

start "Elder Wellness API - Port 5002" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python elder_wellness_api.py"
timeout /t 2 /nobreak > nul

start "Multimodal Item Matching API - Port 5003" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python multimodal_item_matching_api.py"
timeout /t 2 /nobreak > nul

start "ID Verification API - Port 5004" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python id_verification_api.py"
timeout /t 2 /nobreak > nul

start "Depression Prediction API - Port 5007" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python depression_prediction_api.py"
timeout /t 2 /nobreak > nul

start "OCR Text Extraction API - Port 5008 [EasyOCR]" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python ocr_text_extraction_api.py"
timeout /t 2 /nobreak > nul

start "Trust Propagation API - Port 8006 [GNN Social Graph]" cmd /k "cd /d "%~dp0" && call venv\Scripts\activate.bat && python trust_propagation_api.py"

echo.
echo ================================================
echo   All APIs started!
echo ================================================
echo.
echo   Content Moderation API (Legacy): http://localhost:5050
echo   Intelligent Moderation API (NEW): http://localhost:5051
echo   Elder Wellness API:              http://localhost:5002
echo   Multimodal Matching API:         http://localhost:5003 [CLIP+GNN]
echo   ID Verification API:             http://localhost:5004
echo   Depression API:                  http://localhost:5007
echo   OCR Text Extraction API (NEW):   http://localhost:5008
echo   Trust Propagation API (NEW):     http://localhost:8006 [GNN]
echo.
echo   NEW: Enhanced Content Moderation:
echo        - OCR extracts text from images
echo        - Analyzes caption + image text together
echo        - Detects harmful content in images
echo.
echo   NEW: Intelligent Moderation combines:
echo        - Text Analysis (BERT/TF-IDF)
echo        - Image Analysis (CLIP)
echo        - User Trust Scoring (GNN)
echo.
echo   NEW: Trust Propagation System:
echo        - Propagates penalties through social graph
echo        - Affects followers of banned users
echo        - Gradual trust recovery over time
echo.
echo   Each API is running in its own window.
echo   Close this window or press any key to exit.
echo ================================================
pause
