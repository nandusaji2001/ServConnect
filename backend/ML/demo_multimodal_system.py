"""
Standalone Demo of Multimodal + GNN System
Can run without the full API server
"""

from multimodal_matching_service import MultimodalMatchingService
import json

def print_section(title):
    """Print formatted section header"""
    print("\n" + "="*70)
    print(f"  {title}")
    print("="*70)

def demo_text_similarity():
    """Demo 1: Text-only similarity (backward compatible)"""
    print_section("DEMO 1: Text Similarity (CLIP)")
    
    service = MultimodalMatchingService()
    
    item1 = {
        "title": "Black Leather Wallet",
        "category": "Wallet",
        "description": "Lost black leather wallet with credit cards and driver's license",
        "location": "Central Park, near the fountain"
    }
    
    item2 = {
        "title": "Found Wallet",
        "category": "Wallet",
        "description": "Found a black wallet containing cards near park area",
        "location": "Park vicinity"
    }
    
    print("\nItem 1:")
    print(f"  Title: {item1['title']}")
    print(f"  Description: {item1['description']}")
    
    print("\nItem 2:")
    print(f"  Title: {item2['title']}")
    print(f"  Description: {item2['description']}")
    
    print("\nComputing similarity...")
    similarities = service.compute_multimodal_similarity(item1, item2)
    
    print(f"\n✓ Results:")
    print(f"  Text Similarity: {similarities['text_sim']:.3f}")
    print(f"  Combined Score: {similarities['combined_sim']:.3f}")
    print(f"  Match Percentage: {similarities['combined_sim']*100:.1f}%")

def demo_gnn_trust_scoring():
    """Demo 2: GNN trust scoring"""
    print_section("DEMO 2: GNN Trust Scoring")
    
    service = MultimodalMatchingService()
    
    # Sample data
    users = [
        {
            "id": "alice",
            "posts_count": 15,
            "reports_count": 0,
            "account_age_days": 365
        },
        {
            "id": "bob",
            "posts_count": 3,
            "reports_count": 5,
            "account_age_days": 30
        },
        {
            "id": "charlie",
            "posts_count": 25,
            "reports_count": 0,
            "account_age_days": 730
        }
    ]
    
    items = [
        {
            "id": "wallet1",
            "user_id": "alice",
            "claims_count": 2,
            "verified": True,
            "age_days": 5
        },
        {
            "id": "phone1",
            "user_id": "bob",
            "claims_count": 10,
            "verified": False,
            "age_days": 20
        },
        {
            "id": "bag1",
            "user_id": "charlie",
            "claims_count": 1,
            "verified": True,
            "age_days": 3
        }
    ]
    
    interactions = [
        {"user_id": "bob", "item_id": "wallet1", "type": "claim"},
        {"user_id": "charlie", "item_id": "wallet1", "type": "view"},
        {"user_id": "alice", "item_id": "phone1", "type": "view"},
        {"user_id": "charlie", "item_id": "phone1", "type": "claim"}
    ]
    
    print("\nUser Profiles:")
    for user in users:
        print(f"  {user['id']}: {user['posts_count']} posts, "
              f"{user['reports_count']} reports, "
              f"{user['account_age_days']} days old")
    
    print("\nInitializing GNN and computing trust scores...")
    user_trust, item_trust = service.initialize_gnn(users, items, interactions)
    
    print(f"\n✓ User Trust Scores:")
    for user_id, score in sorted(user_trust.items(), key=lambda x: x[1], reverse=True):
        print(f"  {user_id}: {score:.3f}")
    
    print(f"\n✓ Item Trust Scores:")
    for item_id, score in sorted(item_trust.items(), key=lambda x: x[1], reverse=True):
        print(f"  {item_id}: {score:.3f}")

