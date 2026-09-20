using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace ProDomino.Dashboard.Editor
{
    internal static class DashboardPreviewSceneBuilder
    {
        private const string PrefabPath = "Assets/_ProDomino/Dashboard/Prefabs/Dashboard_Panel.prefab";
        private const string ScenePath = "Assets/_ProDomino/Dashboard/Scenes/Dashboard_Preview.unity";

        [MenuItem("ProDomino/Dashboard/Build Preview Scene")]
        public static void Build()
        {
            var folder = "Assets/_ProDomino/Dashboard/Scenes";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_ProDomino/Dashboard", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.004f, 0.004f, 0.047f, 1f); // #01010C
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var eventSystemGo = new GameObject("EventSystem", typeof(RectTransform));
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Input module intentionally omitted: this scene is for visual inspection only.
            // The project uses the new Input System package for real gameplay scenes.

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find prefab at {PrefabPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
            var instRt = (RectTransform)instance.transform;
            instRt.anchorMin = new Vector2(0, 1);
            instRt.anchorMax = new Vector2(0, 1);
            instRt.pivot = new Vector2(0, 1);
            instRt.anchoredPosition = new Vector2(80, -40);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Dashboard preview scene saved at {ScenePath}");
        }
    }
}
