using System.Linq;
using System.Text;
using ProDomino.NotificationSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.NotificationSystem.EditorTools
{
    /// <summary>
    /// The main scene/canvas carry stale layout overrides on the nested notification panel,
    /// so edits to Notifications_Scalable.prefab (tabs, scroll area, sizing) never show.
    /// Reverts layout/visual overrides under every NotificationController in the open scenes.
    /// Script components (controller, toggle group, buttons) are left untouched.
    /// </summary>
    internal static class NotificationPanelOverrideCleaner
    {
        [MenuItem("ProDomino/Notifications/Revert Panel Layout Overrides")]
        private static void RevertPanelLayoutOverrides()
        {
            var log = new StringBuilder();
            var reverted = 0;

            var controllers = Object.FindObjectsByType<NotificationController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                foreach (var transform in controller.GetComponentsInChildren<Transform>(true))
                {
                    // Keep the drawer's placement on screen (root + its direct children are positioned by the canvas)
                    var keepPlacement = transform == controller.transform || transform.parent == controller.transform;

                    var components = transform.GetComponents<Component>()
                        .Where(IsLayoutOrVisual)
                        .Where(c => !(keepPlacement && c is RectTransform))
                        .Cast<Object>()
                        .Append(transform.gameObject);

                    foreach (var target in components)
                    {
                        if (!PrefabUtility.IsPartOfPrefabInstance(target))
                            continue;

                        var modifications = PrefabUtility.GetPropertyModifications(target);
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
                        if (modifications == null || source == null || !modifications.Any(m => m.target == source))
                            continue;

                        // Revert on the outermost instance so nested (scene -> canvas -> UserControlCenter) overrides all go
                        PrefabUtility.RevertObjectOverride(target, InteractionMode.UserAction);
                        log.AppendLine($"{GetPath(transform)} :: {target.GetType().Name}");
                        reverted++;
                    }
                }
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[NotificationPanelOverrideCleaner] Reverted {reverted} overrides under {controllers.Length} notification panel(s). Save the scene.\n{log}");
        }

        private static bool IsLayoutOrVisual(Component component) => component
            is RectTransform
            or LayoutGroup
            or LayoutElement
            or ContentSizeFitter
            or ScrollRect
            or RectMask2D
            or Image
            or TMP_Text;

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while ((transform = transform.parent) != null)
                path = transform.name + "/" + path;
            return path;
        }
    }
}
