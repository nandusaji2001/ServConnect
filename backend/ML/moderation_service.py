"""
Intelligent Moderation Service - Multimodal AI + GNN + OCR
Combines text, image, OCR, and user behavior analysis for content moderation
"""

import numpy as np
from typing import Dict, Optional, List
from PIL import Image
import io
import base64
import easyocr

from text_model import TextModerationModel
from clip_service import CLIPService

# Make GNN optional (requires torch-geometric)
try:
    from gnn_model import CommunityGNNService
    from graph_builder import CommunityGraphBuilder
    GNN_AVAILABLE = True
except ImportError as e:
    print(f"Warning: GNN features disabled. Install torch-geometric to enable: {e}")
    GNN_AVAILABLE = False
    CommunityGNNService = None
    CommunityGraphBuilder = None


class IntelligentModerationService:
    """
    Unified moderation service combining:
    - Text toxicity detection (BERT or TF-IDF)
    - Image content analysis (CLIP)
    - OCR text extraction (EasyOCR)
    - User behavior analysis (GNN)
    """
    
    def __init__(self, 
                 text_model_type: str = "legacy",
                 use_gat: bool = False,
                 use_ocr: bool = True,
                 weights: Dict[str, float] = None):
        """
        Initialize intelligent moderation service
        
        Args:
            text_model_type: "legacy" or "transformer"
            use_gat: Use GAT instead of GraphSAGE for GNN
            use_ocr: Enable OCR text extraction from images
            weights: Scoring weights {alpha, beta, gamma}
        """
        print("Initializing Intelligent Moderation Service...")
        
        # Load models
        self.text_model = TextModerationModel(model_type=text_model_type)
        self.clip_service = CLIPService()
        
        # Initialize GNN if available
        if GNN_AVAILABLE:
            self.gnn_service = CommunityGNNService(use_gat=use_gat)
        else:
            self.gnn_service = None
            print("Warning: GNN service not available. User trust scoring disabled.")
        
        # Load OCR if enabled
        self.use_ocr = use_ocr
        self.ocr_reader = None
        if use_ocr:
            try:
                print("Loading EasyOCR for text extraction...")
                self.ocr_reader = easyocr.Reader(['en'], gpu=False)
                print("OCR reader loaded successfully!")
            except Exception as e:
                print(f"Warning: Could not load OCR reader: {e}")
                print("OCR text extraction will be disabled.")
                self.use_ocr = False
        
        # Scoring weights
        self.weights = weights or {
            'alpha': 0.4,   # Text weight
            'beta': 0.3,    # Image weight
            'gamma': 0.3    # Trust/behavior weight
        }
        
        # Harmful content keywords for CLIP
        self.harmful_keywords = [
            "violence", "hate speech", "offensive content", "explicit content",
            "harassment", "bullying", "threatening", "disturbing imagery",
            "suicide", "self harm", "death threats"
        ]
        
        self.safe_keywords = [
            "friendly", "positive", "educational", "informative", "helpful"
        ]
        
        print("Intelligent Moderation Service ready!")
        if self.use_ocr:
            print("  ✓ OCR text extraction enabled")
        else:
            print("  ✗ OCR text extraction disabled")
    
    def extract_text_from_image(self, image_input) -> str:
        """
        Extract text from image using OCR
        
        Args:
            image_input: PIL Image, file path, URL, or base64
        
        Returns:
            Extracted text string
        """
        if not self.use_ocr or self.ocr_reader is None:
            return ""
        
        try:
            # Load image if needed
            if not isinstance(image_input, Image.Image):
                image = self.clip_service.load_image(image_input)
            else:
                image = image_input
            
            # Convert to numpy array
            import numpy as np
            image_np = np.array(image)
            
            # Extract text using EasyOCR
            results = self.ocr_reader.readtext(image_np)
            
            # Combine all extracted text
            extracted_texts = [text for (_, text, _) in results]
            full_text = ' '.join(extracted_texts)
            
            return full_text
        
        except Exception as e:
            print(f"Error extracting text from image: {e}")
            return ""
    
    def analyze_text(self, text: str) -> Dict[str, float]:
        """
        Analyze text for toxicity
        
        Returns:
            {'toxicity_score': float}
        """
        if not text or len(text.strip()) == 0:
            return {'toxicity_score': 0.0}
        
        return self.text_model.predict(text)
    
    def analyze_image(self, image_input, caption: str = "") -> Dict[str, float]:
        """
        Analyze image for harmful content using CLIP
        
        Args:
            image_input: PIL Image, file path, URL, or base64
            caption: Optional caption text
        
        Returns:
            {
                'image_risk_score': float,
                'image_text_consistency': float (if caption provided)
            }
        """
        try:
            # Encode image
            image_embedding = self.clip_service.encode_image(image_input)
            
            # Compute similarity with harmful vs safe keywords
            harmful_scores = []
            for keyword in self.harmful_keywords:
                keyword_emb = self.clip_service.encode_text(keyword)
                similarity = self.clip_service.compute_similarity(image_embedding, keyword_emb)
                harmful_scores.append(max(0, similarity))  # Only positive similarities
            
            safe_scores = []
            for keyword in self.safe_keywords:
                keyword_emb = self.clip_service.encode_text(keyword)
                similarity = self.clip_service.compute_similarity(image_embedding, keyword_emb)
                safe_scores.append(max(0, similarity))
            
            # Compute risk score
            avg_harmful = np.mean(harmful_scores) if harmful_scores else 0
            avg_safe = np.mean(safe_scores) if safe_scores else 0
            
            # Risk = harmful similarity - safe similarity, normalized to [0, 1]
            image_risk_score = (avg_harmful - avg_safe + 1) / 2
            image_risk_score = max(0.0, min(1.0, image_risk_score))
            
            result = {
                'image_risk_score': float(image_risk_score)
            }
            
            # Check image-text consistency if caption provided
            if caption and len(caption.strip()) > 0:
                consistency = self.clip_service.compute_image_text_similarity(
                    image_input, caption
                )
                result['image_text_consistency'] = float(consistency)
            
            return result
            
        except Exception as e:
            print(f"Error analyzing image: {e}")
            return {
                'image_risk_score': 0.0,
                'error': str(e)
            }
    
    def analyze_content(self, 
                       text: str = None,
                       image_input = None,
                       user_id: str = None,
                       post_id: str = None) -> Dict:
        """
        Comprehensive content analysis combining text, image, OCR, and user behavior
        
        Args:
            text: Post caption or comment text
            image_input: Post image (optional)
            user_id: User who created the content
            post_id: Post ID (for GNN lookup)
        
        Returns:
            {
                'text_toxicity_score': float,
                'image_risk_score': float,
                'ocr_text': str,
                'ocr_toxicity_score': float,
                'user_trust_score': float,
                'post_risk_score': float,
                'final_risk_score': float,
                'is_harmful': bool,
                'recommendation': str,
                'reason': str
            }
        """
        result = {
            'text_toxicity_score': 0.0,
            'image_risk_score': 0.0,
            'ocr_text': '',
            'ocr_toxicity_score': 0.0,
            'user_trust_score': 0.5,  # Default neutral
            'post_risk_score': 0.5,
            'final_risk_score': 0.0,
            'is_harmful': False,
            'recommendation': 'approve',
            'reason': ''
        }
        
        # 1. Analyze caption text
        if text:
            text_result = self.analyze_text(text)
            result['text_toxicity_score'] = text_result['toxicity_score']
        
        # 2. Extract text from image using OCR
        if image_input and self.use_ocr:
            ocr_text = self.extract_text_from_image(image_input)
            result['ocr_text'] = ocr_text
            
            if ocr_text and len(ocr_text.strip()) > 0:
                print(f"OCR extracted text: '{ocr_text}'")
                ocr_result = self.analyze_text(ocr_text)
                result['ocr_toxicity_score'] = ocr_result['toxicity_score']
        
        # 3. Analyze image content with CLIP
        if image_input:
            image_result = self.analyze_image(image_input, caption=text)
            result['image_risk_score'] = image_result['image_risk_score']
            if 'image_text_consistency' in image_result:
                result['image_text_consistency'] = image_result['image_text_consistency']
        
        # 4. Get user trust score from GNN
        if user_id and self.gnn_service is not None and self.gnn_service.model is not None:
            try:
                result['user_trust_score'] = self.gnn_service.get_user_trust_score(user_id)
            except:
                result['user_trust_score'] = 0.5
        
        # 5. Get post risk score from GNN
        if post_id and self.gnn_service is not None and self.gnn_service.model is not None:
            try:
                result['post_risk_score'] = self.gnn_service.get_post_risk_score(post_id)
            except:
                result['post_risk_score'] = 0.5
        
        # 6. Compute final risk score
        alpha = self.weights['alpha']
        beta = self.weights['beta']
        gamma = self.weights['gamma']
        
        # Use maximum of caption toxicity and OCR toxicity for text score
        text_score = max(result['text_toxicity_score'], result['ocr_toxicity_score'])
        image_score = result['image_risk_score']
        trust_score = result['user_trust_score']
        
        # Final risk = weighted combination
        # Higher trust reduces risk
        final_risk = (alpha * text_score + 
                     beta * image_score + 
                     gamma * (1 - trust_score))
        
        result['final_risk_score'] = float(final_risk)
        
        # 7. Make moderation decision
        # IMPORTANT: If text toxicity is high, block immediately regardless of other factors
        if text_score >= 0.3:
            result['is_harmful'] = True
            result['recommendation'] = 'block'
            
            if result['ocr_toxicity_score'] >= 0.3:
                result['reason'] = 'Harmful text detected in image'
            else:
                result['reason'] = 'Harmful text detected in caption'
                
        elif final_risk >= 0.3:
            result['is_harmful'] = True
            result['recommendation'] = 'block'
            
            # Determine reason
            if result['ocr_toxicity_score'] > 0.3:
                result['reason'] = 'Harmful text detected in image'
            elif result['text_toxicity_score'] > 0.3:
                result['reason'] = 'Harmful text detected in caption'
            elif result['image_risk_score'] > 0.3:
                result['reason'] = 'Harmful visual content detected'
            else:
                result['reason'] = 'Combined risk factors exceed threshold'
                
        elif final_risk >= 0.2:
            result['recommendation'] = 'flag'
            result['reason'] = 'Content flagged for monitoring'
        else:
            result['recommendation'] = 'approve'
            result['reason'] = 'Content appears safe'
        
        return result
    
    def initialize_gnn(self, graph_builder, 
                      train: bool = True, epochs: int = 100):
        """
        Initialize and optionally train the GNN model
        
        Args:
            graph_builder: CommunityGraphBuilder with user/post data
            train: Whether to train the model
            epochs: Training epochs
        """
        if not GNN_AVAILABLE or self.gnn_service is None:
            print("Warning: Cannot initialize GNN - torch-geometric not installed")
            return
        
        print("Building community graph...")
        users, posts, interactions = graph_builder.build_graph_data()
        
        self.gnn_service.build_graph(users, posts, interactions)
        
        # Initialize model with correct input dimension
        input_dim = self.gnn_service.graph_data.x.shape[1]
        self.gnn_service.initialize_model(input_dim)
        
        if train:
            print(f"Training GNN for {epochs} epochs...")
            self.gnn_service.train_model(epochs=epochs)
        
        print("GNN ready!")
    
    def batch_analyze(self, contents: List[Dict]) -> List[Dict]:
        """
        Analyze multiple contents in batch
        
        Args:
            contents: List of dicts with {text, image_input, user_id, post_id}
        
        Returns:
            List of analysis results
        """
        results = []
        for content in contents:
            result = self.analyze_content(
                text=content.get('text'),
                image_input=content.get('image_input'),
                user_id=content.get('user_id'),
                post_id=content.get('post_id')
            )
            results.append(result)
        return results
    
    def update_weights(self, alpha: float = None, beta: float = None, gamma: float = None):
        """Update scoring weights"""
        if alpha is not None:
            self.weights['alpha'] = alpha
        if beta is not None:
            self.weights['beta'] = beta
        if gamma is not None:
            self.weights['gamma'] = gamma
        
        # Normalize weights
        total = sum(self.weights.values())
        for key in self.weights:
            self.weights[key] /= total
        
        print(f"Updated weights: {self.weights}")
