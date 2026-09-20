using Newtonsoft.Json;

namespace HelperSharedLibrary
{
    public class SecurityData
    {
        public string encryptedData;
        public string ivBase64;

        [JsonConstructor]
        public SecurityData() 
        {
            encryptedData = string.Empty;
            ivBase64 = string.Empty;
        }
        public SecurityData(string encryptedData, string ivBase64)
        {
            this.encryptedData = encryptedData;
            this.ivBase64 = ivBase64;
        }
    }
}
