using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ProDomino.Shared;
using ProDomino.Authentication;
using Timba.Patterns;
using System;
using ProDomino.GameSystem;

public class JoinCodeManager : MonoBehaviour
{
    [SerializeField] private string baseUrl = "http://localhost:3001";
    private AuthManager authManager;
    private GameManager gameManager;

    public string MatchMakingQueueName { get; private set; }
    public string JoinCode { get; private set; }

    private void Awake()
    {
        authManager = ServiceLocator.Instance.GetService<AuthManager>();
        gameManager = ServiceLocator.Instance.GetService<GameManager>();
    }

    /// <summary>
    /// Obtain from server a join joinCode according the <paramref name="matchMakingQueueName"/> defined
    /// </summary>
    /// <param name="matchMakingQueueName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> TryToObtainRelayCodeAsync(string matchMakingQueueName, CancellationToken cancellationToken)
    {
        int auxPlayerELO = gameManager?.PlayerMatchData?.elo ?? 1150;
        string url = $"{baseUrl}/getRaleycode/{matchMakingQueueName}/{auxPlayerELO}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            var operation = request.SendWebRequest();

            // Await completion or cancellation without blocking main thread
            var startTime = Time.time;
            while (!operation.isDone)
            {
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToObtainRelayCodeAsync)} was cancelled</color>");
                    return null;
                }

                // Timeout after reasonable period
                if (Time.time - startTime > 5f) // 5 seconds timeout
                {
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToObtainRelayCodeAsync)} timed out after 5 seconds.</color>");
                    return null;
                }

                await Task.Yield();
            }

            // If cancelled right after completion but before returning
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToObtainRelayCodeAsync)} was cancelled</color>");
                return null;
            }

            // Handle errors in a neat helper
            if (!IsSuccess(request))
            { 
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b> Failed to obtain relay joinCode from server for queue: {matchMakingQueueName}</color>");
                return null;
            }

            // Parse and return response
            return ParseResponse(request.downloadHandler.text);
        }

        // Local helper: check if request succeeded
        bool IsSuccess(UnityWebRequest request)
        {
            // Check if request was successful
            if (request.result == UnityWebRequest.Result.Success)
            { 
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToObtainRelayCodeAsync)} succeeded.</color>");
                return true;
            }

            // Prepare detailed error message
            var errorMessage = $"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToObtainRelayCodeAsync)} failed: <b>{request.responseCode}</b>" +
                $"\n\nQueue: {MatchMakingQueueName}" +
                $"\nError:\n{request.error}" +
                $"</color>";

            // Include server response if available
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
                errorMessage += $"\n\nDetails:\n{request.downloadHandler.text}";

            Debug.LogError(errorMessage);
            return false;
        }

        // Local helper: parse JSON response safely
        string ParseResponse(string jsonResponse)
        {
            // Prepare default response
            var response = default(RelayCodeResponse);
            try
            {
                // Deserialize JSON to object
                response = JsonUtility.FromJson<RelayCodeResponse>(jsonResponse);

                // Check if response is valid
                JoinCode = response?.relayJoinCode;
                MatchMakingQueueName = matchMakingQueueName;

                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(JoinCodeManager)}]</b> Relay joinCode obtained succesfully from server:" +
                    $"\nQueue: {MatchMakingQueueName}" +
                    $"\nJoinCode: {JoinCode}" +
                    $"</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b> Error parsing relay joinCode response: {ex.Message}</color>");
            }

            return JoinCode;
        }
    }

    /// <summary>
    /// Register into a server the join matchmaking queue name and its join joinCode
    /// </summary>
    public async Task<bool> TryToRegisterRelayCodeInServer(string matchMakingQueueName, string joinCode, CancellationToken cancellationToken)
    {
        if (authManager is null or { IsUGSAuthenticated: false })
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b> Can't register relay joinCode in server. User is not signed in.</color>");
            return false;
        }

        int auxPlayerELO = gameManager?.PlayerMatchData?.elo ?? 750; // Placeholder for future ELO integration

        var url = $"{baseUrl}/createRelayCode";
        var requestBody = new CreateCodeRequest { key = matchMakingQueueName, code = joinCode, playerElo = auxPlayerELO };
        var jsonData = JsonUtility.ToJson(requestBody);

        using var request = new UnityWebRequest(url, "POST");
        var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Cancel gracefully if requested
        using (cancellationToken.Register(() => request.Abort()))
        {
            var operation = request.SendWebRequest();

            try
            {
                var startTime = Time.time;
                while (!operation.isDone)
                {
                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToRegisterRelayCodeInServer)} was cancelled</color>");
                        return false;
                    }

                    // Timeout after reasonable period
                    if (Time.time - startTime > 20f) // 20 seconds timeout
                    {
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToRegisterRelayCodeInServer)} timed out after 10 seconds.</color>");
                        return false;
                    }

                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogRequestError(request, nameof(TryToRegisterRelayCodeInServer));
                    return false;
                }

                JoinCode = joinCode;
                MatchMakingQueueName = matchMakingQueueName;

                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(JoinCodeManager)}]</b> Relay joinCode registered successfully." +
                          $"\nQueue: {matchMakingQueueName}" +
                          $"\nJoinCode: {joinCode}" +
                          $"</color>");
                return true;
            }
            catch (Exception ex) when (ex is OperationCanceledException || request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(JoinCodeManager)}]</b> {nameof(TryToRegisterRelayCodeInServer)} was cancelled.</color>");
                return false;
            }
        }

        // Logs detailed error info for UnityWebRequest.
        void LogRequestError(UnityWebRequest request, string methodName)
        {
            var errorMessage = $"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{methodName} failed: <b>{request.responseCode}</b>" +
                               $"\n\nError:\n{request.error}</color>";

            if (!string.IsNullOrEmpty(request.downloadHandler.text))
                errorMessage += $"\n\nDetails:\n{request.downloadHandler.text}";

            Debug.LogWarning(errorMessage);
        }
    }

    /// <summary>
    /// Try to remove the joinCode registered in the server to avoid using an invalid entry.
    /// </summary>
    public async Task<bool> TryToRemoveRelayCodeAsync(string matchMakingQueueName, string joinCode, CancellationToken? cancellationToken = null)
    {
        if (authManager is null or { IsUGSAuthenticated: false })
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b> Can't remove relay code in server. User is not signed in.</color>");
            return false;
        }

        //string url = $"{baseUrl}/removeRelaycode/{matchMakingQueueName}/{joinCode}";
        string url = $"{baseUrl}/removeRelaycode/{joinCode}";

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();

            // Await completion or cancellation without blocking
            var startTime = Time.time;
            while (!operation.isDone)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToRemoveRelayCodeAsync)} was cancelled</color>");
                    return false;
                }

                // Timeout after reasonable period
                if (Time.time - startTime > 10f) // 10 seconds timeout
                {
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToRemoveRelayCodeAsync)} timed out after 10 seconds.</color>");
                    return false;
                }

                await Task.Yield();
            }

            // Cancelled right after completion but before processing
            if (cancellationToken?.IsCancellationRequested ?? false)
                return false;

            // Handle errors neatly
            if (!IsSuccess(request))
                return false;

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(JoinCodeManager)}]</b> Relay join code <b>{joinCode}</b>, was DELETE successfully</color>");
            return true;
        }

        // Local helper: check request result and log errors if any
        bool IsSuccess(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.Success)
            {
                JoinCode = null;
                MatchMakingQueueName = null;

                return true;
            }

            var errorMessage = $"<color={Consts.Colors.Error}><b>[{nameof(JoinCodeManager)}]</b>{nameof(TryToRemoveRelayCodeAsync)} failed: <b>{req.responseCode}</b>" +
                               $"\n\nError:\n{req.error}</color>";

            if (!string.IsNullOrEmpty(req.downloadHandler.text))
                errorMessage += $"\n\nDetails:\n{req.downloadHandler.text}";

            Debug.LogWarning(errorMessage);
            return false;
        }
    }

    /// <summary>
    /// Directly configure the join code
    /// </summary>
    public void ForceConfigureJoinCode(string joinCode)
    {
        JoinCode = joinCode;
    }

    [Serializable]
    public class CreateCodeRequest
    {
        public string key;
        public string code;
        public int playerElo;
    }

    // Class to map the JSON response
    [Serializable]
    public class RelayCodeResponse
    {
        public string relayJoinCode;
    }
}
