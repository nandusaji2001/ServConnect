"""
GNN Service - Graph Neural Network for Trust Scoring
Uses PyTorch Geometric to compute user and item trust scores
"""

import torch
import torch.nn.functional as F
from torch_geometric.nn import SAGEConv, GATConv
from torch_geometric.data import Data
import numpy as np
from typing import Dict, List, Tuple
from datetime import datetime, timedelta

class TrustGNN(torch.nn.Module):
    """Graph Neural Network for computing trust scores"""
    
    def __init__(self, input_dim: int, hidden_dim: int = 64, output_dim: int = 32, use_gat: bool = False):
        super(TrustGNN, self).__init__()
        self.use_gat = use_gat
        
        if use_gat:
            # Graph Attention Network
            self.conv1 = GATConv(input_dim, hidden_dim, heads=4, concat=True)
            self.conv2 = GATConv(hidden_dim * 4, output_dim, heads=1, concat=False)
        else:
            # GraphSAGE
            self.conv1 = SAGEConv(input_dim, hidden_dim)
            self.conv2 = SAGEConv(hidden_dim, output_dim)
    
    def forward(self, x, edge_index):
        x = self.conv1(x, edge_index)
        x = F.relu(x)
        x = F.dropout(x, p=0.3, training=self.training)
        x = self.conv2(x, edge_index)
        return x

