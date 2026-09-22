using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Coordinates restyling of background and content framing across all screens:
    /// - Global canvas background in ProDomino_MainCanvas.prefab (#01010C)
    /// - Framing card for Achievements_Screen.prefab (#070A14 with 1px #1E2538 border)
    /// - Framing card for Shop_Screen.prefab (#070A14 with 1px #1E2538 border)
    /// - Framing card for LeaderboardUI_NavPanel_New.prefab (#070A14 with 1px #1E2538 border)
    /// - Camera background color in test and main scenes (#01010C)
    /// </summary>
    internal static class GlobalBackgroundRestyler
    {
        private const string CanvasPrefabPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";

        [MenuItem("ProDomino/Dashboard/Restyle All Backgrounds + Apply to Prefabs")]
        public static void ApplyAll()
        {
            try
            {
                Debug.Log("[GlobalBackgroundRestyler] Starting global background restyle...");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                // 1. Ensure Canvas has full solid #01010C page background
                RestyleCanvasBackground();

                // 2. Restyle Achievements Screen
                Debug.Log("[GlobalBackgroundRestyler] Applying Achievements screen restyle...");
                AchievementsRestyler.ApplyAndRender();

                // 3. Restyle Shop Screen
                Debug.Log("[GlobalBackgroundRestyler] Applying Shop screen restyle...");
                ShopRestyler.ApplyAndRender();

                // 4. Restyle Leaderboard Screen
                Debug.Log("[GlobalBackgroundRestyler] Applying Leaderboard screen restyle...");
                LeaderboardRestyler.ApplyAndRender();

                // 5. Update Camera Background in scenes
                UpdateAllSceneCameras();

                // 6. Render screenshots of all screens
                RenderAllScreens();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[GlobalBackgroundRestyler] SUCCESS: All screen backgrounds and card frames successfully applied!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GlobalBackgroundRestyler] ERROR during restyle: {ex}");
                throw;
            }
        }

        private static void RestyleCanvasBackground()
        {
            var root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                // Ensure a full solid page background exists at the very back of the Canvas (child index 0)
                var pageBg = root.transform.Find("Page_Background");
                if (pageBg == null)
                {
                    var go = new GameObject("Page_Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetSiblingIndex(0);
                    pageBg = go.transform;
                }
                else
                {
                    pageBg.SetSiblingIndex(0);
                }

                var pageRt = (RectTransform)pageBg;
                pageRt.anchorMin = Vector2.zero;
                pageRt.anchorMax = Vector2.one;
                pageRt.offsetMin = Vector2.zero;
                pageRt.offsetMax = Vector2.zero;

                var pImg = pageBg.GetComponent<Image>() ?? pageBg.gameObject.AddComponent<Image>();
                pImg.sprite = null;
                pImg.color = PageBg; // Solid #01010C
                pImg.raycastTarget = false;

                // Also ensure the existing Background hierarchy has solid PageBg
                var bg = root.transform.Find("Background");
                if (bg != null)
                {
                    var bgImg = bg.GetComponent<Image>() ?? bg.gameObject.AddComponent<Image>();
                    bgImg.sprite = null;
                    bgImg.color = PageBg;
                    bgImg.raycastTarget = false;

                    var baseFrame = bg.Find("Main_Menu_Base");
                    if (baseFrame != null)
                    {
                        var baseImg = baseFrame.GetComponent<Image>();
                        if (baseImg != null) baseImg.color = PageBg;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log("[GlobalBackgroundRestyler] Saved Canvas with solid PageBg (#01010C).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpdateAllSceneCameras()
        {
            string[] scenePaths = new[]
            {
                "Assets/_tests/_Scenes/Temporary UI tests.unity",
                "Assets/_tests/_Scenes/Test_UIIntegration.unity",
                "Assets/DominoTemplate_v2/Scenes/MainScene.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    bool changed = false;
                    foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    {
                        if (cam.gameObject.scene == scene)
                        {
                            cam.clearFlags = CameraClearFlags.SolidColor;
                            cam.backgroundColor = PageBg;
                            EditorUtility.SetDirty(cam);
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"[GlobalBackgroundRestyler] Updated camera background in {scenePath}.");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("ProDomino/Dashboard/Render All Screens To PNG")]
        public static void RenderAllScreens()
        {
            var outDir = Path.Combine(Application.dataPath, "../pd_renders");
            Directory.CreateDirectory(outDir);

            // 1. Achievements Screen
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_achievements.png"), 1920, 1080, true, root =>
            {
                ActivateScreen(root, "Achievements", "Achievements");
            });

            // 2. Shop Screen
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_shop.png"), 1920, 1080, true, root =>
            {
                ActivateScreen(root, "Shop", "Shop");
            });

            // 3. Leaderboard Screen
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_leaderboard.png"), 1920, 1080, true, root =>
            {
                ActivateScreen(root, "Leaderboard", "Leaderboard");
            });

            Debug.Log($"[GlobalBackgroundRestyler] Rendered all screens to {outDir}");
        }

        private static void ActivateScreen(GameObject canvasRoot, string targetKeyword, string navButtonId)
        {
            // Deactivate Dashboard Content so it doesn't overlay other screens
            var dash = PdUiKit.FindDeep(canvasRoot.transform, "Dashboard_Content");
            if (dash != null)
            {
                dash.gameObject.SetActive(false);
                if (dash.TryGetComponent<CanvasGroup>(out var dcg))
                {
                    dcg.alpha = 0f;
                    dcg.interactable = false;
                    dcg.blocksRaycasts = false;
                }
            }

            // Find target screen under InnerScreen and activate it
            var inner = PdUiKit.FindDeep(canvasRoot.transform, "InnerScreen");
            if (inner != null)
            {
                for (int i = 0; i < inner.childCount; i++)
                {
                    var child = inner.GetChild(i);
                    bool isTarget = child.name.IndexOf(targetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    child.gameObject.SetActive(isTarget);

                    foreach (var cg in child.GetComponentsInChildren<CanvasGroup>(true))
                    {
                        cg.alpha = isTarget ? 1f : 0f;
                        cg.interactable = isTarget;
                        cg.blocksRaycasts = isTarget;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[GlobalBackgroundRestyler] InnerScreen not found in canvas!");
            }

            // Update sidebar button selection state to highlight current tab
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CustomButtonUI")).FirstOrDefault(t => t != null);
            if (type != null)
            {
                var preview = type.GetMethod("PreviewVisualState", BindingFlags.Instance | BindingFlags.Public);
                var idProp = type.GetProperty("CustomButtonID");
                foreach (var b in canvasRoot.GetComponentsInChildren(type, true))
                {
                    string id = (string)idProp?.GetValue(b);
                    bool select = string.Equals(id, navButtonId, StringComparison.OrdinalIgnoreCase);
                    preview?.Invoke(b, new object[] { select });
                }
            }
        }
    }
}
