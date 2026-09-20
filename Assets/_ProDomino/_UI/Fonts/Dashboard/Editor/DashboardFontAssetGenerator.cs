using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProDomino.Dashboard.Editor
{
    internal static class DashboardFontAssetGenerator
    {
        private const string FontDir = "Assets/_ProDomino/_UI/Fonts/Dashboard";

        private static readonly string[] FontFileNames =
        {
            "Montserrat-Regular",
            "Montserrat-Medium",
            "Montserrat-SemiBold",
            "Montserrat-Bold",
            "Montserrat-ExtraBold",
            "Kanit-Regular",
            "Kanit-Medium",
            "Kanit-SemiBold",
            "Kanit-Bold",
        };

        [MenuItem("ProDomino/Dashboard/Generate Font Assets")]
        public static void GenerateAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            foreach (var fontName in FontFileNames)
                GenerateOne(fontName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Dashboard font asset generation complete.");
        }

        private static void GenerateOne(string fontName)
        {
            var ttfPath = $"{FontDir}/{fontName}.ttf";
            var outPath = $"{FontDir}/{fontName} SDF.asset";

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
            {
                Debug.Log($"Skipping (already exists): {outPath}");
                return;
            }

            AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogError($"Could not load source font at {ttfPath}");
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                72,
                5,
                GlyphRenderMode.SDFAA,
                512,
                512,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError($"CreateFontAsset returned null for {ttfPath}");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, outPath);

            if (fontAsset.atlasTextures != null)
            {
                foreach (var tex in fontAsset.atlasTextures)
                {
                    if (tex != null)
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontName + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {outPath}");
        }
    }
}
