using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Data structure representing a player's purchases data.
    /// </summary>
    [Serializable]
    public class PlayerPurchasesData
    {
        [JsonProperty("receipts")]
        public List<PlayerPurchaseReceiptData> receipts;

        [JsonConstructor]
        public PlayerPurchasesData()
        {
            receipts = new();
        }

        /// <summary>
        /// Data structure representing a player's purchase receipt.
        /// </summary>
        [Serializable]
        public class PlayerPurchaseReceiptData
        {
            /// <summary>
            /// Purchase order or capture identifier.
            /// Used for idempotency and reconciliation.
            /// </summary>
            [JsonProperty("externalOrderId")]
            public string externalOrderId;

            /// <summary>
            /// Internal product identifier
            /// </summary>
            [JsonProperty("productId")]
            public string productId;

            /// <summary>
            /// Amount paid, as string to preserve precision.
            /// Example: "9.99"
            /// </summary>
            [JsonProperty("price")]
            public string price;

            /// <summary>
            /// ISO 4217 currency code (USD, EUR, JPY, etc.).
            /// </summary>
            [JsonProperty("currency")]
            public string currency;

            /// <summary>
            /// Indicates whether this purchase is a subscription.
            /// </summary>
            [JsonProperty("isSubscription")]
            public bool isSubscription;

            /// <summary>
            /// UTC timestamp (ISO 8601) when the payment was created.
            /// </summary>
            [JsonProperty("createdAtUtc")]
            public string createdAtUtc;

            /// <summary>
            /// UTC timestamp (ISO 8601) when the payment was confirmed.
            /// </summary>
            [JsonProperty("purchasedAtUtc")]
            public string purchasedAtUtc;

            /// <summary>
            /// Payment provider identifier.
            /// Useful if you add Stripe, Apple, Google later.
            /// </summary>
            [JsonProperty("provider")]
            public PaymentProvider provider;

            /// <summary>
            /// Current status of the purchase.
            /// </summary>
            [JsonProperty("status")]
            public PurchaseStatus status;

            [JsonConstructor]
            public PlayerPurchaseReceiptData()
            {
                externalOrderId = string.Empty;
                productId = string.Empty;
                price = "0.00";
                currency = "USD";
                isSubscription = false;
                createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); // Set to current UTC time when the model is created
                purchasedAtUtc = string.Empty; // Shouldn't be set when the model is created; must be set when the purchase is completed
                provider = PaymentProvider.PayPal; // Default to PayPal
                status = PurchaseStatus.Pending;
            }

            /// <summary>
            /// Set the created at UTC to now. This should be called when the purchase receipt is created.
            /// </summary>
            public void SetPurchasedAtUtcToNow() =>
                purchasedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            /// <summary>
            /// Get the created at UTC date time
            /// </summary>
            /// <returns></returns>
            public DateTime GetCreatedAtUtcDateTime() =>
                DateTime.Parse(
                    createdAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                );

            /// <summary>
            /// Get the purchased at UTC date time
            /// </summary>
            public DateTime GetPurchasedAtUtcDateTime() =>
                DateTime.Parse(
                    purchasedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                );

            public enum PaymentProvider
            {
                PayPal = 0,
                Stripe = 1,
                Apple = 2,
                Google = 3
            }

            public enum PurchaseStatus
            {
                Pending = 0, // Order created, waiting for external provider confirmation
                Completed = 1, // Order completed successfully
                Refunded = 2, // Order refunded
                Cancelled = 3, // Order cancelled by user
                Expired = 4 // Subscription expired
            }
        }
    }
}