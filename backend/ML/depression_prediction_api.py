"""
Depression Prediction & Wellness Task Recommendation API
Uses XGBoost Classifier to predict depression status
Generates personalized 7-day wellness tasks with location-based recommendations
"""

import os
import pickle
import pandas as pd
import numpy as np
from flask import Flask, request, jsonify
from flask_cors import CORS
import random
from datetime import datetime, timedelta
import warnings
warnings.filterwarnings('ignore')

app = Flask(__name__)
CORS(app)

# Global variables
model_data = {}
MODEL_PATH = os.path.join(os.path.dirname(__file__), 'models', 'depression_model.pkl')

# Wellness task categories
TASK_CATEGORIES = {
    'outdoor': {
        'name': 'Outdoor Activity',
        'icon': 'fa-tree',
        'color': '#10B981',
        'tasks': [
            {'title': 'Take a 30-minute walk in nature', 'description': 'Walking in green spaces reduces cortisol and improves mood', 'duration': '30 min'},
            {'title': 'Watch the sunrise or sunset', 'description': 'Natural light exposure helps regulate your circadian rhythm', 'duration': '20 min'},
            {'title': 'Practice outdoor yoga or stretching', 'description': 'Combine physical movement with fresh air for mental clarity', 'duration': '25 min'},
            {'title': 'Have a picnic in a park', 'description': 'Enjoy a meal outdoors to combine nutrition and nature therapy', 'duration': '45 min'},
            {'title': 'Take photos of beautiful things you see', 'description': 'Mindful photography helps you notice positive things around you', 'duration': '30 min'},
            {'title': 'Do some light gardening or plant care', 'description': 'Nurturing plants can be therapeutic and grounding', 'duration': '30 min'},
            {'title': 'Walk barefoot on grass (grounding)', 'description': 'Earthing has been shown to reduce inflammation and stress', 'duration': '15 min'},
        ]
    },
    'social': {
        'name': 'Social Connection',
        'icon': 'fa-users',
        'color': '#3B82F6',
        'tasks': [
            {'title': 'Call or video chat with a friend or family', 'description': 'Social connections are vital for mental health', 'duration': '20 min'},
            {'title': 'Write a thank you message to someone', 'description': 'Expressing gratitude strengthens relationships and boosts mood', 'duration': '10 min'},
            {'title': 'Join a community event or class', 'description': 'Engaging with others in shared activities builds belonging', 'duration': '1 hour'},
            {'title': 'Send a thoughtful message to 3 people', 'description': 'Small gestures of connection can brighten both your days', 'duration': '15 min'},
            {'title': 'Have a meaningful conversation with someone', 'description': 'Deep conversations improve emotional well-being', 'duration': '30 min'},
            {'title': 'Volunteer or help someone in need', 'description': 'Helping others releases feel-good hormones', 'duration': '1 hour'},
            {'title': 'Play a board game or activity with friends', 'description': 'Fun social activities reduce stress and build bonds', 'duration': '1 hour'},
        ]
    },
    'mindfulness': {
        'name': 'Mindfulness & Relaxation',
        'icon': 'fa-spa',
        'color': '#8B5CF6',
        'tasks': [
            {'title': 'Practice 10 minutes of deep breathing', 'description': 'Deep breathing activates your parasympathetic nervous system', 'duration': '10 min'},
            {'title': 'Try a guided meditation session', 'description': 'Meditation reduces anxiety and improves focus', 'duration': '15 min'},
            {'title': 'Take a relaxing bath or shower', 'description': 'Warm water relaxes muscles and calms the mind', 'duration': '30 min'},
            {'title': 'Practice progressive muscle relaxation', 'description': 'Systematically tensing and releasing muscles reduces stress', 'duration': '15 min'},
            {'title': 'Listen to calming music or nature sounds', 'description': 'Soothing sounds can lower blood pressure and anxiety', 'duration': '20 min'},
            {'title': 'Do a body scan meditation', 'description': 'Increases awareness of physical sensations and releases tension', 'duration': '15 min'},
            {'title': 'Practice mindful eating during a meal', 'description': 'Fully experiencing food improves digestion and satisfaction', 'duration': '20 min'},
        ]
    },
    'physical': {
        'name': 'Physical Wellness',
        'icon': 'fa-running',
        'color': '#F59E0B',
        'tasks': [
            {'title': 'Do 15 minutes of stretching exercises', 'description': 'Stretching releases physical tension and improves flexibility', 'duration': '15 min'},
            {'title': 'Take a dance break to your favorite songs', 'description': 'Dancing releases endorphins and lifts your mood', 'duration': '15 min'},
            {'title': 'Try a beginner workout video', 'description': 'Exercise is one of the most effective mood boosters', 'duration': '30 min'},
            {'title': 'Go for a bike ride', 'description': 'Cycling combines exercise with the joy of movement', 'duration': '30 min'},
            {'title': 'Do simple exercises at home', 'description': 'Even light exercise improves mental clarity', 'duration': '20 min'},
            {'title': 'Practice tai chi or gentle movement', 'description': 'Slow, mindful movement reduces stress hormones', 'duration': '20 min'},
            {'title': 'Take the stairs instead of elevator today', 'description': 'Small physical challenges build confidence', 'duration': '5 min'},
        ]
    },
    'creative': {
        'name': 'Creative Expression',
        'icon': 'fa-palette',
        'color': '#EC4899',
        'tasks': [
            {'title': 'Draw or doodle for 15 minutes', 'description': 'Art expression reduces stress without needing skill', 'duration': '15 min'},
            {'title': 'Write in a journal about your feelings', 'description': 'Journaling helps process emotions and track patterns', 'duration': '20 min'},
            {'title': 'Try a new recipe or cook something comforting', 'description': 'Cooking engages multiple senses and provides accomplishment', 'duration': '45 min'},
            {'title': 'Listen to or play music', 'description': 'Music therapy is proven to improve mood and reduce anxiety', 'duration': '30 min'},
            {'title': 'Write a poem or short story', 'description': 'Creative writing helps express and process emotions', 'duration': '30 min'},
            {'title': 'Try a craft project (origami, knitting, etc.)', 'description': 'Repetitive craft movements can be meditative', 'duration': '45 min'},
            {'title': 'Create a vision board or collage', 'description': 'Visualizing positive goals can improve motivation', 'duration': '40 min'},
        ]
    },
    'self_care': {
        'name': 'Self Care & Routine',
        'icon': 'fa-heart',
        'color': '#14B8A6',
        'tasks': [
            {'title': 'Get 7-8 hours of quality sleep tonight', 'description': 'Adequate sleep is crucial for mental health', 'duration': '8 hours'},
            {'title': 'Prepare and eat a nutritious meal', 'description': 'Good nutrition directly affects brain chemistry', 'duration': '45 min'},
            {'title': 'Organize one small area of your space', 'description': 'A tidy environment can reduce mental clutter', 'duration': '20 min'},
            {'title': 'Take a digital detox for 2 hours', 'description': 'Screen breaks reduce anxiety and improve sleep', 'duration': '2 hours'},
            {'title': 'Drink 8 glasses of water today', 'description': 'Hydration affects mood and cognitive function', 'duration': 'All day'},
            {'title': 'Set 3 small achievable goals for tomorrow', 'description': 'Goal-setting provides purpose and direction', 'duration': '10 min'},
            {'title': 'Practice saying "no" to one draining commitment', 'description': 'Setting boundaries protects your energy', 'duration': '5 min'},
        ]
    },
    'place_visit': {
        'name': 'Place to Visit',
        'icon': 'fa-map-marker-alt',
        'color': '#6366F1',
        'place_types': ['park', 'cafe', 'restaurant', 'hindu_temple', 'church', 'mosque', 'museum', 'library', 'tourist_attraction', 'spa', 'gym', 'bakery', 'book_store'],
        'descriptions': {
            'park': 'Spend time in nature to reduce stress and improve mood',
            'cafe': 'Enjoy a relaxing drink in a cozy atmosphere',
            'restaurant': 'Treat yourself to a nice meal - you deserve it',
            'hindu_temple': 'Find peace and serenity at a temple',
            'church': 'Connect with your spiritual side for inner peace',
            'mosque': 'Find tranquility and reflection at a mosque',
            'museum': 'Engage your mind with art and culture',
            'library': 'Find a quiet space for reading and reflection',
            'tourist_attraction': 'Explore something new and exciting nearby',
            'spa': 'Pamper yourself with relaxation and self-care',
            'gym': 'Get active and release endorphins through exercise',
            'bakery': 'Enjoy a sweet treat to lift your spirits',
            'book_store': 'Browse books and find new inspiration'
        }
    }
}

