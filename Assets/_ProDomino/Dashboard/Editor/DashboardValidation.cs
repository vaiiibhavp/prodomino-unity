using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    internal static class DashboardValidation
    {
        [MenuItem("ProDomino/Dashboard/Validate NavButton Restyle")]
        public static void ValidateNavButton()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ProDomino/NavigationSystem/Prefabs/NavegationPanel_Button.prefab");
            if (prefab == null)
            {
                Debug.LogError("VALIDATION FAILED: could not load NavegationPanel_Button.prefab at all.");
                return;
            }

            var toggleGraphic = prefab.transform.Find("NPButton_Hoverindicator"); // sibling structure check
            var allImages = prefab.GetComponentsInChildren<Image>(true);
            bool foundGradient = false;
            foreach (var img in allImages)
            {
                if (img.sprite != null && img.sprite.name == "Grad_ButtonPrimary")
                {
                    foundGradient = true;
                    Debug.Log($"OK: found ToggleGraphic image '{img.gameObject.name}' using sprite '{img.sprite.name}', type={img.type}");
                }
            }
            if (!foundGradient)
                Debug.LogError("VALIDATION FAILED: no Image component references the Grad_ButtonPrimary sprite (sprite reference may be broken).");

            var texts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.font == null)
                {
                    Debug.LogError($"VALIDATION FAILED: text '{t.gameObject.name}' has a NULL font reference.");
                }
                else
                {
                    Debug.Log($"OK: text '{t.gameObject.name}' uses font '{t.font.name}'");
                }
            }

            Debug.Log("Validation pass complete.");
        }

        [MenuItem("ProDomino/Dashboard/Validate Header Font Swap")]
        public static void ValidateHeaderFonts()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            string[] paths =
            {
                "Assets/_ProDomino/Shared/Prefabs/UserControlCenter.prefab",
                "Assets/_ProDomino/Prefabs/UI/GamesPlayedToday_Scalable.prefab",
                "Assets/_ProDomino/Prefabs/UI/UsersPlayingNow_Scalable.prefab",
            };

            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"VALIDATION FAILED: could not load {path}");
                    continue;
                }

                var texts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                int nullCount = 0, montserratCount = 0;
                foreach (var t in texts)
                {
                    if (t.font == null) { nullCount++; Debug.LogError($"VALIDATION FAILED in {path}: text '{t.gameObject.name}' has NULL font."); }
                    else if (t.font.name.StartsWith("Montserrat")) montserratCount++;
                }
                Debug.Log($"OK: {path} — {texts.Length} text(s), {montserratCount} using Montserrat, {nullCount} null.");
            }

            Debug.Log("Header font validation complete.");
        }
    }
}
