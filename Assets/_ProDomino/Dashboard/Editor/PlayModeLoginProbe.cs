using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    // Runs the game, waits for the logged-out header, then fires a real UI raycast at the centre of
    // the "Log In" button and reports what the click actually hits. Batch use:
    //   Unity -batchmode -executeMethod ProDomino.Dashboard.Editor.PlayModeLoginProbe.Run   (no -quit)
    internal static class PlayModeLoginProbe
    {
        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
        private const string Flag = "PD_LOGIN_PROBE";
        private const string StartedAt = "PD_LOGIN_PROBE_T0";
        private const string Stage = "PD_LOGIN_PROBE_STAGE";

        public static void Run()
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetFloat(StartedAt, 0f);
            SessionState.SetInt(Stage, 0);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Rehook()
        {
            if (!SessionState.GetBool(Flag, false)) return;

            // Only a probe that is mid-run (the domain reloads on the way into play mode) may
            // continue; a flag left behind by an interrupted run is dropped instead.
            if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Flag, false); return; }
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var now = (float)EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                // Still entering play mode, or the probe already finished.
                if (SessionState.GetInt(Stage, 0) >= 2) Finish();
                return;
            }

            if (SessionState.GetFloat(StartedAt, 0f) <= 0f) { SessionState.SetFloat(StartedAt, now); return; }
            float elapsed = now - SessionState.GetFloat(StartedAt, now);

            var profile = FindByName("Options_Profile");
            var notLogged = profile ? profile.Find("NotLogged_Interface") : null;
            var cg = notLogged ? notLogged.GetComponent<CanvasGroup>() : null;

            // Wait for the logged-out header AND for the boot-time loading overlay to close,
            // so the probe measures the state the player actually clicks in.
            var overlay = FindByName("HandleProcessesController");
            var overlayCg = overlay ? overlay.GetComponent<CanvasGroup>() : null;
            bool ready = cg && cg.alpha > 0.5f && (overlayCg == null || overlayCg.alpha < 0.01f);
            if (elapsed < 30f) return;                       // the boot overlay only appears after a moment
            if (!ready && elapsed < 120f) return;            // then wait for it to close

            int stage = SessionState.GetInt(Stage, 0);
            if (stage == 0)
            {
                Probe(profile, elapsed, ready);
                SessionState.SetInt(Stage, 1);
                return;
            }
            if (stage >= 1 && stage < 60) { SessionState.SetInt(Stage, stage + 1); return; }   // let the UI react
            if (stage == 60)                                 // a second after the simulated click
            {
                ReportAuthUi("after click");
                ReportAuthRects();
                var shot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pd_renders", "playmode_after_click.png");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(shot));
                ScreenCapture.CaptureScreenshot(shot);
                Debug.Log($"PROBE: screenshot requested at {shot}");
                SessionState.SetInt(Stage, 61);
                return;
            }
            if (stage >= 61 && stage < 120) { SessionState.SetInt(Stage, stage + 1); return; } // let the capture land
            if (stage == 120)
            {
                SessionState.SetInt(Stage, 2);
                EditorApplication.isPlaying = false;
            }
        }

        private static void Probe(Transform profile, float elapsed, bool ready)
        {
            Debug.Log($"PROBE: play mode reached after {elapsed:0}s, logged-out header ready={ready}, screen={Screen.width}x{Screen.height}");
            if (profile == null) { Debug.Log("PROBE: Options_Profile not found"); return; }

            foreach (var n in new[] { "NotLogged_Interface", "Logged_Interface", "Profile_ChipBg" })
            {
                var t = profile.Find(n);
                if (t == null) { Debug.Log($"PROBE: {n} missing"); continue; }
                var g = t.GetComponent<CanvasGroup>();
                Debug.Log($"PROBE: {n} activeInHierarchy={t.gameObject.activeInHierarchy} " +
                          $"cg={(g ? $"alpha={g.alpha} blocks={g.blocksRaycasts} inter={g.interactable}" : "-")}");
            }

            var login = LoginButton();
            if (login == null) { Debug.Log("PROBE: OptionsUI.openAuthInterfaceButton is null"); return; }
            var rt = (RectTransform)login.transform;
            Debug.Log($"PROBE: button {Path(login.transform)} activeInHierarchy={login.gameObject.activeInHierarchy} " +
                      $"interactable={login.interactable} rect={rt.rect.size:0} listeners={login.onClick.GetPersistentEventCount()}");

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var world = (corners[0] + corners[2]) * 0.5f;
            var canvas = login.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            Debug.Log($"PROBE: world={world:0.00} screenPoint={screen:0} canvas={canvas.name}/{canvas.renderMode} eventCamera={(cam ? cam.name : "none")}");

            var es = EventSystem.current;
            if (es == null) { Debug.Log("PROBE: no EventSystem in the scene"); return; }
            var data = new PointerEventData(es) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);

            if (hits.Count == 0) Debug.Log("PROBE: HIT nothing — the click lands on no UI element at all");
            for (int i = 0; i < hits.Count; i++)
            {
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[i].gameObject);
                Debug.Log($"PROBE: HIT[{i}] {Path(hits[i].gameObject.transform)} depth={hits[i].depth} sorting={hits[i].sortingOrder} " +
                          $"module={hits[i].module?.GetType().Name} -> clickHandler={(handler ? Path(handler.transform) : "none")}");
            }

            // The overlay that the click landed on, if it was not the button.
            if (hits.Count > 0 && !hits[0].gameObject.transform.IsChildOf(profile))
            {
                var blocker = hits[0].gameObject.transform;
                for (var t = blocker; t != null; t = t.parent)
                {
                    var g = t.GetComponent<CanvasGroup>();
                    var img = t.GetComponent<Graphic>();
                    var cv = t.GetComponent<Canvas>();
                    var brt = t as RectTransform;
                    Debug.Log($"PROBE: blocker chain {Path(t)} active={t.gameObject.activeSelf} " +
                              $"rect={(brt ? brt.rect.size.ToString("0") : "-")} " +
                              $"graphic={(img ? $"{img.GetType().Name} raycast={img.raycastTarget} color={img.color}" : "-")} " +
                              $"cg={(g ? $"alpha={g.alpha} blocks={g.blocksRaycasts}" : "-")} " +
                              $"canvas={(cv ? $"override={cv.overrideSorting} order={cv.sortingOrder}" : "-")}");
                }
            }

            ReportProcessController();
            ReportAuthUi("before click");
            if (hits.Count > 0)
            {
                // Same path the input module takes: the click runs on the first ancestor that handles it.
                var go = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
                Debug.Log($"PROBE: clicked {Path(hits[0].gameObject.transform)} -> handled by {(go ? Path(go.transform) : "nothing")}");
            }
        }

        // Why the loading/error overlay is up: its state, pending task count and internet status.
        private static void ReportProcessController()
        {
            var ctrl = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(m => m && m.GetType().Name == "HandleProcessesController" && m.gameObject.scene.IsValid());
            if (ctrl == null) { Debug.Log("PROBE: HandleProcessesController not found"); return; }

            var type = ctrl.GetType();
            const BindingFlags all = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            string Value(string name)
            {
                var f = type.GetField(name, all);
                if (f != null)
                {
                    var v = f.GetValue(ctrl);
                    if (v is System.Collections.ICollection col) return col.Count.ToString();
                    if (v is CanvasGroup cg) return $"{cg.name}(alpha={cg.alpha},blocks={cg.blocksRaycasts})";
                    return v?.ToString() ?? "null";
                }
                var p = type.GetProperty(name, all);
                return p != null ? p.GetValue(ctrl)?.ToString() ?? "null" : "?";
            }

            Debug.Log($"PROBE: controller state={Value("CurrentHandleProcessErrorType")} pendingTasks={Value("currentAsyncTasks")} " +
                      $"failedEntries={Value("failedEntries")}");
            Debug.Log($"PROBE: controller rootCanvas={Value("rootCanvas")} loadOrReconnect={Value("loadOrReconnectionCanvasGroup")} tryAgain={Value("tryAgainCanvas")}");

            var checkerField = type.GetField("internetChecker", all);
            var checker = checkerField?.GetValue(ctrl) as MonoBehaviour;
            if (checker == null) { Debug.Log("PROBE: internetChecker not assigned"); return; }
            var statusProp = checker.GetType().GetProperty("LastInternetStatus", all);
            Debug.Log($"PROBE: internetChecker={checker.name} LastInternetStatus={statusProp?.GetValue(checker)}");
        }

        private static void ReportAuthUi(string when)
        {
            var auth = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(m => m && m.GetType().Name == "AuthUI" && m.gameObject.scene.IsValid());
            if (auth == null) { Debug.Log($"PROBE: AuthUI component not present ({when})"); return; }
            var group = auth.GetComponent<CanvasGroup>() ?? auth.GetComponentInChildren<CanvasGroup>(true);
            Debug.Log($"PROBE: AuthUI ({when}) {Path(auth.transform)} activeInHierarchy={auth.gameObject.activeInHierarchy} " +
                      $"cg={(group ? $"{group.name} alpha={group.alpha} blocks={group.blocksRaycasts}" : "-")}");
        }

        // Where the auth screens actually sit on screen, and whether they are visible.
        private static void ReportAuthRects()
        {
            var auth = FindByName("AuthUI");
            if (auth == null) { Debug.Log("PROBE: AuthUI object not found"); return; }
            var cam = Camera.main;
            foreach (var name in new[] { "AuthUI", "Auth_Container", "SignIn_Container", "SignUp_Container", "Recovery_Container" })
            {
                var t = name == "AuthUI" ? auth : auth.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (t == null) { Debug.Log($"PROBE: {name} missing"); continue; }
                var rt = t as RectTransform;
                var corners = new Vector3[4];
                if (rt) rt.GetWorldCorners(corners);
                var min = rt ? RectTransformUtility.WorldToScreenPoint(cam, corners[0]) : Vector2.zero;
                var max = rt ? RectTransformUtility.WorldToScreenPoint(cam, corners[2]) : Vector2.zero;
                var g = t.GetComponent<CanvasGroup>();
                Debug.Log($"PROBE: rect {name} active={t.gameObject.activeInHierarchy} size={(rt ? rt.rect.size.ToString("0") : "-")} " +
                          $"screen=({min.x:0},{min.y:0})-({max.x:0},{max.y:0}) scale={t.lossyScale.x:0.00} " +
                          $"cg={(g ? $"alpha={g.alpha} blocks={g.blocksRaycasts}" : "-")}");
            }
        }

        private static Button LoginButton()
        {
            var options = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(m => m && m.GetType().Name == "OptionsUI" && m.gameObject.scene.IsValid());
            if (options == null) return null;
            var field = options.GetType().GetField("openAuthInterfaceButton", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(options) as Button;
        }

        private static Transform FindByName(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var go in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                var hit = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (hit) return hit;
            }
            return null;
        }

        private static string Path(Transform t)
        {
            var p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static void Finish()
        {
            Debug.Log("PROBE_DONE");
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }
    }
}