# Positive affirmations for each day
DAILY_AFFIRMATIONS = [
    "You are stronger than you think. Every step forward counts.",
    "Today is a new beginning. Be gentle with yourself.",
    "Your feelings are valid, and you're doing your best.",
    "Small progress is still progress. Keep going!",
    "You deserve happiness and peace. Believe in yourself.",
    "Every day brings new opportunities for joy.",
    "You have overcome challenges before. You can do it again.",
]

def load_model():
    """Load the trained model"""
    global model_data
    
    if os.path.exists(MODEL_PATH):
        with open(MODEL_PATH, 'rb') as f:
            model_data = pickle.load(f)
        print("✅ Depression model loaded successfully!")
        return True
    else:
        print(f"⚠️ Model not found at {MODEL_PATH}. Please train the model first.")
        return False

def prepare_input(data, is_student):
    """Prepare input features for prediction"""
    
    encoders = model_data.get('encoders', {})
    feature_cols = model_data.get('feature_columns', [])
    categorical_cols = model_data.get('categorical_columns', [])
    
    features = {}
    
    # Process common features
    for col in model_data.get('common_features', []):
        if col in categorical_cols:
            value = data.get(col, 'unknown')
            if col in encoders:
                try:
                    features[col + '_encoded'] = encoders[col].transform([str(value)])[0]
                except:
                    features[col + '_encoded'] = 0
            else:
                features[col + '_encoded'] = 0
        else:
            features[col] = float(data.get(col, 0))
    
    # Process user-type specific features
    if is_student:
        for col in model_data.get('student_features', []):
            features[col] = float(data.get(col, 3))
        for col in model_data.get('working_features', []):
            features[col] = 3.0  # Neutral value for non-applicable features
    else:
        for col in model_data.get('working_features', []):
            features[col] = float(data.get(col, 3))
        for col in model_data.get('student_features', []):
            features[col] = 3.0  # Neutral value
            if col == 'CGPA':
                features[col] = 7.0
    
    # Add user type encoding
    user_type = 'Student' if is_student else 'Working Professional'
    if 'Working Professional or Student' in encoders:
        try:
            features['UserType_encoded'] = encoders['Working Professional or Student'].transform([user_type])[0]
        except:
            features['UserType_encoded'] = 0
    else:
        features['UserType_encoded'] = 0
    
    # Arrange features in correct order
    feature_vector = []
    for col in feature_cols:
        feature_vector.append(features.get(col, 0))
    
    return np.array(feature_vector).reshape(1, -1)

