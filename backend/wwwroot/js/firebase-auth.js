// Firebase Authentication Integration
class FirebaseAuthManager {
    constructor() {
        this.firebaseConfig = null;
        this.auth = null;
        this.initialized = false;
    }

    async initialize() {
        try {
            // Get Firebase configuration from server
            const response = await fetch('/Account/GetFirebaseConfig');
            this.firebaseConfig = await response.json();

            // Initialize Firebase
            if (!firebase.apps.length) {
                firebase.initializeApp(this.firebaseConfig);
            }
            
            this.auth = firebase.auth();
            this.initialized = true;
            
            console.log('Firebase initialized successfully');
            return true;
        } catch (error) {
            console.error('Failed to initialize Firebase:', error);
            return false;
        }
    }

    async signInWithGoogle() {
        if (!this.initialized) {
            throw new Error('Firebase not initialized');
        }

        try {
            const provider = new firebase.auth.GoogleAuthProvider();
            provider.addScope('email');
            provider.addScope('profile');
            
            const result = await this.auth.signInWithPopup(provider);
            const idToken = await result.user.getIdToken();
            
            return {
                success: true,
                user: result.user,
                idToken: idToken
            };
        } catch (error) {
            console.error('Google sign-in failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async signInWithEmailAndPassword(email, password) {
        if (!this.initialized) {
            throw new Error('Firebase not initialized');
        }

        try {
            const result = await this.auth.signInWithEmailAndPassword(email, password);
            const idToken = await result.user.getIdToken();
            
            return {
                success: true,
                user: result.user,
                idToken: idToken
            };
        } catch (error) {
            console.error('Email sign-in failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async createUserWithEmailAndPassword(email, password) {
        if (!this.initialized) {
            throw new Error('Firebase not initialized');
        }

        try {
            const result = await this.auth.createUserWithEmailAndPassword(email, password);
            const idToken = await result.user.getIdToken();
            
            return {
                success: true,
                user: result.user,
                idToken: idToken
            };
        } catch (error) {
            console.error('Email registration failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    async signOut() {
        if (!this.initialized) {
            return;
        }

        try {
            await this.auth.signOut();
            console.log('User signed out successfully');
        } catch (error) {
            console.error('Sign out failed:', error);
        }
    }

    getCurrentUser() {
        return this.auth ? this.auth.currentUser : null;
    }

    onAuthStateChanged(callback) {
        if (this.auth) {
            return this.auth.onAuthStateChanged(callback);
        }
        return null;
    }
}

// Global instance
window.firebaseAuthManager = new FirebaseAuthManager();

// Authentication helper functions
window.FirebaseAuth = {
    // Initialize Firebase
    init: async function() {
        return await window.firebaseAuthManager.initialize();
    },

    // Google Sign-In
    signInWithGoogle: async function() {
        return await window.firebaseAuthManager.signInWithGoogle();
    },

    // Email/Password Sign-In
    signInWithEmail: async function(email, password) {
        return await window.firebaseAuthManager.signInWithEmailAndPassword(email, password);
    },

    // Email/Password Registration
    registerWithEmail: async function(email, password) {
        return await window.firebaseAuthManager.createUserWithEmailAndPassword(email, password);
    },

    // Sign Out
    signOut: async function() {
        await window.firebaseAuthManager.signOut();
    },

    // Get current user
    getCurrentUser: function() {
        return window.firebaseAuthManager.getCurrentUser();
    },

    // Listen to auth state changes
    onAuthStateChanged: function(callback) {
        return window.firebaseAuthManager.onAuthStateChanged(callback);
    },

    // Login with server integration
    loginWithServer: async function(idToken, returnUrl = null) {
        try {
            const response = await fetch('/Account/FirebaseLogin', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                },
                body: JSON.stringify({
                    idToken: idToken,
                    returnUrl: returnUrl
                })
            });

            const result = await response.json();
            return result;
        } catch (error) {
            console.error('Server login failed:', error);
            return { success: false, message: 'Server communication failed' };
        }
    },

    // Register with server integration
    registerWithServer: async function(idToken, userData) {
        try {
            const response = await fetch('/Account/FirebaseRegister', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                },
                body: JSON.stringify({
                    idToken: idToken,
                    name: userData.name,
                    email: userData.email,
                    phoneNumber: userData.phoneNumber,
                    address: userData.address,
                    role: userData.role,
                    returnUrl: userData.returnUrl
                })
            });

            const result = await response.json();
            return result;
        } catch (error) {
            console.error('Server registration failed:', error);
            return { success: false, message: 'Server communication failed' };
        }
    }
};

// Auto-initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', async function() {
    await window.FirebaseAuth.init();
});