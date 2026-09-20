using Newtonsoft.Json;

namespace FirebaseSharedLibrary
{
    public class FirebaseServiceAccountData
    {
        [JsonProperty("type")]
        public string type;

        [JsonProperty("project_id")]
        public string projectId;

        [JsonProperty("private_key_id")]
        public string privateKeyId;

        [JsonProperty("private_key")]
        public string privateKey;

        [JsonProperty("client_email")]
        public string clientEmail;

        [JsonProperty("client_id")]
        public string clientId;

        [JsonProperty("auth_uri")]
        public string authUri;

        [JsonProperty("token_uri")]
        public string tokenUri;

        [JsonProperty("auth_provider_x509_cert_url")]
        public string authProviderCertUrl;

        [JsonProperty("client_x509_cert_url")]
        public string clientCertUrl;

        [JsonProperty("universe_domain")]
        public string universeDomain;

        public FirebaseServiceAccountData(string type, string projectId, string privateKeyId, string privateKey,
                                          string clientEmail, string clientId, string authUri, string tokenUri,
                                          string authProviderCertUrl, string clientCertUrl, string universeDomain)
        {
            this.type = type;
            this.projectId = projectId;
            this.privateKeyId = privateKeyId;
            this.privateKey = privateKey;
            this.clientEmail = clientEmail;
            this.clientId = clientId;
            this.authUri = authUri;
            this.tokenUri = tokenUri;
            this.authProviderCertUrl = authProviderCertUrl;
            this.clientCertUrl = clientCertUrl;
            this.universeDomain = universeDomain;
        }
    }
}
