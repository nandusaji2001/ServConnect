"""
Test script for Multimodal Item Matching
Demonstrates CLIP + GNN integration
"""

import requests
import json
from datetime import datetime, timedelta

API_URL = "http://localhost:5003"

def test_health():
    """Test health endpoint"""
    print("\n" + "="*60)
    print("Testing Health Endpoint")
    print("="*60)
    
    response = requests.get(f"{API_URL}/health")
    print(f"Status: {response.status_code}")
    print(f"Response: {json.dumps(response.json(), indent=2)}")
    return response.status_code == 200

def test_similarity():
    """Test similarity computation"""
    print("\n" + "="*60)
    print("Testing Multimodal Similarity")
    print("="*60)
    
    item1 = {
        "title": "Black Leather Wallet",
        "category": "Wallet",
        "description": "Lost black leather wallet with credit cards and ID",
        "location": "Central Park"
    }
    
    item2 = {
        "title": "Found Wallet",
        "category": "Wallet",
        "description": "Black wallet found near park bench, contains cards",
        "location": "Park area"
    }
    
    payload = {
        "item1": item1,
        "item2": item2
    }
    
    response = requests.post(f"{API_URL}/similarity", json=payload)
    print(f"Status: {response.status_code}")
    result = response.json()
    print(f"Response: {json.dumps(result, indent=2)}")
    
    if result.get('success'):
        print(f"\n✓ Similarity Score: {result['similarity_score']:.2f}")
        print(f"✓ Match Percentage: {result['match_percentage']}%")
        details = result.get('details', {})
        print(f"  - Text Similarity: {details.get('text_similarity', 0):.2f}")
        print(f"  - Image Similarity: {details.get('image_similarity', 0):.2f}")
        print(f"  - Cross-Modal: {details.get('cross_modal_similarity', 0):.2f}")
    
    return response.status_code == 200

def test_trust_scores():
    """Test GNN trust scoring"""
    print("\n" + "="*60)
    print("Testing GNN Trust Scoring")
    print("="*60)
    
    users = [
        {
            "id": "user1",
            "posts_count": 15,
            "reports_count": 0,
            "account_age_days": 365
        },
        {
            "id": "user2",
            "posts_count": 3,
            "reports_count": 2,
            "account_age_days": 30
        },
        {
            "id": "user3",
            "posts_count": 25,
            "reports_count": 0,
            "account_age_days": 730
        }
    ]
    
    items = [
        {
            "id": "item1",
            "user_id": "user1",
            "claims_count": 2,
            "verified": True,
            "age_days": 5
        },
        {
            "id": "item2",
            "user_id": "user2",
            "claims_count": 8,
            "verified": False,
            "age_days": 15
        },
        {
            "id": "item3",
            "user_id": "user3",
            "claims_count": 1,
            "verified": True,
            "age_days": 2
        }
    ]
    
    interactions = [
        {"user_id": "user2", "item_id": "item1", "type": "claim"},
        {"user_id": "user3", "item_id": "item1", "type": "view"},
        {"user_id": "user1", "item_id": "item2", "type": "view"},
        {"user_id": "user3", "item_id": "item2", "type": "claim"}
    ]
    
    payload = {
        "users": users,
        "items": items,
        "interactions": interactions
    }
    
    response = requests.post(f"{API_URL}/trust_scores", json=payload)
    print(f"Status: {response.status_code}")
    result = response.json()
    
    if result.get('success'):
        print(f"\n✓ User Trust Scores:")
        for user_id, score in result['user_trust_scores'].items():
            print(f"  - {user_id}: {score:.3f}")
        
        print(f"\n✓ Item Trust Scores:")
        for item_id, score in result['item_trust_scores'].items():
            print(f"  - {item_id}: {score:.3f}")
    else:
        print(f"Error: {result.get('error')}")
    
    return response.status_code == 200

