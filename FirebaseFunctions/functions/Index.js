// --------------------------------------------------
// Firebase Functions v2 - PayPal Integration
// Node.js 22 compatible
// --------------------------------------------------

const { onRequest } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");

const fetch = require("node-fetch");
const admin = require("firebase-admin");

// --------------------------------------------------
// PurchaseStatus enum (must match Unity enum)
// --------------------------------------------------
const PurchaseStatus = Object.freeze({
  Pending: 0,
  Completed: 1,
  Refunded: 2,
  Cancelled: 3,
  Expired: 4,
});

// --------------------------------------------------
// Firebase Admin initialization
// --------------------------------------------------
if (!admin.apps.length) {
  admin.initializeApp();
}

// --------------------------------------------------
// Secrets (configured via Firebase CLI)
// --------------------------------------------------
const PAYPAL_CLIENT = defineSecret("PAYPAL_CLIENT");
const PAYPAL_SECRET = defineSecret("PAYPAL_SECRET");
const PAYPAL_WEBHOOK_ID = defineSecret("PAYPAL_WEBHOOK_ID");

// --------------------------------------------------
// Helpers
// --------------------------------------------------
function getPayPalBaseUrl(environment) {
  return environment === "sandbox"
    ? "https://api-m.sandbox.paypal.com"
    : "https://api-m.paypal.com";
}

function getPayPalAuthHeader(client, secret) {
  const token = Buffer.from(`${client}:${secret}`).toString("base64");
  return `Basic ${token}`;
}

async function getPayPalAccessToken(baseUrl, client, secret) {
  const response = await fetch(`${baseUrl}/v1/oauth2/token`, {
    method: "POST",
    headers: {
      Authorization: getPayPalAuthHeader(client, secret),
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body: "grant_type=client_credentials",
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(`Failed to get PayPal access token: ${error}`);
  }

  const data = await response.json();
  return data.access_token;
}

// --------------------------------------------------
// Firebase Deleting Helpers
// --------------------------------------------------
async function removeUserFromClubs(firebaseUid) {
    const db = admin.firestore();

  const clubsRef = db.collection("clubs");
  const clubChatsRef = db.collection("clubChats");

  const clubsSnapshot = await clubsRef.get();
  const batch = db.batch();

  const clubsToDelete = [];

  clubsSnapshot.forEach(clubDoc => {
    const members = clubDoc.data().members;
    if (!members) return;

    let changed = false;
    let newMembers = { ...members };

    for (const key of Object.keys(members)) {
      if (members[key].firebaseMemberId === firebaseUid) {
        delete newMembers[key];
        changed = true;
      }
    }

    if (!changed) return;
    
    if (Object.keys(newMembers).length === 0) {
      clubsToDelete.push(clubDoc.id);
    } else {
      batch.update(clubDoc.ref, { members: newMembers });
    }
  });

  if (batch._ops?.length) {
    await batch.commit();
  }

  // IMPORTANT PART
  for (const clubId of clubsToDelete) {
    // Delete club document
    await clubsRef.doc(clubId).delete();

    // Delete clubChats document + ALL subcollections
    await db.recursiveDelete(
      clubChatsRef.doc(clubId)
    );
  }
}

async function removeUserFromRTDB(unityPlayerId) {
  if (!unityPlayerId) {
    throw new Error("unityPlayerId is required for RTDB delete");
  }

  const rtdb = admin.database();
  await rtdb.ref(`users/${unityPlayerId}`).remove();
}


async function removeUserFromAuth(uid) {
  await admin.auth().deleteUser(uid);
}

async function removePlayerFromAllLeaderboards(unityPlayerId, nationality) {

  if (!unityPlayerId) {
    throw new Error("unityPlayerId is required");
  }

  if (!nationality) {
    throw new Error("nationality is required");
  }

  const rtdb = admin.database();
  const leaderboardsRef = rtdb.ref("leaderboards");

  const snapshot = await leaderboardsRef.get();

  if (!snapshot.exists()) {
    return;
  }

  const updates = {};

  // Iterate over each leaderboard (Block1v1, French1v1, etc)
  snapshot.forEach(leaderboardSnap => {

    const leaderboardId = leaderboardSnap.key;

    // Build exact path using known nationality
    const path = `leaderboards/${leaderboardId}/${nationality}/${unityPlayerId}`;

    // Setting null removes the node in RTDB
    updates[path] = null;
  });

  // Atomic multi-location delete
  await rtdb.ref().update(updates);
}


// --------------------------------------------------
// Internal helper: Capture PayPal Order
// --------------------------------------------------
async function captureOrderInternal(externalOrderId, baseUrl, secrets) {
  const accessToken = await getPayPalAccessToken(
    baseUrl,
    secrets.clientId,
    secrets.secret
  );

  const response = await fetch(
    `${baseUrl}/v2/checkout/orders/${externalOrderId}/capture`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
    }
  );

  const data = await response.json();

  if (!response.ok) {
    throw new Error(
      `PayPal capture failed for ${externalOrderId}: ${JSON.stringify(data)}`
    );
  }

  return data;
}


// --------------------------------------------------
// PayPal webhook signature validation
// --------------------------------------------------
async function validatePayPalSignature(req, baseUrl, secrets) {
  const payload = {
    transmission_id: req.headers["paypal-transmission-id"],
    transmission_time: req.headers["paypal-transmission-time"],
    cert_url: req.headers["paypal-cert-url"],
    auth_algo: req.headers["paypal-auth-algo"],
    transmission_sig: req.headers["paypal-transmission-sig"],
    webhook_id: secrets.webhookId,
    webhook_event: req.body,
  };

  const response = await fetch(
    `${baseUrl}/v1/notifications/verify-webhook-signature`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: getPayPalAuthHeader(
          secrets.clientId,
          secrets.secret
        ),
      },
      body: JSON.stringify(payload),
    }
  );

  if (!response.ok) {
    const error = await response.text();
    throw new Error(`Webhook signature verification failed: ${error}`);
  }

  const result = await response.json();
  return result.verification_status === "SUCCESS";
}


