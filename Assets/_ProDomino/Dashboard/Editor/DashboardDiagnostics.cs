using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProDomino.Dashboard.Editor
{
    // Read-only checks used while wiring the dashboard.
    internal static class DashboardDiagnostics
    {
        public static void LogLeaderboardPanels()
        {
            foreach (var path in new[]
                     {
                         "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab",
                         "Assets/_tests/Temporal/ProDomino_MainCanvas_Andres10_11_25.prefab",
                     })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { LogPanels(path, root.transform); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                LogPanels("scene", go.transform);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType().Name != "LeaderboardManager") continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Component c
                            && c.GetType().Name.StartsWith("LeaderboardUI"))
                            Debug.Log($"DIAG: LeaderboardManager.{it.propertyPath} -> {c.GetType().Name} on '{c.gameObject.name}'");
                }
            }
            Debug.Log("DIAG_DONE");
        }

        // Lists every script field in the game scene that points at an object that no longer
        // exists (a "Missing" reference), plus empty fields on the scripts the menu depends on.
        public static void LogBrokenReferences()
        {
            string[] watched = { "OptionsUI", "OpenFriendListPopUpDirectly", "NavigationPanelController", "GameModeConfig",
                "MenuControllerGameMode", "QuickMatchController", "DashboardController", "MatchManager", "PartyMembers",
                "NotificationController", "NationalityController", "PlayerBestRankController", "HeaderTokenChip" };

            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            int missing = 0;
            foreach (var go in scene.GetRootGameObjects())
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;
                var it = new SerializedObject(mb).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    bool isMissing = it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0;
                    bool isEmptyWatched = it.objectReferenceValue == null && watched.Contains(typeName) && !it.propertyPath.Contains("m_PersistentCalls");
                    if (isMissing) { missing++; Debug.Log($"DIAG: MISSING {typeName}.{it.propertyPath} on {Path(mb.transform)}"); }
                    else if (isEmptyWatched) Debug.Log($"DIAG: empty {typeName}.{it.propertyPath} on {Path(mb.transform)}");
                }
            }
            Debug.Log($"DIAG: {missing} missing references. DIAG_DONE");
        }

        // Script links that were cleared while the pop-ups were deleted. For the menu scripts below,
        // any reference that is empty now but was set in the older canvas copy (made before the
        // deletion) is pointed at the same object again. Anything else is left alone.
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string ReferenceCanvasPath = "Assets/_tests/Temporal/ProDomino_MainCanvas_Andres10_11_25.prefab";
        private static readonly string[] RelinkTypes =
            { "OptionsUI", "OpenFriendListPopUpDirectly", "NotificationController", "NavigationPanelController", "QuickMatchController" };

        public static void RelinkClearedReferences()
        {
            // 1. Read the references from the older copy: (script type, field) -> (target path, target type)
            var expected = new System.Collections.Generic.Dictionary<string, (string path, System.Type type)>();
            var refRoot = PrefabUtility.LoadPrefabContents(ReferenceCanvasPath);
            try
            {
                foreach (var mb in refRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !RelinkTypes.Contains(mb.GetType().Name)) continue;
                    var it = new SerializedObject(mb).GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        if (it.propertyPath.Contains("m_PersistentCalls") || it.propertyPath == "m_Script") continue;
                        var target = it.objectReferenceValue;
                        var targetTransform = target is Component c ? c.transform : (target as GameObject)?.transform;
                        if (targetTransform == null || !targetTransform.IsChildOf(refRoot.transform)) continue;
                        expected[$"{mb.GetType().Name}.{it.propertyPath}"] = (RelPath(targetTransform, refRoot.transform), target.GetType());
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(refRoot); }

            // 2. Fill the empty ones in the current canvas.
            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            int fixedCount = 0;
            try
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !RelinkTypes.Contains(mb.GetType().Name)) continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null) continue;
                        if (!expected.TryGetValue($"{mb.GetType().Name}.{it.propertyPath}", out var want)) continue;

                        var t = root.transform.Find(want.path);
                        UnityEngine.Object value = null;
                        if (t != null)
                            value = want.type == typeof(GameObject) ? t.gameObject : t.GetComponent(want.type);
                        if (value == null)
                        {
                            Debug.Log($"DIAG: could not relink {mb.GetType().Name}.{it.propertyPath}: '{want.path}' ({want.type.Name}) not found.");
                            continue;
                        }
                        it.objectReferenceValue = value;
                        fixedCount++;
                        Debug.Log($"DIAG: relinked {mb.GetType().Name}.{it.propertyPath} -> {want.type.Name} on '{want.path}'");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // 3. The game scene may hold its own emptied copies of these fields; drop those so the
            //    scene uses the prefab's links.
            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            int sceneReverted = 0;
            foreach (var go in scene.GetRootGameObjects())
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !RelinkTypes.Contains(mb.GetType().Name) || !PrefabUtility.IsPartOfPrefabInstance(mb)) continue;
                var it = new SerializedObject(mb).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null || !it.prefabOverride) continue;
                    if (!expected.ContainsKey($"{mb.GetType().Name}.{it.propertyPath}")) continue;
                    PrefabUtility.RevertPropertyOverride(it, InteractionMode.AutomatedAction);
                    sceneReverted++;
                    Debug.Log($"DIAG: scene now uses prefab link for {mb.GetType().Name}.{it.propertyPath}");
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"DIAG: relinked {fixedCount} references in the canvas, {sceneReverted} in the scene. DIAG_DONE");
        }

        // The e-mail verification pop-up was added after the older canvas copy was made, so its link
        // can't be read from there; OptionsUI.alertEmailVerificationPopup_cg is its root CanvasGroup.
        public static void LinkEmailVerificationAlert()
        {
            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                var popup = root.transform.Find("MiddleScreen_Scalable/InnerScreen/EmailVerificationPopup");
                var options = root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(m => m && m.GetType().Name == "OptionsUI");
                if (popup == null || options == null || !popup.TryGetComponent<CanvasGroup>(out var cg))
                {
                    Debug.Log("DIAG: e-mail verification pop-up or OptionsUI not found; nothing linked. DIAG_DONE");
                    return;
                }
                var so = new SerializedObject(options);
                var prop = so.FindProperty("alertEmailVerificationPopup_cg");
                if (prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = cg;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
                    Debug.Log("DIAG: linked OptionsUI.alertEmailVerificationPopup_cg -> EmailVerificationPopup CanvasGroup.");
                }
                Debug.Log("DIAG_DONE");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void LogRecoveryScreen()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_ProDomino/Authentication/Prefabs/AuthUI.prefab");
            try
            {
                var rec = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Recovery_Container");
                if (rec == null) { Debug.Log("DIAG: no Recovery_Container. DIAG_DONE"); return; }
                foreach (var t in rec.GetComponentsInChildren<Transform>(true))
                {
                    var rt = t as RectTransform;
                    var g = t.GetComponent<UnityEngine.UI.Graphic>();
                    Debug.Log($"DIAG: {Path(t).Replace("AuthUI/Auth_Container/Recovery_Container/", "")} active={t.gameObject.activeSelf} " +
                              $"rect={(rt ? rt.rect.size.ToString("0") : "-")} pos={(rt ? rt.anchoredPosition.ToString("0") : "-")} " +
                              $"graphic={(g ? g.GetType().Name + (g.enabled ? "" : "(off)") : "-")}");
                }
                Debug.Log("DIAG_DONE");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void LogAuthOverrides()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab");
            try
            {
                var auth = root.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "AuthUI");
                if (auth == null) { Debug.Log("DIAG: AuthUI not found. DIAG_DONE"); return; }
                foreach (var comp in auth.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || comp is MonoBehaviour) continue;
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    var props = new System.Collections.Generic.List<string>();
                    while (it.Next(true)) if (it.prefabOverride && !it.propertyPath.StartsWith("m_Children")) props.Add(it.propertyPath);
                    if (props.Count > 0)
                        Debug.Log($"DIAG: overrides on {comp.GetType().Name} @ {Path(comp.transform)}: {string.Join(", ", props.Take(8))}");
                }
                Debug.Log("DIAG_DONE");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string RelPath(Transform t, Transform root)
        {
            var p = t.name;
            for (var x = t.parent; x != null && x != root; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static string Path(Transform t)
        {
            var p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static void LogPanels(string label, Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("LeaderboardUI_")))
                Debug.Log($"DIAG: [{label}] {t.name} activeSelf={t.gameObject.activeSelf} parent={t.parent?.name}");
        }

        // Fires a UI raycast at the centre of the button OptionsUI opens the auth screen with,
        // in the logged-out state, and reports everything the click would hit, topmost first.
        public static void SimulateLoginClick()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);

            UnityEngine.UI.Button login = null;
            foreach (var go in scene.GetRootGameObjects())
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "OptionsUI") continue;
                var so = new SerializedObject(mb);
                login = so.FindProperty("openAuthInterfaceButton")?.objectReferenceValue as UnityEngine.UI.Button;

                // Logged-out runtime state, the way OptionsUI.Configure leaves it.
                Set(so, "notLoginButtonInterface", true);
                Set(so, "loginButtonInterface", false);
            }
            if (login == null) { Debug.Log("DIAG: openAuthInterfaceButton is not assigned. DIAG_DONE"); return; }

            Debug.Log($"DIAG: target {Path(login.transform)} activeSelf={login.gameObject.activeSelf} " +
                      $"activeInHierarchy={login.gameObject.activeInHierarchy} interactable={login.interactable} " +
                      $"targetGraphic={(login.targetGraphic ? login.targetGraphic.name : "NONE")} " +
                      $"components={string.Join(",", login.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name))}");

            foreach (var comp in login.GetComponents<Component>())
            {
                if (comp == null || comp is Transform or CanvasRenderer) continue;
                var cso = new SerializedObject(comp);
                var cit = cso.GetIterator();
                while (cit.NextVisible(true))
                {
                    if (cit.propertyType is SerializedPropertyType.Boolean)
                        Debug.Log($"DIAG: {comp.GetType().Name}.{cit.propertyPath} = {cit.boolValue}");
                    else if (cit.propertyType is SerializedPropertyType.ObjectReference && cit.propertyPath.Contains("Graphic"))
                        Debug.Log($"DIAG: {comp.GetType().Name}.{cit.propertyPath} = {(cit.objectReferenceValue ? cit.objectReferenceValue.name : "NULL")}");
                }
            }

            Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)login.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var world = (corners[0] + corners[2]) * 0.5f;

            var canvas = login.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            Debug.Log($"DIAG: canvas={canvas.name} mode={canvas.renderMode} camera={(cam ? cam.name : "none")} screenPoint={screen}");

            var es = UnityEngine.EventSystems.EventSystem.current;
            var temp = es ? null : new GameObject("TempEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            if (!es) es = temp.GetComponent<UnityEngine.EventSystems.EventSystem>();
            try
            {
                var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen };
                var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                foreach (var gr in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    gr.Raycast(data, results);

                if (results.Count == 0) Debug.Log("DIAG: HIT nothing (the raycast returned no results)");
                foreach (var r in results)
                {
                    var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(r.gameObject);
                    Debug.Log($"DIAG: HIT depth={r.depth} sorting={r.sortingOrder} {Path(r.gameObject.transform)} " +
                              $"-> clickHandler={(handler ? Path(handler.transform) : "none")}");
                }
            }
            finally { if (temp) UnityEngine.Object.DestroyImmediate(temp); }
            Debug.Log("DIAG_DONE");
        }

        private static void Set(SerializedObject so, string field, bool active)
        {
            if (so.FindProperty(field)?.objectReferenceValue is not CanvasGroup cg) return;
            cg.alpha = active ? 1f : 0f;
            cg.interactable = active;
            cg.blocksRaycasts = active;
        }

        // Every screen the canvas holds, with the scripts that own it: the inventory the UI
        // redesign plan is built from.
        public static void LogScreenInventory()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab");
            try
            {
                foreach (var t in root.transform.GetComponentsInChildren<Transform>(true))
                {
                    var parent = t.parent ? t.parent.name : "-";
                    bool isPanel = parent is "InnerScreen" or "Static_PopUps" or "MiddleScreen_Scalable";
                    bool isRoot = t.parent == root.transform;
                    if (!isPanel && !isRoot) continue;

                    var source = PrefabUtility.IsPartOfPrefabInstance(t.gameObject)
                        ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)
                        : "(in canvas)";
                    var rect = t as RectTransform;
                    Debug.Log($"INV-SRC: {t.name} rect={(rect ? rect.rect.size.ToString("0") : "-")} source={source}");

                    var scripts = t.GetComponents<MonoBehaviour>().Where(m => m).Select(m => m.GetType().Name).ToArray();
                    var nav = t.GetComponents<MonoBehaviour>().Where(m => m)
                        .SelectMany(m => m.GetType().GetInterfaces())
                        .Any(i => i.Name == "INavigationPanel");
                    Debug.Log($"INV: [{parent}] {t.name} active={t.gameObject.activeSelf} children={t.childCount} " +
                              $"navPanel={nav} scripts={(scripts.Length == 0 ? "-" : string.Join(",", scripts))}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
            {
                var canvases = go.GetComponentsInChildren<UnityEngine.Canvas>(true).Length;
                Debug.Log($"INV: [scene-root] {go.name} active={go.activeSelf} canvases={canvases} " +
                          $"scripts={string.Join(",", go.GetComponents<MonoBehaviour>().Where(m => m).Select(m => m.GetType().Name))}");
            }
            Debug.Log("DIAG_DONE");
        }

        // Any UI object collapsed by a zero scale: it stays "open" in code but draws nothing.
        public static void LogZeroScales()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab");
            try { LogZeroScales("canvas", root.transform); }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) LogZeroScales("scene", go.transform);
            Debug.Log("DIAG_DONE");
        }

        private static void LogZeroScales(string label, Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var s = t.localScale;
                if (s.x > 0.0001f && s.y > 0.0001f) continue;
                Debug.Log($"DIAG: [{label}] zero scale {s:0.###} on {Path(t)} active={t.gameObject.activeSelf}");
            }
        }

        // What OptionsUI is wired to, in the canvas prefab and in the scene instance.
        public static void LogOptionsUiLinks()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab");
            try { LogOptionsUi("prefab", root.transform); }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) LogOptionsUi("scene", go.transform);
            Debug.Log("DIAG_DONE");
        }

        private static void LogOptionsUi(string label, Transform root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "OptionsUI") continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var v = it.objectReferenceValue;
                    Debug.Log($"DIAG: [{label}] OptionsUI.{it.propertyPath} -> " +
                              (v == null ? "NULL" : $"{v.GetType().Name} on '{(v is Component c ? Path(c.transform) : v.name)}'"));
                }
            }
        }

        // Why a header widget does not take clicks: lists the profile chip's own state, then every
        // raycast target that covers the "Log In" button, in draw order (last = on top).
        public static void LogLoginButtonBlockers()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
                .FirstOrDefault(c => c.name == "ProDomino_MainCanvas" || c.transform.Find("Background"));
            if (canvas == null) { Debug.Log("DIAG: no canvas. DIAG_DONE"); return; }

            var profile = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Options_Profile");
            if (profile == null) { Debug.Log("DIAG: no Options_Profile. DIAG_DONE"); return; }

            foreach (var t in profile.GetComponentsInChildren<Transform>(true))
            {
                var rt = t as RectTransform;
                var g = t.GetComponent<UnityEngine.UI.Graphic>();
                var b = t.GetComponent<UnityEngine.UI.Button>();
                var cg = t.GetComponent<CanvasGroup>();
                Debug.Log($"DIAG: {Path(t).Replace("ProDomino_MainCanvas/", "")} active={t.gameObject.activeSelf} " +
                          $"rect={(rt ? rt.rect.size.ToString("0") : "-")} " +
                          $"graphic={(g ? $"{g.GetType().Name} raycast={g.raycastTarget} enabled={g.enabled}" : "-")} " +
                          $"button={(b ? $"enabled={b.enabled} interactable={b.interactable} calls={b.onClick.GetPersistentEventCount()}" : "-")} " +
                          $"cg={(cg ? $"alpha={cg.alpha} blocks={cg.blocksRaycasts} inter={cg.interactable}" : "-")} " +
                          $"extra={string.Join(",", t.GetComponents<MonoBehaviour>().Where(m => m).Select(m => m.GetType().Name))}");
            }

            // Everything that would be hit at the centre of the "Log In" button.
            var notLogged = profile.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "NotLogged_Interface");
            var login = notLogged ? notLogged.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.GetComponent<UnityEngine.UI.Button>()) : null;
            var target = (RectTransform)(login ? login : profile);
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var point = (corners[0] + corners[2]) * 0.5f;
            Debug.Log($"DIAG: probing at the centre of '{Path(target).Replace("ProDomino_MainCanvas/", "")}'");

            foreach (var g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (!g.raycastTarget || !g.enabled) continue;
                var rt = (RectTransform)g.transform;
                var local = rt.InverseTransformPoint(point);
                if (!rt.rect.Contains(local)) continue;
                Debug.Log($"DIAG: COVERS order={DrawOrder(g.transform, canvas.transform)} {Path(g.transform).Replace("ProDomino_MainCanvas/", "")} " +
                          $"activeInHierarchy={g.gameObject.activeInHierarchy} type={g.GetType().Name} blockedByGroup={BlockingGroup(g.transform, canvas.transform)}");
            }
            Debug.Log("DIAG_DONE");
        }

        // Sibling-index path, so entries sort in draw order (the last one drawn wins a click).
        private static string DrawOrder(Transform t, Transform root)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.GetSiblingIndex().ToString("000"));
            return string.Join(".", parts);
        }

        private static string BlockingGroup(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root.parent; x = x.parent)
                if (x.TryGetComponent<CanvasGroup>(out var cg) && (!cg.blocksRaycasts || cg.alpha <= 0.01f))
                    return $"{x.name}(alpha={cg.alpha},blocks={cg.blocksRaycasts})";
            return "-";
        }
    }
}
