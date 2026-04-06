// ServConnect Chatbot Widget
(function() {
    'use strict';

    class Chatbot {
        constructor() {
            this.isOpen = false;
            this.messages = [];
            this.init();
        }

        init() {
            this.createChatbotHTML();
            this.attachEventListeners();
            this.addWelcomeMessage();
        }

        createChatbotHTML() {
            const chatbotHTML = `
                <div id="chatbot-container" class="chatbot-container">
                    <!-- Chatbot Toggle Button -->
                    <button id="chatbot-toggle" class="chatbot-toggle" aria-label="Open chatbot">
                        <svg class="chatbot-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                        </svg>
                        <svg class="chatbot-close-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <line x1="18" y1="6" x2="6" y2="18"></line>
                            <line x1="6" y1="6" x2="18" y2="18"></line>
                        </svg>
                        <span class="chatbot-badge">?</span>
                    </button>

                    <!-- Chatbot Window -->
                    <div id="chatbot-window" class="chatbot-window">
                        <!-- Header -->
                        <div class="chatbot-header">
                            <div class="chatbot-header-content">
                                <div class="chatbot-avatar">
                                    <svg viewBox="0 0 24 24" fill="currentColor">
                                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 3c1.66 0 3 1.34 3 3s-1.34 3-3 3-3-1.34-3-3 1.34-3 3-3zm0 14.2c-2.5 0-4.71-1.28-6-3.22.03-1.99 4-3.08 6-3.08 1.99 0 5.97 1.09 6 3.08-1.29 1.94-3.5 3.22-6 3.22z"/>
                                    </svg>
                                </div>
                                <div>
                                    <h3 class="chatbot-title">ServConnect Assistant</h3>
                                    <p class="chatbot-status">Online</p>
                                </div>
                            </div>
                            <button id="chatbot-minimize" class="chatbot-minimize-btn" aria-label="Minimize chatbot">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <line x1="5" y1="12" x2="19" y2="12"></line>
                                </svg>
                            </button>
                        </div>

                        <!-- Messages Area -->
                        <div id="chatbot-messages" class="chatbot-messages">
                            <!-- Messages will be inserted here -->
                        </div>

                        <!-- Quick Suggestions -->
                        <div id="chatbot-suggestions" class="chatbot-suggestions">
                            <!-- Suggestions will be inserted here -->
                        </div>

                        <!-- Input Area -->
                        <div class="chatbot-input-area">
                            <input 
                                type="text" 
                                id="chatbot-input" 
                                class="chatbot-input" 
                                placeholder="Ask me anything..."
                                autocomplete="off"
                            />
                            <button id="chatbot-send" class="chatbot-send-btn" aria-label="Send message">
                                <svg viewBox="0 0 24 24" fill="currentColor">
                                    <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                                </svg>
                            </button>
                        </div>
                    </div>
                </div>
            `;

            document.body.insertAdjacentHTML('beforeend', chatbotHTML);
        }

        attachEventListeners() {
            const toggleBtn = document.getElementById('chatbot-toggle');
            const minimizeBtn = document.getElementById('chatbot-minimize');
            const sendBtn = document.getElementById('chatbot-send');
            const input = document.getElementById('chatbot-input');

            toggleBtn.addEventListener('click', () => this.toggleChatbot());
            minimizeBtn.addEventListener('click', () => this.toggleChatbot());
            sendBtn.addEventListener('click', () => this.sendMessage());
            input.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') this.sendMessage();
            });
        }

        toggleChatbot() {
            this.isOpen = !this.isOpen;
            const container = document.getElementById('chatbot-container');
            const window = document.getElementById('chatbot-window');
            
            if (this.isOpen) {
                container.classList.add('chatbot-open');
                window.classList.add('chatbot-window-open');
                document.getElementById('chatbot-input').focus();
                
                // Load suggestions on first open
                if (this.messages.length === 1) {
                    this.loadQuickSuggestions();
                }
            } else {
                container.classList.remove('chatbot-open');
                window.classList.remove('chatbot-window-open');
            }
        }

        addWelcomeMessage() {
            const welcomeMsg = {
                type: 'bot',
                text: "👋 Hi! I'm your ServConnect Assistant. I can help you with:\n\n" +
                      "• Booking services\n" +
                      "• Publishing house rentals\n" +
                      "• Lost & Found AI recommendations\n" +
                      "• Community features\n" +
                      "• And much more!\n\n" +
                      "What would you like to know?"
            };
            this.messages.push(welcomeMsg);
            this.renderMessages();
        }

        async loadQuickSuggestions() {
            try {
                const response = await fetch('/api/Chatbot/suggestions');
                if (response.ok) {
                    const suggestions = await response.json();
                    this.renderSuggestions(suggestions.slice(0, 4));
                }
            } catch (error) {
                console.error('Error loading suggestions:', error);
            }
        }

        renderSuggestions(suggestions) {
            const container = document.getElementById('chatbot-suggestions');
            container.innerHTML = suggestions.map(suggestion => 
                `<button class="chatbot-suggestion-btn" data-suggestion="${this.escapeHtml(suggestion)}">
                    ${this.escapeHtml(suggestion)}
                </button>`
            ).join('');

            // Attach click handlers
            container.querySelectorAll('.chatbot-suggestion-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const suggestion = btn.getAttribute('data-suggestion');
                    document.getElementById('chatbot-input').value = suggestion;
                    this.sendMessage();
                });
            });
        }

        async sendMessage() {
            const input = document.getElementById('chatbot-input');
            const message = input.value.trim();

            if (!message) return;

            // Add user message
            this.messages.push({ type: 'user', text: message });
            this.renderMessages();
            input.value = '';

            // Show typing indicator
            this.showTypingIndicator();

            try {
                const response = await fetch('/api/Chatbot/query', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ message })
                });

                if (response.ok) {
                    const data = await response.json();
                    this.hideTypingIndicator();
                    
                    // Add bot response
                    this.messages.push({
                        type: 'bot',
                        text: data.message,
                        navigationUrl: data.navigationUrl,
                        navigationLabel: data.navigationLabel,
                        relatedQuestions: data.relatedQuestions
                    });
                    this.renderMessages();

                    // Update suggestions with related questions
                    if (data.relatedQuestions && data.relatedQuestions.length > 0) {
                        this.renderSuggestions(data.relatedQuestions);
                    }
                } else {
                    throw new Error('Failed to get response');
                }
            } catch (error) {
                this.hideTypingIndicator();
                this.messages.push({
                    type: 'bot',
                    text: "Sorry, I'm having trouble connecting. Please try again in a moment."
                });
                this.renderMessages();
            }
        }

        showTypingIndicator() {
            const messagesContainer = document.getElementById('chatbot-messages');
            const typingHTML = `
                <div class="chatbot-message bot-message typing-indicator" id="typing-indicator">
                    <div class="chatbot-message-avatar">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 3c1.66 0 3 1.34 3 3s-1.34 3-3 3-3-1.34-3-3 1.34-3 3-3zm0 14.2c-2.5 0-4.71-1.28-6-3.22.03-1.99 4-3.08 6-3.08 1.99 0 5.97 1.09 6 3.08-1.29 1.94-3.5 3.22-6 3.22z"/>
                        </svg>
                    </div>
                    <div class="chatbot-message-content">
                        <div class="typing-dots">
                            <span></span>
                            <span></span>
                            <span></span>
                        </div>
                    </div>
                </div>
            `;
            messagesContainer.insertAdjacentHTML('beforeend', typingHTML);
            this.scrollToBottom();
        }

        hideTypingIndicator() {
            const indicator = document.getElementById('typing-indicator');
            if (indicator) indicator.remove();
        }

        renderMessages() {
            const container = document.getElementById('chatbot-messages');
            container.innerHTML = this.messages.map(msg => this.createMessageHTML(msg)).join('');
            this.scrollToBottom();
        }

        createMessageHTML(message) {
            const isBot = message.type === 'bot';
            const formattedText = this.formatMessage(message.text);
            
            let html = `
                <div class="chatbot-message ${isBot ? 'bot-message' : 'user-message'}">
                    ${isBot ? `
                        <div class="chatbot-message-avatar">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 3c1.66 0 3 1.34 3 3s-1.34 3-3 3-3-1.34-3-3 1.34-3 3-3zm0 14.2c-2.5 0-4.71-1.28-6-3.22.03-1.99 4-3.08 6-3.08 1.99 0 5.97 1.09 6 3.08-1.29 1.94-3.5 3.22-6 3.22z"/>
                            </svg>
                        </div>
                    ` : ''}
                    <div class="chatbot-message-content">
                        <div class="chatbot-message-text">${formattedText}</div>
                        ${message.navigationUrl ? `
                            <a href="${message.navigationUrl}" class="chatbot-nav-btn">
                                ${this.escapeHtml(message.navigationLabel || 'Go to Page')}
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M5 12h14M12 5l7 7-7 7"/>
                                </svg>
                            </a>
                        ` : ''}
                    </div>
                </div>
            `;
            
            return html;
        }

        formatMessage(text) {
            // Convert line breaks
            text = this.escapeHtml(text);
            text = text.replace(/\n/g, '<br>');
            
            // Bold text between **
            text = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
            
            // Bullet points
            text = text.replace(/^• /gm, '<span class="bullet">•</span> ');
            
            return text;
        }

        escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        scrollToBottom() {
            const container = document.getElementById('chatbot-messages');
            setTimeout(() => {
                container.scrollTop = container.scrollHeight;
            }, 100);
        }
    }

    // Initialize chatbot when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => new Chatbot());
    } else {
        new Chatbot();
    }
})();
