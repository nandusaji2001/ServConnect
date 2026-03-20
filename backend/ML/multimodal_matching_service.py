"""
Multimodal Matching Service
Combines CLIP embeddings, SBERT text similarity, and GNN trust scores
"""

import numpy as np
from typing import Dict, List, Optional, Tuple
from clip_service import CLIPService
from gnn_service import GNNService
from sentence_transformers import SentenceTransformer
from sklearn.metrics.pairwise import cosine_similarity

class MultimodalMatchingService:
    def __init__(self, use_gat: bool = False):
        """Initialize multimodal matching service"""
        print("Initializing Multimodal Matching Service...")
        
        # Initialize CLIP for multimodal embeddings
        self.clip_service = CLIPService()
        
        # Initialize SBERT for text-only fallback
        self.sbert_model = SentenceTransformer('all-MiniLM-L6-v2')
        
        # Initialize GNN for trust scoring
        self.gnn_service = GNNService(use_gat=use_gat)
        
        print("Multimodal Matching Service initialized!")
    
    def create_item_text(self, item: Dict) -> str:
        """Create combined text representation of an item"""
        parts = []
        if item.get('title'):
            parts.append(item['title'])
        if item.get('category'):
            parts.append(f"Category: {item['category']}")
        if item.get('description'):
            parts.append(item['description'])
        if item.get('location'):
            parts.append(f"Location: {item['location']}")
        return ' '.join(parts)
    
    def compute_multimodal_similarity(self, item1: Dict, item2: Dict) -> Dict[str, float]:
        """
        Compute multimodal similarity between two items
        
        Returns:
            Dict with image_sim, text_sim, cross_modal_sim, and combined_sim
        """
        similarities = {
            'image_sim': 0.0,
            'text_sim': 0.0,
            'cross_modal_sim': 0.0,
            'combined_sim': 0.0
        }
        
        # Text similarity using CLIP
        text1 = self.create_item_text(item1)
        text2 = self.create_item_text(item2)
        
        if text1 and text2:
            similarities['text_sim'] = self.clip_service.compute_text_text_similarity(text1, text2)
        
        # Image similarity using CLIP
        images1 = item1.get('images', [])
        images2 = item2.get('images', [])
        
        if images1 and images2:
            # Compare first images (or average if multiple)
            try:
                img_sim = self.clip_service.compute_image_image_similarity(images1[0], images2[0])
                similarities['image_sim'] = img_sim
            except Exception as e:
                print(f"Error computing image similarity: {e}")
                similarities['image_sim'] = 0.0
        
        # Cross-modal similarity (image1 <-> text2 and text1 <-> image2)
        cross_modal_scores = []
        
        if images1 and text2:
            try:
                score = self.clip_service.compute_image_text_similarity(images1[0], text2)
                cross_modal_scores.append(score)
            except:
                pass
        
        if images2 and text1:
            try:
                score = self.clip_service.compute_image_text_similarity(images2[0], text1)
                cross_modal_scores.append(score)
            except:
                pass
        
        if cross_modal_scores:
            similarities['cross_modal_sim'] = np.mean(cross_modal_scores)
        
        # Combined similarity (weighted average)
        weights = {
            'image': 0.4,
            'text': 0.4,
            'cross_modal': 0.2
        }
        
        combined = (
            weights['image'] * similarities['image_sim'] +
            weights['text'] * similarities['text_sim'] +
            weights['cross_modal'] * similarities['cross_modal_sim']
        )
        
        similarities['combined_sim'] = combined
        
        # Category boost
        if item1.get('category') and item2.get('category'):
            if item1['category'].lower() == item2['category'].lower():
                similarities['combined_sim'] = min(1.0, similarities['combined_sim'] + 0.1)
        
        return similarities
    
    def compute_final_score(self, similarity_score: float, trust_score: float, 
                           alpha: float = 0.7, beta: float = 0.3) -> float:
        """
        Compute final matching score combining similarity and trust
        
        Args:
            similarity_score: Multimodal similarity (0-1)
            trust_score: GNN trust score (0-1)
            alpha: Weight for similarity
            beta: Weight for trust
        
        Returns:
            Final score (0-1)
        """
        return alpha * similarity_score + beta * trust_score
    
    def find_matches(self, query_item: Dict, candidate_items: List[Dict],
                    user_trust_scores: Optional[Dict[str, float]] = None,
                    item_trust_scores: Optional[Dict[str, float]] = None,
                    threshold: float = 0.5, top_k: int = 5,
                    alpha: float = 0.7, beta: float = 0.3) -> List[Dict]:
        """
        Find matching items using multimodal similarity and trust scores
        
        Args:
            query_item: Item to match
            candidate_items: List of candidate items
            user_trust_scores: Dict mapping user_id to trust score
            item_trust_scores: Dict mapping item_id to trust score
            threshold: Minimum final score threshold
            top_k: Maximum number of matches to return
            alpha: Weight for similarity score
            beta: Weight for trust score
        
        Returns:
            List of matches with scores
        """
        if not candidate_items:
            return []
        
        matches = []
        
        for candidate in candidate_items:
            # Compute multimodal similarity
            similarities = self.compute_multimodal_similarity(query_item, candidate)
            similarity_score = similarities['combined_sim']
            
            # Get trust scores
            user_id = candidate.get('user_id', '')
            item_id = candidate.get('id', '')
            
            user_trust = 0.5  # Default
            item_trust = 0.5  # Default
            
            if user_trust_scores and user_id:
                user_trust = user_trust_scores.get(user_id, 0.5)
            
            if item_trust_scores and item_id:
                item_trust = item_trust_scores.get(item_id, 0.5)
            
            # Combined trust score
            trust_score = (user_trust + item_trust) / 2.0
            
            # Final score
            final_score = self.compute_final_score(similarity_score, trust_score, alpha, beta)
            
            if final_score >= threshold:
                matches.append({
                    'item': candidate,
                    'similarity_score': float(similarity_score),
                    'trust_score': float(trust_score),
                    'final_score': float(final_score),
                    'match_percentage': round(float(final_score) * 100, 1),
                    'details': {
                        'image_similarity': float(similarities['image_sim']),
                        'text_similarity': float(similarities['text_sim']),
                        'cross_modal_similarity': float(similarities['cross_modal_sim']),
                        'user_trust': float(user_trust),
                        'item_trust': float(item_trust)
                    }
                })
        
        # Sort by final score descending
        matches.sort(key=lambda x: x['final_score'], reverse=True)
        return matches[:top_k]
    
    def initialize_gnn(self, users: List[Dict], items: List[Dict], 
                      interactions: List[Dict]) -> Tuple[Dict[str, float], Dict[str, float]]:
        """
        Initialize and train GNN, return trust scores
        
        Returns:
            Tuple of (user_trust_scores, item_trust_scores)
        """
        print("Building graph...")
        self.gnn_service.build_graph(users, items, interactions)
        
        print("Initializing GNN model...")
        input_dim = self.gnn_service.graph_data.x.shape[1]
        self.gnn_service.initialize_model(input_dim)
        
        print("Training GNN...")
        self.gnn_service.simulate_training(epochs=50)
        
        print("Computing trust scores...")
        all_trust_scores = self.gnn_service.compute_trust_scores()
        
        # Split into user and item trust scores
        user_trust_scores = {}
        item_trust_scores = {}
        
        for node_id, score in all_trust_scores.items():
            if node_id.startswith('user_'):
                user_id = node_id.replace('user_', '')
                user_trust_scores[user_id] = score
            elif node_id.startswith('item_'):
                item_id = node_id.replace('item_', '')
                item_trust_scores[item_id] = score
        
        return user_trust_scores, item_trust_scores
