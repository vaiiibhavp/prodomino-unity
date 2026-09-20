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
    }
}
