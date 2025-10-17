# ServConnect Render Deployment Guide

## Prerequisites
1. **MongoDB Atlas**: Set up a MongoDB Atlas cluster (free tier available)
2. **Render Account**: Create an account at [render.com](https://render.com)
3. **Environment Variables**: Prepare your configuration values

## Deployment Steps

### 1. Prepare Your Repository
Ensure your repository contains:
- ✅ `Dockerfile` (created)
- ✅ `.dockerignore` (created)
- ✅ Your application code in `/backend` folder

### 2. Environment Variables for Render
Set these environment variables in your Render service:

```bash
# Required Environment Variables
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:$PORT

# MongoDB Configuration (replace with your MongoDB Atlas connection string)
MongoDB__ConnectionString=mongodb+srv://username:password@cluster.mongodb.net/ServConnectDb?retryWrites=true&w=majority
MongoDB__DatabaseName=ServConnectDb

# Razorpay Configuration (if using payments)
Razorpay__KeyId=your_razorpay_key_id
Razorpay__KeySecret=your_razorpay_key_secret

# Google Maps API (if using location services)
GoogleMaps__ApiKey=your_google_maps_api_key

# Firebase Configuration (if using push notifications)
Firebase__ProjectId=your_firebase_project_id
Firebase__PrivateKeyId=your_private_key_id
Firebase__PrivateKey=your_private_key
Firebase__ClientEmail=your_client_email
Firebase__ClientId=your_client_id
Firebase__AuthUri=https://accounts.google.com/o/oauth2/auth
Firebase__TokenUri=https://oauth2.googleapis.com/token

# Security (generate a strong random string)
JWT__SecretKey=your_jwt_secret_key_here
```

### 3. Deploy on Render

1. **Connect Repository**:
   - Go to Render Dashboard
   - Click "New +" → "Web Service"
   - Connect your GitHub repository

2. **Configure Service**:
   - **Name**: `servconnect-app`
   - **Environment**: `Docker`
   - **Region**: Choose closest to your users
   - **Branch**: `main` (or your deployment branch)
   - **Build Command**: Leave empty (Docker handles this)
   - **Start Command**: Leave empty (Docker handles this)

3. **Set Environment Variables**:
   - Add all the environment variables listed above
   - Make sure to use your actual values

4. **Advanced Settings**:
   - **Auto-Deploy**: Enable for automatic deployments
   - **Health Check Path**: `/` (or create a health endpoint)

### 4. MongoDB Atlas Setup

1. **Create Cluster**:
   - Go to [MongoDB Atlas](https://cloud.mongodb.com)
   - Create a free cluster
   - Choose a region close to your Render deployment

2. **Configure Access**:
   - **Database Access**: Create a user with read/write permissions
   - **Network Access**: Add `0.0.0.0/0` (allow access from anywhere)
   - **Connection String**: Copy the connection string and update the environment variable

### 5. Domain Configuration (Optional)

1. **Custom Domain**:
   - In Render dashboard, go to your service
   - Click "Settings" → "Custom Domains"
   - Add your domain and configure DNS

## Local Development with Docker

To test locally before deploying:

```bash
# Build and run with Docker Compose
docker-compose up --build

# Or build and run manually
docker build -t servconnect .
docker run -p 8080:8080 servconnect
```

## Troubleshooting

### Common Issues:

1. **Build Failures**:
   - Check Dockerfile syntax
   - Ensure all dependencies are in UsersApp.csproj
   - Verify .dockerignore is not excluding necessary files

2. **Runtime Errors**:
   - Check environment variables are set correctly
   - Verify MongoDB connection string
   - Check Render logs for detailed error messages

3. **Port Issues**:
   - Render automatically sets the PORT environment variable
   - Application listens on port 8080 internally
   - Render handles external port mapping

### Viewing Logs:
- Go to your Render service dashboard
- Click "Logs" tab to view real-time application logs
- Use logs to debug any deployment issues

## Security Considerations

1. **Environment Variables**: Never commit sensitive data to your repository
2. **MongoDB**: Use MongoDB Atlas with proper authentication
3. **HTTPS**: Render provides free SSL certificates
4. **API Keys**: Store all API keys as environment variables

## Performance Optimization

1. **Docker Image**: Multi-stage build reduces image size
2. **Static Files**: Consider using CDN for static assets
3. **Database**: Use MongoDB Atlas for better performance and reliability
4. **Caching**: Implement Redis caching if needed (can be added as another Render service)

Your ServConnect application should now be successfully deployed on Render! 🚀
