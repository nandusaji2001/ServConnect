# ID Verification with OCR - Auto-Approval Feature

## Overview

This feature implements automatic ID verification using OCR (Optical Character Recognition) to compare the name on a user's uploaded ID card with the name they provided during registration. If the names match (case-insensitive), the user is automatically approved without requiring admin intervention.

## How It Works

1. **User Registration Flow:**
   - User registers and logs in
   - User is directed to Profile Completion page
   - User uploads their ID card (image format: JPEG, PNG, GIF, WebP)

2. **OCR Verification Process:**
   - When an ID card image is uploaded, the system sends it to the ID Verification ML API
   - The API uses EasyOCR to extract all text from the ID card
   - The system identifies potential names from the extracted text
   - The extracted names are compared with the user's registered name using fuzzy matching

3. **Auto-Approval Logic:**
   - If the similarity score is ≥ 75% (configurable), the user is auto-approved
   - The system is case-insensitive (handles uppercase/lowercase variations)
   - If the name doesn't match, the user is notified and waits for admin approval

## Components

### 1. Python ML API (`backend/ML/id_verification_api.py`)

A Flask-based API running on port 5004 that provides:
- **POST /verify** - Main verification endpoint
- **POST /extract-text** - Debug endpoint to extract all text from an image
- **GET /health** - Health check endpoint

### 2. C# Backend Service (`backend/Services/IdVerificationService.cs`)

A service that:
- Communicates with the Python API
- Handles base64 image encoding
- Processes verification results
- Includes health check and error handling

### 3. Database Fields (Users model)

New fields added to track verification status:
- `IsIdVerified` - Whether OCR verification was attempted
- `IsIdAutoApproved` - Whether the user was auto-approved via OCR
- `IdVerificationScore` - Similarity score (0-1)
- `IdVerificationMessage` - Status message for user
- `ExtractedNameFromId` - The name that was detected on the ID
- `IdVerifiedAtUtc` - Timestamp of verification

### 4. View Updates (`Views/Account/Profile.cshtml`)

Updated to show:
- Success message when auto-approved
- Verification score and extracted name
- Waiting message when pending admin approval
- Helpful information about why verification failed

## Configuration

### appsettings.json

```json
{
  "IdVerification": {
    "ApiUrl": "http://localhost:5004",
    "Threshold": "0.75"
  }
}
```

- **ApiUrl**: URL where the ID Verification API is running
- **Threshold**: Minimum similarity score (0-1) for auto-approval. Default is 0.75 (75%)

## Running the ID Verification API

### Option 1: Individual Start
```bash
cd backend/ML
./start_id_verification_api.bat
```

### Option 2: Start All ML APIs
```bash
cd backend/ML
./start_all_apis.bat
```

### Option 3: Manual Start
```bash
cd backend/ML
python id_verification_api.py
```

## Supported ID Formats

The OCR system is optimized for:
- **Indian ID Cards**: Aadhaar, Voter ID, PAN Card, Driving License
- **Image Formats**: JPEG, PNG, GIF, WebP
- **Note**: PDF files require manual admin approval (cannot be OCR processed)

## Name Matching Algorithm

1. **Text Extraction**: EasyOCR extracts all text from the ID image
2. **Name Detection**: The system looks for:
   - Text following "Name:", "NAAM:", etc.
   - Standalone names (properly capitalized words)
   - Text near name indicators

3. **Fuzzy Matching**: Uses Python's `SequenceMatcher` for comparison:
   - Handles typos and minor variations
   - Case-insensitive comparison
   - Ignores special characters and extra spaces

4. **Similarity Score**: Returns a score between 0-1:
   - ≥ 0.75 (75%): Auto-approved
   - < 0.75: Requires admin approval

## User Messages

| Scenario | Message Shown |
|----------|---------------|
| Auto-approved | "Your ID has been verified successfully! Your account is now approved." |
| Name mismatch | "Name mismatch detected. Best match: X%. Requires admin approval." |
| No name found | "Could not extract name from ID card. Requires admin approval." |
| PDF uploaded | "PDF documents require admin review for verification." |
| API unavailable | "ID verification service unavailable. Your ID will be reviewed by an admin." |

## Debug Endpoints

- **GET /debug/id-verification/status** - Check service availability and configuration

## Dependencies

### Python (ML API)
- `easyocr>=1.7.0`
- `Pillow>=10.0.0`
- `flask>=2.3.0`
- `flask-cors>=4.0.0`

### C# Backend
- No additional NuGet packages required

## Error Handling

- If the ML API is unavailable, users are queued for manual admin approval
- Timeout is set to 60 seconds for OCR processing
- All errors are logged for debugging
- Users always receive clear feedback about their verification status

## Security Considerations

- ID card images are stored securely on the server
- Only image formats are processed by OCR (not PDFs)
- Base64 encoding is used for API communication
- No sensitive data is logged (only status messages)
