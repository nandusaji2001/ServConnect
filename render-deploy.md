# ServConnect Render Deployment Guide

## Prerequisites
1. **MongoDB Atlas**: Set up a MongoDB Atlas cluster (free tier available)
2. **Render Account**: Create an account at [render.com](https://render.com)
3. **Pre-trained ML Models**: Ensure `backend/ML/models/*.pkl` files are committed

## Pre-Deployment: Train ML Model Locally

Before deploying, ensure the ML models are trained and committed:

```bash
cd backend/ML
pip install -r requirements.txt
python train_model.py
git add models/*.pkl
git commit -m "Add pre-trained ML models"
git push
```

## Deployment Steps

### 1. Deploy ML Content Moderation API (First)

1. **Create New Web Service**:
   - Go to Render Dashboard → "New +" → "Web Service"
   - Connect your GitHub repository
   
2. **Configure Service**:
   - **Name**: `servconnect-ml-api`
   - **Root Directory**: `backend/ML`
   - **Environment**: `Docker`
   - **Region**: Choose closest to your users
   - **Instance Type**: Free (or Starter for better performance)

3. **No Environment Variables Needed** for ML API

4. **Note the URL**: After deployment, note the URL (e.g., `https://servconnect-ml-api.onrender.com`)

### 2. Deploy Main Application (Second)

1. **Create New Web Service**:
   - Go to Render Dashboard → "New +" → "Web Service"
   - Connect your GitHub repository

2. **Configure Service**:
   - **Name**: `servconnect-app`
   - **Root Directory**: Leave empty (uses root Dockerfile)
   - **Environment**: `Docker`
   - **Region**: Same as ML API
   - **Instance Type**: Free or Starter

3. **Set Environment Variables**:

```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT

# MongoDB Atlas Connection
MongoDB__ConnectionString=mongodb+srv://username:password@cluster.mongodb.net/ServConnectDb?retryWrites=true&w=majority
MongoDB__DatabaseName=ServConnectDb

# Content Moderation ML API (use your ML API URL from step 1)
ContentModeration__ApiUrl=https://servconnect-ml-api.onrender.com
ContentModeration__Threshold=0.7

# Razorpay (if using payments)
Razorpay__KeyId=your_razorpay_key_id
Razorpay__KeySecret=your_razorpay_key_secret

# Firebase (if using notifications)
Firebase__ProjectId=your_firebase_project_id

# Other services as needed...
```

### 3. MongoDB Atlas Setup

1. **Create Cluster**:
   - Go to [MongoDB Atlas](https://cloud.mongodb.com)
   - Create a free cluster
   - Choose a region close to your Render deployment

2. **Configure Access**:
   - **Database Access**: Create a user with read/write permissions
   - **Network Access**: Add `0.0.0.0/0` (allow access from anywhere)
   - **Connection String**: Copy and use in environment variables

## Local Development

### Without Docker (Recommended for Development)

**Terminal 1 - ML API:**
```bash
cd backend/ML
pip install -r requirements.txt
python train_model.py  # Only needed once
python content_moderation_api.py
```

**Terminal 2 - .NET App:**
```bash
cd backend
dotnet run
```

### With Docker

```bash
# Start Docker Desktop first, then:
docker-compose up --build
```

## Testing Content Moderation

Test the ML API directly:
```bash
curl -X POST http://localhost:5050/predict \
  -H "Content-Type: application/json" \
  -d '{"text": "I will kill you", "threshold": 0.7}'
```

Test via .NET debug endpoint:
```
GET https://localhost:PORT/debug/content-moderation/test?text=I%20will%20kill%20you
```

## Troubleshooting

### ML API Issues
- Check Render logs for the ML service
- Ensure the model files are being created during build
- Verify the API is responding at `/health` endpoint

### Content Moderation Not Working
- Verify `ContentModeration__ApiUrl` is set correctly
- Check if ML API service is running
- Test the debug endpoint to verify connectivity

### Free Tier Limitations
- Render free tier services spin down after inactivity
- First request after spin-down may take 30-60 seconds
- Consider Starter tier for production use

## Architecture

```
┌─────────────────┐     ┌─────────────────┐
│   .NET App      │────▶│   ML API        │
│   (Port 8080)   │     │   (Port 5050)   │
└────────┬────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐
│  MongoDB Atlas  │
└─────────────────┘
```

Your ServConnect application with ML content moderation is now ready for deployment! 🚀
