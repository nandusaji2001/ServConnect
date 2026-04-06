# ServConnect Chatbot Feature

## Overview
An intelligent, context-aware chatbot assistant that helps users navigate the ServConnect platform and understand its features. The chatbot appears as a floating widget in the bottom-right corner of every page and provides instant guidance on how to use various platform functionalities.

## Features

### 🤖 Intelligent Response System
- **Keyword-based matching**: Understands user queries through intelligent keyword analysis
- **Contextual responses**: Provides detailed, step-by-step instructions for each feature
- **Direct navigation**: Offers clickable buttons to navigate directly to relevant pages
- **Related questions**: Suggests follow-up questions to help users explore more

### 💬 User-Friendly Interface
- **Floating widget**: Always accessible in the bottom-right corner
- **Smooth animations**: Modern, polished UI with smooth transitions
- **Responsive design**: Works perfectly on desktop, tablet, and mobile devices
- **Scrollable on all pages**: Remains visible even when scrolling
- **Quick suggestions**: Pre-defined questions for common queries

### 📚 Knowledge Base Coverage

The chatbot can help with:

1. **Service Booking**
   - How to book services
   - Tracking bookings
   - Canceling bookings
   - Rating service providers

2. **House Rentals**
   - Publishing rental properties
   - Managing rental inquiries
   - Understanding rental subscriptions
   - Editing rental listings

3. **Lost & Found (AI-Powered)**
   - Viewing AI recommendations
   - Reporting lost items
   - Reporting found items
   - Claiming items
   - Understanding AI matching accuracy

4. **Community Features**
   - Creating posts
   - Sending direct messages
   - Reporting inappropriate content
   - Community guidelines

5. **Events Management**
   - Creating events
   - Purchasing tickets
   - Verifying tickets at events
   - Managing event attendance

6. **Elder Care**
   - Setting up elder profiles
   - Guardian monitoring features
   - AI wellness predictions
   - Health tracking

7. **Mental Health Support**
   - Wellness assessments
   - AI mood analysis
   - Rewards system
   - Privacy information

8. **Gas Subscription (IoT)**
   - Automatic gas ordering
   - IoT sensor setup
   - Manual ordering
   - Subscription management

9. **Transit Updates**
   - Viewing transport schedules
   - Adding updates
   - Real-time notifications
   - Route information

10. **Shopping & Orders**
    - Browsing items
    - Cart management
    - Order tracking
    - Payment methods

11. **Complaints System**
    - Filing complaints
    - Tracking complaint status
    - Resolution timelines
    - Updating complaints

12. **Local Directory**
    - Finding local businesses
    - Business information
    - Reviews and ratings
    - Adding businesses

13. **Account Management**
    - Updating profile
    - Changing password
    - Notification preferences
    - Language settings

## Technical Implementation

### Backend Components

#### 1. ChatbotController.cs
- **Location**: `backend/Controllers/ChatbotController.cs`
- **Endpoints**:
  - `POST /api/Chatbot/query` - Process user queries
  - `GET /api/Chatbot/suggestions` - Get quick suggestions

#### 2. IChatbotService.cs
- **Location**: `backend/Services/IChatbotService.cs`
- **Interface**: Defines chatbot service contract
- **Models**: ChatbotResponse with navigation support

#### 3. ChatbotService.cs
- **Location**: `backend/Services/ChatbotService.cs`
- **Features**:
  - Knowledge base initialization
  - Keyword matching algorithm
  - Response generation
  - Related questions suggestion

### Frontend Components

#### 1. chatbot.js
- **Location**: `backend/wwwroot/js/chatbot.js`
- **Features**:
  - Widget initialization
  - Message handling
  - API communication
  - Typing indicators
  - Suggestion rendering
  - Smooth animations

#### 2. chatbot.css
- **Location**: `backend/wwwroot/css/chatbot.css`
- **Features**:
  - Modern gradient design
  - Responsive layout
  - Smooth transitions
  - Accessibility support
  - Print-friendly (hidden in print)

### Integration

