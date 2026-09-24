using System.Linq;
using Timba.Database;
using UnityEditor;
using UnityEngine;

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
    // Fix applied once already via live eval on 2026-09-24 (Fund_Default -> Gameplay_BoardBg,
    // Boards_Default -> a transparent sprite). This script exists to reapply it if the database
    // ever reverts (e.g. a merge, or PdUiKit's generated sprites get regenerated with a new name).
    internal static class CosmeticBoardDefaultFix
    {
        private const string DatabasePath = "Assets/_ProDomino/Shared/ScriptableObjects/SpriteDictionaryDatabase.asset";
        private const string TransparentSpritePath = "Assets/_ProDomino/Dashboard/Generated/Gameplay_Transparent.png";

        [MenuItem("ProDomino/Dashboard/Fix Cosmetic Board Defaults")]
        public static void Fix()
        {
            var db = AssetDatabase.LoadAssetAtPath<SpriteDictionaryDatabase>(DatabasePath);
            if (db == null) { Debug.LogError("COSMETIC-FIX: database asset not found at " + DatabasePath); return; }

            var boardPanel = PdUiKit.MakePanelSprite("Gameplay_BoardBg", 64, 64, 24,
                PdUiKit.Hex("#0A1128"), PdUiKit.Hex("#0A1128"), PdUiKit.Hex("#FDC653"), 3f);
            var transparent = MakeTransparentSprite();

            var fund = db.SpriteDictionaries.FirstOrDefault(d => d.DictionaryName == "Fund");
            var boards = db.SpriteDictionaries.FirstOrDefault(d => d.DictionaryName == "Boards");
            if (fund == null) { Debug.LogError("COSMETIC-FIX: 'Fund' dictionary not found."); return; }
            if (boards == null) { Debug.LogError("COSMETIC-FIX: 'Boards' dictionary not found."); return; }

            Debug.Log($"COSMETIC-FIX: before -- Fund_Default={fund.SerializableDictionary["Fund_Default"]?.name}, Boards_Default={boards.SerializableDictionary["Boards_Default"]?.name}");

            fund.SerializableDictionary["Fund_Default"] = boardPanel;
            boards.SerializableDictionary["Boards_Default"] = transparent;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"COSMETIC-FIX: after -- Fund_Default={fund.SerializableDictionary["Fund_Default"]?.name}, Boards_Default={boards.SerializableDictionary["Boards_Default"]?.name}");
            Debug.Log("COSMETIC_FIX_DONE");
        }

        private static Sprite MakeTransparentSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(TransparentSpritePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/_ProDomino/Dashboard/Generated"))
                AssetDatabase.CreateFolder("Assets/_ProDomino/Dashboard", "Generated");
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            return PdUiKit.SaveSprite(TransparentSpritePath, tex);
        }
    }
}
