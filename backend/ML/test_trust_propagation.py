"""
Test script for Trust Propagation Service
"""

from trust_propagation_service import TrustPropagationService, create_sample_social_graph

def test_basic_propagation():
    """Test basic trust score propagation"""
    print("=" * 60)
    print("TEST 1: Basic Trust Score Propagation")
    print("=" * 60)
    
    service = create_sample_social_graph()
    
    print("\nInitial Trust Scores:")
    for user_id, score in service.user_trust_scores.items():
        print(f"  {user_id}: {score}")
    
    print("\nBanning user1 and propagating penalties...")
    affected = service.propagate_ban_penalty('user1', max_hops=2, base_penalty=0.15)
    
    print(f"\nAffected {len(affected)} users:\n")
    for user_id, data in sorted(affected.items()):
        print(f"{user_id}:")
        print(f"  Distance: {data['distance']} hops from banned user")
        print(f"  Relationship: {data['relationship_type']}")
        print(f"  Connection Weight: {data['connection_weight']}")
        print(f"  User Trust Score: {data['new_user_trust_score']} (penalty: -{data['penalty_applied']})")
        print(f"  Content Trust Score: {data['new_content_trust_score']} (stricter moderation)")
        print()
    
    summary = service.get_affected_users_summary(affected)
    print("Summary:")
    print(f"  Total affected: {summary['total_affected']}")
    print(f"  By distance: {summary['by_distance']}")
    print(f"  By relationship: {summary['by_relationship']}")
    print(f"  Average penalty: {summary['avg_penalty']}")
    print()

def test_follower_impact():
    """Test that followers receive higher penalties"""
    print("=" * 60)
    print("TEST 2: Follower vs Non-Follower Impact")
    print("=" * 60)
    
    service = TrustPropagationService()
    
    # Create scenario: user1 (banned), user2 (follower), user3 (non-follower)
    followers = [
        {'follower_id': 'user2', 'following_id': 'user1'},  # user2 follows user1
    ]
    
    interactions = [
        {'user_id': 'user3', 'target_user_id': 'user1', 'type': 'like', 'weight': 0.3},  # weak connection
    ]
    
    service.build_social_graph(followers, interactions)
    service.set_user_trust_scores({
        'user1': 0.3,  # Will be banned
        'user2': 0.8,  # Follower
        'user3': 0.8,  # Non-follower
    })
    
    affected = service.propagate_ban_penalty('user1', max_hops=1, base_penalty=0.15)
    
    print("\nResults:")
    print(f"user2 (follower):")
    print(f"  Penalty: {affected['user2']['penalty_applied']}")
    print(f"  New Trust: {affected['user2']['new_user_trust_score']}")
    print(f"  Content Trust: {affected['user2']['new_content_trust_score']}")
    
    print(f"\nuser3 (non-follower, weak connection):")
    print(f"  Penalty: {affected['user3']['penalty_applied']}")
    print(f"  New Trust: {affected['user3']['new_user_trust_score']}")
    print(f"  Content Trust: {affected['user3']['new_content_trust_score']}")
    
    print(f"\nFollower received {affected['user2']['penalty_applied'] / affected['user3']['penalty_applied']:.2f}x higher penalty")
    print()

def test_distance_decay():
    """Test that penalties decrease with distance"""
    print("=" * 60)
    print("TEST 3: Distance Decay Effect")
    print("=" * 60)
    
    service = TrustPropagationService()
    
    # Create chain: user1 -> user2 -> user3 -> user4
    followers = [
        {'follower_id': 'user2', 'following_id': 'user1'},
        {'follower_id': 'user3', 'following_id': 'user2'},
        {'follower_id': 'user4', 'following_id': 'user3'},
    ]
    
    service.build_social_graph(followers, [])
    service.set_user_trust_scores({
        'user1': 0.3,
        'user2': 0.8,
        'user3': 0.8,
        'user4': 0.8,
    })
    
    affected = service.propagate_ban_penalty('user1', max_hops=3, base_penalty=0.15)
    
    print("\nPenalty by distance:")
    for user_id in ['user2', 'user3', 'user4']:
        if user_id in affected:
            data = affected[user_id]
            print(f"{user_id} (distance {data['distance']}): penalty = {data['penalty_applied']}")
    print()

def test_trust_recovery():
    """Test trust score recovery over time"""
    print("=" * 60)
    print("TEST 4: Trust Score Recovery Over Time")
    print("=" * 60)
    
    service = TrustPropagationService()
    service.user_trust_scores = {'user1': 0.5}
    service.user_content_trust_scores = {'user1': 0.7}
    
    print("\nInitial scores:")
    print(f"  User Trust: 0.5")
    print(f"  Content Trust: 0.7")
    
    print("\nRecovery over time:")
    for days in [7, 14, 30, 60, 90]:
        recovery = service.compute_trust_recovery('user1', days, recovery_rate=0.01)
        print(f"  After {days} days:")
        print(f"    User Trust: {recovery['new_user_trust_score']}")
        print(f"    Content Trust: {recovery['new_content_trust_score']}")
    print()

if __name__ == "__main__":
    print("\n" + "=" * 60)
    print("TRUST PROPAGATION SERVICE - TEST SUITE")
    print("=" * 60 + "\n")
    
    test_basic_propagation()
    test_follower_impact()
    test_distance_decay()
    test_trust_recovery()
    
    print("=" * 60)
    print("ALL TESTS COMPLETED")
    print("=" * 60)
