using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend;

public static class FirebaseTokenFactory
{
    private const string SecretServiceAccount = "FIREBASE_SERVICE_ACCOUNT_JSON";

    /// <summary>
    /// Creates a Firebase Custom Token signed with Service Account
    /// </summary>
    public static async Task<string> CreateCustomTokenAsync(IGameApiClient api, IExecutionContext ctx)
    {
        // Load both secrets in parallel
        var secretServiceAccount = await api.SecretManager.GetSecret(ctx, SecretServiceAccount);
        if (secretServiceAccount == null || string.IsNullOrEmpty(secretServiceAccount.Value))
            throw new Exception("Required secrets are missing or empty.");

        // Parse secrets
        var ssaParsed = JObject.Parse(secretServiceAccount.Value);

        string clientEmail = ssaParsed["client_email"]!.ToString();
        string privateKey = ssaParsed["private_key"]!.ToString();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Build header (Firebase requires RS256)
        var header = new
        {
            alg = "RS256",
            typ = "JWT"
        };

        string headerJson = JsonConvert.SerializeObject(header);
        string headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        // Build payload according to Firebase Custom Token specs
        var payloadObj = new JObject
        {
            ["iss"] = clientEmail,    // Issuer must be the service account email
            ["sub"] = clientEmail,    // Subject must be the same as issuer
            ["aud"] = "https://identitytoolkit.googleapis.com/google.identity.identitytoolkit.v1.IdentityToolkit",
            ["iat"] = now,
            ["exp"] = now + 3600,     // Max 1 hour allowed by Firebase
            ["uid"] = "cloudcode-backend",     // UID of backend user
            ["claims"] = new JObject
            {
                ["backend"] = true     // Claims are optional but valid
            }
        };

        // Serialize payload WITHOUT indentation (Firebase rejects pretty JSON)
        string payloadJson = JsonConvert.SerializeObject(payloadObj, Formatting.None);
        string payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        // Final string to sign
        string toSign = $"{headerEncoded}.{payloadEncoded}";

        // Sign the JWT
        string signature = SignWithRsaSha256(toSign, privateKey);

        // Return final custom token
        return $"{toSign}.{signature}";
    }

    /// <summary>
    /// Exchanges a custom token for an ID token
    /// </summary>
    public static async Task<string> ExchangeCustomTokenForIdTokenAsync(string customToken, string apiKey)
    {
        using var http = new System.Net.Http.HttpClient();

        // Firebase requires JSON, not form-url-encoded
        var payload = new JObject
        {
            ["token"] = customToken,
            ["returnSecureToken"] = true
        };

        var content = new System.Net.Http.StringContent(
            payload.ToString(Newtonsoft.Json.Formatting.None),
            Encoding.UTF8,
            "application/json"
        );

        var resp = await http.PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?key={apiKey}",
            content
        );

        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Custom token exchange failed: {body}");

        var parsed = JObject.Parse(body);

        return parsed["idToken"]!.ToString();
    }


    /// <summary>
    /// Builds a Realtime Database endpoint correctly, including the auth token.
    /// This prevents breaking Firestore, which uses headers.
    /// </summary>
    internal static string BuildRealtimeEndpoint(string path, string idToken)
    {
        // Separate query string if exists
        var queryIndex = path.IndexOf('?');

        string basePath;
        string queryString = string.Empty;

        if (queryIndex >= 0)
        {
            basePath = path[..queryIndex];
            queryString = path[queryIndex..]; // includes '?'
        } 
        else
            basePath = path;

        // Ensure .json suffix
        if (!basePath.EndsWith(".json"))
            basePath += ".json";

        // Build final endpoint
        if (string.IsNullOrEmpty(queryString))
            return $"{basePath}?auth={idToken}";

        return $"{basePath}{queryString}&auth={idToken}";
    }


    // Signs content with RSA SHA256 using the service account private key
    private static string SignWithRsaSha256(string data, string privateKey)
    {
        // Create RSA instance
        using var rsa = RSA.Create();

        // Import PKCS8 private key
        rsa.ImportFromPem(privateKey.ToCharArray());

        byte[] bytesToSign = Encoding.UTF8.GetBytes(data);

        // Sign with SHA256 + PKCS1 padding
        byte[] signature = rsa.SignData(bytesToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Base64UrlEncode(signature);
    }


    // Encodes bytes in Base64 URL format
    private static string Base64UrlEncode(byte[] input)
    {
        string base64 = Convert.ToBase64String(input);
        return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
