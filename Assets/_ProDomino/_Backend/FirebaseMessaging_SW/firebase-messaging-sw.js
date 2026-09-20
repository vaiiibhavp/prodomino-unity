// firebase-messaging-sw.js

// Load Firebase SDK scripts required for messaging
importScripts("https://www.gstatic.com/firebasejs/11.6.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/11.6.0/firebase-messaging-compat.js");

// Initialize Firebase App (required again inside Service Worker)
firebase.initializeApp({
  apiKey: "AIzaSyBnu-WZPckWqGTXAxezQTMHCk2yrH7_1ls",
  authDomain: "prodomino-sandbox.firebaseapp.com",
  projectId: "prodomino-sandbox",
  messagingSenderId: "716639166455",
  appId: "1:716639166455:web:77cd72c0fb6136d22018f2",
});

// Retrieve Firebase Messaging instance
const messaging = firebase.messaging();

// Handle background push messages
messaging.onBackgroundMessage(function(payload) {
  console.log('[firebase-messaging-sw.js] Background message received:', payload);

  // Extract notification content
  const notificationTitle = payload?.notification?.title || 'ProDomino';
  const notificationOptions = {
    body: payload?.notification?.body || 'You have a new message!',
    icon: '/TemplateData/Prodomino_Small_Logo.png', // Optional: Change path to your icon
    data: payload?.data || {}
  };

  // Show system notification
  self.registration.showNotification(notificationTitle, notificationOptions);
});