def test_full_matching():
    """Test full matching with multimodal + GNN"""
    print("\n" + "="*60)
    print("Testing Full Multimodal + GNN Matching")
    print("="*60)
    
    query_item = {
        "id": "found_wallet_1",
        "title": "Black Leather Wallet Found",
        "category": "Wallet",
        "description": "Found a black leather wallet near the subway station. Contains credit cards.",
        "location": "Downtown Subway Station"
    }
    
    candidate_items = [
        {
            "id": "lost_wallet_1",
            "user_id": "user1",
            "title": "Lost Black Wallet",
            "category": "Wallet",
            "description": "Lost my black wallet with credit cards and driver's license",
            "location": "Downtown area"
        },
        {
            "id": "lost_phone_1",
            "user_id": "user2",
            "title": "Lost iPhone",
            "category": "Mobile",
            "description": "Lost iPhone 13 Pro in black color",
            "location": "Downtown"
        },
        {
            "id": "lost_wallet_2",
            "user_id": "user3",
            "title": "Missing Wallet",
            "category": "Wallet",
            "description": "Brown leather wallet missing, has ID cards",
            "location": "City Center"
        }
    ]
    
    users = [
        {"id": "user1", "posts_count": 10, "reports_count": 0, "account_age_days": 365},
        {"id": "user2", "posts_count": 5, "reports_count": 3, "account_age_days": 60},
        {"id": "user3", "posts_count": 20, "reports_count": 0, "account_age_days": 500}
    ]
    
    items_metadata = [
        {"id": "lost_wallet_1", "user_id": "user1", "claims_count": 1, "verified": False, "age_days": 3},
        {"id": "lost_phone_1", "user_id": "user2", "claims_count": 5, "verified": False, "age_days": 10},
        {"id": "lost_wallet_2", "user_id": "user3", "claims_count": 0, "verified": False, "age_days": 7}
    ]
    
    interactions = [
        {"user_id": "user2", "item_id": "lost_wallet_1", "type": "view"}
    ]
    
    payload = {
        "query_item": query_item,
        "candidate_items": candidate_items,
        "users": users,
        "items_metadata": items_metadata,
        "interactions": interactions,
        "threshold": 0.3,
        "top_k": 5,
        "alpha": 0.7,  # 70% similarity weight
        "beta": 0.3    # 30% trust weight
    }
    
    response = requests.post(f"{API_URL}/match", json=payload)
    print(f"Status: {response.status_code}")
    result = response.json()
    
    if result.get('success'):
        print(f"\n✓ Found {result['matches_found']} matches out of {result['total_candidates']} candidates")
        print(f"✓ GNN Enabled: {result.get('gnn_enabled', False)}")
        
        print(f"\n{'='*60}")
        print("Top Matches:")
        print("="*60)
        
        for i, match in enumerate(result['matches'], 1):
            item = match['item']
            print(f"\n{i}. {item['title']} (ID: {item['id']})")
            print(f"   User: {item['user_id']}")
            print(f"   Category: {item['category']}")
            print(f"   Location: {item['location']}")
            print(f"   ---")
            print(f"   Final Score: {match['final_score']:.3f} ({match['match_percentage']}%)")
            print(f"   Similarity: {match['similarity_score']:.3f}")
            print(f"   Trust: {match['trust_score']:.3f}")
            
            details = match.get('details', {})
            print(f"   Details:")
            print(f"     - Text Similarity: {details.get('text_similarity', 0):.3f}")
            print(f"     - Image Similarity: {details.get('image_similarity', 0):.3f}")
            print(f"     - User Trust: {details.get('user_trust', 0):.3f}")
            print(f"     - Item Trust: {details.get('item_trust', 0):.3f}")
    else:
        print(f"Error: {result.get('error')}")
    
    return response.status_code == 200

def main():
    """Run all tests"""
    print("\n" + "="*60)
    print("MULTIMODAL ITEM MATCHING TEST SUITE")
    print("CLIP + GNN + SBERT Integration")
    print("="*60)
    
    tests = [
        ("Health Check", test_health),
        ("Multimodal Similarity", test_similarity),
        ("GNN Trust Scoring", test_trust_scores),
        ("Full Matching Pipeline", test_full_matching)
    ]
    
    results = []
    
    for test_name, test_func in tests:
        try:
            success = test_func()
            results.append((test_name, success))
        except Exception as e:
            print(f"\n✗ Test failed with error: {e}")
            results.append((test_name, False))
    
    # Summary
    print("\n" + "="*60)
    print("TEST SUMMARY")
    print("="*60)
    
    for test_name, success in results:
        status = "✓ PASS" if success else "✗ FAIL"
        print(f"{status}: {test_name}")
    
    passed = sum(1 for _, success in results if success)
    total = len(results)
    print(f"\nTotal: {passed}/{total} tests passed")
    print("="*60)

if __name__ == "__main__":
    main()
