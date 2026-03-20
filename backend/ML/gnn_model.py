"""
GNN Model - Graph Neural Network for User Trust and Post Risk Scoring
"""

import torch
import torch.nn.functional as F
from torch_geometric.nn import SAGEConv, GATConv
from torch_geometric.data import Data
import numpy as np
from typing import Dict, List, Tuple

class CommunityTrustGNN(torch.nn.Module):
    """
    Graph Neural Network for computing user trust and post risk scores
    Supports both GraphSAGE and GAT architectures
    """
    
    def __init__(self, input_dim: int, hidden_dim: int = 64, 
                 output_dim: int = 32, use_gat: bool = False, num_layers: int = 2):
        super(CommunityTrustGNN, self).__init__()
        self.use_gat = use_gat
        self.num_layers = num_layers
        
        if use_gat:
            # Graph Attention Network
            self.conv1 = GATConv(input_dim, hidden_dim, heads=4, concat=True)
            self.conv2 = GATConv(hidden_dim * 4, output_dim, heads=1, concat=False)
        else:
            # GraphSAGE (better for large graphs)
            self.conv1 = SAGEConv(input_dim, hidden_dim)
            self.conv2 = SAGEConv(hidden_dim, output_dim)
        
        # Trust score predictor
        self.trust_predictor = torch.nn.Sequential(
            torch.nn.Linear(output_dim, 16),
            torch.nn.ReLU(),
            torch.nn.Dropout(0.2),
            torch.nn.Linear(16, 1),
            torch.nn.Sigmoid()
        )
    
    def forward(self, x, edge_index):
        # GNN layers
        x = self.conv1(x, edge_index)
        x = F.relu(x)
        x = F.dropout(x, p=0.3, training=self.training)
        x = self.conv2(x, edge_index)
        
        # Compute trust scores
        trust_scores = self.trust_predictor(x)
        
        return x, trust_scores


