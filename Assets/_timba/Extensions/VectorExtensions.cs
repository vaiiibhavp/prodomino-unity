using UnityEngine;

public static class VectorExtensions
{
    /// <summary>
    /// Converts a position from one canvas to another.
    /// Maintains visual equivalence on screen.
    /// </summary>
    /// <param name="positionInCanvasA">Position relative to canvasA (local)</param>
    /// <param name="canvasA">Source canvas</param>
    /// <param name="canvasB">Target canvas</param>
    /// <returns>Position relative to canvasB (local)</returns>
    public static Vector3 ConvertPosition(this Vector3 positionInCanvasA, Canvas canvasB)
    {
        // Step 1: Convert local canvasA position to world position
        Vector3 worldPos = positionInCanvasA;

        // Step 2: Convert world position to local position in canvasB
        Vector3 localPosInB = canvasB.transform.InverseTransformPoint(worldPos);

        return localPosInB;
    }
}
