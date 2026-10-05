using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace ProDomino.EditorTools
{
    /// <summary>
    /// Writes a size report after every player build and opens it.
    /// Output: <project>/BuildReports/<target>_<timestamp>.txt (outside the build folder, so it is never deployed).
    /// </summary>
    public class BuildSizeReport : IPostprocessBuildWithReport
    {
        const int TopAssetCount = 60;
        const string LastBuildReportPath = "Library/LastBuild.buildreport";

        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            // packedAssets is filled after the callback, so wait one editor tick.
            EditorApplication.delayCall += () => WriteAndOpen(LoadLastReport() ?? report);
        }

        [MenuItem("ProDomino/Build/Open Last Build Size Report")]
        static void OpenFromLastBuild()
        {
            var report = LoadLastReport();
            if (report == null)
            {
                EditorUtility.DisplayDialog("Build Size Report", "No build report found. Make a build first.", "OK");
                return;
            }
            WriteAndOpen(report);
        }

        static BuildReport LoadLastReport()
        {
            if (!File.Exists(LastBuildReportPath)) return null;
            return InternalEditorUtility.LoadSerializedFileAndForget(LastBuildReportPath)
                .OfType<BuildReport>()
                .FirstOrDefault();
        }

        static void WriteAndOpen(BuildReport report)
        {
            var text = Build(report);
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "BuildReports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{report.summary.platform}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, text);

            Debug.Log($"[BuildSizeReport] Written to {path}\n{text.Substring(0, Math.Min(text.Length, 4000))}");
            EditorUtility.OpenWithDefaultApp(path);
        }

        static string Build(BuildReport report)
        {
            var sb = new StringBuilder();
            var s = report.summary;
            sb.AppendLine($"Build: {s.platform}  Result: {s.result}  Time: {s.totalTime}");
            sb.AppendLine($"Output: {s.outputPath}");
            sb.AppendLine($"Total size: {Fmt(s.totalSize)}");
            sb.AppendLine();

            // Output files on disk (.data / .wasm / .framework.js). 25 MiB is the Cloudflare Pages per-file limit.
            sb.AppendLine("== Output files ==");
            foreach (var f in report.GetFiles().OrderByDescending(f => f.size).Take(15))
            {
                var flag = f.size > 25UL * 1024 * 1024 ? "  <-- OVER 25 MiB" : "";
                sb.AppendLine($"{Fmt(f.size),12}  {f.role,-20} {Path.GetFileName(f.path)}{flag}");
            }
            sb.AppendLine();

            // Group packed contents by source asset. Sprite atlas pages share the atlas path.
            var assets = new Dictionary<string, (ulong size, string type)>();
            foreach (var packed in report.packedAssets)
            foreach (var info in packed.contents)
            {
                var key = string.IsNullOrEmpty(info.sourceAssetPath) ? "(built-in)" : info.sourceAssetPath;
                assets.TryGetValue(key, out var cur);
                assets[key] = (cur.size + info.packedSize, info.type != null ? info.type.Name : cur.type);
            }

            var total = assets.Values.Aggregate(0UL, (a, v) => a + v.size);

            sb.AppendLine("== Size by type (uncompressed) ==");
            foreach (var g in assets.GroupBy(a => a.Value.type ?? "?")
                         .Select(g => (type: g.Key, size: g.Aggregate(0UL, (a, v) => a + v.Value.size)))
                         .OrderByDescending(g => g.size))
                sb.AppendLine($"{Fmt(g.size),12}  {Pct(g.size, total),6}  {g.type}");
            sb.AppendLine();

            sb.AppendLine($"== Top {TopAssetCount} assets (uncompressed) ==");
            foreach (var a in assets.OrderByDescending(a => a.Value.size).Take(TopAssetCount))
                sb.AppendLine($"{Fmt(a.Value.size),12}  {Pct(a.Value.size, total),6}  {a.Value.type,-18} {a.Key}");

            return sb.ToString();
        }

        static string Pct(ulong v, ulong total) => total == 0 ? "-" : $"{100.0 * v / total:0.0}%";

        static string Fmt(ulong bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.00} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
            return $"{bytes} B";
        }
    }
}
