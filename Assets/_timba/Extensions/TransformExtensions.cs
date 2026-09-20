using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class TransformExtensions 
{
    /// <summary>
    /// Destroy all children of a transform
    /// </summary>
    /// <param name="transform"></param>
    public static void DestroyChildren(this Transform transform) {
        foreach(Transform child in transform) {
            GameObject.Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Destroy all children of a transform
    /// </summary>
    /// <param name="transform"></param>
    public static void DestroyChildrenImmediate(this Transform transform) {
        foreach (Transform child in transform) {
            GameObject.DestroyImmediate(child.gameObject);
        }
    }

    public static void RefreshLayoutGroupsImmediateAndRecursive(this Transform root)
    {
        var layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true).Reverse();
        foreach (var layoutGroup in layoutGroups)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.GetComponent<RectTransform>());
    }
    
    public static void RefreshContentSizeFitterImmediateAndRecursive(this Transform root, MonoBehaviour executer)
    {
        foreach (var contentSizeFitter in root.GetComponentsInChildren<ContentSizeFitter>())
            contentSizeFitter.enabled = false;

        executer?.StartCoroutine(ReEnableContentSizeFitters());

        IEnumerator ReEnableContentSizeFitters()
        { 
            yield return new WaitForEndOfFrame();

            foreach (var contentSizeFitter in root.GetComponentsInChildren<ContentSizeFitter>())
                contentSizeFitter.enabled = true;
        }
    }

    /// <summary>
    /// Returns all children of a Transform at the specified hierarchy depth.
    /// Depth 1 = direct children, Depth 2 = grandchildren, etc.
    /// </summary>
    public static IEnumerable<Transform> GetChildrenAtDepth(this Transform root, int targetDepth)
    {
        return root
            .GetComponentsInChildren<Transform>(true) // incluye inactivos
            .Where(t => t != root && GetDepth(t, root) == targetDepth);
    }

    /// <summary>
    /// Calculates the hierarchy depth of a Transform relative to a root.
    /// </summary>
    private static int GetDepth(Transform current, Transform root)
    {
        int depth = 0;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }
        return depth;
    }
}
