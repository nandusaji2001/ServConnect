# ML Content Moderation API Dockerfile
# Uses pre-trained models committed to repository
FROM python:3.11-slim

WORKDIR /app

# Copy requirements and install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy pre-trained models (trained locally and committed to repo)
COPY models/ ./models/

# Copy application file
COPY content_moderation_api.py .

# Expose port
EXPOSE 5050

# Health check
# HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
#     CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:5050/health')" || exit 1

# Run the API
CMD ["python", "content_moderation_api.py"]
