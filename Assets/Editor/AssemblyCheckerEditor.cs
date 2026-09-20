// AssemblyCheckerEditor.cs
// Editor helper to show the assembly fullname for types in the shared DLL.
// Use this to verify the exact assembly name you must use in link.xml.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

public class AssemblyCheckerEditor : EditorWindow
{
    [MenuItem("Window/Assembly Checker/Print Shared Assembly Names")]
    public static void ShowWindow()
    {
        var window = GetWindow<AssemblyCheckerEditor>("Assembly Checker");
        window.minSize = new Vector2(400, 100);
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Print HelperSharedLibrary assembly fullname"))
        {
            PrintAssemblyFullname();
        }

        GUILayout.Space(10);
        GUILayout.Label("Open the Console to see the output.", EditorStyles.helpBox);
    }

    private static void PrintAssemblyFullname()
    {
        // Replace with a type from your DLL to inspect its assembly.
        var t = typeof(HelperSharedLibrary.FirestoreClubDataResponse);
        Assembly asm = t.Assembly;
        Debug.Log($"Assembly fullname for HelperSharedLibrary.FirestoreClubDataResponse: {asm.FullName}");
        Debug.Log($"Assembly location (if available): {asm.Location}");
    }
}
#endif
