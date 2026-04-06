"""
Trust Score Propagation Service
Propagates trust score penalties when users are banned based on their interactions
"""

import torch
import numpy as np
from typing import Dict, List, Tuple, Set
from datetime import datetime
from collections import defaultdict, deque

class TrustPropagationService:
    """
    Propagates trust score changes through the social graph when users are banned.
    Uses graph traversal to identify affected users and compute penalty scores.
    """
    
    def __init__(self):
        self.user_graph = defaultdict(set)  # user_id -> set of connected user_ids
        self.interaction_weights = {}  # (user1, user2) -> weight
        self.user_trust_scores = {}  # user_id -> current trust score
        self.user_content_trust_scores = {}  # user_id -> content trust score
    
    def build_social_graph(self, followers: List[Dict], interactions: List[Dict]):
        """
        Build social graph from follower relationships and interactions
        
        Args:
            followers: List of {follower_id, following_id}
            interactions: List of {user_id, target_user_id, type, weight}
        """
        self.user_graph.clear()
        self.interaction_weights.clear()
        
        # Add follower relationships
        for follow in followers:
            follower_id = follow['follower_id']
            following_id = follow['following_id']
            
            # Bidirectional connection
            self.user_graph[follower_id].add(following_id)
            self.user_graph[following_id].add(follower_id)
            
            # Follower has stronger connection to following
            self.interaction_weights[(follower_id, following_id)] = 0.8
            self.interaction_weights[(following_id, follower_id)] = 0.3
        
        # Add interaction-based connections (likes, comments, etc.)
        for interaction in interactions:
            user_id = interaction['user_id']
            target_user_id = interaction.get('target_user_id')
            weight = interaction.get('weight', 0.5)
            
            if target_user_id:
                self.user_graph[user_id].add(target_user_id)
                self.user_graph[target_user_id].add(user_id)
                
                # Update weights (keep maximum)
                key = (user_id, target_user_id)
                self.interaction_weights[key] = max(
                    self.interaction_weights.get(key, 0), 
                    weight
                )
    
    def set_user_trust_scores(self, trust_scores: Dict[str, float]):
        """Set current trust scores for users"""
        self.user_trust_scores = trust_scores.copy()
        self.user_content_trust_scores = trust_scores.copy()
    
    def propagate_ban_penalty(self, banned_user_id: str, 
                             max_hops: int = 2,
                             base_penalty: float = 0.15) -> Dict[str, Dict[str, float]]:
        """
        Propagate trust score penalties when a user is banned
        
        Args:
            banned_user_id: ID of the banned user
            max_hops: Maximum distance to propagate (1=direct connections, 2=friends of friends)
            base_penalty: Base penalty for direct connections (0-1)
        
        Returns:
            Dict mapping user_id to {
                'new_user_trust_score': float,
                'new_content_trust_score': float,
                'penalty_applied': float,
                'distance': int,
                'relationship_type': str
            }
        """
        if banned_user_id not in self.user_graph:
            return {}
        
        affected_users = {}
        visited = set()
        queue = deque([(banned_user_id, 0)])  # (user_id, distance)
        
        while queue:
            current_user, distance = queue.popleft()
            
            if current_user in visited or distance > max_hops:
                continue
            
            visited.add(current_user)
            
            # Skip the banned user themselves
            if current_user == banned_user_id:
                # Add neighbors to queue
                for neighbor in self.user_graph[current_user]:
                    if neighbor not in visited:
                        queue.append((neighbor, distance + 1))
                continue
            
            # Calculate penalty based on distance and connection strength
            connection_weight = self.interaction_weights.get(
                (current_user, banned_user_id), 
                0.5
            )
            
            # Penalty decreases with distance
            distance_factor = 1.0 / (distance ** 1.5)
            penalty = base_penalty * connection_weight * distance_factor
            
            # Get current scores
            current_trust = self.user_trust_scores.get(current_user, 0.5)
            current_content_trust = self.user_content_trust_scores.get(current_user, 0.5)
            
            # Apply penalties
            # User trust score decreases (less trustworthy)
            new_user_trust = max(0.1, current_trust - penalty)
            
            # Content trust score increases (more scrutiny needed)
            # Higher content trust = stricter moderation
            content_penalty_factor = 1.5  # Content scrutiny increases more
            new_content_trust = min(0.9, current_content_trust + (penalty * content_penalty_factor))
            
            # Determine relationship type
            relationship_type = self._determine_relationship(
                current_user, banned_user_id
            )
            
            affected_users[current_user] = {
                'new_user_trust_score': round(new_user_trust, 4),
                'new_content_trust_score': round(new_content_trust, 4),
                'penalty_applied': round(penalty, 4),
                'distance': distance,
                'relationship_type': relationship_type,
                'connection_weight': round(connection_weight, 4)
            }
            
            # Update internal scores for cascading effects
            self.user_trust_scores[current_user] = new_user_trust
            self.user_content_trust_scores[current_user] = new_content_trust
            
            # Add neighbors to queue
            for neighbor in self.user_graph[current_user]:
                if neighbor not in visited:
                    queue.append((neighbor, distance + 1))
        
        return affected_users
    
    def _determine_relationship(self, user_id: str, banned_user_id: str) -> str:
        """Determine the type of relationship between users"""
        # Check if user follows banned user
        is_follower = (user_id, banned_user_id) in self.interaction_weights
        is_following = (banned_user_id, user_id) in self.interaction_weights
        
        if is_follower and is_following:
            return "mutual_follow"
        elif is_follower:
            return "follower"
        elif is_following:
            return "following"
        else:
            return "indirect_connection"
    
    def compute_trust_recovery(self, user_id: str, 
                               days_since_penalty: int,
                               recovery_rate: float = 0.01) -> Dict[str, float]:
        """
        Compute trust score recovery over time for users who were penalized
        
        Args:
            user_id: User ID
            days_since_penalty: Days since penalty was applied
            recovery_rate: Daily recovery rate (0-1)
        
        Returns:
            Dict with recovered trust scores
        """
        current_trust = self.user_trust_scores.get(user_id, 0.5)
        current_content_trust = self.user_content_trust_scores.get(user_id, 0.5)
        
        # Recovery increases trust, decreases content scrutiny
        recovery_amount = min(recovery_rate * days_since_penalty, 0.3)
        
        new_user_trust = min(0.9, current_trust + recovery_amount)
        new_content_trust = max(0.3, current_content_trust - recovery_amount)
        
        return {
            'new_user_trust_score': round(new_user_trust, 4),
            'new_content_trust_score': round(new_content_trust, 4),
            'recovery_applied': round(recovery_amount, 4)
        }
    
    def get_affected_users_summary(self, affected_users: Dict) -> Dict:
        """Generate summary statistics for affected users"""
        if not affected_users:
            return {
                'total_affected': 0,
                'by_distance': {},
                'by_relationship': {},
                'avg_penalty': 0.0
            }
        
        by_distance = defaultdict(int)
        by_relationship = defaultdict(int)
        total_penalty = 0.0
        
        for user_id, data in affected_users.items():
            by_distance[data['distance']] += 1
            by_relationship[data['relationship_type']] += 1
            total_penalty += data['penalty_applied']
        
        return {
            'total_affected': len(affected_users),
            'by_distance': dict(by_distance),
            'by_relationship': dict(by_relationship),
            'avg_penalty': round(total_penalty / len(affected_users), 4)
        }