class CommunityGNNService:
    """Service for building and using GNN for community moderation"""
    
    def __init__(self, use_gat: bool = False):
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.use_gat = use_gat
        self.model = None
        self.graph_data = None
        self.node_mapping = {}
        self.reverse_mapping = {}
        print(f"Community GNN Service initialized on {self.device}")
    
    def build_graph(self, users: List[Dict], posts: List[Dict], 
                    interactions: List[Dict]) -> Data:
        """
        Build graph from community data
        
        Args:
            users: List of user dicts with features
            posts: List of post dicts with features
            interactions: List of interaction dicts
        
        Returns:
            PyTorch Geometric Data object
        """
        # Create node mapping
        self.node_mapping = {}
        self.reverse_mapping = {}
        node_idx = 0
        
        # Map users
        user_indices = {}
        for user in users:
            user_id = user['id']
            self.node_mapping[f"user_{user_id}"] = node_idx
            self.reverse_mapping[node_idx] = f"user_{user_id}"
            user_indices[user_id] = node_idx
            node_idx += 1
        
        # Map posts
        post_indices = {}
        for post in posts:
            post_id = post['id']
            self.node_mapping[f"post_{post_id}"] = node_idx
            self.reverse_mapping[node_idx] = f"post_{post_id}"
            post_indices[post_id] = node_idx
            node_idx += 1
        
        num_nodes = node_idx
        
        # Create node features
        node_features = []
        node_types = []  # 0 = user, 1 = post
        
        # User features: [posts_count, harmful_posts, reports_received, 
        #                 reports_made, account_age, trust_ratio, is_user]
        for user in users:
            features = [
                user.get('posts_count', 0) / 100.0,
                user.get('harmful_posts', 0) / 10.0,
                user.get('reports_received', 0) / 10.0,
                user.get('reports_made', 0) / 10.0,
                user.get('account_age_days', 0) / 365.0,
                user.get('trust_ratio', 0.5),
                1.0  # is_user flag
            ]
            node_features.append(features)
            node_types.append(0)
        
        # Post features: [likes_count, comments_count, reports_count, 
        #                 is_flagged, age_days, engagement_score, is_post]
        for post in posts:
            features = [
                post.get('likes_count', 0) / 50.0,
                post.get('comments_count', 0) / 20.0,
                post.get('reports_count', 0) / 5.0,
                1.0 if post.get('is_flagged', False) else 0.0,
                post.get('age_days', 0) / 30.0,
                post.get('engagement_score', 0) / 100.0,
                0.0  # is_post flag
            ]
            node_features.append(features)
            node_types.append(1)
        
        x = torch.tensor(node_features, dtype=torch.float)
        node_types = torch.tensor(node_types, dtype=torch.long)
        
        # Create edges with weights
        edge_list = []
        edge_weights = []
        
        # User -> Post edges (posted, liked, commented, reported)
        for interaction in interactions:
            user_id = interaction.get('user_id')
            post_id = interaction.get('post_id')
            weight = interaction.get('weight', 1.0)
            
            if user_id and post_id and user_id in user_indices and post_id in post_indices:
                # Bidirectional edges
                edge_list.append([user_indices[user_id], post_indices[post_id]])
                edge_list.append([post_indices[post_id], user_indices[user_id]])
                edge_weights.extend([weight, weight])
        
        # User -> User edges (optional)
        for interaction in interactions:
            if 'user_id_2' in interaction:
                user_id_1 = interaction['user_id']
                user_id_2 = interaction['user_id_2']
                weight = interaction.get('weight', 0.5)
                
                if user_id_1 in user_indices and user_id_2 in user_indices:
                    edge_list.append([user_indices[user_id_1], user_indices[user_id_2]])
                    edge_list.append([user_indices[user_id_2], user_indices[user_id_1]])
                    edge_weights.extend([weight, weight])
        
        if not edge_list:
            # Create self-loops if no edges
            edge_list = [[i, i] for i in range(num_nodes)]
            edge_weights = [1.0] * num_nodes
        
        edge_index = torch.tensor(edge_list, dtype=torch.long).t().contiguous()
        edge_attr = torch.tensor(edge_weights, dtype=torch.float).unsqueeze(1)
        
        # Create graph data
        self.graph_data = Data(
            x=x, 
            edge_index=edge_index,
            edge_attr=edge_attr,
            node_types=node_types
        )
        
        return self.graph_data
    
    def initialize_model(self, input_dim: int):
        """Initialize the GNN model"""
        self.model = CommunityTrustGNN(
            input_dim, 
            hidden_dim=64, 
            output_dim=32, 
            use_gat=self.use_gat
        )
        self.model = self.model.to(self.device)
        self.model.eval()
        print(f"GNN model initialized with input_dim={input_dim}")
    
    def compute_trust_scores(self) -> Dict[str, float]:
        """
        Compute trust scores for all nodes
        
        Returns:
            Dict mapping node_id to trust score (0-1, higher = more trustworthy)
        """
        if self.model is None or self.graph_data is None:
            raise ValueError("Model or graph not initialized")
        
        self.graph_data = self.graph_data.to(self.device)
        
        with torch.no_grad():
            embeddings, trust_scores = self.model(
                self.graph_data.x, 
                self.graph_data.edge_index
            )
        
        trust_scores = trust_scores.cpu().squeeze()
        
        # Map scores back to node IDs
        result = {}
        for node_idx, node_id in self.reverse_mapping.items():
            result[node_id] = float(trust_scores[node_idx].item())
        
        return result
    
    def get_user_trust_score(self, user_id: str) -> float:
        """Get trust score for a specific user (0-1, higher = more trustworthy)"""
        trust_scores = self.compute_trust_scores()
        return trust_scores.get(f"user_{user_id}", 0.5)
    
    def get_post_risk_score(self, post_id: str) -> float:
        """
        Get risk score for a specific post (0-1, higher = more risky)
        Risk = 1 - trust
        """
        trust_scores = self.compute_trust_scores()
        trust = trust_scores.get(f"post_{post_id}", 0.5)
        return 1.0 - trust  # Convert trust to risk
    
    def train_model(self, epochs: int = 100, lr: float = 0.01):
        """
        Train the GNN model (semi-supervised)
        In production, use labeled data for supervision
        """
        if self.model is None or self.graph_data is None:
            raise ValueError("Model or graph not initialized")
        
        self.model.train()
        optimizer = torch.optim.Adam(self.model.parameters(), lr=lr)
        
        self.graph_data = self.graph_data.to(self.device)
        
        for epoch in range(epochs):
            optimizer.zero_grad()
            
            embeddings, trust_scores = self.model(
                self.graph_data.x, 
                self.graph_data.edge_index
            )
            
            # Unsupervised loss: encourage meaningful embeddings
            # 1. Embedding regularization
            emb_loss = 0.01 * embeddings.norm(dim=1).mean()
            
            # 2. Trust score should correlate with node features
            # Users with high reports_received should have low trust
            node_types = self.graph_data.node_types
            user_mask = node_types == 0
            
            if user_mask.sum() > 0:
                # Feature index 2 = reports_received (normalized)
                reports_feature = self.graph_data.x[user_mask, 2]
                user_trust = trust_scores[user_mask].squeeze()
                
                # High reports should mean low trust
                trust_loss = F.mse_loss(user_trust, 1.0 - reports_feature)
            else:
                trust_loss = torch.tensor(0.0).to(self.device)
            
            loss = emb_loss + trust_loss
            
            loss.backward()
            optimizer.step()
            
            if (epoch + 1) % 20 == 0:
                print(f"Epoch {epoch+1}/{epochs}, Loss: {loss.item():.4f}")
        
        self.model.eval()
        print("GNN training completed")
