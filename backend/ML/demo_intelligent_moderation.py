"""
Demo: Intelligent Moderation System
Demonstrates the multimodal AI + GNN moderation system
"""

import sys
from datetime import datetime, timedelta
from PIL import Image, ImageDraw, ImageFont
import io

from moderation_service import IntelligentModerationService
from graph_builder import CommunityGraphBuilder


def create_sample_image(text: str, color: str = "white") -> Image.Image:
    """Create a simple test image with text"""
    img = Image.new('RGB', (400, 300), color=color)
    draw = ImageDraw.Draw(img)
    
    # Draw text in center
    try:
        font = ImageFont.truetype("arial.ttf", 40)
    except:
        font = ImageFont.load_default()
    
    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    
    position = ((400 - text_width) // 2, (300 - text_height) // 2)
    draw.text(position, text, fill="black", font=font)
    
    return img


def demo_text_analysis(service: IntelligentModerationService):
    """Demo text toxicity detection"""
    print("\n" + "="*60)
    print("DEMO 1: Text Toxicity Analysis")
    print("="*60)
    
    test_texts = [
        "Welcome to our community! Let's be kind to each other.",
        "This is a helpful post about gardening tips.",
        "I hate this stupid thing, you're all idiots!",
        "You should be ashamed of yourself, loser.",
    ]
    
    for i, text in enumerate(test_texts, 1):
        print(f"\nText {i}: {text[:60]}...")
        result = service.analyze_text(text)
        print(f"  Toxicity Score: {result['toxicity_score']:.3f}")
        print(f"  Assessment: {'🚨 TOXIC' if result['toxicity_score'] > 0.5 else '✅ SAFE'}")


def demo_image_analysis(service: IntelligentModerationService):
    """Demo image content analysis with CLIP"""
    print("\n" + "="*60)
    print("DEMO 2: Image Content Analysis (CLIP)")
    print("="*60)
    
    # Create test images
    test_cases = [
        ("Friendly Community", "white", "A friendly community gathering"),
        ("Violence Warning", "red", "Violent content warning"),
        ("Educational Content", "lightblue", "Educational material"),
    ]
    
    for i, (img_text, color, caption) in enumerate(test_cases, 1):
        print(f"\nImage {i}: {img_text}")
        print(f"  Caption: {caption}")
        
        img = create_sample_image(img_text, color)
        result = service.analyze_image(img, caption)
        
        print(f"  Image Risk Score: {result['image_risk_score']:.3f}")
        if 'image_text_consistency' in result:
            print(f"  Image-Text Consistency: {result['image_text_consistency']:.3f}")
        print(f"  Assessment: {'🚨 RISKY' if result['image_risk_score'] > 0.5 else '✅ SAFE'}")


def demo_gnn_trust_scoring(service: IntelligentModerationService):
    """Demo GNN-based user trust scoring"""
    print("\n" + "="*60)
    print("DEMO 3: GNN User Trust Scoring")
    print("="*60)
    
    # Build sample community graph
    builder = CommunityGraphBuilder()
    now = datetime.now()
    
    # Add users with different behavior patterns
    users_data = [
        ("user1", 365, 50, 2, 1, "Good user - few violations"),
        ("user2", 180, 20, 8, 5, "Problematic user - many violations"),
        ("user3", 730, 100, 0, 0, "Excellent user - no violations"),
        ("user4", 30, 5, 0, 0, "New user - no history"),
    ]
    
    for user_id, age_days, posts, harmful, reports, desc in users_data:
        builder.add_user(
            user_id, 
            now - timedelta(days=age_days),
            posts_count=posts,
            harmful_posts=harmful,
            reports_received=reports
        )
    
    # Add posts
    builder.add_post('post1', 'user1', now - timedelta(days=5), likes_count=10, reports_count=0)
    builder.add_post('post2', 'user2', now - timedelta(days=2), likes_count=2, reports_count=3, is_flagged=True)
    builder.add_post('post3', 'user3', now - timedelta(days=10), likes_count=50, reports_count=0)
    builder.add_post('post4', 'user4', now - timedelta(days=1), likes_count=5, reports_count=0)
    
    # Add interactions
    for post_id, user_id in [('post1', 'user1'), ('post2', 'user2'), ('post3', 'user3'), ('post4', 'user4')]:
        builder.add_interaction(user_id, post_id, 'posted', weight=1.0)
    
    builder.add_interaction('user1', 'post3', 'liked', weight=0.5)
    builder.add_interaction('user3', 'post1', 'liked', weight=0.5)
    builder.add_interaction('user1', 'post2', 'reported', weight=1.5)
    
    # Initialize GNN
    print("\nInitializing GNN with community graph...")
    service.initialize_gnn(builder, train=True, epochs=50)
    
    # Display trust scores
    print("\nUser Trust Scores:")
    for user_id, _, _, _, _, desc in users_data:
        trust_score = service.gnn_service.get_user_trust_score(user_id)
        print(f"  {user_id}: {trust_score:.3f} - {desc}")
    
    print("\nPost Risk Scores:")
    for post_id in ['post1', 'post2', 'post3', 'post4']:
        risk_score = service.gnn_service.get_post_risk_score(post_id)
        print(f"  {post_id}: {risk_score:.3f}")


def demo_comprehensive_analysis(service: IntelligentModerationService):
    """Demo comprehensive content analysis"""
    print("\n" + "="*60)
    print("DEMO 4: Comprehensive Content Analysis")
    print("="*60)
    
    test_cases = [
        {
            'text': "Check out this amazing sunset photo!",
            'image_text': "Beautiful Sunset",
            'image_color': "orange",
            'user_id': 'user3',
            'post_id': 'post3',
            'description': "Good user, positive content"
        },
        {
            'text': "This is terrible, I hate everyone here!",
            'image_text': "Angry Message",
            'image_color': "red",
            'user_id': 'user2',
            'post_id': 'post2',
            'description': "Problematic user, toxic content"
        },
        {
            'text': "Join our community event this weekend!",
            'image_text': "Community Event",
            'image_color': "lightgreen",
            'user_id': 'user1',
            'post_id': 'post1',
            'description': "Good user, positive content"
        },
    ]
    
    for i, case in enumerate(test_cases, 1):
        print(f"\n--- Case {i}: {case['description']} ---")
        print(f"Text: {case['text']}")
        print(f"User: {case['user_id']}, Post: {case['post_id']}")
        
        img = create_sample_image(case['image_text'], case['image_color'])
        
        result = service.analyze_content(
            text=case['text'],
            image_input=img,
            user_id=case['user_id'],
            post_id=case['post_id']
        )
        
        print(f"\nResults:")
        print(f"  Text Toxicity:    {result['text_toxicity_score']:.3f}")
        print(f"  Image Risk:       {result['image_risk_score']:.3f}")
        print(f"  User Trust:       {result['user_trust_score']:.3f}")
        print(f"  Post Risk:        {result['post_risk_score']:.3f}")
        print(f"  Final Risk:       {result['final_risk_score']:.3f}")
        print(f"  Is Harmful:       {result['is_harmful']}")
        print(f"  Recommendation:   {result['recommendation'].upper()}")
        
        # Visual indicator
        if result['recommendation'] == 'block':
            print("  🚫 ACTION: BLOCK CONTENT")
        elif result['recommendation'] == 'review':
            print("  ⚠️  ACTION: SEND FOR MANUAL REVIEW")
        elif result['recommendation'] == 'flag':
            print("  🏴 ACTION: FLAG FOR MONITORING")
        else:
            print("  ✅ ACTION: APPROVE CONTENT")


def demo_weight_tuning(service: IntelligentModerationService):
    """Demo weight adjustment"""
    print("\n" + "="*60)
    print("DEMO 5: Weight Tuning")
    print("="*60)
    
    test_text = "This is somewhat questionable content"
    test_img = create_sample_image("Questionable", "yellow")
    
    weight_configs = [
        {'alpha': 0.7, 'beta': 0.2, 'gamma': 0.1, 'name': 'Text-focused'},
        {'alpha': 0.2, 'beta': 0.7, 'gamma': 0.1, 'name': 'Image-focused'},
        {'alpha': 0.2, 'beta': 0.2, 'gamma': 0.6, 'name': 'Trust-focused'},
        {'alpha': 0.33, 'beta': 0.33, 'gamma': 0.34, 'name': 'Balanced'},
    ]
    
    for config in weight_configs:
        print(f"\n{config['name']} (α={config['alpha']}, β={config['beta']}, γ={config['gamma']})")
        
        service.update_weights(
            alpha=config['alpha'],
            beta=config['beta'],
            gamma=config['gamma']
        )
        
        result = service.analyze_content(
            text=test_text,
            image_input=test_img,
            user_id='user1',
            post_id='post1'
        )
        
        print(f"  Final Risk: {result['final_risk_score']:.3f}")
        print(f"  Recommendation: {result['recommendation'].upper()}")


def main():
    """Run all demos"""
    print("="*60)
    print("INTELLIGENT MODERATION SYSTEM DEMO")
    print("Multimodal AI + Graph Neural Networks")
    print("="*60)
    
    # Initialize service
    print("\nInitializing Intelligent Moderation Service...")
    service = IntelligentModerationService(
        text_model_type="legacy",  # Use legacy for demo
        use_gat=False,
        weights={'alpha': 0.4, 'beta': 0.3, 'gamma': 0.3}
    )
    
    # Run demos
    try:
        demo_text_analysis(service)
        demo_image_analysis(service)
        demo_gnn_trust_scoring(service)
        demo_comprehensive_analysis(service)
        demo_weight_tuning(service)
        
        print("\n" + "="*60)
        print("DEMO COMPLETED SUCCESSFULLY!")
        print("="*60)
        print("\nKey Features Demonstrated:")
        print("  ✓ Text toxicity detection (TF-IDF + Logistic Regression)")
        print("  ✓ Image content analysis (CLIP)")
        print("  ✓ User trust scoring (GNN)")
        print("  ✓ Comprehensive multimodal analysis")
        print("  ✓ Configurable weight tuning")
        print("\nNext Steps:")
        print("  1. Replace legacy text model with BERT for better accuracy")
        print("  2. Integrate with real community database")
        print("  3. Train GNN on labeled data")
        print("  4. Deploy API: python intelligent_moderation_api.py")
        
    except Exception as e:
        print(f"\n❌ Error during demo: {e}")
        import traceback
        traceback.print_exc()


if __name__ == '__main__':
    main()
