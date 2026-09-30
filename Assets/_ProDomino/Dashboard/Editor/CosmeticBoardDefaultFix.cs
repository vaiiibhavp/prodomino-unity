using System.Linq;
using Timba.Database;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    // The actual root cause behind the gameplay board never showing the restyle in real matches:
    // the board's visible sprites are NOT read from the mode prefabs. ExtendedGameController.cs
    // (~line 792-799) overwrites boardImage/boardFundImage every match with gameManager.Board /
    // gameManager.BoardFund, which resolve through GameManager.GetBoard()/GetBoardFund() ->
    // DictionaryService.GetSprite("Boards"/"Fund", ...) -> SpriteDictionaryDatabase's
    // "Boards_Default"/"Fund_Default" entries, unless the player has a different cosmetic
    // equipped. Any prefab-level sprite edit was invisible in a real match because this always
    // wins. Confirmed live via unity-cli eval on a running match: BoardFund's sprite was
    // "DARK BLUE" (Fund_Default's asset before this fix), not any restyled sprite.
    //
    // Boards_Default pointed at a fully transparent sprite and the prefabs pointed at a
    // Gameplay_BoardBg sprite that no longer exists on disk, so a guest match drew the table area
    // as a plain white quad. Both defaults now take the first real skin in their own dictionary
    // (table_green / tablefund_green), which is a shipped, visible sprite.
    internal static class CosmeticBoardDefaultFix
    {
        private const string DatabasePath = "Assets/_ProDomino/Shared/ScriptableObjects/SpriteDictionaryDatabase.asset";

        [MenuItem("ProDomino/Dashboard/Fix Cosmetic Board Defaults")]
        public static void Fix()
        {
            var db = AssetDatabase.LoadAssetAtPath<SpriteDictionaryDatabase>(DatabasePath);
            if (db == null) { Debug.LogError("COSMETIC-FIX: database asset not found at " + DatabasePath); return; }

            var fund = db.SpriteDictionaries.FirstOrDefault(d => d.DictionaryName == "Fund");
            var boards = db.SpriteDictionaries.FirstOrDefault(d => d.DictionaryName == "Boards");
            if (fund == null) { Debug.LogError("COSMETIC-FIX: 'Fund' dictionary not found."); return; }
            if (boards == null) { Debug.LogError("COSMETIC-FIX: 'Boards' dictionary not found."); return; }

            Debug.Log($"COSMETIC-FIX: before -- Fund_Default={fund.SerializableDictionary["Fund_Default"]?.name}, Boards_Default={boards.SerializableDictionary["Boards_Default"]?.name}");

            var boardSprite = FirstRealSkin(boards, "Boards_Default");
            var fundSprite = FirstRealSkin(fund, "Fund_Default");
            if (boardSprite == null) { Debug.LogError("COSMETIC-FIX: 'Boards' has no non-default sprite to use."); return; }
            if (fundSprite == null) { Debug.LogError("COSMETIC-FIX: 'Fund' has no non-default sprite to use."); return; }

            boards.SerializableDictionary["Boards_Default"] = boardSprite;
            fund.SerializableDictionary["Fund_Default"] = fundSprite;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"COSMETIC-FIX: after -- Fund_Default={fund.SerializableDictionary["Fund_Default"]?.name}, Boards_Default={boards.SerializableDictionary["Boards_Default"]?.name}");

            RepairControllerPrefabs(fundSprite, boardSprite);

            Debug.Log("COSMETIC_FIX_DONE");
        }

        // The first entry that is not the default key and actually has a sprite assigned.
        private static Sprite FirstRealSkin(SpriteDictionaryDatabase.DictionarySprite dictionary, string defaultKey)
        {
            foreach (var pair in dictionary.SerializableDictionary)
            {
                if (pair.Key == defaultKey) continue;
                if (pair.Value) return pair.Value;
            }

            return null;
        }

        // The gameplay controller prefabs referenced a Gameplay_BoardBg sprite that no longer exists on
        // disk, so BoardFund resolved to a missing sprite and Unity drew it as a plain white quad. That
        // is what a guest sees before any cosmetic override lands. Repoint the serialized references to
        // the same default sprites the dictionary now serves, so the prefab is correct on its own.
        private static void RepairControllerPrefabs(Sprite fundSprite, Sprite boardSprite)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DominoTemplate_v2/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                var changed = false;

                // Resolved through SerializedObject instead of the controller type so this editor
                // assembly does not need a reference to ProDomino.GameModes.
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    var so = new SerializedObject(behaviour);
                    var fund = so.FindProperty("boardFundImage");
                    var board = so.FindProperty("boardImage");
                    if (fund == null && board == null) continue;

                    changed |= ApplySprite(fund, fundSprite, Color.white);
                    changed |= ApplySprite(board, boardSprite, Color.white);
                }

                if (!changed) continue;

                PrefabUtility.SavePrefabAsset(root);
                Debug.Log($"COSMETIC-FIX: repaired board sprites in {path}");
            }

            AssetDatabase.SaveAssets();
        }

        private static bool ApplySprite(SerializedProperty property, Sprite sprite, Color color)
        {
            if (property?.objectReferenceValue is not Image image) return false;
            if (image.sprite == sprite && image.color == color) return false;

            image.sprite = sprite;
            image.color = color;
            EditorUtility.SetDirty(image);
            return true;
        }

    }
}
