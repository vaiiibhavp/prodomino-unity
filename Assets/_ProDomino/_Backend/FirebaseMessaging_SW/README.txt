# firebase-messaging-sw.js

This file is a **Firebase Cloud Messaging (FCM) Service Worker** required to enable web push notifications for the WebGL version of the game.

---

## 🔍 What is it?

`firebase-messaging-sw.js` is a **JavaScript Service Worker** that listens for push notifications from Firebase **when the Unity WebGL game is open** or even **when the tab is in the background** (as long as the browser is running and has the service worker registered).

This file is **mandatory** for Firebase Cloud Messaging to function properly in web environments.

---

## 🚀 How does it work?

- Firebase pushes notifications to the browser via the Web Push API.
- The browser delegates this request to any active **Service Worker** registered under the same domain.
- This file intercepts the message via `onBackgroundMessage()` and tells the browser to show a system-level notification using `self.registration.showNotification(...)`.

> ⚠️ Without this file, Firebase will **fail silently** when trying to deliver web push notifications.

---

## 📌 Requirements

- Must be located at the **public root** of your WebGL app (same directory as `index.html`)  
  Example:  

Build/
index.html
firebase-messaging-sw.js


- Must be named **exactly** `firebase-messaging-sw.js`  
Firebase looks for it at the root:  
`https://yourdomain.com/firebase-messaging-sw.js`

---

## 📁 Use in Unity + WebGL

This file is **not executed by Unity**, but it is essential for allowing push messages to reach the WebGL runtime.

Unity will receive the messages via JavaScript (through `messaging.onMessage`) and pass them to a GameObject via `SendMessage`.

See also:
- `index.html`: FCM token initialization and message forwarding
- `FirebaseWebGLReceiver.cs`: Unity MonoBehaviour that handles received messages

---

## 🔐 Security Considerations

- This file should never contain sensitive logic or authentication data.
- It runs in the user's browser and can be viewed or modified by the user.

---

## ✅ Version Control

Keeping this file under source control ensures:
- Easier collaboration with frontend/backend developers.
- Consistency across deployments and environments.
- Traceability of changes over time.

---

## 🧪 Testing

To test if the service worker is correctly registered:

1. Open browser dev tools → **Application → Service Workers**
2. You should see `firebase-messaging-sw.js` listed and marked as **active**.
3. Try sending a test message via the Firebase Console or REST API.

---

## 🧠 More Information

- [Firebase Web Push Setup](https://firebase.google.com/docs/cloud-messaging/js/client)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)

