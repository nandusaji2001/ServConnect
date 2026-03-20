"""
CLIP Service - Multimodal Embeddings for Lost & Found
Generates embeddings for both images and text using OpenAI's CLIP model
"""

import torch
import numpy as np
from PIL import Image
from transformers import CLIPProcessor, CLIPModel
from typing import Union, List, Dict
import requests
from io import BytesIO
import base64

class CLIPService:
    def __init__(self, model_name='openai/clip-vit-base-patch32'):
        """Initialize CLIP model and processor"""
        print(f"Loading CLIP model: {model_name}...")
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.model = CLIPModel.from_pretrained(model_name).to(self.device)
        self.processor = CLIPProcessor.from_pretrained(model_name)
        self.model.eval()
        print(f"CLIP model loaded on {self.device}")
    
    def load_image(self, image_input: Union[str, bytes]) -> Image.Image:
        """Load image from URL, file path, or bytes"""
        try:
            if isinstance(image_input, bytes):
                return Image.open(BytesIO(image_input)).convert('RGB')
            elif image_input.startswith('http'):
                response = requests.get(image_input, timeout=10)
                return Image.open(BytesIO(response.content)).convert('RGB')
            elif image_input.startswith('data:image'):
                # Base64 encoded image
                header, encoded = image_input.split(',', 1)
                data = base64.b64decode(encoded)
                return Image.open(BytesIO(data)).convert('RGB')
            else:
                return Image.open(image_input).convert('RGB')
        except Exception as e:
            print(f"Error loading image: {e}")
            raise
    
    def encode_image(self, image_input: Union[str, bytes, Image.Image]) -> np.ndarray:
        """Generate CLIP embedding for an image"""
        if not isinstance(image_input, Image.Image):
            image = self.load_image(image_input)
        else:
            image = image_input
        
        with torch.no_grad():
            inputs = self.processor(images=image, return_tensors="pt").to(self.device)
            image_features = self.model.get_image_features(**inputs)
            # Normalize embeddings
            image_features = image_features / image_features.norm(dim=-1, keepdim=True)
            return image_features.cpu().numpy()[0]
    
    def encode_text(self, text: str) -> np.ndarray:
        """Generate CLIP embedding for text"""
        with torch.no_grad():
            inputs = self.processor(text=[text], return_tensors="pt", padding=True).to(self.device)
            text_features = self.model.get_text_features(**inputs)
            # Normalize embeddings
            text_features = text_features / text_features.norm(dim=-1, keepdim=True)
            return text_features.cpu().numpy()[0]
    
    def encode_batch_images(self, images: List[Union[str, bytes, Image.Image]]) -> np.ndarray:
        """Generate CLIP embeddings for multiple images"""
        loaded_images = []
        for img in images:
            if not isinstance(img, Image.Image):
                loaded_images.append(self.load_image(img))
            else:
                loaded_images.append(img)
        
        with torch.no_grad():
            inputs = self.processor(images=loaded_images, return_tensors="pt").to(self.device)
            image_features = self.model.get_image_features(**inputs)
            image_features = image_features / image_features.norm(dim=-1, keepdim=True)
            return image_features.cpu().numpy()
    
    def encode_batch_texts(self, texts: List[str]) -> np.ndarray:
        """Generate CLIP embeddings for multiple texts"""
        with torch.no_grad():
            inputs = self.processor(text=texts, return_tensors="pt", padding=True).to(self.device)
            text_features = self.model.get_text_features(**inputs)
            text_features = text_features / text_features.norm(dim=-1, keepdim=True)
            return text_features.cpu().numpy()
    
    def compute_similarity(self, embedding1: np.ndarray, embedding2: np.ndarray) -> float:
        """Compute cosine similarity between two embeddings"""
        return float(np.dot(embedding1, embedding2))
    
    def compute_image_text_similarity(self, image_input: Union[str, bytes, Image.Image], text: str) -> float:
        """Compute similarity between an image and text"""
        image_emb = self.encode_image(image_input)
        text_emb = self.encode_text(text)
        return self.compute_similarity(image_emb, text_emb)
    
    def compute_image_image_similarity(self, image1: Union[str, bytes, Image.Image], 
                                       image2: Union[str, bytes, Image.Image]) -> float:
        """Compute similarity between two images"""
        emb1 = self.encode_image(image1)
        emb2 = self.encode_image(image2)
        return self.compute_similarity(emb1, emb2)
    
    def compute_text_text_similarity(self, text1: str, text2: str) -> float:
        """Compute similarity between two texts"""
        emb1 = self.encode_text(text1)
        emb2 = self.encode_text(text2)
        return self.compute_similarity(emb1, emb2)
