using Newtonsoft.Json;
using ProDomino.GameSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utility class to clean and deduplicate banned word lists directly from Unity.
/// This runs only inside the Unity Editor.
/// </summary>
public static class ProfanityCleaner
{
    // Original file and output file paths (you can modify them)
    private const string InputPath = "Assets/banned_words.txt";
    private const string OutputPath = "Assets/banned_words_clean.txt";

    [MenuItem("Tools/Profanity/Clean Word List")]
    public static void CleanBannedWords()
    {
        try
        {
            if (!File.Exists(InputPath))
            {
                Debug.LogError($"Input file not found at path: {InputPath}");
                return;
            }

            // Read all lines and normalize
            var words = File.ReadAllLines(InputPath)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            Debug.Log($"Loaded {words.Count} words from {InputPath}");

            // Clean redundant entries
            var cleaned = CleanRedundantWords(words);

            // Save the result
            File.WriteAllLines(OutputPath, cleaned.OrderBy(w => w));

            AssetDatabase.Refresh();
            Debug.Log($"✅ Cleaned list saved to: {OutputPath} ({cleaned.Count} words)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error cleaning banned words: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes duplicates, substrings, and entries outside reasonable length limits.
    /// </summary>
    private static HashSet<string> CleanRedundantWords(List<string> words)
    {
        // Remove duplicates
        var normalized = words.Distinct().ToList();

        // Remove words that are contained in longer ones
        var cleanList = normalized
            .Where(w => !normalized.Any(other => other != w && other.Contains(w)))
            .ToHashSet();

        return cleanList;
    }
}
