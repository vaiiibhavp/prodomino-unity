using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Lightweight HTTP Bridge inside Unity Editor that connects the running Editor to the
    /// Antigravity Model Context Protocol (MCP) server.
    /// Exposes:
    /// - /status (Unity version, active scene, playmode state)
    /// - /menu-item (execute any Editor menu item on main thread)
    /// - /logs (retrieve recent console logs, warnings, errors)
    /// - /clear-logs (clear console buffer)
    /// - /hierarchy (list scene GameObjects and components)
    /// - /inspect (inspect specific GameObject components and RectTransform properties)
    /// - /play-mode (play, pause, stop)
    /// - /screenshot (capture game/scene view to image)
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMcpBridge
    {
        private const int DefaultPort = 8080;
        private static HttpListener listener;
        private static Thread listenerThread;
        private static volatile bool isRunning;
        private static int activePort = DefaultPort;

        // Thread-safe log buffer (keeps last 150 entries)
        [Serializable]
        public struct LogEntry
        {
            public string time;
            public string type;
            public string message;
            public string stackTrace;
        }

        private static readonly ConcurrentQueue<LogEntry> logBuffer = new ConcurrentQueue<LogEntry>();
        private static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
        private const int MaxLogCount = 150;

        private static double lastCheckTime = 0;

        static UnityMcpBridge()
        {
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += StartServer;
            EditorApplication.quitting += Stop;
            EditorApplication.update += ProcessMainThreadQueue;

            EditorApplication.delayCall += StartServer;
        }

        private static void ProcessMainThreadQueue()
        {
            if (!isRunning && EditorApplication.timeSinceStartup - lastCheckTime > 2.0)
            {
                lastCheckTime = EditorApplication.timeSinceStartup;
                StartServer();
            }

            while (mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                time = DateTime.Now.ToString("HH:mm:ss.fff"),
                type = type.ToString(),
                message = condition,
                stackTrace = (type == LogType.Error || type == LogType.Exception) ? stackTrace : string.Empty
            };

            logBuffer.Enqueue(entry);
            while (logBuffer.Count > MaxLogCount && logBuffer.TryDequeue(out _)) { }
        }

        private static void OnBeforeAssemblyReload()
        {
            Stop();
        }

        [MenuItem("ProDomino/MCP/Restart Bridge Server")]
        public static void RestartServer()
        {
            Stop();
            StartServer();
        }

        [MenuItem("ProDomino/MCP/Check Status")]
        public static void CheckStatus()
        {
            Debug.Log($"[UnityMcpBridge] Running: {isRunning} on port {activePort}");
        }

        public static void StartServer()
        {
            if (isRunning) return;

            int port = DefaultPort;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();
                    activePort = port;
                    isRunning = true;
                    break;
                }
                catch (Exception)
                {
                    listener?.Close();
                    port++;
                }
            }

            if (!isRunning)
            {
                Debug.LogWarning("[UnityMcpBridge] Could not bind HttpListener to ports 8080-8084.");
                return;
            }

            listenerThread = new Thread(ListenLoop) { IsBackground = true };
            listenerThread.Start();
            Debug.Log($"[UnityMcpBridge] Connected & listening on http://127.0.0.1:{activePort}/");
        }

        public static void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch { }
            listener = null;
        }

        private static void ListenLoop()
        {
            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var ctx = listener.GetContext();
                    Task.Run(() => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityMcpBridge] Error in listen loop: {ex.Message}");
                }
            }
        }

        private static async Task HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            resp.ContentType = "application/json; charset=utf-8";
            resp.Headers.Add("Access-Control-Allow-Origin", "*");

            string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            string body = string.Empty;
            if (req.HasEntityBody)
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                body = await reader.ReadToEndAsync();
            }

            try
            {
                string jsonResponse = "{}";

                switch (path)
                {
                    case "/status":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            var scene = SceneManager.GetActiveScene();
                            return $"{{\"status\":\"ok\",\"port\":{activePort},\"unityVersion\":\"{Application.unityVersion}\",\"project\":\"{Application.productName}\",\"activeScene\":\"{scene.name}\",\"isPlaying\":{(EditorApplication.isPlaying ? "true" : "false")},\"isPaused\":{(EditorApplication.isPaused ? "true" : "false")}}}";
                        });
                        break;

                    case "/menu-item":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            string menuPath = ExtractJsonString(body, "menuPath");
                            if (string.IsNullOrEmpty(menuPath))
                                return "{\"success\":false,\"error\":\"Missing menuPath\"}";

                            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
                            return $"{{\"success\":{(executed ? "true" : "false")},\"menuPath\":\"{EscapeJson(menuPath)}\"}}";
                        });
                        break;

                    case "/logs":
                        var logsList = new List<LogEntry>(logBuffer);
                        var sb = new StringBuilder();
                        sb.Append("{\"logs\":[");
                        for (int i = 0; i < logsList.Count; i++)
                        {
                            if (i > 0) sb.Append(",");
                            var l = logsList[i];
                            sb.Append($"{{\"time\":\"{l.time}\",\"type\":\"{l.type}\",\"message\":\"{EscapeJson(l.message)}\",\"stackTrace\":\"{EscapeJson(l.stackTrace)}\"}}");
                        }
                        sb.Append("]}");
                        jsonResponse = sb.ToString();
                        break;

                    case "/clear-logs":
                        while (logBuffer.TryDequeue(out _)) { }
                        jsonResponse = "{\"success\":true}";
                        break;

                    case "/hierarchy":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            var scene = SceneManager.GetActiveScene();
                            var roots = scene.GetRootGameObjects();
                            var sbH = new StringBuilder();
                            sbH.Append($"{{\"scene\":\"{scene.name}\",\"rootCount\":{roots.Length},\"roots\":[");
                            for (int i = 0; i < roots.Length; i++)
                            {
                                if (i > 0) sbH.Append(",");
                                var r = roots[i];
                                sbH.Append($"{{\"name\":\"{EscapeJson(r.name)}\",\"active\":{(r.activeSelf ? "true" : "false")},\"childCount\":{r.transform.childCount}}}");
                            }
                            sbH.Append("]}");
                            return sbH.ToString();
                        });
                        break;

                    case "/inspect":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            string targetName = ExtractJsonString(body, "target");
                            if (string.IsNullOrEmpty(targetName))
                                return "{\"success\":false,\"error\":\"Missing target name\"}";

                            var go = GameObject.Find(targetName);
                            if (go == null)
                                return $"{{\"success\":false,\"error\":\"GameObject '{EscapeJson(targetName)}' not found in active scene\"}}";

                            var comps = go.GetComponents<Component>();
                            var compNames = new List<string>();
                            foreach (var c in comps)
                                if (c != null) compNames.Add(c.GetType().Name);

                            string rectInfo = string.Empty;
                            if (go.GetComponent<RectTransform>() is RectTransform rt)
                            {
                                rectInfo = $",\"rect\":{{\"anchoredPosition\":[{rt.anchoredPosition.x},{rt.anchoredPosition.y}],\"sizeDelta\":[{rt.sizeDelta.x},{rt.sizeDelta.y}],\"anchorMin\":[{rt.anchorMin.x},{rt.anchorMin.y}],\"anchorMax\":[{rt.anchorMax.x},{rt.anchorMax.y}]}}";
                            }

                            return $"{{\"success\":true,\"name\":\"{EscapeJson(go.name)}\",\"active\":{(go.activeSelf ? "true" : "false")},\"components\":[\"{string.Join("\",\"", compNames)}\"]{rectInfo}}}";
                        });
                        break;

                    case "/play-mode":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            string action = ExtractJsonString(body, "action").ToLowerInvariant();
                            if (action == "play") EditorApplication.isPlaying = true;
                            else if (action == "stop") EditorApplication.isPlaying = false;
                            else if (action == "pause") EditorApplication.isPaused = !EditorApplication.isPaused;

                            return $"{{\"success\":true,\"isPlaying\":{(EditorApplication.isPlaying ? "true" : "false")},\"isPaused\":{(EditorApplication.isPaused ? "true" : "false")}}}";
                        });
                        break;

                    case "/screenshot":
                        jsonResponse = await RunOnMainThread(() =>
                        {
                            string dir = Path.Combine(Application.dataPath, "..", "Temp", "McpScreenshots");
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            string path = Path.Combine(dir, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                            ScreenCapture.CaptureScreenshot(path);
                            return $"{{\"success\":true,\"path\":\"{EscapeJson(path)}\"}}";
                        });
                        break;

                    default:
                        resp.StatusCode = 404;
                        jsonResponse = "{\"error\":\"Endpoint not found\"}";
                        break;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                resp.ContentLength64 = buffer.Length;
                await resp.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                resp.OutputStream.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    resp.StatusCode = 500;
                    byte[] errBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{EscapeJson(ex.Message)}\"}}");
                    resp.ContentLength64 = errBytes.Length;
                    await resp.OutputStream.WriteAsync(errBytes, 0, errBytes.Length);
                    resp.OutputStream.Close();
                }
                catch { }
            }
        }

        private static Task<T> RunOnMainThread<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<T>();
            mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }
    }
}
