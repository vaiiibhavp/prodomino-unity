using HelperSharedLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This runs only inside the Unity Editor.
/// </summary>
public static class DeserializerTester
{
    // Original file and output file paths (you can modify them)
    private const string InputPath = "Assets/toDeserializeTest.txt";

    [MenuItem("Tools/Deserializer/Test")]
    public static void DeserializeTest()
    {
        try
        {
            if (!File.Exists(InputPath))
            {
                Debug.LogError($"Input file not found at path: {InputPath}");
                return;
            }

            // Read all lines and normalize
            var doc = File.ReadAllText(InputPath);

            Debug.Log($"Loaded json");

            // Clean redundant entries
            var settings = new JsonSerializerSettings
            {
                Converters = { new FirestoreTimestampConverter() }
            };

            var docDeserialized = JsonConvert.DeserializeObject<FirestoreClubData>(doc, settings);

            Debug.Log($"✅ Deserialized firestore player club data: \n{JsonConvert.SerializeObject(docDeserialized)}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error cleaning banned words: {ex.Message}");
        }
    }
}
