"""
Elder Wellness Prediction API
Uses Random Forest Classifier to predict diet recommendations, diet plans, and heart risk
Based on elder health data input by guardians
"""

import os
import pickle
import pandas as pd
import numpy as np
from flask import Flask, request, jsonify
from flask_cors import CORS
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder
import warnings
warnings.filterwarnings('ignore')

app = Flask(__name__)
CORS(app)

# Global variables for models and encoders
models = {}
encoders = {}
MODEL_PATH = os.path.join(os.path.dirname(__file__), 'models')

# Feature columns used for prediction
FEATURE_COLUMNS = [
    'age', 'gender', 'bmi', 'systolic_bp', 'diastolic_bp', 
    'cholesterol', 'triglycerides', 'family_history_t2d', 'family_history_cvd',
    'sleep_hours', 'sleep_quality', 'stress_level', 'physical_activity_level'
]

# Categorical columns that need encoding
CATEGORICAL_COLUMNS = ['gender', 'sleep_quality', 'physical_activity_level']

def load_and_train_models():
    """Load dataset and train Random Forest models"""
    global models, encoders
    
    data_path = os.path.join(os.path.dirname(__file__), 'Data', 'elder_diet_ml_ready_dataset_v3.csv')
    
    if not os.path.exists(data_path):
        print(f"Dataset not found at {data_path}")
        return False
    
    print("Loading dataset...")
    df = pd.read_csv(data_path)
    print(f"Dataset loaded with {len(df)} records")
    
    # Prepare encoders for categorical variables
    for col in CATEGORICAL_COLUMNS:
        encoders[col] = LabelEncoder()
        df[col + '_encoded'] = encoders[col].fit_transform(df[col].astype(str))
    
    # Prepare feature matrix
    feature_cols = []
    for col in FEATURE_COLUMNS:
        if col in CATEGORICAL_COLUMNS:
            feature_cols.append(col + '_encoded')
        else:
            feature_cols.append(col)
    
    X = df[feature_cols].fillna(0)
    
    # Train model for diet_recommendation
    print("Training diet_recommendation model...")
    y_diet_rec = df['diet_recommendation'].fillna('Balanced whole-food diet.')
    encoders['diet_recommendation'] = LabelEncoder()
    y_diet_rec_encoded = encoders['diet_recommendation'].fit_transform(y_diet_rec)
    
    X_train, X_test, y_train, y_test = train_test_split(X, y_diet_rec_encoded, test_size=0.2, random_state=42)
    models['diet_recommendation'] = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=-1)
    models['diet_recommendation'].fit(X_train, y_train)
    print(f"Diet recommendation model accuracy: {models['diet_recommendation'].score(X_test, y_test):.2f}")
    
    # Train model for diet_plan
    print("Training diet_plan model...")
    y_diet_plan = df['diet_plan'].fillna('vegetarian')
    encoders['diet_plan'] = LabelEncoder()
    y_diet_plan_encoded = encoders['diet_plan'].fit_transform(y_diet_plan)
    
    X_train, X_test, y_train, y_test = train_test_split(X, y_diet_plan_encoded, test_size=0.2, random_state=42)
    models['diet_plan'] = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=-1)
    models['diet_plan'].fit(X_train, y_train)
    print(f"Diet plan model accuracy: {models['diet_plan'].score(X_test, y_test):.2f}")
    
    # Train model for heart_risk
    print("Training heart_risk model...")
    y_heart_risk = df['heart_risk'].fillna('moderate')
    encoders['heart_risk'] = LabelEncoder()
    y_heart_risk_encoded = encoders['heart_risk'].fit_transform(y_heart_risk)
    
    X_train, X_test, y_train, y_test = train_test_split(X, y_heart_risk_encoded, test_size=0.2, random_state=42)
    models['heart_risk'] = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=-1)
    models['heart_risk'].fit(X_train, y_train)
    print(f"Heart risk model accuracy: {models['heart_risk'].score(X_test, y_test):.2f}")
    
    # Save models
    os.makedirs(MODEL_PATH, exist_ok=True)
    with open(os.path.join(MODEL_PATH, 'wellness_models.pkl'), 'wb') as f:
        pickle.dump({'models': models, 'encoders': encoders}, f)
    print("Models saved successfully!")
    
    return True

def load_models():
    """Load pre-trained models from disk"""
    global models, encoders
    
    model_file = os.path.join(MODEL_PATH, 'wellness_models.pkl')
    if os.path.exists(model_file):
        with open(model_file, 'rb') as f:
            data = pickle.load(f)
            models = data['models']
            encoders = data['encoders']
        print("Models loaded from disk")
        return True
    return False

