# Installation Guide - Multimodal Lost & Found System

## Quick Start (5 minutes)

### Step 1: Navigate to ML Directory
```bash
cd backend/ML
```

### Step 2: Create Virtual Environment (if not exists)
```bash
python -m venv venv
```

### Step 3: Activate Virtual Environment

**Windows:**
```bash
venv\Scripts\activate.bat
```

**Linux/Mac:**
```bash
source venv/bin/activate
```

### Step 4: Install Dependencies
```bash
pip install -r requirements.txt
```

This will install:
- PyTorch (CPU or GPU)
- Transformers (for CLIP)
- PyTorch Geometric (for GNN)
- Sentence Transformers (for SBERT)
- Flask (for API)
- Other dependencies

**Note:** First installation may take 5-10 minutes as it downloads models.

### Step 5: Start the API

**Option A: Start only multimodal matching API**
```bash
start_multimodal_matching_api.bat
```

**Option B: Start all ML APIs**
```bash
start_all_apis.bat
```

The API will start on `http://localhost:5003`

### Step 6: Test the System

**Option A: Run test suite**
```bash
python test_multimodal_matching.py
```

**Option B: Run standalone demo**
```bash
python demo_multimodal_system.py
```

## Detailed Installation

### System Requirements

**Minimum:**
- Python 3.8+
- 4GB RAM
- 2GB disk space
- CPU (Intel/AMD)

**Recommended:**
- Python 3.9+
- 8GB RAM
- 5GB disk space
- GPU (NVIDIA with CUDA) - Optional but faster

### Dependency Installation

#### 1. Core Dependencies
```bash
pip install torch>=2.0.0
pip install transformers>=4.30.0
pip install sentence-transformers>=2.2.0
```

#### 2. PyTorch Geometric

**For CPU:**
```bash
pip install torch-geometric
```

**For GPU (CUDA 11.8):**
```bash
pip install torch-geometric -f https://data.pyg.org/whl/torch-2.0.0+cu118.html
```

#### 3. Other Dependencies
```bash
pip install flask flask-cors
pip install Pillow requests
pip install numpy pandas scikit-learn
```

### Troubleshooting Installation

#### Issue 1: PyTorch Geometric Installation Fails

**Solution 1:** Install PyTorch first
```bash
pip install torch
pip install torch-geometric
```

**Solution 2:** Use conda
```bash
conda install pytorch torchvision torchaudio -c pytorch
conda install pyg -c pyg
```

#### Issue 2: CUDA/GPU Issues

**Solution:** Force CPU installation
```bash
pip install torch --index-url https://download.pytorch.org/whl/cpu
```

#### Issue 3: Transformers Download Fails

**Solution:** Set HuggingFace cache directory
```bash
set HF_HOME=C:\huggingface_cache
pip install transformers
```

#### Issue 4: Out of Memory

**Solution:** Use CPU mode
- Edit `clip_service.py`
- Change `self.device = "cuda"` to `self.device = "cpu"`

### Verifying Installation

#### 1. Check Python Version
```bash
python --version
# Should be 3.8 or higher
```

#### 2. Check PyTorch
```python
import torch
print(torch.__version__)
print(torch.cuda.is_available())  # True if GPU available
```

#### 3. Check Transformers
```python
from transformers import CLIPModel
print("Transformers OK")
```

#### 4. Check PyTorch Geometric
```python
import torch_geometric
print(torch_geometric.__version__)
```

#### 5. Check Sentence Transformers
```python
from sentence_transformers import SentenceTransformer
model = SentenceTransformer('all-MiniLM-L6-v2')
print("SBERT OK")
```

### Model Downloads

On first run, the system will download:

1. **CLIP Model** (~600MB)
   - `openai/clip-vit-base-patch32`
   - Location: `~/.cache/huggingface/`

2. **SBERT Model** (~90MB)
   - `all-MiniLM-L6-v2`
   - Location: `~/.cache/torch/sentence_transformers/`

**Total download:** ~700MB

**Download time:** 5-10 minutes (depending on internet speed)

### Configuration

#### Change API Port

Edit `multimodal_item_matching_api.py`:
```python
port = int(os.environ.get('MULTIMODAL_MATCHING_PORT', 5003))
```

Or set environment variable:
```bash
set MULTIMODAL_MATCHING_PORT=8080
```

#### Use Different CLIP Model

Edit `clip_service.py`:
```python
def __init__(self, model_name='openai/clip-vit-large-patch14'):
    # Use larger model for better accuracy
```

Available models:
- `openai/clip-vit-base-patch32` (default, 151M params)
- `openai/clip-vit-base-patch16` (151M params, better quality)
- `openai/clip-vit-large-patch14` (428M params, best quality)

#### Use GAT Instead of GraphSAGE

Edit `multimodal_item_matching_api.py`:
```python
matching_service = MultimodalMatchingService(use_gat=True)
```

### Performance Optimization

#### 1. Use GPU
- Install CUDA toolkit
- Install GPU version of PyTorch
- System will automatically use GPU if available

#### 2. Reduce Model Size
- Use smaller CLIP model: `openai/clip-vit-base-patch32`
- Use smaller SBERT model: `all-MiniLM-L6-v2`

#### 3. Batch Processing
- Process multiple items at once
- Use `encode_batch_images` and `encode_batch_texts`

#### 4. Caching
- Cache embeddings in database
- Reuse embeddings for repeated queries

### Docker Installation (Optional)

Create `Dockerfile.multimodal`:
```dockerfile
FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install -r requirements.txt

COPY *.py .
COPY models/ models/

EXPOSE 5003

CMD ["python", "multimodal_item_matching_api.py"]
```

Build and run:
```bash
docker build -f Dockerfile.multimodal -t multimodal-matching .
docker run -p 5003:5003 multimodal-matching
```

### Integration with Backend

Update `appsettings.json`:
```json
{
  "ItemMatching": {
    "ApiUrl": "http://localhost:5003"
  }
}
```

The existing `ItemMatchingService.cs` will work with the new API as it's backward compatible.

### Testing Installation

Run the complete test:
```bash
python test_multimodal_matching.py
```

Expected output:
```
✓ PASS: Health Check
✓ PASS: Multimodal Similarity
✓ PASS: GNN Trust Scoring
✓ PASS: Full Matching Pipeline

Total: 4/4 tests passed
```

### Next Steps

1. Read the [README](MULTIMODAL_MATCHING_README.md) for API documentation
2. Run the [demo](demo_multimodal_system.py) to see examples
3. Integrate with your C# backend using existing `ItemMatchingService.cs`
4. Monitor performance and adjust weights as needed

### Support

For issues:
1. Check logs in the API console window
2. Verify all dependencies are installed
3. Check Python version (3.8+)
4. Ensure sufficient disk space for models
5. Try CPU mode if GPU issues occur

### Uninstallation

To remove the system:
```bash
# Deactivate virtual environment
deactivate

# Remove virtual environment
rmdir /s venv

# Remove cached models (optional)
rmdir /s %USERPROFILE%\.cache\huggingface
rmdir /s %USERPROFILE%\.cache\torch
```
