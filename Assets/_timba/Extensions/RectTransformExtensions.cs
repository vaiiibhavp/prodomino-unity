using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RectTransformExtensions
{
    /// <summary>
    /// Converts a RectTransform's position from sourceCanvas to targetCanvas.
    /// Maintains world position so it visually stays in the same place.
    /// </summary>
    /// <param name="rect">RectTransform to move</param>
    /// <param name="newParent">Canvas to move rect to</param>
    public static void DeepParenting(this RectTransform rect, Transform newParent)
    {
        // Register pre-change data
        var oldParent = rect.parent;
        var oldLocalPosition = rect.localPosition;
        var oldLocalScale = rect.localScale;
        var oldRotation = rect.rotation;
        var oldWorldSize = rect.GetWorldSize();

        // Change object parent and set its anchores to the center (t avoid scalling)
        rect.SetParent(newParent, worldPositionStays: false);
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f; // Centrar anclas para evitar cambios de posición inesperados
        rect.offsetMax = rect.offsetMax = Vector2.zero; // Reset offsets to avoid unwanted shifts

        // Move the object to has the same position before the change
        var newWorldPosition = oldParent?.TransformPoint(oldLocalPosition) ?? newParent.TransformPoint(oldLocalPosition);
         rect.position = newWorldPosition;

        // Adujust the size to match the previous version
        rect.sizeDelta = new Vector2(
            oldWorldSize.x / oldLocalScale.x,
            oldWorldSize.y / oldLocalScale.y
        );

        rect.localScale = Vector3.one;
        rect.rotation = oldRotation;

        Debug.Log("Parent Parent");
    }

    /// <summary>
    /// Gets the world-space width and height of a RectTransform,
    /// taking into account the lossyScale of all parents.
    /// </summary>
    public static Vector2 GetWorldSize(this RectTransform rect)
    {
        // sizeDelta = tamaño local del rect (antes de escalar)
        Vector2 localSize = rect.rect.size;

        // lossyScale = escala acumulada en todo el hierarchy
        Vector3 scale = rect.localScale;

        // El tamaño final en mundo es el tamaño local multiplicado por la escala
        return new Vector2(localSize.x * scale.x, localSize.y * scale.y);
    }

    /// <summary>
    /// Returns all children of a RectTransform at the specified hierarchy depth.
    /// Depth 1 = direct children, Depth 2 = grandchildren, etc.
    /// </summary>
    public static IEnumerable<RectTransform> GetChildrenAtDepth(this RectTransform root, int targetDepth)
    {
        return root
            .GetComponentsInChildren<RectTransform>(true) // incluye inactivos
            .Where(t => t != root && GetDepth(t, root) == targetDepth);
    }

    /// <summary>
    /// Calculates the hierarchy depth of a Transform relative to a root.
    /// </summary>
    private static int GetDepth(RectTransform current, RectTransform root)
    {
        int depth = 0;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent as RectTransform;
        }
        return depth;
    }
}