// --------------------------------------------------
// Firebase Function: CreatePurchaseOrder
// Called by Unity Cloud Code (RequestPurchaseModule)
// --------------------------------------------------
exports.CreatePurchaseOrder = onRequest(
  {
    secrets: [PAYPAL_CLIENT, PAYPAL_SECRET],
  },
  async (req, res) => {

    if (req.method !== "POST") {
      return res.status(405).send("Method Not Allowed");
    }
      // === Token verification
      const authHeader = req.headers.authorization;

      if (!authHeader?.startsWith("Bearer ")) {
        return res.status(401).send("Missing Authorization header");
      }

      const idToken = authHeader.replace("Bearer ", "");

      let decodedToken;
      try {
        decodedToken = await admin.auth().verifyIdToken(idToken);
      } catch (err) {
        console.error("Invalid ID token", err);
        return res.status(403).send("Invalid token");
      }

      // Validate custom backend claim
      if (decodedToken.backend !== true) {
        return res.status(403).send("Caller is not backend");
      }

      // Optional: extra safety check
      if (decodedToken.uid !== "cloudcode-backend") {
        return res.status(403).send("Unexpected UID");
      }
      // === Token verification

    try {
      const {
        productId,
        price,
        currency,
        environment,
        customId, // PlayerId
      } = req.body;

      if (!productId || !price || !currency || !environment || !customId) {
        return res.status(400).send("Invalid request payload");
      }

      // Defensive price validation
      if (!/^\d+(\.\d{1,2})?$/.test(price)) {
        return res.status(400).send("Invalid price format");
      }

      const baseUrl = getPayPalBaseUrl(environment);
      const clientId = PAYPAL_CLIENT.value();
      const secret = PAYPAL_SECRET.value();

      const accessToken = await getPayPalAccessToken(
        baseUrl,
        clientId,
        secret
      );

      const orderResponse = await fetch(
        `${baseUrl}/v2/checkout/orders`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${accessToken}`,
          },
          body: JSON.stringify({
            intent: "CAPTURE",
            purchase_units: [
              {
                reference_id: productId,
                custom_id: customId,
                amount: {
                  currency_code: currency,
                  value: price,
                },
              },
            ],
            application_context: {
                return_url: "https://playprodomino.com/paypal/return.html",
                cancel_url: "https://playprodomino.com/paypal/cancel.html",
                user_action: "PAY_NOW",
                shipping_preference: "NO_SHIPPING",
                brand_name: "ProDomino"
            }
          }),
        }
      );

      if (!orderResponse.ok) {
        const error = await orderResponse.text();
        throw new Error(`PayPal order creation failed: ${error}`);
      }

      const orderData = await orderResponse.json();
      const approvalLink = orderData.links?.find(
        (link) => link.rel === "approve"
      );

      if (!approvalLink) {
        throw new Error("Approval URL not found in PayPal response");
      }

      return res.status(200).json({
        externalOrderId: orderData.id,
        approvalUrl: approvalLink.href,
      });
    } catch (error) {
      console.error("CreatePurchaseOrder error:", error);
      return res.status(500).send("Internal server error");
    }
  }
);

// --------------------------------------------------
// Firebase Function: PayPal Webhook
// --------------------------------------------------
exports.paypalWebhook = onRequest(
  {
    secrets: [
      PAYPAL_CLIENT,
      PAYPAL_SECRET,
      PAYPAL_WEBHOOK_ID,
    ],
  },
  async (req, res) => {
    if (req.method !== "POST") {
      return res.status(405).send("Method Not Allowed");
    }

    try {
      const secrets = {
        clientId: PAYPAL_CLIENT.value(),
        secret: PAYPAL_SECRET.value(),
        webhookId: PAYPAL_WEBHOOK_ID.value(),
      };

      // Sandbox vs live is encoded in cert URL
      const baseUrl = req.headers["paypal-cert-url"]?.includes("sandbox")
        ? "https://api-m.sandbox.paypal.com"
        : "https://api-m.paypal.com";

      // --------------------------------------------------
      // 1. Validate PayPal signature
      // --------------------------------------------------
      const isValid = await validatePayPalSignature(
        req,
        baseUrl,
        secrets
      );

      if (!isValid) {
        console.error("Invalid PayPal webhook signature");
        return res.status(400).send("Invalid signature");
      }

      // --------------------------------------------------
      // 2. Extract event data
      // --------------------------------------------------
      const eventType = req.body.event_type;
      const resource = req.body.resource;

      const externalOrderId =
        resource?.supplementary_data?.related_ids?.order_id ||
        resource?.id;

      const playerId =
        resource?.custom_id ||
        resource?.purchase_units?.[0]?.custom_id;

      if (!externalOrderId || !playerId) {
        console.warn("Missing externalOrderId or playerId");
        return res.status(200).send("Ignored");
      }

      // --------------------------------------------------
      // 3. RTDB reference
      // --------------------------------------------------
      const purchaseRef = admin
        .database()
        .ref(`purchases/${playerId}/${externalOrderId}`);

      const snapshot = await purchaseRef.get();
      const existing = snapshot.exists() ? snapshot.val() : null;

      // --------------------------------------------------
      // 4. Idempotency guard
      // --------------------------------------------------
      if (existing?.status === PurchaseStatus.Completed) {
        console.log("Already completed. Ignoring duplicate webhook.");
        return res.status(200).send("OK");
      }

      // --------------------------------------------------
      // 5. Map PayPal event to internal state
      // --------------------------------------------------
      let newStatus = null;

      switch (eventType) {
        case "PAYMENT.CAPTURE.COMPLETED":
          newStatus = PurchaseStatus.Completed;
          break;

        case "PAYMENT.CAPTURE.DENIED":
          newStatus = PurchaseStatus.Cancelled;
          break;

        case "PAYMENT.CAPTURE.REFUNDED":
          newStatus = PurchaseStatus.Refunded;
          break;

        case "CHECKOUT.ORDER.APPROVED": {
          if (!existing) {
            console.warn("Approved order but receipt does not exist");
            return res.status(200).send("Ignored");
          }

          if (existing.status !== PurchaseStatus.Pending) {
            console.log("Order already processed:", existing.status);
            return res.status(200).send("OK");
          }

          console.log(`Webhook capturing approved order ${externalOrderId}`);

          await captureOrderInternal(
            externalOrderId,
            baseUrl,
            secrets
            );

          return res.status(200).send("OK");
        }

        case "CHECKOUT.ORDER.COMPLETED":
            // Informational only. Capture is driven by CHECKOUT.ORDER.APPROVED
            console.log(
                "Checkout order completed (informational):",
                externalOrderId
            );
            return res.status(200).send("OK");

        default:
          console.log("Unhandled PayPal event:", eventType);
          return res.status(200).send("Ignored");
      }

      // --------------------------------------------------
      // 6. Patch payload
      // --------------------------------------------------
      const patchData = {
        status: newStatus,
        purchasedAtUtc:
          newStatus === PurchaseStatus.Completed
            ? new Date().toISOString()
            : existing?.purchasedAtUtc ?? null,
        rawProviderPayload: req.body,
      };

      // --------------------------------------------------
      // 7. Auto-healing receipt creation
      // --------------------------------------------------
      if (!existing) {
        console.warn("Missing receipt. Creating emergency record.");

        await purchaseRef.set({
          externalOrderId,
          playerId,
          status: newStatus,
          provider: "PayPal",
          createdAtUtc: new Date().toISOString(),
          purchasedAtUtc: patchData.purchasedAtUtc,
          rawProviderPayload: req.body,
        });
      } else {
        await purchaseRef.update(patchData);
      }

      console.log(
        `Purchase ${externalOrderId} for player ${playerId} → ${newStatus}`
      );

      return res.status(200).send("OK");
    } catch (error) {
      console.error("paypalWebhook fatal error:", error);
      return res.status(500).send("Internal error");
    }
  }
);

// --------------------------------------------------
// Firebase Function: Capture PayPal Order
// --------------------------------------------------
exports.CapturePurchaseOrder = onRequest(
  {
    secrets: [PAYPAL_CLIENT, PAYPAL_SECRET],
  },
  async (req, res) => {
    if (req.method !== "POST") {
      return res.status(405).send("Method Not Allowed");
    }

    try {
      const { externalOrderId, environment } = req.body;

      if (!externalOrderId || !environment) {
        return res.status(400).send("Invalid request payload");
      }

      const baseUrl = getPayPalBaseUrl(environment);
      const clientId = PAYPAL_CLIENT.value();
      const secret = PAYPAL_SECRET.value();

      // 1. Get PayPal access token
      const accessToken = await getPayPalAccessToken(
        baseUrl,
        clientId,
        secret
      );

      // 2. Capture the order
      const captureResponse = await fetch(
        `${baseUrl}/v2/checkout/orders/${externalOrderId}/capture`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${accessToken}`,
          },
        }
      );

      const captureData = await captureResponse.json();

      if (!captureResponse.ok) {
        console.error(
          "PayPal capture failed:",
          JSON.stringify(captureData)
        );
        return res.status(500).json(captureData);
      }

      // 3. Return capture result (webhook will finalize state)
      return res.status(200).json(captureData);
    } catch (error) {
      console.error("CapturePurchaseOrder error:", error);
      return res.status(500).send("Internal server error");
    }
  }
);

