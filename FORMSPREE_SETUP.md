# Formspree Setup Instructions

To enable the contact form to send emails via Formspree, follow these steps:

## 1. Sign up for Formspree
1. Go to [https://formspree.io](https://formspree.io)
2. Click "Get Started" and create a free account
3. Verify your email address

## 2. Create a New Form
1. After logging in, click "Create Form" or "+ New Form"
2. Select the "HTML Form" option
3. Enter your email address where you want to receive submissions
4. Click "Create Form"

## 3. Get Your Form Endpoint
1. After creating the form, you'll see an endpoint URL that looks like:
   ```
   https://formspree.io/f/mXXXXXXX
   ```
2. Copy the entire endpoint URL

## 4. Update the Contact Form
1. Open `backend/Views/Home/Contact.cshtml`
2. Find the form action attribute:
   ```html
   <form action="https://formspree.io/f/YOUR_FORMSPREE_ENDPOINT" method="POST">
   ```
3. Replace `YOUR_FORMSPREE_ENDPOINT` with your actual endpoint ID (the part after `/f/`)
   For example, if your endpoint is `https://formspree.io/f/m1234567`, then use:
   ```html
   <form action="https://formspree.io/f/m1234567" method="POST">
   ```

## 5. Test the Form
1. Save the file and run your application
2. Navigate to the Contact page
3. Fill out and submit the form
4. Check your email for the submission

## Notes
- The free tier of Formspree allows 50 submissions per month
- For higher volume, consider upgrading to a paid plan
- Formspree handles spam protection automatically
- Submissions will include the sender's email in the "Reply-To" header