class GNNService:
    def __init__(self, use_gat: bool = False):
        """Initialize GNN service"""
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.use_gat = use_gat
        self.model = None
        self.graph_data = None
        self.node_mapping = {}  # Maps user_id/item_id to node index
        print(f"GNN Service initialized on {self.device}")
    
    def build_graph(self, users: List[Dict], items: List[Dict], 
                    interactions: List[Dict]) -> Data:
        """
        Build graph from users, items, and interactions
        
        Args:
            users: List of user dicts with {id, posts_count, reports_count, account_age_days}
            items: List of item dicts with {id, user_id, claims_count, verified}
            interactions: List of interaction dicts with {user_id, item_id, type}
        
        Returns:
            PyTorch Geometric Data object
        """
        # Create node mapping
        self.node_mapping = {}
        node_idx = 0
        
        # Map users
        user_indices = {}
        for user in users:
            user_id = user['id']
            self.node_mapping[f"user_{user_id}"] = node_idx
            user_indices[user_id] = node_idx
            node_idx += 1
        
        # Map items
        item_indices = {}
        for item in items:
            item_id = item['id']
            self.node_mapping[f"item_{item_id}"] = node_idx
            item_indices[item_id] = node_idx
            node_idx += 1
        
        num_nodes = node_idx
        
        # Create node features
        node_features = []
        
        # User features: [posts_count, reports_count, account_age_days, is_user]
        for user in users:
            features = [
                user.get('posts_count', 0) / 100.0,  # Normalize
                user.get('reports_count', 0) / 10.0,
                user.get('account_age_days', 0) / 365.0,
                1.0  # is_user flag
            ]
            node_features.append(features)
        
        # Item features: [claims_count, verified, age_days, is_item]
        for item in items:
            features = [
                item.get('claims_count', 0) / 10.0,
                1.0 if item.get('verified', False) else 0.0,
                item.get('age_days', 0) / 30.0,
                0.0  # is_item flag
            ]
            node_features.append(features)
        
        x = torch.tensor(node_features, dtype=torch.float)
        
        # Create edges
        edge_list = []
        
        # User -> Item edges (posted)
        for item in items:
            user_id = item['user_id']
            item_id = item['id']
            if user_id in user_indices and item_id in item_indices:
                edge_list.append([user_indices[user_id], item_indices[item_id]])
                edge_list.append([item_indices[item_id], user_indices[user_id]])  # Bidirectional
        
        # User -> Item edges (interactions: claims, views, etc.)
        for interaction in interactions:
            user_id = interaction['user_id']
            item_id = interaction['item_id']
            if user_id in user_indices and item_id in item_indices:
                edge_list.append([user_indices[user_id], item_indices[item_id]])
                edge_list.append([item_indices[item_id], user_indices[user_id]])
        
        if not edge_list:
            # Create self-loops if no edges
            edge_list = [[i, i] for i in range(num_nodes)]
        
        edge_index = torch.tensor(edge_list, dtype=torch.long).t().contiguous()
        
        # Create graph data
        self.graph_data = Data(x=x, edge_index=edge_index)
        
        return self.graph_data
    
    def initialize_model(self, input_dim: int):
        """Initialize the GNN model"""
        self.model = TrustGNN(input_dim, hidden_dim=64, output_dim=32, use_gat=self.use_gat)
        self.model = self.model.to(self.device)
        self.model.eval()
        print(f"GNN model initialized with input_dim={input_dim}")
    
    def compute_embeddings(self) -> torch.Tensor:
        """Compute node embeddings using the GNN"""
        if self.model is None or self.graph_data is None:
            raise ValueError("Model or graph not initialized")
        
        self.graph_data = self.graph_data.to(self.device)
        
        with torch.no_grad():
            embeddings = self.model(self.graph_data.x, self.graph_data.edge_index)
        
        return embeddings.cpu()
    
    def compute_trust_scores(self) -> Dict[str, float]:
        """
        Compute trust scores for all nodes
        
        Returns:
            Dict mapping node_id to trust score (0-1)
        """
        embeddings = self.compute_embeddings()
        
        # Compute trust score as normalized embedding magnitude
        trust_scores = {}
        
        for node_id, node_idx in self.node_mapping.items():
            emb = embeddings[node_idx]
            # Trust score: combination of embedding norm and features
            trust_score = float(torch.sigmoid(emb.norm()).item())
            trust_scores[node_id] = trust_score
        
        return trust_scores
    
    def get_user_trust_score(self, user_id: str) -> float:
        """Get trust score for a specific user"""
        trust_scores = self.compute_trust_scores()
        return trust_scores.get(f"user_{user_id}", 0.5)  # Default 0.5
    
    def get_item_trust_score(self, item_id: str) -> float:
        """Get trust score for a specific item"""
        trust_scores = self.compute_trust_scores()
        return trust_scores.get(f"item_{item_id}", 0.5)  # Default 0.5
    
    def simulate_training(self, epochs: int = 50):
        """
        Simulate training (for demo purposes)
        In production, you'd train on labeled data
        """
        if self.model is None or self.graph_data is None:
            raise ValueError("Model or graph not initialized")
        
        self.model.train()
        optimizer = torch.optim.Adam(self.model.parameters(), lr=0.01)
        
        self.graph_data = self.graph_data.to(self.device)
        
        for epoch in range(epochs):
            optimizer.zero_grad()
            
            # Forward pass
            out = self.model(self.graph_data.x, self.graph_data.edge_index)
            
            # Dummy loss: encourage embeddings to be distinct but not too large
            loss = F.mse_loss(out, torch.zeros_like(out)) + 0.01 * out.norm()
            
            loss.backward()
            optimizer.step()
            
            if (epoch + 1) % 10 == 0:
                print(f"Epoch {epoch+1}/{epochs}, Loss: {loss.item():.4f}")
        
        self.model.eval()
        print("Training completed")

def create_sample_graph_data():
    """Create sample data for testing"""
    users = [
        {'id': 'user1', 'posts_count': 10, 'reports_count': 0, 'account_age_days': 365},
        {'id': 'user2', 'posts_count': 5, 'reports_count': 2, 'account_age_days': 180},
        {'id': 'user3', 'posts_count': 20, 'reports_count': 0, 'account_age_days': 730},
    ]
    
    items = [
        {'id': 'item1', 'user_id': 'user1', 'claims_count': 2, 'verified': True, 'age_days': 5},
        {'id': 'item2', 'user_id': 'user2', 'claims_count': 5, 'verified': False, 'age_days': 10},
        {'id': 'item3', 'user_id': 'user3', 'claims_count': 1, 'verified': True, 'age_days': 3},
    ]
    
    interactions = [
        {'user_id': 'user2', 'item_id': 'item1', 'type': 'claim'},
        {'user_id': 'user3', 'item_id': 'item2', 'type': 'view'},
        {'user_id': 'user1', 'item_id': 'item3', 'type': 'view'},
    ]
    
    return users, items, interactions
