"""
Quick Test Script for Intelligent Moderation API
Tests if the API is running and responding correctly
"""

import requests
import json
import base64
from PIL import Image
import io

API_URL = "http://localhost:5051"

def test_health():
    """Test health endpoint"""
    print("Testing health endpoint...")
    try:
        response = requests.get(f"{API_URL}/health", timeout=5)
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Health check passed: {data}")
            return True
        else:
            print(f"❌ Health check failed: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Cannot connect to API: {e}")
        print(f"   Make sure the API is running on {API_URL}")
        return False

def test_text_analysis():
    """Test text analysis endpoint"""
    print("\nTesting text analysis...")
    try:
        payload = {
            "text": "This is a test message for content moderation"
        }
        response = requests.post(
            f"{API_URL}/analyze/text",
            json=payload,
            timeout=10
        )
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Text analysis passed")
            print(f"   Toxicity Score: {data.get('toxicity_score', 'N/A')}")
            return True
        else:
            print(f"❌ Text analysis failed: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Text analysis error: {e}")
        return False

def test_content_analysis():
    """Test comprehensive content analysis"""
    print("\nTesting comprehensive content analysis...")
    try:
        payload = {
            "text": "Welcome to our community!",
            "user_id": "user1",
            "post_id": "post1"
        }
        response = requests.post(
            f"{API_URL}/analyze/content",
            json=payload,
            timeout=15
        )
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Content analysis passed")
            print(f"   Text Toxicity: {data.get('text_toxicity_score', 'N/A'):.3f}")
            print(f"   Image Risk: {data.get('image_risk_score', 'N/A'):.3f}")
            print(f"   User Trust: {data.get('user_trust_score', 'N/A'):.3f}")
            print(f"   Final Risk: {data.get('final_risk_score', 'N/A'):.3f}")
            print(f"   Recommendation: {data.get('recommendation', 'N/A').upper()}")
            return True
        else:
            print(f"❌ Content analysis failed: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Content analysis error: {e}")
        return False

def test_trust_score():
    """Test GNN trust score endpoint"""
    print("\nTesting GNN trust score...")
    try:
        payload = {
            "user_id": "user1"
        }
        response = requests.post(
            f"{API_URL}/gnn/trust",
            json=payload,
            timeout=10
        )
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Trust score passed")
            print(f"   User: {data.get('user_id', 'N/A')}")
            print(f"   Trust Score: {data.get('trust_score', 'N/A'):.3f}")
            return True
        else:
            print(f"❌ Trust score failed: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Trust score error: {e}")
        return False

def test_legacy_endpoint():
    """Test legacy backward compatibility endpoint"""
    print("\nTesting legacy endpoint (backward compatibility)...")
    try:
        payload = {
            "text": "This is a test",
            "threshold": 0.5
        }
        response = requests.post(
            f"{API_URL}/predict",
            json=payload,
            timeout=10
        )
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Legacy endpoint passed")
            print(f"   Is Harmful: {data.get('is_harmful', 'N/A')}")
            print(f"   Confidence: {data.get('confidence', 'N/A'):.3f}")
            return True
        else:
            print(f"❌ Legacy endpoint failed: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Legacy endpoint error: {e}")
        return False

def main():
    """Run all tests"""
    print("="*60)
    print("INTELLIGENT MODERATION API TEST")
    print("="*60)
    print(f"API URL: {API_URL}")
    print()
    
    results = []
    
    # Test health first
    if not test_health():
        print("\n" + "="*60)
        print("❌ API is not running or not accessible")
        print("="*60)
        print("\nTo start the API:")
        print("  1. cd backend/ML")
        print("  2. python intelligent_moderation_api.py")
        print("\nOr use: start_all_apis.bat")
        return
    
    # Run other tests
    results.append(("Text Analysis", test_text_analysis()))
    results.append(("Content Analysis", test_content_analysis()))
    results.append(("Trust Score", test_trust_score()))
    results.append(("Legacy Endpoint", test_legacy_endpoint()))
    
    # Summary
    print("\n" + "="*60)
    print("TEST SUMMARY")
    print("="*60)
    
    passed = sum(1 for _, result in results if result)
    total = len(results)
    
    for test_name, result in results:
        status = "✅ PASSED" if result else "❌ FAILED"
        print(f"{test_name:20s} {status}")
    
    print()
    print(f"Total: {passed}/{total} tests passed")
    
    if passed == total:
        print("\n🎉 All tests passed! API is working correctly.")
    else:
        print(f"\n⚠️  {total - passed} test(s) failed. Check the API logs.")
    
    print("="*60)

if __name__ == '__main__':
    main()
