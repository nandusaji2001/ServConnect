# Troubleshooting Guide - Trust Propagation System

## Common Issues and Solutions

### 1. Port Already in Use (Error 10048)

**Error:**
```
ERROR: [Errno 10048] error while attempting to bind on address ('0.0.0.0', 8006)
```

**Cause:** The API is already running from a previous session.

**Solution:**

#### Option A: Find and Kill the Process
```bash
# Find what's using port 8006
netstat -ano | findstr :8006

# Kill the process (replace PID with the number from above)
taskkill /PID <PID> /F
```

#### Option B: Use a Different Port
Edit `trust_propagation_api.py` and change the port:
```python
# Change this line at the bottom:
uvicorn.run(app, host="0.0.0.0", port=8007)  # Changed from 8006 to 8007
```

Then update `appsettings.json`:
```json
{
  "MLServices": {
    "TrustPropagationApi": "http://localhost:8007"
  }
}
```

### 2. Module Not Found: fastapi

**Error:**
```
ModuleNotFoundError: No module named 'fastapi'
```

**Solution:**
```bash
pip install fastapi uvicorn pydantic
```

If that doesn't work, try:
```bash
python -m pip install fastapi uvicorn pydantic
```

### 3. Both APIs Need to Run

**Question:** Do I need both APIs running?

**Answer:** YES! You need:
1. **Intelligent Moderation API** (port 8002) - Detects harmful content
2. **Trust Propagation API** (port 8006) - Propagates trust penalties

**Easy Start:**
```bash
cd backend/ML
START_ALL_MODERATION_APIS.bat
```

This opens both APIs in separate windows.

### 4. API Won't Start - Missing Dependencies

**Error:** Various import errors

**Solution:** Install all requirements:
```bash
cd backend/ML
pip install -r requirements.txt
```

### 5. Can't Connect to API from C#

**Error:** Connection refused or timeout

**Checklist:**
- [ ] Is the API running? Check the console window
- [ ] Is it on the right port? (8006 for trust propagation)
- [ ] Is the URL correct in appsettings.json?
- [ ] Is your firewall blocking it?

**Test manually:**
```bash
curl http://localhost:8006/
```

Should return:
```json
{
  "service": "Trust Propagation API",
  "status": "running",
  "version": "1.0.0"
}
```

### 6. Trust Scores Not Updating

**Possible Causes:**

1. **API not called**
   - Check if TrustPropagationService is registered in Program.cs
   - Check controller integration
   - Look for error logs

2. **No followers in database**
   - Verify UserB actually follows UserA
   - Check UserFollows collection in MongoDB

3. **MongoDB connection issue**
   - Verify MongoDB is running
   - Check connection string in appsettings.json

**Debug Steps:**
```bash
# Check if graph is built
curl -X POST http://localhost:8006/graph-stats

# Check MongoDB
mongo
use ServConnectDb
db.UserFollows.find().count()
db.CommunityProfiles.find({}, {Username:1, UserTrustScore:1})
```

### 7. Build Errors in C#

**Error:** `'PostComment' does not contain a definition for 'UserId'`

**Solution:** Already fixed! Use `AuthorId` instead of `UserId`.

If you still see this error:
```bash
# Clean and rebuild
dotnet clean backend
dotnet build backend
```

### 8. Python Version Issues

**Minimum Requirements:**
- Python 3.8 or higher
- pip 20.0 or higher

**Check your version:**
```bash
python --version
pip --version
```

**Upgrade if needed:**
```bash
python -m pip install --upgrade pip
```

### 9. Test Content Not Being Flagged

**Problem:** Content that should be flagged is being approved

**Solutions:**

1. **Test toxicity score first:**
```bash
python test_demo_content.py "Your content here"
```

2. **Adjust content to be more critical:**
   - Use stronger negative language
   - Add more critical statements
   - Test with the provided examples

3. **Check trust scores:**
```javascript
// In MongoDB
db.CommunityProfiles.find({Username: "Follower"})
// Should show ContentTrustScore > 0.6
```

### 10. Demo Not Working

**Checklist:**

- [ ] Both APIs running (8002 and 8006)
- [ ] UserB follows UserA in database
- [ ] UserA has been banned (5 violations)
- [ ] Trust scores updated (check MongoDB)
- [ ] Using borderline content (toxicity 0.55-0.70)

**Quick Test:**
```bash
# Test APIs
curl http://localhost:8002/
curl http://localhost:8006/

# Test trust propagation
python test_trust_propagation.py

# Test demo content
python test_demo_content.py
```

## Quick Fixes

### Reset Everything

If nothing works, start fresh:

```bash
# 1. Stop all Python processes
taskkill /F /IM python.exe

# 2. Reinstall dependencies
cd backend/ML
pip uninstall fastapi uvicorn pydantic -y
pip install fastapi uvicorn pydantic

# 3. Start APIs
START_ALL_MODERATION_APIS.bat

# 4. Test
python test_trust_propagation.py
```

### Check API Status

```bash
# Windows
netstat -ano | findstr :8002
netstat -ano | findstr :8006

# Test endpoints
curl http://localhost:8002/
curl http://localhost:8006/
curl http://localhost:8006/graph-stats
```

### View Logs

APIs print logs to console. Check the terminal windows for:
- Startup messages
- Error messages
- Request logs

## Getting Help

### Collect Debug Info

Before asking for help, collect:

1. **API Status:**
```bash
curl http://localhost:8002/
curl http://localhost:8006/
```

2. **Python Version:**
```bash
python --version
pip list | findstr "fastapi\|uvicorn\|pydantic"
```

3. **Error Messages:**
   - Copy full error from console
   - Include stack trace

4. **MongoDB Data:**
```javascript
db.UserFollows.find().count()
db.CommunityProfiles.find({}, {Username:1, UserTrustScore:1, ContentTrustScore:1})
```

### Common Error Patterns

| Error | Cause | Fix |
|-------|-------|-----|
| Port in use | API already running | Kill process or use different port |
| Module not found | Missing package | `pip install <package>` |
| Connection refused | API not running | Start the API |
| No users affected | No followers | Create follower relationships |
| Build error | Wrong property name | Use `AuthorId` not `UserId` |

## Still Having Issues?

1. Check the documentation:
   - `TRUST_PROPAGATION_README.md`
   - `DEMO_GUIDE_COMPLETE.md`
   - `QUICK_DEMO_REFERENCE.md`

2. Run the tests:
```bash
python test_trust_propagation.py
python test_demo_content.py
```

3. Verify your setup matches the requirements in `TRUST_PROPAGATION_CHECKLIST.md`
