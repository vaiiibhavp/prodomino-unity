using Newtonsoft.Json;

namespace HelperSharedLibrary
{
    public class CloudPlayerClubData
    {
        [JsonProperty("clubName")]
        public string? clubName;

        public CloudPlayerClubData()
        {
        }
    }
}
