using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Readability pass for the Login and Create Account cards. The design's 16 px body text came
    // out soft, especially once FitInArea scales the card down on smaller windows, so the text is
    // set a step larger, drawn with sharper-edged copies of the Montserrat materials, and the boxes
    // holding it are grown to fit. Only text on those two cards is touched; layout, colours and
    // every other screen sharing the fonts stay as they are. Safe to run again.
    internal static class AuthTextClarity
    {
        private const string AuthPath = "Assets/_ProDomino/Authentication/Prefabs/AuthUI.prefab";
        private static readonly string[] Screens = { "SignIn_Container", "SignUp_Container" };

        // Text sizes (the design's 14 / 16 / 24 / 36, one step up) and the line boxes they need.
        private const float Body = 18f, Feedback = 16f, ButtonText = 26f, Heading = 38f;
        private const float LabelH = 22f, HeadingH = 46f;
        private const float SubtitleW = 400f, SubtitleH = 44f;   // two lines of Body

        private static readonly System.Collections.Generic.HashSet<string> BoldTexts = new()
        {
            "SignIn_Description_Text", "SignIn_Password_Feedback", "SignIn_Feedback",
            "SignUp_Description_Text", "SignUp_Username_Feedback", "SignUp_Email_Feedback",
            "SignUp_TermAndConditions_Toggle", "SignUp_DataTreatment_Toggle",
        };

        // Edge sharpness for the auth materials. 0 is the TMP default, which at body sizes on a
        // dark card blurs the stroke edges into the background.
        private const float Sharpness = 0.3f;

        [MenuItem("ProDomino/Dashboard/Sharpen Login + Register Text")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(AuthPath);
            try
            {
                var container = root.transform.Find("Auth_Container");
                foreach (var name in Screens)
                {
                    var screen = container ? container.Find(name) : null;
                    if (!screen) { Debug.LogWarning($"AUTH_TEXT: {name} not found."); continue; }
                    ApplyToScreen(screen);
                }
                PrefabUtility.SaveAsPrefabAsset(root, AuthPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("AUTH_TEXT_DONE");
        }

        private static void ApplyToScreen(Transform screen)
        {
            // Validation messages: the texts HideWhenEmpty switches on and off.
            var feedback = screen.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m && m.GetType().Name == "HideWhenEmpty")
                .Select(m => new SerializedObject(m).FindProperty("target")?.objectReferenceValue as TextMeshProUGUI)
                .Where(t => t)
                .ToHashSet();

            foreach (var t in screen.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!t.gameObject.activeInHierarchy && !feedback.Contains(t)) continue;   // unused legacy widgets

                var name = t.name;
                var parent = t.transform.parent ? t.transform.parent.name : "";
                float size = Body;

                if (feedback.Contains(t)) size = Feedback;
                else if (name.EndsWith("_Header_Text"))
                {
                    size = Heading;
                    Height(t.transform, HeadingH);
                    var row = t.transform.parent;
                    if (row && row.name == "Auth_TitleRow")
                    {
                        Height(row, HeadingH);
                        var icon = row.Find("Auth_TitleIcon") as RectTransform;
                        if (icon) icon.sizeDelta = new Vector2(Heading, Heading);   // the 👋 keeps pace with the words
                    }
                }
                else if (name.EndsWith("_Description_Text")) Slot(t.transform, SubtitleW, SubtitleH);
                else if (name.EndsWith("_Label") && parent.EndsWith("_Group")) Height(t.transform, LabelH);
                else if (parent == "SignIn_Button" || parent == "SignUp_Button") size = ButtonText;
                else if (parent.EndsWith("_Google_Button") || parent.EndsWith("_Facebook_Button"))
                    Height(t.transform, LabelH);   // the label's line inside the icon + label row

                // Set bold at 18 by request, so a re-run (or a full restyle) keeps them that way.
                // A toggle's text is its "Label" child, so it is matched through the toggle's name.
                // The sign-up password message is also called "SignIn_Feedback"; only the login one is meant.
                bool signUpCopy = name == "SignIn_Feedback" && screen.name != "SignIn_Container";
                if (!signUpCopy && (BoldTexts.Contains(name) || (name == "Label" && BoldTexts.Contains(parent))))
                {
                    size = 18f;
                    t.fontStyle = FontStyles.Bold;
                    Debug.Log($"AUTH_TEXT: bold 18 -> {parent}/{name}");
                }

                t.fontSize = size;
                // A few labels were squeezed together (-7 to -9) when they were first fitted.
                t.characterSpacing = 0f;
                t.fontSharedMaterial = SharpMaterial(t.font);
            }
        }

        private static void Height(Transform t, float h)
        {
            var rt = (RectTransform)t;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            if (t.TryGetComponent<LayoutElement>(out var le) && le.preferredHeight >= 0f) le.preferredHeight = h;
        }

        // A copy of the font's own material with crisper edges, saved next to the font so TMP lists
        // it as one of that font's presets. Atlases the dynamic font adds later inherit it.
        private static Material SharpMaterial(TMP_FontAsset font)
        {
            if (!font || !font.material) return font ? font.material : null;
            var path = $"{FontDir}/{font.name} - Auth Sharp.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(font.material) { name = $"{font.name} - Auth Sharp" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_Sharpness, Sharpness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