def prepare_input(data):
    """Prepare input data for prediction"""
    features = []
    
    for col in FEATURE_COLUMNS:
        if col in CATEGORICAL_COLUMNS:
            # Encode categorical variable
            value = data.get(col, 'unknown')
            if col in encoders:
                try:
                    encoded_value = encoders[col].transform([str(value)])[0]
                except:
                    # If unknown category, use 0
                    encoded_value = 0
                features.append(encoded_value)
            else:
                features.append(0)
        else:
            # Numeric variable
            features.append(float(data.get(col, 0)))
    
    return np.array(features).reshape(1, -1)

def get_detailed_diet_plan(diet_plan, diet_preference, food_allergies):
    """Generate detailed diet plan based on prediction and preferences"""
    
    allergies = [a.strip().lower() for a in (food_allergies or '').split(',') if a.strip()]
    
    diet_plans = {
        'vegetarian': {
            'title': 'Vegetarian Diet Plan',
            'description': 'A plant-based diet rich in nutrients, fiber, and antioxidants.',
            'breakfast': [
                'Oatmeal with fresh fruits and nuts',
                'Whole grain toast with avocado',
                'Vegetable upma with coconut chutney',
                'Idli/Dosa with sambar',
                'Poha with vegetables and peanuts'
            ],
            'lunch': [
                'Brown rice with dal and mixed vegetables',
                'Roti with paneer curry and salad',
                'Quinoa bowl with chickpeas and vegetables',
                'Vegetable biryani with raita',
                'Khichdi with vegetables and ghee'
            ],
            'dinner': [
                'Vegetable soup with whole grain bread',
                'Roti with mixed vegetable curry',
                'Moong dal khichdi with vegetables',
                'Vegetable stir-fry with tofu',
                'Light dal with rice and salad'
            ],
            'snacks': [
                'Fresh fruits', 'Roasted nuts', 'Sprouts salad',
                'Vegetable sandwich', 'Buttermilk/Lassi'
            ]
        },
        'vegan': {
            'title': 'Vegan Diet Plan',
            'description': 'A completely plant-based diet without any animal products.',
            'breakfast': [
                'Smoothie bowl with plant milk and fruits',
                'Overnight oats with almond milk',
                'Vegetable poha',
                'Fruit salad with nuts and seeds',
                'Whole grain toast with nut butter'
            ],
            'lunch': [
                'Buddha bowl with quinoa and vegetables',
                'Lentil soup with whole grain bread',
                'Vegetable curry with brown rice',
                'Chickpea salad wrap',
                'Mixed bean stew with rice'
            ],
            'dinner': [
                'Vegetable stir-fry with tofu',
                'Dal with roti and salad',
                'Vegetable soup with bread',
                'Grilled vegetables with hummus',
                'Light khichdi with vegetables'
            ],
            'snacks': [
                'Fresh fruits', 'Trail mix', 'Hummus with vegetables',
                'Roasted chickpeas', 'Coconut water'
            ]
        },
        'keto': {
            'title': 'Keto-Friendly Diet Plan',
            'description': 'A low-carb, high-fat diet suitable for blood sugar management.',
            'breakfast': [
                'Scrambled eggs with vegetables',
                'Paneer bhurji with low-carb vegetables',
                'Avocado smoothie with coconut milk',
                'Cheese omelette with spinach',
                'Greek yogurt with nuts (small portion)'
            ],
            'lunch': [
                'Grilled paneer with cauliflower rice',
                'Palak paneer with low-carb roti',
                'Egg curry with vegetables',
                'Cauliflower fried rice',
                'Zucchini noodles with paneer'
            ],
            'dinner': [
                'Paneer tikka with salad',
                'Vegetable soup with cheese',
                'Stuffed bell peppers',
                'Cauliflower mash with curry',
                'Egg bhurji with vegetables'
            ],
            'snacks': [
                'Cheese cubes', 'Nuts (almonds, walnuts)',
                'Cucumber with cream cheese', 'Boiled eggs', 'Avocado'
            ]
        },
        'high_protein': {
            'title': 'High Protein Diet Plan',
            'description': 'A protein-rich diet for muscle maintenance and energy.',
            'breakfast': [
                'Egg white omelette with vegetables',
                'Paneer paratha with curd',
                'Protein smoothie with milk and nuts',
                'Sprouts chaat with lemon',
                'Moong dal chilla'
            ],
            'lunch': [
                'Dal with brown rice and vegetables',
                'Paneer curry with roti',
                'Rajma/Chole with rice',
                'Soya chunks curry with rice',
                'Egg curry with roti'
            ],
            'dinner': [
                'Grilled paneer with vegetables',
                'Dal soup with whole grain bread',
                'Tofu stir-fry with vegetables',
                'Egg bhurji with roti',
                'Lentil soup with salad'
            ],
            'snacks': [
                'Roasted chana', 'Paneer cubes', 'Boiled eggs',
                'Protein shake', 'Sprouts salad'
            ]
        }
    }
    
    # Default to vegetarian if plan not found
    plan = diet_plans.get(diet_plan, diet_plans['vegetarian'])
    
    # Filter out items containing allergens (basic filtering)
    if allergies:
        for meal_type in ['breakfast', 'lunch', 'dinner', 'snacks']:
            plan[meal_type] = [
                item for item in plan[meal_type]
                if not any(allergen in item.lower() for allergen in allergies)
            ]
    
    return plan