def generate_weekly_tasks(severity_score, user_location):
    """Generate 7 days of personalized wellness tasks"""
    
    tasks = []
    categories = list(TASK_CATEGORIES.keys())
    
    for day in range(7):
        # Day 1 starts TODAY (day=0 means +0 days = today)
        day_date = datetime.utcnow() + timedelta(days=day)
        day_tasks = []
        
        # Shuffle categories for variety
        random.shuffle(categories)
        
        # Select 3-4 tasks per day from different categories
        num_tasks = 4 if severity_score > 0.6 else 3
        selected_categories = categories[:num_tasks]
        
        for i, category in enumerate(selected_categories):
            cat_data = TASK_CATEGORIES[category]
            
            if category == 'place_visit':
                # Generate place visit task
                place_type = random.choice(cat_data['place_types'])
                task = {
                    'id': f"day{day+1}_task{i+1}",
                    'day': day + 1,
                    'date': day_date.strftime('%Y-%m-%d'),
                    'dayName': day_date.strftime('%A'),
                    'category': category,
                    'categoryName': cat_data['name'],
                    'icon': cat_data['icon'],
                    'color': cat_data['color'],
                    'title': f"Visit a nearby {place_type.replace('_', ' ')}",
                    'description': cat_data['descriptions'].get(place_type, 'Explore a new place nearby'),
                    'duration': '1 hour',
                    'placeType': place_type,
                    'requiresLocation': True,
                    'isCompleted': False,
                    'completedAt': None
                }
            else:
                # Select random task from category
                selected_task = random.choice(cat_data['tasks'])
                task = {
                    'id': f"day{day+1}_task{i+1}",
                    'day': day + 1,
                    'date': day_date.strftime('%Y-%m-%d'),
                    'dayName': day_date.strftime('%A'),
                    'category': category,
                    'categoryName': cat_data['name'],
                    'icon': cat_data['icon'],
                    'color': cat_data['color'],
                    'title': selected_task['title'],
                    'description': selected_task['description'],
                    'duration': selected_task['duration'],
                    'requiresLocation': False,
                    'isCompleted': False,
                    'completedAt': None
                }
            
            day_tasks.append(task)
        
        tasks.append({
            'day': day + 1,
            'date': day_date.strftime('%Y-%m-%d'),
            'dayName': day_date.strftime('%A'),
            'affirmation': DAILY_AFFIRMATIONS[day % len(DAILY_AFFIRMATIONS)],
            'tasks': day_tasks
        })
    
    return tasks

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'service': 'Depression Prediction API',
        'model_loaded': bool(model_data)
    })

