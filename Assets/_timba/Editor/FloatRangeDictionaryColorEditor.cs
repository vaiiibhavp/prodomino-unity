using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FloatRangeDictionaryColor))]
public class FloatRangeDictionaryColorEditor : Editor
{
    private FloatRangeDictionaryColor rangeDict;
    private const float SLIDER_MIN = 0f;
    private const float SLIDER_MAX = 100f;

    private void OnEnable()
    {
        rangeDict = (FloatRangeDictionaryColor)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("New Color Range", EditorStyles.boldLabel);

        EditorGUILayout.MinMaxSlider(new GUIContent("Range"), ref rangeDict.newMin, ref rangeDict.newMax, SLIDER_MIN, SLIDER_MAX);
        rangeDict.newMin = EditorGUILayout.FloatField("Min", rangeDict.newMin);
        rangeDict.newMax = EditorGUILayout.FloatField("Max", rangeDict.newMax);
        rangeDict.newValue = EditorGUILayout.ColorField("Color", rangeDict.newValue);

        bool valid = rangeDict.newMin < rangeDict.newMax && !HasOverlap(rangeDict);

        EditorGUI.BeginDisabledGroup(!valid);
        if (GUILayout.Button("Add Range"))
        {
            rangeDict.entries.Add(new FloatRangeValue<Color>
            {
                min = rangeDict.newMin,
                max = rangeDict.newMax,
                value = rangeDict.newValue
            });

            rangeDict.newMin = 0f;
            rangeDict.newMax = 1f;
            rangeDict.newValue = Color.white;
            EditorUtility.SetDirty(rangeDict);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Existing Ranges", EditorStyles.boldLabel);

        for (int i = 0; i < rangeDict.entries.Count; i++)
        {
            var entry = rangeDict.entries[i];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Range: {entry.min:F2} – {entry.max:F2}");
            EditorGUILayout.ColorField("Color", entry.value);

            if (GUILayout.Button("Remove"))
            {
                rangeDict.entries.RemoveAt(i);
                EditorUtility.SetDirty(rangeDict);
                break;
            }

            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool HasOverlap(FloatRangeDictionaryColor dict)
    {
        var newRange = new FloatRangeValue<Color>
        {
            min = dict.newMin,
            max = dict.newMax
        };

        foreach (var entry in dict.entries)
        {
            if (entry.Overlaps(newRange))
                return true;
        }

        return false;
    }
}
