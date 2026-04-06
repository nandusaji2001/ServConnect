"""
Test script to verify toxicity scores for demo content
This helps you find the perfect borderline content for demonstration
"""

import requests
import json

# Test content examples
test_contents = [
    {
        "name": "Borderline Political",
        "text": "I really disagree with this policy. The government officials are making terrible decisions that hurt our community. This is frustrating and needs to change immediately!"
    },
    {
        "name": "Negative Review",
        "text": "This service is absolutely terrible. I'm so disappointed with how things are being handled. What a waste of time and money. Not recommended at all."
    },
    {
        "name": "Strong Criticism",
        "text": "I strongly oppose this decision. The leadership has failed to consider the consequences. This approach is wrong and will cause problems. We need better solutions now."
    },
    {
        "name": "Frustrated Complaint",
        "text": "This is ridiculous! I can't believe how badly this was handled. The people in charge clearly don't care about us. What a disaster."
    },
    {
        "name": "Angry but Civil",
        "text": "I'm extremely upset about this situation. The management has completely failed us. This is unacceptable and needs immediate attention."
    },
    {
        "name": "Mild Negative",
        "text": "I'm not happy with this outcome. Things could have been handled better. Hopefully improvements will be made soon."
    },
    {
        "name": "Neutral Critical",
        "text": "There are several issues with this approach. The current system has flaws that should be addressed. Better planning is needed."
    }
]

def test_content(api_url="http://localhost:8002"):
    """Test content against moderation API"""
    
    print("=" * 70)
    print("TESTING DEMO CONTENT FOR TRUST PROPAGATION")
    print("=" * 70)
    print()
    
    # Trust score configurations
    normal_user = {
        "name": "Normal User",
        "user_trust": 0.8,
        "content_trust": 0.5,
        "threshold": 0.7
    }
    
    affected_user = {
        "name": "Follower of Banned User",
        "user_trust": 0.68,
        "content_trust": 0.68,
        "threshold": 0.7 * (1.0 - 0.68 * 0.3)  # 0.557
    }
    
    print(f"Normal User Threshold: {normal_user['threshold']:.3f}")
    print(f"Affected User Threshold: {affected_user['threshold']:.3f}")
    print()
    print("Looking for content with toxicity between 0.557 and 0.700")
    print("(Approved for normal user, flagged for affected user)")
    print()
    print("=" * 70)
    print()
    
    perfect_examples = []
    
    for content in test_contents:
        print(f"Testing: {content['name']}")
        print(f"Text: {content['text'][:60]}...")
        
        try:
            # Call moderation API
            response = requests.post(
                f"{api_url}/moderate",
                json={
                    "text": content['text'],
                    "media_urls": []
                },
                timeout=10
            )
            
            if response.status_code == 200:
                result = response.json()
                toxicity = result.get('toxicity_score', 0)
                
                # Check if it's in the perfect range
                is_perfect = (affected_user['threshold'] < toxicity < normal_user['threshold'])
                
                # Determine results
                normal_result = "✅ APPROVED" if toxicity < normal_user['threshold'] else "❌ FLAGGED"
                affected_result = "✅ APPROVED" if toxicity < affected_user['threshold'] else "❌ FLAGGED"
                
                print(f"  Toxicity Score: {toxicity:.3f}")
                print(f"  Normal User: {normal_result}")
                print(f"  Affected User: {affected_result}")
                
                if is_perfect:
                    print(f"  ⭐ PERFECT FOR DEMO! ⭐")
                    perfect_examples.append({
                        'name': content['name'],
                        'text': content['text'],
                        'toxicity': toxicity
                    })
                
                print()
            else:
                print(f"  ❌ API Error: {response.status_code}")
                print()
                
        except requests.exceptions.ConnectionError:
            print(f"  ❌ Cannot connect to API at {api_url}")
            print(f"  Make sure the Intelligent Moderation API is running!")
            print()
            return
        except Exception as e:
            print(f"  ❌ Error: {str(e)}")
            print()
    
    # Summary
    print("=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print()
    
    if perfect_examples:
        print(f"Found {len(perfect_examples)} PERFECT examples for demo:")
        print()
        for i, example in enumerate(perfect_examples, 1):
            print(f"{i}. {example['name']} (Toxicity: {example['toxicity']:.3f})")
            print(f"   \"{example['text'][:80]}...\"")
            print()
        
        print("Use any of these for your demonstration!")
        print()
        print("Expected behavior:")
        print("  - Normal user: Content APPROVED ✅")
        print("  - Follower of banned user: Content FLAGGED ❌")
    else:
        print("No perfect examples found in the test set.")
        print("Try adjusting the content to be more critical/negative.")
    
    print()
    print("=" * 70)

def test_custom_content(text, api_url="http://localhost:8002"):
    """Test custom content"""
    
    print("=" * 70)
    print("TESTING CUSTOM CONTENT")
    print("=" * 70)
    print()
    print(f"Text: {text}")
    print()
    
    try:
        response = requests.post(
            f"{api_url}/moderate",
            json={"text": text, "media_urls": []},
            timeout=10
        )
        
        if response.status_code == 200:
            result = response.json()
            toxicity = result.get('toxicity_score', 0)
            
            normal_threshold = 0.7
            affected_threshold = 0.557
            
            print(f"Toxicity Score: {toxicity:.3f}")
            print()
            print(f"Normal User (threshold {normal_threshold:.3f}):")
            print(f"  {'✅ APPROVED' if toxicity < normal_threshold else '❌ FLAGGED'}")
            print()
            print(f"Affected User (threshold {affected_threshold:.3f}):")
            print(f"  {'✅ APPROVED' if toxicity < affected_threshold else '❌ FLAGGED'}")
            print()
            
            if affected_threshold < toxicity < normal_threshold:
                print("⭐ PERFECT FOR DEMO! ⭐")
            elif toxicity < affected_threshold:
                print("⚠️ Too mild - both users will approve")
            else:
                print("⚠️ Too toxic - both users will flag")
        else:
            print(f"❌ API Error: {response.status_code}")
    
    except Exception as e:
        print(f"❌ Error: {str(e)}")
    
    print()
    print("=" * 70)

if __name__ == "__main__":
    import sys
    
    print()
    print("Trust Propagation Demo Content Tester")
    print()
    
    # Check if API is running
    try:
        response = requests.get("http://localhost:8002/", timeout=2)
        print("✅ Intelligent Moderation API is running")
        print()
    except:
        print("❌ Intelligent Moderation API is NOT running!")
        print()
        print("Please start it first:")
        print("  cd backend/ML")
        print("  python intelligent_moderation_api.py")
        print()
        sys.exit(1)
    
    # Run tests
    if len(sys.argv) > 1:
        # Test custom content
        custom_text = " ".join(sys.argv[1:])
        test_custom_content(custom_text)
    else:
        # Test predefined examples
        test_content()
    
    print()
    print("TIP: To test custom content, run:")
    print('  python test_demo_content.py "Your custom text here"')
    print()