def get_heart_risk_recommendations(risk_level, bmi, systolic_bp, cholesterol):
    """Generate detailed recommendations based on heart risk level"""
    
    recommendations = {
        'low': {
            'risk_description': 'Your cardiovascular risk is LOW. Keep up the good work!',
            'exercises': [
                {'name': 'Brisk Walking', 'duration': '30-45 minutes', 'frequency': '5 days/week', 'description': 'Maintain your heart health with regular walks in the morning or evening.'},
                {'name': 'Light Yoga', 'duration': '20-30 minutes', 'frequency': '3-4 days/week', 'description': 'Gentle yoga poses help maintain flexibility and reduce stress.'},
                {'name': 'Swimming', 'duration': '30 minutes', 'frequency': '2-3 days/week', 'description': 'Low-impact exercise excellent for overall cardiovascular health.'},
                {'name': 'Cycling', 'duration': '20-30 minutes', 'frequency': '3 days/week', 'description': 'Stationary or outdoor cycling for heart health maintenance.'},
                {'name': 'Stretching', 'duration': '10-15 minutes', 'frequency': 'Daily', 'description': 'Daily stretching keeps muscles flexible and joints healthy.'}
            ],
            'dietary_tips': [
                'Continue eating a balanced diet rich in fruits and vegetables',
                'Maintain adequate water intake (8-10 glasses daily)',
                'Include omega-3 rich foods like walnuts and flaxseeds',
                'Limit processed foods and added sugars',
                'Keep salt intake moderate'
            ],
            'lifestyle_tips': [
                'Maintain regular sleep schedule (7-8 hours)',
                'Continue stress management practices',
                'Regular health check-ups every 6 months',
                'Stay socially active and engaged'
            ]
        },
        'moderate': {
            'risk_description': 'Your cardiovascular risk is MODERATE. Some lifestyle modifications are recommended.',
            'exercises': [
                {'name': 'Walking', 'duration': '30 minutes', 'frequency': 'Daily', 'description': 'Start with moderate pace walking, gradually increase intensity.'},
                {'name': 'Chair Exercises', 'duration': '15-20 minutes', 'frequency': 'Daily', 'description': 'Seated exercises for strength and flexibility without strain.'},
                {'name': 'Tai Chi', 'duration': '20-30 minutes', 'frequency': '3-4 days/week', 'description': 'Gentle movements that improve balance and reduce stress.'},
                {'name': 'Light Resistance Training', 'duration': '15-20 minutes', 'frequency': '2-3 days/week', 'description': 'Using light weights or resistance bands for muscle strength.'},
                {'name': 'Deep Breathing Exercises', 'duration': '10 minutes', 'frequency': 'Twice daily', 'description': 'Pranayama or deep breathing to manage blood pressure.'}
            ],
            'dietary_tips': [
                'Reduce sodium intake to less than 2000mg daily',
                'Increase fiber intake through whole grains and vegetables',
                'Choose lean proteins and plant-based options',
                'Limit saturated fats and avoid trans fats',
                'Include heart-healthy foods like oats, nuts, and olive oil',
                'Reduce sugar and refined carbohydrate intake'
            ],
            'lifestyle_tips': [
                'Monitor blood pressure regularly',
                'Aim for 7-8 hours of quality sleep',
                'Practice stress reduction techniques daily',
                'Avoid smoking and limit alcohol',
                'Schedule regular check-ups every 3-4 months'
            ]
        },
        'high': {
            'risk_description': 'Your cardiovascular risk is HIGH. Please consult a healthcare provider and follow these recommendations carefully.',
            'exercises': [
                {'name': 'Gentle Walking', 'duration': '15-20 minutes', 'frequency': 'Daily', 'description': 'Start slow, walk on flat surfaces. Stop if you feel dizzy or short of breath.'},
                {'name': 'Seated Exercises', 'duration': '10-15 minutes', 'frequency': 'Daily', 'description': 'Chair-based exercises to maintain mobility without overexertion.'},
                {'name': 'Breathing Exercises', 'duration': '10-15 minutes', 'frequency': '3 times daily', 'description': 'Deep breathing and relaxation techniques to manage stress and blood pressure.'},
                {'name': 'Gentle Stretching', 'duration': '10 minutes', 'frequency': 'Daily', 'description': 'Light stretches while seated or standing with support.'},
                {'name': 'Supervised Exercise', 'duration': 'As advised', 'frequency': 'As advised', 'description': 'Consider cardiac rehabilitation program under medical supervision.'}
            ],
            'dietary_tips': [
                'STRICTLY limit sodium to less than 1500mg daily',
                'Follow a DASH or Mediterranean diet pattern',
                'Avoid all processed and packaged foods',
                'Eliminate fried foods and saturated fats',
                'Increase potassium-rich foods (bananas, spinach, sweet potatoes)',
                'Eat small, frequent meals instead of large ones',
                'Include garlic, turmeric, and ginger in cooking',
                'Drink plenty of water, avoid sugary beverages'
            ],
            'lifestyle_tips': [
                'Monitor blood pressure twice daily',
                'Take all prescribed medications on time',
                'Immediate medical attention for chest pain or shortness of breath',
                'Complete bed rest if advised by doctor',
                'Regular follow-ups with cardiologist',
                'Keep emergency contacts readily available',
                'Avoid strenuous activities and heavy lifting'
            ]
        }
    }
    
    result = recommendations.get(risk_level, recommendations['moderate'])
    
    # Add specific warnings based on metrics
    warnings = []
    if bmi and bmi > 30:
        warnings.append('Your BMI indicates obesity. Weight management is crucial for heart health.')
    if systolic_bp and systolic_bp > 140:
        warnings.append('Your blood pressure is elevated. Monitor regularly and consult your doctor.')
    if cholesterol and cholesterol > 240:
        warnings.append('Your cholesterol level is high. Dietary changes and medication may be needed.')
    
    result['warnings'] = warnings
    
    return result

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({'status': 'healthy', 'models_loaded': len(models) > 0})

