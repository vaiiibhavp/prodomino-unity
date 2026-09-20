using HelperSharedLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend;

/// <summary>
/// This class is used to validate credentials for Unity Gaming Services (UGS) using Cloud Code.<br></br>
/// It fetches the blocked email domains from Remote Config and checks if the provided email domain is blocked using Rest API
/// </summary>
internal class CloudCodeCredentialsValidator : CredentialsValidator
{
    private readonly IGameApiClient _gameApiClient;
    private readonly IExecutionContext _executionContext;

    public CloudCodeCredentialsValidator(IGameApiClient gameApiClient, IExecutionContext executionContext)
    {
        _gameApiClient = gameApiClient;
        _executionContext = executionContext;
    }

    public override async Task<HashSet<string>> FetchBlockedDomainsAsync()
    {
        var response = await UGSApiHelper.GetRemoteConfig(_gameApiClient, _executionContext, UGSConfigData.blockedEmailDomainsConfig);
        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

        if (data?.TryGetValue("value", out var jsonCollectionObj) == true &&
            jsonCollectionObj is JArray jsonCollection)
        {
            var blockedDomains = jsonCollection
                .OfType<JObject>()
                .FirstOrDefault(x => x.TryGetValue("key", out var key) && key.ToString() == "blocked_email_domains")
                ?.GetValue("value") as JObject;

            var domainList = blockedDomains?["blockedDomains"] as JArray;
            return domainList != null ? [.. domainList.Select(x => x.ToString())] : new HashSet<string>();
        }

        throw new Exception("Invalid response from Remote Config.");
    }
}