def demo_full_matching():
    """Demo 3: Full matching with multimodal + GNN"""
    print_section("DEMO 3: Full Matching Pipeline (Multimodal + GNN)")
    
    service = MultimodalMatchingService()
    
    # Query item (found item)
    query_item = {
        "id": "found_wallet_downtown",
        "title": "Black Leather Wallet Found",
        "category": "Wallet",
        "description": "Found a black leather wallet near the subway station. Contains credit cards and some cash.",
        "location": "Downtown Subway Station"
    }
    
    # Candidate items (lost items)
    candidate_items = [
        {
            "id": "lost_wallet_1",
            "user_id": "alice",
            "title": "Lost Black Wallet",
            "category": "Wallet",
            "description": "Lost my black wallet with credit cards and driver's license near downtown",
            "location": "Downtown area"
        },
        {
            "id": "lost_phone_1",
            "user_id": "bob",
            "title": "Lost iPhone",
            "category": "Mobile",
            "description": "Lost iPhone 13 Pro in black color with cracked screen",
            "location": "Downtown"
        },
        {
            "id": "lost_wallet_2",
            "user_id": "charlie",
            "title": "Missing Brown Wallet",
            "category": "Wallet",
            "description": "Brown leather wallet missing, has ID cards and business cards",
            "location": "City Center"
        },
        {
            "id": "lost_wallet_3",
            "user_id": "david",
            "title": "Lost Wallet Near Subway",
            "category": "Wallet",
            "description": "Black wallet lost at subway station, contains bank cards",
            "location": "Subway Station"
        }
    ]
    
    # User data for GNN
    users = [
        {"id": "alice", "posts_count": 10, "reports_count": 0, "account_age_days": 365},
        {"id": "bob", "posts_count": 5, "reports_count": 3, "account_age_days": 60},
        {"id": "charlie", "posts_count": 20, "reports_count": 0, "account_age_days": 500},
        {"id": "david", "posts_count": 8, "reports_count": 1, "account_age_days": 200}
    ]
    
    # Item metadata for GNN
    items_metadata = [
        {"id": "lost_wallet_1", "user_id": "alice", "claims_count": 1, "verified": False, "age_days": 3},
        {"id": "lost_phone_1", "user_id": "bob", "claims_count": 5, "verified": False, "age_days": 10},
        {"id": "lost_wallet_2", "user_id": "charlie", "claims_count": 0, "verified": False, "age_days": 7},
        {"id": "lost_wallet_3", "user_id": "david", "claims_count": 2, "verified": False, "age_days": 2}
    ]
    
    # Interactions for GNN
    interactions = [
        {"user_id": "bob", "item_id": "lost_wallet_1", "type": "view"},
        {"user_id": "charlie", "item_id": "lost_phone_1", "type": "view"}
    ]
    
    print("\nQuery Item (Found):")
    print(f"  Title: {query_item['title']}")
    print(f"  Description: {query_item['description']}")
    print(f"  Location: {query_item['location']}")
    
    print(f"\nSearching among {len(candidate_items)} lost items...")
    
    # Initialize GNN
    print("\nInitializing GNN for trust scoring...")
    user_trust, item_trust = service.initialize_gnn(users, items_metadata, interactions)
    
    # Find matches
    matches = service.find_matches(
        query_item=query_item,
        candidate_items=candidate_items,
        user_trust_scores=user_trust,
        item_trust_scores=item_trust,
        threshold=0.3,
        top_k=5,
        alpha=0.7,  # 70% similarity
        beta=0.3    # 30% trust
    )
    
    print(f"\n✓ Found {len(matches)} potential matches")
    print("\n" + "-"*70)
    print("Top Matches (Ranked by Final Score):")
    print("-"*70)
    
    for i, match in enumerate(matches, 1):
        item = match['item']
        print(f"\n{i}. {item['title']}")
        print(f"   User: {item['user_id']}")
        print(f"   Category: {item['category']}")
        print(f"   Location: {item['location']}")
        print(f"   Description: {item['description'][:60]}...")
        print(f"   ---")
        print(f"   Final Score: {match['final_score']:.3f} ({match['match_percentage']}%)")
        print(f"   ├─ Similarity: {match['similarity_score']:.3f}")
        print(f"   └─ Trust: {match['trust_score']:.3f}")
        
        details = match['details']
        print(f"   Details:")
        print(f"     • Text Similarity: {details['text_similarity']:.3f}")
        print(f"     • User Trust: {details['user_trust']:.3f}")
        print(f"     • Item Trust: {details['item_trust']:.3f}")

def demo_scoring_weights():
    """Demo 4: Effect of different scoring weights"""
    print_section("DEMO 4: Scoring Weight Comparison")
    
    service = MultimodalMatchingService()
    
    # Simple scenario
    similarity_score = 0.8
    trust_score = 0.5
    
    print(f"\nScenario:")
    print(f"  Similarity Score: {similarity_score}")
    print(f"  Trust Score: {trust_score}")
    
    print(f"\nFinal Scores with Different Weights:")
    print(f"  {'Alpha':>6} {'Beta':>6} {'Final Score':>12} {'Strategy':>20}")
    print(f"  {'-'*6} {'-'*6} {'-'*12} {'-'*20}")
    
    weight_configs = [
        (1.0, 0.0, "Similarity Only"),
        (0.9, 0.1, "Mostly Similarity"),
        (0.7, 0.3, "Balanced (Default)"),
        (0.5, 0.5, "Equal Weight"),
        (0.3, 0.7, "Trust Focused"),
        (0.0, 1.0, "Trust Only")
    ]
    
    for alpha, beta, strategy in weight_configs:
        final = service.compute_final_score(similarity_score, trust_score, alpha, beta)
        print(f"  {alpha:>6.1f} {beta:>6.1f} {final:>12.3f} {strategy:>20}")
    
    print(f"\n💡 Insight:")
    print(f"  - High alpha (0.7-0.9): Prioritize matching accuracy")
    print(f"  - High beta (0.5-0.7): Prioritize user trustworthiness")
    print(f"  - Balanced (0.7/0.3): Best for general use")

def main():
    """Run all demos"""
    print("\n" + "="*70)
    print("  MULTIMODAL LOST & FOUND MATCHING SYSTEM")
    print("  CLIP + GNN + SBERT Integration Demo")
    print("="*70)
    
    demos = [
        ("Text Similarity", demo_text_similarity),
        ("GNN Trust Scoring", demo_gnn_trust_scoring),
        ("Full Matching Pipeline", demo_full_matching),
        ("Scoring Weight Comparison", demo_scoring_weights)
    ]
    
    for i, (name, demo_func) in enumerate(demos, 1):
        try:
            demo_func()
        except Exception as e:
            print(f"\n✗ Demo failed: {e}")
            import traceback
            traceback.print_exc()
    
    print("\n" + "="*70)
    print("  Demo Complete!")
    print("="*70)
    print("\nNext Steps:")
    print("  1. Start the API: start_multimodal_matching_api.bat")
    print("  2. Run tests: python test_multimodal_matching.py")
    print("  3. Read docs: MULTIMODAL_MATCHING_README.md")
    print("="*70 + "\n")

if __name__ == "__main__":
    main()
