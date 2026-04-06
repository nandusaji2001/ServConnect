"""
Test script to verify false positive fix
Tests the previously blocked innocent text
"""

import requests
import json

API_URL = "http://localhost:5051"

def test_content(text, expected_result="approve"):
    """Test a piece of content"""
    print(f"\n{'='*60}")
    print(f"Testing: '{text}'")
    print(f"Expected: {expected_result}")
    print(f"{'='*60}")
    
    try:
        response = requests.post(
            f"{API_URL}/analyze/content",
            json={"text": text},
            timeout=10
        )
        
        if response.status_code == 200:
            result = response.json()
            
            print(f"✓ API Response:")
            print(f"  - Toxicity Score: {result.get('text_toxicity_score', 0):.4f}")
            print(f"  - Final Risk Score: {result.get('final_risk_score', 0):.4f}")
            print(f"  - Is Harmful: {result.get('is_harmful', False)}")
            print(f"  - Recommendation: {result.get('recommendation', 'unknown')}")
            print(f"  - Reason: {result.get('reason', 'N/A')}")
            
            actual = result.get('recommendation', 'unknown')
            if actual == expected_result:
                print(f"\n✅ PASS: Got expected result '{expected_result}'")
            else:
                print(f"\n❌ FAIL: Expected '{expected_result}' but got '{actual}'")
            
            return result
        else:
            print(f"❌ API Error: {response.status_code}")
            print(response.text)
            return None
            
    except requests.exceptions.ConnectionError:
        print("❌ ERROR: Cannot connect to API. Is it running on port 5051?")
        print("\nTo start the API, run:")
        print("  cd backend/ML")
        print("  start_intelligent_api_no_gnn.bat")
        return None
    except Exception as e:
        print(f"❌ ERROR: {e}")
        return None


def main():
    print("="*60)
    print("FALSE POSITIVE FIX - TEST SUITE")
    print("="*60)
    print("\nTesting with new threshold of 0.7")
    print("(Previously was 0.3, causing false positives)")
    
    # Check if API is running
    try:
        health = requests.get(f"{API_URL}/health", timeout=5)
        if health.status_code == 200:
            print(f"\n✓ API is running on {API_URL}")
            print(f"  Service: {health.json()}")
        else:
            print(f"\n⚠ API health check failed: {health.status_code}")
    except:
        print(f"\n❌ API is not running on {API_URL}")
        print("\nPlease start the API first:")
        print("  cd backend/ML")
        print("  start_intelligent_api_no_gnn.bat")
        return
    
    # Test cases
    test_cases = [
        # Previously blocked (false positive) - should now be approved
        ("What are you doing. Here I am having a cup of tea", "approve"),
        
        # Innocent content - should be approved
        ("Hello everyone! How is your day going?", "approve"),
        ("I love this community, everyone is so helpful!", "approve"),
        ("Looking forward to the weekend!", "approve"),
        
        # Borderline content - might be flagged
        ("This is stupid", "flag"),
        
        # Clearly toxic - should be blocked
        ("You are an idiot and I hate you", "block"),
        ("Go kill yourself", "block"),
        ("I will hurt you", "block"),
    ]
    
    results = []
    passed = 0
    failed = 0
    
    for text, expected in test_cases:
        result = test_content(text, expected)
        if result:
            actual = result.get('recommendation', 'unknown')
            if actual == expected:
                passed += 1
            else:
                failed += 1
            results.append((text, expected, actual, result))
    
    # Summary
    print("\n" + "="*60)
    print("TEST SUMMARY")
    print("="*60)
    print(f"Total tests: {len(test_cases)}")
    print(f"✅ Passed: {passed}")
    print(f"❌ Failed: {failed}")
    
    if failed > 0:
        print("\nFailed tests:")
        for text, expected, actual, result in results:
            if expected != actual:
                print(f"  - '{text[:50]}...'")
                print(f"    Expected: {expected}, Got: {actual}")
    
    print("\n" + "="*60)
    if failed == 0:
        print("🎉 ALL TESTS PASSED!")
    else:
        print("⚠️ Some tests failed. Review thresholds if needed.")
    print("="*60)


if __name__ == "__main__":
    main()
