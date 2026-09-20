#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

/// <summary>
/// Post-build processor for WebGL builds that ensures firebase-messaging-sw.js
/// is copied to the output folder (same directory as index.html).
/// </summary>
public class WebGLPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Only apply to WebGL builds
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        // Path inside your project where the JS file is stored
        string sourcePath = Path.Combine("Assets", "_ProDomino", "_Backend", "FirebaseMessaging_SW", "firebase-messaging-sw.js");

        // Destination path (next to index.html in the final build folder)
        string buildOutputPath = report.summary.outputPath;
        string buildDirectory = Path.GetDirectoryName(buildOutputPath);
        string destPath = Path.Combine(buildDirectory, "firebase-messaging-sw.js");

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            UnityEngine.Debug.Log($"✅ firebase-messaging-sw.js copied to: {destPath}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"⚠️ firebase-messaging-sw.js not found at: {sourcePath}");
        }
    }
}
#endif
