using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    // Restyles specific header chip instances (Subscribe button, bell, profile) INSIDE
    // UserControlCenter.prefab by setting component properties directly via the Editor API
    // (LoadPrefabContents -> modify -> SaveAsPrefabAsset). This lets Unity compute the correct
    // instance-level override through the prefab variant chain itself, rather than hand-writing
    // override YAML (error-prone) or touching the shared source prefabs (which are reused in
    // ~40 other places across the project and must not change).
    internal static class DashboardHeaderRestyler
    {
        private const string UserControlCenterPath = "Assets/_ProDomino/Shared/Prefabs/UserControlCenter.prefab";

        private static readonly Color DarkChip = new Color(0.016f, 0.027f, 0.09f, 1f); // ~#040717

        // Read-only: lists every Image component under each target (name, current color,
        // sprite, rect size) so the actual background can be identified by eye before any
        // property is changed. No modifications, nothing saved.
        [MenuItem("ProDomino/Dashboard/Diagnose Header Chips")]
        public static void DiagnoseHeaderChips()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var root = PrefabUtility.LoadPrefabContents(UserControlCenterPath);
            try
            {
                foreach (var name in new[] { "MonthlySubscription_Button", "Notifications_Scalable", "User_Button", "PlayerBestRank_Scalable" })
                {
                    var target = FindByNameContains(root.transform, name);
                    if (target == null)
                    {
                        Debug.LogWarning($"DIAGNOSE: could not find '{name}'.");
                        continue;
                    }
                    Debug.Log($"=== {name} (path: {GetFullPath(target)}) ===");
                    var images = target.GetComponentsInChildren<Image>(true);
                    foreach (var img in images)
                    {
                        var rt = (RectTransform)img.transform;
                        Debug.Log($"  Image '{GetFullPath(img.transform, target)}' color=({img.color.r:F2},{img.color.g:F2},{img.color.b:F2},{img.color.a:F2}) sprite={(img.sprite != null ? img.sprite.name : "null")} size=({rt.rect.width:F0}x{rt.rect.height:F0})");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Confirmed via DiagnoseHeaderChips: these two targets each have exactly one clear
        // "Bg_Image" that is currently white/untinted (their visible color today comes from the
        // sprite itself). Notifications_Scalable turned out to be a large nested dropdown panel
        // with no single safe background, and PlayerBestRank_Scalable (Class C) has no
        // background image at all — both left untouched rather than guessed at.
        [MenuItem("ProDomino/Dashboard/Restyle Header Chips")]
        public static void RestyleHeaderChips()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var root = PrefabUtility.LoadPrefabContents(UserControlCenterPath);
            try
            {
                RestyleConfirmedBackground(root.transform, "MonthlySubscription_Button");
                RestyleConfirmedBackground(root.transform, "User_Button");

                PrefabUtility.SaveAsPrefabAsset(root, UserControlCenterPath);
                Debug.Log($"Header chips restyled and saved to {UserControlCenterPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RestyleConfirmedBackground(Transform root, string targetName)
        {
            var target = FindByNameContains(root, targetName);
            if (target == null)
            {
                Debug.LogWarning($"Could not find '{targetName}' inside UserControlCenter — skipping.");
                return;
            }

            var bg = target.Find("Bg_Image");
            if (bg == null)
            {
                Debug.LogError($"VALIDATION FAILED: '{targetName}/Bg_Image' not found — structure changed since diagnosis, skipping to avoid guessing.");
                return;
            }

            var img = bg.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogError($"VALIDATION FAILED: '{targetName}/Bg_Image' has no Image component.");
                return;
            }

            img.color = new Color(DarkChip.r, DarkChip.g, DarkChip.b, img.color.a);
            Debug.Log($"'{targetName}/Bg_Image' recolored to dark chip.");
        }

        private static string GetFullPath(Transform t, Transform relativeTo)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null && current != relativeTo)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static string GetFullPath(Transform t) => GetFullPath(t, null);

        private static Transform FindByNameContains(Transform root, string nameContains)
        {
            if (root.name.Contains(nameContains))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var result = FindByNameContains(root.GetChild(i), nameContains);
                if (result != null) return result;
            }
            return null;
        }
    }
}
