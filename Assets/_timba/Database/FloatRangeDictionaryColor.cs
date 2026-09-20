using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = nameof(FloatRangeDictionaryColor), menuName = "Timba/" + nameof(FloatRangeDictionaryColor))]
public class FloatRangeDictionaryColor : ScriptableObject
{
    public List<FloatRangeValue<Color>> entries = new();

    // Temp input para agregar
    [HideInInspector] public float newMin = 0f;
    [HideInInspector] public float newMax = 1f;
    [HideInInspector] public Color newValue = Color.white;

    public Color GetColorForValue(float input, bool isNormalized = false)
    {
        if (isNormalized)
        {
            input = Mathf.Clamp01(input); // Ensure input is between 0 and 1
            input *= 100f; // Scale to match the range of the entries
        }

        foreach (var entry in entries)
        {
            if (input >= entry.min && input <= entry.max)
                return entry.value;
        }

        return Color.clear; // default if no range matches
    }
}
