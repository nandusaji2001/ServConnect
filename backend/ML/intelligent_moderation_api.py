"""
Intelligent Moderation API
Flask API for multimodal content moderation with GNN-based trust scoring
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import base64
import io
from PIL import Image
import os

from moderation_service import IntelligentModerationService

# Make GNN optional
try:
    from graph_builder import CommunityGraphBuilder, create_sample_community_graph
    GNN_AVAILABLE = True
except ImportError:
    print("Warning: GNN features disabled. Install torch-geometric to enable.")
    GNN_AVAILABLE = False
    CommunityGraphBuilder = None
    create_sample_community_graph = None

app = Flask(__name__)
CORS(app)

# Global service
moderation_service = None
graph_builder = None

def load_service():
    """Initialize the intelligent moderation service"""
    global moderation_service, graph_builder
    
    print("Loading Intelligent Moderation Service...")
    
    # Initialize service (use legacy model for backward compatibility)
    moderation_service = IntelligentModerationService(
        text_model_type="legacy",  # Change to "transformer" for BERT
        use_gat=False,  # Use GraphSAGE (faster)
        use_ocr=True,   # Enable OCR text extraction
        weights={'alpha': 0.4, 'beta': 0.3, 'gamma': 0.3}
    )
    
    # Initialize with sample graph if GNN is available
    if GNN_AVAILABLE:
        print("Initializing GNN with sample data...")
        graph_builder = create_sample_community_graph()
        moderation_service.initialize_gnn(graph_builder, train=True, epochs=50)
    else:
        print("GNN features disabled - running without user trust scoring")
    
    print("Service ready!")
    return True


@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'service_loaded': moderation_service is not None,
        'features': ['text', 'image', 'gnn']
    })


@app.route('/analyze/text', methods=['POST'])
def analyze_text():
    """
    Analyze text only
    
    Request:
    {
        "text": "content to analyze"
    }
    
    Response:
    {
        "toxicity_score": 0.85
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'text' not in data:
        return jsonify({'error': 'Missing text field'}), 400
    
    result = moderation_service.analyze_text(data['text'])
    return jsonify(result)


@app.route('/analyze/image', methods=['POST'])
def analyze_image():
    """
    Analyze image with optional caption
    
    Request:
    {
        "image": "base64_encoded_image",
        "caption": "optional caption"
    }
    
    Response:
    {
        "image_risk_score": 0.3,
        "image_text_consistency": 0.85
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'image' not in data:
        return jsonify({'error': 'Missing image field'}), 400
    
    try:
        # Decode base64 image
        image_data = data['image']
        if image_data.startswith('data:image'):
            image_data = image_data.split(',')[1]
        
        image_bytes = base64.b64decode(image_data)
        image = Image.open(io.BytesIO(image_bytes))
        
        caption = data.get('caption', '')
        result = moderation_service.analyze_image(image, caption)
        
        return jsonify(result)
    
    except Exception as e:
        return jsonify({'error': str(e)}), 400


@app.route('/analyze/content', methods=['POST'])
def analyze_content():
    """
    Comprehensive content analysis (text + image + user behavior)
    
    Request:
    {
        "text": "post caption",
        "image": "base64_encoded_image (optional)",
        "user_id": "user123",
        "post_id": "post456"
    }
    
    Response:
    {
        "text_toxicity_score": 0.2,
        "image_risk_score": 0.1,
        "user_trust_score": 0.85,
        "post_risk_score": 0.15,
        "final_risk_score": 0.25,
        "is_harmful": false,
        "recommendation": "approve"
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    
    text = data.get('text')
    user_id = data.get('user_id')
    post_id = data.get('post_id')
    
    # Handle image if provided
    image = None
    if 'image' in data and data['image']:
        try:
            image_data = data['image']
            if image_data.startswith('data:image'):
                image_data = image_data.split(',')[1]
            
            image_bytes = base64.b64decode(image_data)
            image = Image.open(io.BytesIO(image_bytes))
        except Exception as e:
            print(f"Error decoding image: {e}")
    
    result = moderation_service.analyze_content(
        text=text,
        image_input=image,
        user_id=user_id,
        post_id=post_id
    )
    
    return jsonify(result)


@app.route('/analyze/batch', methods=['POST'])
def analyze_batch():
    """
    Batch content analysis
    
    Request:
    {
        "contents": [
            {"text": "...", "user_id": "...", "post_id": "..."},
            ...
        ]
    }
    
    Response:
    {
        "results": [...]
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'contents' not in data:
        return jsonify({'error': 'Missing contents field'}), 400
    
    results = moderation_service.batch_analyze(data['contents'])
    return jsonify({'results': results})


@app.route('/gnn/trust', methods=['POST'])
def get_trust_score():
    """
    Get user trust score from GNN
    
    Request:
    {
        "user_id": "user123"
    }
    
    Response:
    {
        "user_id": "user123",
        "trust_score": 0.85
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'user_id' not in data:
        return jsonify({'error': 'Missing user_id field'}), 400
    
    user_id = data['user_id']
    trust_score = moderation_service.gnn_service.get_user_trust_score(user_id)
    
    return jsonify({
        'user_id': user_id,
        'trust_score': trust_score
    })


@app.route('/gnn/post-risk', methods=['POST'])
def get_post_risk():
    """
    Get post risk score from GNN
    
    Request:
    {
        "post_id": "post123"
    }
    
    Response:
    {
        "post_id": "post123",
        "risk_score": 0.15
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'post_id' not in data:
        return jsonify({'error': 'Missing post_id field'}), 400
    
    post_id = data['post_id']
    risk_score = moderation_service.gnn_service.get_post_risk_score(post_id)
    
    return jsonify({
        'post_id': post_id,
        'risk_score': risk_score
    })


@app.route('/config/weights', methods=['POST'])
def update_weights():
    """
    Update scoring weights
    
    Request:
    {
        "alpha": 0.4,  # text weight
        "beta": 0.3,   # image weight
        "gamma": 0.3   # trust weight
    }
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    
    moderation_service.update_weights(
        alpha=data.get('alpha'),
        beta=data.get('beta'),
        gamma=data.get('gamma')
    )
    
    return jsonify({
        'weights': moderation_service.weights
    })


# Legacy endpoint for backward compatibility
@app.route('/predict', methods=['POST'])
def predict_legacy():
    """
    Legacy endpoint - text-only prediction
    Maintains backward compatibility with existing system
    """
    if moderation_service is None:
        return jsonify({'error': 'Service not loaded'}), 500
    
    data = request.get_json()
    if not data or 'text' not in data:
        return jsonify({'error': 'Missing text field'}), 400
    
    text = data['text']
    threshold = data.get('threshold', 0.5)
    
    result = moderation_service.analyze_text(text)
    toxicity_score = result['toxicity_score']
    
    return jsonify({
        'is_harmful': toxicity_score >= threshold,
        'confidence': toxicity_score,
        'threshold': threshold
    })


if __name__ == '__main__':
    if load_service():
        print("Starting Intelligent Moderation API on port 5051...")
        app.run(host='0.0.0.0', port=5051, debug=False)
    else:
        print("Failed to load service")