def create_sample_social_graph():
    """Create sample social graph for testing"""
    service = TrustPropagationService()
    
    # Sample followers
    followers = [
        {'follower_id': 'user2', 'following_id': 'user1'},  # user2 follows user1
        {'follower_id': 'user3', 'following_id': 'user1'},  # user3 follows user1
        {'follower_id': 'user4', 'following_id': 'user2'},  # user4 follows user2
        {'follower_id': 'user5', 'following_id': 'user3'},  # user5 follows user3
    ]
    
    # Sample interactions (likes, comments)
    interactions = [
        {'user_id': 'user2', 'target_user_id': 'user1', 'type': 'like', 'weight': 0.6},
        {'user_id': 'user3', 'target_user_id': 'user1', 'type': 'comment', 'weight': 0.7},
        {'user_id': 'user4', 'target_user_id': 'user1', 'type': 'like', 'weight': 0.4},
    ]
    
    service.build_social_graph(followers, interactions)
    
    # Set initial trust scores
    trust_scores = {
        'user1': 0.3,  # Will be banned
        'user2': 0.7,
        'user3': 0.8,
        'user4': 0.75,
        'user5': 0.85
    }
    service.set_user_trust_scores(trust_scores)
    
    return service


if __name__ == "__main__":
    # Test the service
    print("=== Trust Propagation Service Test ===\n")
    
    service = create_sample_social_graph()
    
    # Simulate banning user1
    print("Banning user1 and propagating penalties...\n")
    affected = service.propagate_ban_penalty('user1', max_hops=2, base_penalty=0.15)
    
    print(f"Affected {len(affected)} users:\n")
    for user_id, data in affected.items():
        print(f"{user_id}:")
        print(f"  Distance: {data['distance']} hops")
        print(f"  Relationship: {data['relationship_type']}")
        print(f"  User Trust: {data['new_user_trust_score']} (penalty: {data['penalty_applied']})")
        print(f"  Content Trust: {data['new_content_trust_score']}")
        print()
    
    # Summary
    summary = service.get_affected_users_summary(affected)
    print("Summary:")
    print(f"  Total affected: {summary['total_affected']}")
    print(f"  By distance: {summary['by_distance']}")
    print(f"  By relationship: {summary['by_relationship']}")
    print(f"  Average penalty: {summary['avg_penalty']}")
