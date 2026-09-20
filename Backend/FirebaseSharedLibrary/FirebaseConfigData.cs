using Newtonsoft.Json;

namespace FirebaseSharedLibrary
{
    public class FirebaseConfigData
    {
        [JsonProperty("apiKey")]
        public string apiKey;

        [JsonProperty("authDomain")]
        public string authDomain;

        [JsonProperty("databaseURL")]
        public string databaseURL;

        [JsonProperty("projectId")]
        public string projectId;

        [JsonProperty("storageBucket")]
        public string storageBucket;

        [JsonProperty("messagingSenderId")]
        public string messagingSenderId;

        [JsonProperty("appId")]
        public string appId;

        [JsonProperty("functionsRegion")]
        public string functionsRegion;

        public FirebaseConfigData(string apiKey, string authDomain, string databaseURL, string projectId,
                                  string storageBucket, string messagingSenderId, string appId, 
                                  string functionsRegion)
        {
            this.apiKey = apiKey;
            this.authDomain = authDomain;
            this.databaseURL = databaseURL;
            this.projectId = projectId;
            this.storageBucket = storageBucket;
            this.messagingSenderId = messagingSenderId;
            this.appId = appId;
            this.functionsRegion = functionsRegion;
        }
    }
}