// --------------------------------------------------
// Firebase Function: Delete Firebase Account (ORCHESTRATOR)
// --------------------------------------------------
exports.deleteFirebaseAccount = onRequest(async (req, res) => {

  // ----------- security -----------
  if (req.method !== "POST") {
    return res.status(405).send("Method Not Allowed");
  }

  // === Token verification ===
  const authHeader = req.headers.authorization;

  if (!authHeader?.startsWith("Bearer ")) {
    return res.status(401).send("Missing Authorization header");
  }

  const idToken = authHeader.replace("Bearer ", "");

  let decodedToken;
  try {
    decodedToken = await admin.auth().verifyIdToken(idToken);
  } catch (err) {
    console.error("Invalid ID token", err);
    return res.status(403).send("Invalid token");
  }

  // Validate backend caller
  if (decodedToken.backend !== true) {
    return res.status(403).send("Caller is not backend");
  }
  // === Token verification ===

  // ----------- payload validation -----------
  const { firebaseUid, unityPlayerId, nationality } = req.body;

  if (!firebaseUid || !unityPlayerId || !nationality) {
    return res.status(400).json({
      success: false,
      retryable: false,
      error: "Missing firebaseUid, unityPlayerId or nationality"
    });
  }

  // ----------- execution -----------
  try {
    // 1. Firestore (clubs use Firebase UID)
    await removeUserFromClubs(firebaseUid);

  } catch (err) {
    console.error("removeUserFromClubs failed", err);
    return res.status(500).json({
      success: false,
      retryable: true,
      step: "removeUserFromClubs",
      error: err.message
    });
  }

  try {
    // 2. Realtime Database (uses Unity Player ID)
    await removeUserFromRTDB(unityPlayerId);

  } catch (err) {
    console.error("removeUserFromRTDB failed", err);
    return res.status(500).json({
      success: false,
      retryable: true,
      step: "removeUserFromRTDB",
      error: err.message
    });
  }

  try {
    // 3. Realtime Database leaderboard (uses  Unity Player ID and Nationality)
    await removePlayerFromAllLeaderboards(unityPlayerId, nationality)
  
  } catch (err) {
    console.error("removePlayerFromAllLeaderboards failed", err);
    return res.status(500).json({
      success: false,
      retryable: true,
      step: "removePlayerFromAllLeaderboards",
      error: err.message
    });
  }

  try {
    // 4. Firebase Auth (ALWAYS LAST, uses Firebase UID)
    await removeUserFromAuth(firebaseUid);

  } catch (err) {
    console.error("removeUserFromAuth failed", err);
    return res.status(500).json({
      success: false,
      retryable: false,
      step: "removeUserFromAuth",
      error: err.message
    });
  }

  // ----------- success -----------
  return res.status(200).json({
    success: true,
    retryable: false,
    firebaseUid,
    unityPlayerId
  });
});
