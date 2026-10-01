using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles the Delete Account pop-up to the dashboard look: dark gradient card with a thin
    /// border, Montserrat type, gold accent bar under the title, gold primary (Delete) button and a
    /// dark outlined secondary (Cancel) button. Objects are kept in place (only restyled and
    /// re-laid out) so DeleteAccountController / PopUp references and localization stay intact.
    /// Applied to the base prefab, the no-email variant and the instance in the main canvas, because
    /// the variant and the canvas both carry their own layout overrides.
    /// </summary>
    public static class DeleteAccountRestyler
    {
        private const string BasePath = "Assets/_ProDomino/_GameManager/Prefabs/DeleteAccountPopUp.prefab";
        private const string VariantPath = "Assets/_ProDomino/_GameManager/Prefabs/DeleteAccountPopUp_WithoutEmailVerification Variant.prefab";
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";

        private const float CardWidth = 560f;
        private const float Pad = 36f;
        private const float Inner = CardWidth - Pad * 2f;
        private const string AccentName = "DeleteAccountPopUp_TitleAccent";

        private static Sprite cardBg, accentBar, goldBtn, darkBtn, fieldBg, warnBg;

        [MenuItem("ProDomino/Dashboard/Restyle Delete Account Popup + Render")]
        public static void ApplyAndRender()
        {
            Apply();
            Render();
        }

        public static void Apply()
        {
            PrepareAssets();
            ApplyToPrefab(BasePath, root => Restyle(root.transform));
            ApplyToPrefab(VariantPath, root => Restyle(root.transform));
            ApplyToPrefab(CanvasPath, root =>
            {
                foreach (var ctrl in root.GetComponentsInChildren<GameSystem.DeleteAccountController>(true))
                    Restyle(ctrl.transform);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[DeleteAccountRestyler] Done.");
        }

        private static void PrepareAssets()
        {
            cardBg = MakePanelSprite("DeleteAccount_Card", 64, 64, (int)CardRadius + 4, Hex("#111625"), Hex("#0B0F1A"), ScreenCardBorder, 1.5f);
            accentBar = MakeRoundedSprite("DeleteAccount_AccentBar", 48, 6, 3, AccentStart, AccentEnd);
            goldBtn = MakeRoundedSprite("DeleteAccount_GoldBtn", 64, 52, (int)Radius, AccentStart, AccentEnd);
            darkBtn = MakePanelSprite("DeleteAccount_DarkBtn", 64, 52, (int)Radius, Hex("#1A2234"), Hex("#141B2A"), Hex("#2A354C"), 1.5f);
            fieldBg = MakePanelSprite("DeleteAccount_Field", 48, 48, (int)Radius, FieldFill, FieldFill, FieldBorder, 1f);
            warnBg = MakePanelSprite("DeleteAccount_WarnBox", 48, 48, (int)Radius, new Color(1f, 0.42f, 0.42f, 0.10f),
                new Color(1f, 0.42f, 0.42f, 0.10f), new Color(1f, 0.42f, 0.42f, 0.35f), 1f);
        }

        private static void ApplyToPrefab(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Restyle(Transform root)
        {
            var scaler = Need(root, "DeleteAccountPopUp_Scaler");
            var container = Need(scaler, "DeleteAccountPopUp_Container");

            // Dim backdrop, a bit darker than before so the card reads as the focus.
            var panel = root.Find("Panel");
            if (panel && panel.TryGetComponent<Image>(out var dim)) dim.color = new Color(0.004f, 0.004f, 0.047f, 0.78f);

            if (container.TryGetComponent<AspectRatioFitter>(out var arf)) arf.enabled = false;
            Stretch((RectTransform)container);

            var bg = Need(container, "DeleteAccountPopUp_Background");
            Stretch((RectTransform)bg);
            if (bg.TryGetComponent<Image>(out var bgImg))
            {
                bgImg.sprite = cardBg; bgImg.type = Image.Type.Sliced; bgImg.color = Color.white; bgImg.pixelsPerUnitMultiplier = 1f;
            }
            if (bg.TryGetComponent<Outline>(out var bgOutline)) bgOutline.enabled = false;
            if (bg.TryGetComponent<Shadow>(out var bgShadow)) bgShadow.enabled = false;

            float y = 34f;

            // Title
            var title = Need(container, "DeleteAccountPopUp_TitleText (TMP)").GetComponent<TextMeshProUGUI>();
            Text(title, null, Bold, 26f, TextPrimary, TextAlignmentOptions.Center);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true; title.fontSizeMin = 18f; title.fontSizeMax = 26f;
            TL((RectTransform)title.transform, Pad, y, Inner, 34f);
            y += 34f + 12f;

            // Gold accent bar under the title
            var accent = container.Find(AccentName);
            if (!accent)
            {
                accent = MakeImage(container, AccentName, accentBar, Color.white, Image.Type.Sliced).transform;
                accent.SetSiblingIndex(title.transform.GetSiblingIndex() + 1);
            }
            TL((RectTransform)accent, (CardWidth - 48f) / 2f, y, 48f, 4f);
            y += 4f + 24f;

            // Description (localized at runtime; size from the authored text plus headroom)
            var desc = Need(container, "DeleteAccountPopUp_MainDescriptionText (TMP)").GetComponent<TextMeshProUGUI>();
            Text(desc, null, Regular, 16f, TextLabel, TextAlignmentOptions.TopLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.lineSpacing = 6f;
            desc.enableAutoSizing = true; desc.fontSizeMin = 12f; desc.fontSizeMax = 16f;
            desc.overflowMode = TextOverflowModes.Overflow;
            float descH = Mathf.Ceil(desc.GetPreferredValues(desc.text, Inner, 0f).y) + 12f;
            TL((RectTransform)desc.transform, Pad, y, Inner, descH);
            y += descH + 18f;

            // Divider
            var divider = Need(container, "DeleteAccountPopUp_Divider");
            if (divider.TryGetComponent<Image>(out var divImg)) { divImg.sprite = null; divImg.color = Divider; }
            if (divider.TryGetComponent<Outline>(out var divOutline)) divOutline.enabled = false;
            TL((RectTransform)divider, Pad, y, Inner, 1f);
            y += 1f + 18f;

            // Email confirmation (hidden in the no-email variant)
            var emailLabel = Need(container, "DeleteAccountPopUp_ConfirmEmailText (TMP)");
            var emailField = Need(container, "DeleteAccountPopUp_ConfirmEmailInputfield (TMP)");
            if (emailLabel.gameObject.activeSelf)
            {
                var t = emailLabel.GetComponent<TextMeshProUGUI>();
                Text(t, null, Medium, 15f, TextMuted, TextAlignmentOptions.MidlineLeft);
                t.textWrappingMode = TextWrappingModes.Normal;
                TL((RectTransform)emailLabel, Pad, y, Inner, 22f);
                y += 22f + 8f;
            }
            if (emailField.gameObject.activeSelf)
            {
                SetSprite(emailField, fieldBg);
                if (emailField.TryGetComponent<Image>(out var fImg)) fImg.color = Color.white;
                if (emailField.TryGetComponent<Outline>(out var fOutline)) fOutline.enabled = false;
                if (emailField.TryGetComponent<TMP_InputField>(out var input))
                {
                    if (input.textComponent is TextMeshProUGUI txt) Text(txt, null, Medium, 16f, TextPrimary, TextAlignmentOptions.MidlineLeft);
                    if (input.placeholder is TextMeshProUGUI ph) Text(ph, null, Regular, 16f, TextPlaceholder, TextAlignmentOptions.MidlineLeft);
                    var area = input.textViewport;
                    if (area) { Stretch(area); area.offsetMin = new Vector2(16f, 4f); area.offsetMax = new Vector2(-16f, -4f); }
                }
                TL((RectTransform)emailField, Pad, y, Inner, FieldHeight);
                y += FieldHeight + 16f;
            }

            // Final warning, in a soft red box
            var warn = Need(container, "DeleteAccountPopUp_LastWarningText (TMP)").GetComponent<TextMeshProUGUI>();
            Text(warn, null, SemiBold, 14f, Danger, TextAlignmentOptions.Center);
            warn.textWrappingMode = TextWrappingModes.Normal;
            warn.enableAutoSizing = true; warn.fontSizeMin = 11f; warn.fontSizeMax = 14f;
            warn.margin = new Vector4(16f, 10f, 16f, 10f);
            float warnH = Mathf.Ceil(warn.GetPreferredValues(warn.text, Inner - 32f, 0f).y) + 26f;
            TL((RectTransform)warn.transform, Pad, y, Inner, warnH);
            var warnBox = container.Find("DeleteAccountPopUp_WarningBox");
            if (!warnBox)
            {
                warnBox = MakeImage(container, "DeleteAccountPopUp_WarningBox", warnBg, Color.white, Image.Type.Sliced).transform;
                warnBox.SetSiblingIndex(warn.transform.GetSiblingIndex());
            }
            TL((RectTransform)warnBox, Pad, y, Inner, warnH);
            y += warnH + 24f;

            // Buttons: Cancel (dark, left) and Delete (gold, right)
            const float btnH = 52f, gap = 16f;
            float btnW = (Inner - gap) / 2f;
            foreach (var btn in container.GetComponentsInChildren<Button>(true).Where(b => b.transform.parent == container))
            {
                bool cancel = btn.name.ToLowerInvariant().Contains("cancel");
                StyleButton(btn, cancel ? darkBtn : goldBtn, cancel ? TextPrimary : OnAccent);
                TL((RectTransform)btn.transform, cancel ? Pad : Pad + btnW + gap, y, btnW, btnH);
            }
            y += btnH + 32f;

            // Card: fixed width, height from content, centred in the pop-up
            var srt = (RectTransform)scaler;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(CardWidth, y);
        }

        private static void StyleButton(Button btn, Sprite sprite, Color labelColor)
        {
            if (btn.targetGraphic is Image img)
            {
                img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f;
                foreach (var fx in img.GetComponents<Shadow>()) fx.enabled = false;
            }
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label)
            {
                Text(label, null, Bold, 17f, labelColor, TextAlignmentOptions.Center);
                label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 17f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
        }

        public static void Render()
        {
            string outDir = System.Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "delete_account_popup.png");

            SidebarRestyler.RenderCanvas(outPath, 1920, 1080, true, root =>
            {
                var ctrl = root.GetComponentsInChildren<GameSystem.DeleteAccountController>(true).FirstOrDefault();
                if (!ctrl) { Debug.LogWarning("[DeleteAccountRestyler] Pop-up not found in canvas"); return; }
                for (var t = ctrl.transform; t; t = t.parent) t.gameObject.SetActive(true);
                var cg = ctrl.GetComponent<CanvasGroup>();
                if (cg) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
                ctrl.transform.SetAsLastSibling();
            });
            Debug.Log($"[DeleteAccountRestyler] Rendered {outPath}");
        }
    }
}