@app.route('/predict', methods=['POST'])
def predict():
    """Main prediction endpoint"""
    try:
        data = request.json
        
        if not data:
            return jsonify({'error': 'No data provided'}), 400
        
        # Prepare input features
        input_features = prepare_input(data)
        
        # Make predictions
        diet_rec_pred = models['diet_recommendation'].predict(input_features)[0]
        diet_plan_pred = models['diet_plan'].predict(input_features)[0]
        heart_risk_pred = models['heart_risk'].predict(input_features)[0]
        
        # Decode predictions
        diet_recommendation = encoders['diet_recommendation'].inverse_transform([diet_rec_pred])[0]
        diet_plan = encoders['diet_plan'].inverse_transform([diet_plan_pred])[0]
        heart_risk = encoders['heart_risk'].inverse_transform([heart_risk_pred])[0]
        
        # Get detailed recommendations
        diet_preference = data.get('diet_preference', 'vegetarian')
        food_allergies = data.get('food_allergies', '')
        
        # Use predicted diet plan or user preference
        final_diet_plan = diet_preference if diet_preference else diet_plan
        
        detailed_diet = get_detailed_diet_plan(final_diet_plan, diet_preference, food_allergies)
        heart_recommendations = get_heart_risk_recommendations(
            heart_risk,
            data.get('bmi'),
            data.get('systolic_bp'),
            data.get('cholesterol')
        )
        
        response = {
            'success': True,
            'predictions': {
                'diet_recommendation': diet_recommendation,
                'diet_plan': diet_plan,
                'heart_risk': heart_risk
            },
            'detailed_diet_plan': detailed_diet,
            'heart_risk_details': heart_recommendations,
            'input_summary': {
                'age': data.get('age'),
                'bmi': data.get('bmi'),
                'blood_pressure': f"{data.get('systolic_bp', 'N/A')}/{data.get('diastolic_bp', 'N/A')}",
                'cholesterol': data.get('cholesterol'),
                'physical_activity': data.get('physical_activity_level')
            }
        }
        
        return jsonify(response)
        
    except Exception as e:
        print(f"Prediction error: {str(e)}")
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/train', methods=['POST'])
def train():
    """Endpoint to retrain models"""
    try:
        success = load_and_train_models()
        if success:
            return jsonify({'message': 'Models trained successfully', 'success': True})
        else:
            return jsonify({'error': 'Failed to train models', 'success': False}), 500
    except Exception as e:
        return jsonify({'error': str(e), 'success': False}), 500

if __name__ == '__main__':
    # Try to load existing models, otherwise train new ones
    if not load_models():
        print("No existing models found. Training new models...")
        load_and_train_models()
    
    port = int(os.environ.get('WELLNESS_API_PORT', 5002))
    print(f"Starting Elder Wellness API on port {port}")
    app.run(host='0.0.0.0', port=port, debug=False)