The chatbot is integrated into the main layout (`_Layout.cshtml`) and appears on all pages:
- CSS loaded in `<head>`
- JavaScript loaded before closing `</body>`
- Automatically initializes on page load

## Usage

### For Users

1. **Opening the Chatbot**
   - Click the purple floating button in the bottom-right corner
   - The chatbot window will slide up with a welcome message

2. **Asking Questions**
   - Type your question in the input field
   - Press Enter or click the send button
   - The chatbot will respond with helpful information

3. **Using Quick Suggestions**
   - Click any suggestion button to ask that question
   - Suggestions update based on your conversation

4. **Navigating to Pages**
   - Click the "Go to..." button in bot responses
   - You'll be taken directly to the relevant page

5. **Closing the Chatbot**
   - Click the X button in the header
   - Or click the floating button again

### For Developers

#### Adding New Knowledge

To add new topics to the chatbot, edit `ChatbotService.cs`:

```csharp
["your_topic_key"] = new ChatbotKnowledge
{
    Keywords = new List<string> { "keyword1", "keyword2", "phrase" },
    Response = "Your detailed response with instructions...",
    NavigationUrl = "/Controller/Action",
    NavigationLabel = "Button Text",
    RelatedQuestions = new List<string>
    {
        "Related question 1?",
        "Related question 2?"
    }
}
```

#### Customizing Appearance

Edit `chatbot.css` to customize:
- Colors: Change gradient values
- Size: Adjust width/height properties
- Position: Modify bottom/right values
- Animations: Update transition properties

#### Extending Functionality

The chatbot can be extended with:
- User authentication context
- Personalized responses
- Multi-language support
- Voice input/output
- Integration with backend analytics

## Best Practices

### Writing Responses
1. **Be concise**: Use numbered steps for instructions
2. **Be helpful**: Provide context and explanations
3. **Be actionable**: Always include next steps
4. **Be friendly**: Use conversational tone

### Keywords Selection
1. **Use variations**: Include synonyms and common phrases
2. **Think like users**: Consider how they might ask
3. **Multi-word phrases**: Score higher in matching
4. **Avoid conflicts**: Ensure keywords are unique to topics

### Navigation URLs
1. **Always provide**: Help users take immediate action
2. **Use descriptive labels**: Make button text clear
3. **Test links**: Ensure URLs are correct and accessible

## Accessibility

The chatbot is designed with accessibility in mind:
- **Keyboard navigation**: Full keyboard support
- **ARIA labels**: Proper labeling for screen readers
- **Color contrast**: WCAG compliant color ratios
- **Reduced motion**: Respects user preferences
- **Focus management**: Proper focus handling

## Performance

- **Lightweight**: Minimal JavaScript footprint
- **No dependencies**: Pure vanilla JavaScript
- **Lazy loading**: Suggestions loaded on demand
- **Efficient matching**: Fast keyword algorithm
- **Cached responses**: Quick subsequent responses

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Future Enhancements

Potential improvements:
1. **AI/ML Integration**: Use GPT or similar for natural language understanding
2. **Voice Support**: Add speech-to-text and text-to-speech
3. **Multi-language**: Support multiple languages
4. **Analytics**: Track common questions and user satisfaction
5. **Personalization**: Context-aware responses based on user role
6. **Rich Media**: Support images, videos, and interactive elements
7. **Conversation History**: Save and resume conversations
8. **Feedback System**: Allow users to rate responses

## Troubleshooting

### Chatbot Not Appearing
- Check if `chatbot.css` and `chatbot.js` are loaded
- Verify service is registered in `Program.cs`
- Check browser console for errors

### Responses Not Working
- Verify API endpoint is accessible
- Check network tab for failed requests
- Ensure `IChatbotService` is properly registered

### Styling Issues
- Clear browser cache
- Check for CSS conflicts
- Verify z-index values

## Support

For issues or questions:
1. Check this documentation
2. Review the code comments
3. Test in browser developer tools
4. Contact the development team

---

**Version**: 1.0.0  
**Last Updated**: March 2026  
**Maintained By**: ServConnect Development Team