@app.route('/predict', methods=['POST'])
def predict_depression():
    """Predict depression status and generate wellness plan"""
    try:
        data = request.json
        
        if not data:
            return jsonify({'error': 'No data provided'}), 400
        
        if not model_data:
            return jsonify({'error': 'Model not loaded'}), 500
        
        # Determine if student or working professional
        is_student = data.get('isStudent', data.get('Working Professional or Student') == 'Student')
        
        # Prepare input
        input_features = prepare_input(data, is_student)
        
        # Get prediction
        model = model_data['models']['depression']
        prediction = model.predict(input_features)[0]
        probabilities = model.predict_proba(input_features)[0]
        
        # Get label from encoder
        encoders = model_data.get('encoders', {})
        depression_encoder = encoders.get('Depression')
        
        if depression_encoder:
            prediction_label = depression_encoder.inverse_transform([prediction])[0]
            # Find index for 'Yes' class
            yes_index = list(depression_encoder.classes_).index('Yes') if 'Yes' in depression_encoder.classes_ else 1
            depression_probability = float(probabilities[yes_index])
        else:
            prediction_label = 'Yes' if prediction == 1 else 'No'
            depression_probability = float(probabilities[1]) if len(probabilities) > 1 else float(prediction)
        
        is_depressed = prediction_label == 'Yes'
        
        # Generate response
        response = {
            'success': True,
            'prediction': {
                'isDepressed': is_depressed,
                'label': prediction_label,
                'confidence': float(max(probabilities)),
                'depressionProbability': depression_probability,
                'severityLevel': 'high' if depression_probability > 0.7 else ('medium' if depression_probability > 0.4 else 'low')
            },
            'message': ''
        }
        
        if is_depressed:
            # Generate wellness plan
            user_location = data.get('location', {})
            wellness_plan = generate_weekly_tasks(depression_probability, user_location)
            
            response['wellnessPlan'] = {
                'startDate': datetime.utcnow().strftime('%Y-%m-%d'),
                'endDate': (datetime.utcnow() + timedelta(days=7)).strftime('%Y-%m-%d'),
                'days': wellness_plan,
                'totalTasks': sum(len(day['tasks']) for day in wellness_plan),
                'recommendations': [
                    "Consider speaking with a mental health professional",
                    "Reach out to friends and family for support",
                    "Practice self-compassion and patience with yourself",
                    "Remember that depression is treatable and temporary",
                    "If you have thoughts of self-harm, please call a helpline immediately"
                ]
            }
            response['message'] = "Based on your responses, we recommend following our 7-day wellness plan to help improve your mental well-being."
        else:
            response['message'] = "Your responses indicate a healthy mental state. Keep up the good habits!"
            response['tips'] = [
                "Continue maintaining your healthy routines",
                "Stay connected with friends and family",
                "Practice regular exercise and mindfulness",
                "Get adequate sleep and nutrition"
            ]
        
        return jsonify(response)
        
    except Exception as e:
        print(f"Prediction error: {str(e)}")
        import traceback
        traceback.print_exc()
        return jsonify({'error': str(e)}), 500

@app.route('/task-suggestions', methods=['GET'])
def get_task_suggestions():
    """Get all available task categories and suggestions"""
    
    suggestions = {}
    for category, cat_data in TASK_CATEGORIES.items():
        if category != 'place_visit':
            suggestions[category] = {
                'name': cat_data['name'],
                'icon': cat_data['icon'],
                'color': cat_data['color'],
                'tasks': cat_data['tasks']
            }
        else:
            suggestions[category] = {
                'name': cat_data['name'],
                'icon': cat_data['icon'],
                'color': cat_data['color'],
                'placeTypes': cat_data['place_types'],
                'descriptions': cat_data['descriptions']
            }
    
    return jsonify({
        'success': True,
        'categories': suggestions,
        'affirmations': DAILY_AFFIRMATIONS
    })

@app.route('/regenerate-tasks', methods=['POST'])
def regenerate_tasks():
    """Regenerate wellness tasks for a specific day or full week"""
    try:
        data = request.json
        day = data.get('day')
        severity_score = data.get('severityScore', 0.5)
        user_location = data.get('location', {})
        
        if day:
            # Regenerate for specific day
            tasks = generate_weekly_tasks(severity_score, user_location)
            return jsonify({
                'success': True,
                'day': tasks[day - 1] if day <= len(tasks) else tasks[0]
            })
        else:
            # Regenerate full week
            tasks = generate_weekly_tasks(severity_score, user_location)
            return jsonify({
                'success': True,
                'days': tasks
            })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

if __name__ == '__main__':
    print("=" * 60)
    print("DEPRESSION PREDICTION & WELLNESS API")
    print("=" * 60)
    
    # Load model
    if load_model():
        print("\n🚀 Starting API server on port 5007...")
        app.run(host='0.0.0.0', port=5007, debug=True)
    else:
        print("\n⚠️ Please train the model first using: python train_depression_model.py")
        print("Starting server anyway for testing...")
        app.run(host='0.0.0.0', port=5007, debug=True)
