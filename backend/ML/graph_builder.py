"""
Graph Builder - Constructs user-post interaction graphs for GNN
"""

from typing import List, Dict, Tuple
from datetime import datetime, timedelta
from collections import defaultdict

class CommunityGraphBuilder:
    """Build graph structure from community data"""
    
    def __init__(self):
        self.users = []
        self.posts = []
        self.interactions = []
        self.user_stats = {}
        self.post_stats = {}
    
    def add_user(self, user_id: str, account_created: datetime, 
                 posts_count: int = 0, harmful_posts: int = 0, 
                 reports_received: int = 0, reports_made: int = 0):
        """Add user node with features"""
        account_age_days = (datetime.now() - account_created).days
        
        self.users.append({
            'id': user_id,
            'posts_count': posts_count,
            'harmful_posts': harmful_posts,
            'reports_received': reports_received,
            'reports_made': reports_made,
            'account_age_days': max(account_age_days, 1),
            'trust_ratio': self._compute_user_trust_ratio(posts_count, harmful_posts, reports_received)
        })
        
        self.user_stats[user_id] = {
            'posts_count': posts_count,
            'harmful_posts': harmful_posts,
            'reports_received': reports_received
        }
    
    def add_post(self, post_id: str, user_id: str, created_at: datetime,
                 likes_count: int = 0, comments_count: int = 0, 
                 reports_count: int = 0, is_flagged: bool = False):
        """Add post node with features"""
        post_age_days = (datetime.now() - created_at).days
        
        self.posts.append({
            'id': post_id,
            'user_id': user_id,
            'likes_count': likes_count,
            'comments_count': comments_count,
            'reports_count': reports_count,
            'is_flagged': is_flagged,
            'age_days': max(post_age_days, 0),
            'engagement_score': likes_count + comments_count * 2
        })
        
        self.post_stats[post_id] = {
            'reports_count': reports_count,
            'is_flagged': is_flagged
        }
    
    def add_interaction(self, user_id: str, post_id: str, 
                       interaction_type: str, weight: float = 1.0):
        """
        Add user-post interaction edge
        
        Args:
            interaction_type: 'posted', 'liked', 'commented', 'reported', 'viewed'
            weight: Edge weight (importance)
        """
        self.interactions.append({
            'user_id': user_id,
            'post_id': post_id,
            'type': interaction_type,
            'weight': weight
        })
    
    def add_user_interaction(self, user_id_1: str, user_id_2: str, 
                            interaction_type: str = 'follows'):
        """Add user-user interaction (optional)"""
        self.interactions.append({
            'user_id': user_id_1,
            'user_id_2': user_id_2,
            'type': interaction_type,
            'weight': 0.5
        })
    
    def _compute_user_trust_ratio(self, posts_count: int, harmful_posts: int, 
                                  reports_received: int) -> float:
        """Compute initial trust ratio for user"""
        if posts_count == 0:
            return 0.5  # Neutral for new users
        
        harmful_ratio = harmful_posts / posts_count
        report_penalty = min(reports_received * 0.1, 0.5)
        
        trust = 1.0 - harmful_ratio - report_penalty
        return max(0.0, min(1.0, trust))
    
    def build_graph_data(self) -> Tuple[List[Dict], List[Dict], List[Dict]]:
        """
        Build graph data structure for GNN
        
        Returns:
            (users, posts, interactions) ready for GNN service
        """
        return self.users, self.posts, self.interactions
    
    def get_user_features(self, user_id: str) -> Dict:
        """Get features for a specific user"""
        for user in self.users:
            if user['id'] == user_id:
                return user
        return None
    
    def get_post_features(self, post_id: str) -> Dict:
        """Get features for a specific post"""
        for post in self.posts:
            if post['id'] == post_id:
                return post
        return None
    
    def compute_graph_statistics(self) -> Dict:
        """Compute graph statistics"""
        return {
            'num_users': len(self.users),
            'num_posts': len(self.posts),
            'num_interactions': len(self.interactions),
            'avg_posts_per_user': sum(u['posts_count'] for u in self.users) / max(len(self.users), 1),
            'avg_reports_per_post': sum(p['reports_count'] for p in self.posts) / max(len(self.posts), 1),
            'flagged_posts': sum(1 for p in self.posts if p['is_flagged'])
        }


def create_sample_community_graph():
    """Create sample community graph for testing"""
    builder = CommunityGraphBuilder()
    
    # Add users
    now = datetime.now()
    builder.add_user('user1', now - timedelta(days=365), posts_count=50, harmful_posts=2, reports_received=1)
    builder.add_user('user2', now - timedelta(days=180), posts_count=20, harmful_posts=8, reports_received=5)
    builder.add_user('user3', now - timedelta(days=730), posts_count=100, harmful_posts=0, reports_received=0)
    builder.add_user('user4', now - timedelta(days=30), posts_count=5, harmful_posts=0, reports_received=0)
    
    # Add posts
    builder.add_post('post1', 'user1', now - timedelta(days=5), likes_count=10, comments_count=3, reports_count=0)
    builder.add_post('post2', 'user2', now - timedelta(days=2), likes_count=2, comments_count=1, reports_count=3, is_flagged=True)
    builder.add_post('post3', 'user3', now - timedelta(days=10), likes_count=50, comments_count=20, reports_count=0)
    builder.add_post('post4', 'user4', now - timedelta(days=1), likes_count=5, comments_count=2, reports_count=0)
    builder.add_post('post5', 'user2', now - timedelta(days=3), likes_count=1, comments_count=0, reports_count=2, is_flagged=True)
    
    # Add interactions
    builder.add_interaction('user1', 'post1', 'posted', weight=1.0)
    builder.add_interaction('user2', 'post2', 'posted', weight=1.0)
    builder.add_interaction('user3', 'post3', 'posted', weight=1.0)
    builder.add_interaction('user4', 'post4', 'posted', weight=1.0)
    builder.add_interaction('user2', 'post5', 'posted', weight=1.0)
    
    builder.add_interaction('user1', 'post3', 'liked', weight=0.5)
    builder.add_interaction('user3', 'post1', 'liked', weight=0.5)
    builder.add_interaction('user4', 'post1', 'commented', weight=0.7)
    builder.add_interaction('user1', 'post2', 'reported', weight=1.5)
    builder.add_interaction('user3', 'post2', 'reported', weight=1.5)
    
    return builder
