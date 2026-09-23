using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.Shared;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles SettingsController_PopUp.prefab to match the Figma reference (media_1790146267560.png):
    /// - Modal Card (630x440, #111625 -> #0D121F, radius 16px, 1.2px border #1E2538)
    /// - Domino Watermark pattern on top-left (opacity ~0.26)
    /// - Top-right Close Button (rounded square #1E2538, border #2E3A52, 16px X icon)
    /// - Header: "Settings" (Montserrat-Bold 26px) + Subtitle (Montserrat-Regular 13px, #94A3B8)
    /// - Audio Card (550x84, #131826):
    ///     - Speaker icon on left (30x30, clickable to mute/unmute)
    ///     - Master Volume slider (dark capsule track #222B3D, 18px height, golden fill #FFA800, white rounded pill handle 10x24px)
    ///     - Percentage label on right ("30%", Montserrat-Bold 20px, #FFA800)
    /// - Two side-by-side action buttons (550x52):
    ///     - "Learning to Play" (navigates to Learn)
    ///     - "EULA Agreement" (navigates to Help/Rules)
    /// - Version footer: "VERSION 0.7015" (Montserrat-Medium 11px, #64748B)
    /// </summary>
    public static class SettingsRestyler
    {
        private const string SettingsPrefabPath = "Assets/_ProDomino/Shared/Prefabs/Settings/SettingsController_PopUp.prefab";
        private const string MiddleScreenPrefabPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;
        private static Sprite modalBg, sliderCardBg, sliderTrackBg, sliderFillBg, sliderHandleBg;
        private static Sprite btnNormalBg, btnHoverBg, closeBtnBg;
        private static Sprite watermarkSprite, speakerOnSprite, speakerMuteSprite, closeIconSprite;

        [MenuItem("ProDomino/Dashboard/Restyle Settings Popup + Render")]
        public static void ApplyAndRender()
        {
            Debug.Log("[SettingsRestyler] Starting Settings Popup restyling...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            PrepareAssets();
            BuildSettingsPrefab();
            EnsureInMiddleScreen();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderScreenshots();
            Debug.Log("[SettingsRestyler] SUCCESS: Settings popup restyled and rendered successfully!");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            // Modal card background (64x64, radius 16, #111625 -> #0D121F, border #1E2538)
            modalBg = MakePanelSprite("Settings_Modal_Bg", 64, 64, 16, Hex("#111625"), Hex("#0D121F"), Hex("#1E2538"), 1.2f, true);

            // Slider card container (48x48, radius 12, #131826 -> #0E1320, border #1F2637)
            sliderCardBg = MakePanelSprite("Settings_Slider_Card_Bg", 48, 48, 12, Hex("#131826"), Hex("#0E1320"), Hex("#1F2637"), 1f, true);

            // Slider track (36x18, capsule radius 9, #222B3D)
            sliderTrackBg = MakePanelSprite("Settings_Slider_Track_Bg", 36, 18, 9, Hex("#222B3D"), Hex("#222B3D"), Color.clear, 0f);

            // Slider fill (36x18, capsule radius 9, golden gradient #FFA000 -> #FFC107, horizontal)
            sliderFillBg = MakePanelSprite("Settings_Slider_Fill_Bg", 36, 18, 9, Hex("#FFA000"), Hex("#FFC107"), Color.clear, 0f, false);

            // Slider handle (white vertical pill 18x32, radius 7, border #CBD5E1)
            sliderHandleBg = MakePanelSprite("Settings_Slider_Handle", 18, 32, 7, Hex("#FFFFFF"), Hex("#F8FAFC"), Hex("#CBD5E1"), 1f, true);

            // Action buttons (radius 10, #1A2234, border #2A354C)
            btnNormalBg = MakePanelSprite("Settings_Button_Normal", 48, 48, 10, Hex("#1A2234"), Hex("#161D2B"), Hex("#2A354C"), 1.2f, true);
            btnHoverBg = MakePanelSprite("Settings_Button_Hover", 48, 48, 10, Hex("#222C44"), Hex("#1C2538"), Hex("#3B4A6B"), 1.2f, true);

            // Close button (radius 8, #1E2538, border #2E3A52)
            closeBtnBg = MakePanelSprite("Settings_Close_Btn_Bg", 36, 36, 8, Hex("#1E2538"), Hex("#1E2538"), Hex("#2E3A52"), 1f);

            // Watermark & icons
            watermarkSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedDir}/Rules_Domino_Watermark.png");
            speakerOnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ProDomino/_UI/Icons/Icons_Base_128/Sound_icon_off.png");
            speakerMuteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ProDomino/_UI/Icons/Icons_Base_128/Sound_Off_Icon.png");
            closeIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ProDomino/_UI/Icons/Icons_Base_64/X_icon.png");
        }

        private static void BuildSettingsPrefab()
        {
            var rootGo = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
            try
            {
                var rootRt = rootGo.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
                rootRt.pivot = new Vector2(0.5f, 0.5f);

                var settingsCtrl = rootGo.GetComponent<SettingsController>();
                if (settingsCtrl == null)
                    settingsCtrl = rootGo.AddComponent<SettingsController>();

                var rootCg = rootGo.GetComponent<CanvasGroup>();
                if (rootCg == null)
                    rootCg = rootGo.AddComponent<CanvasGroup>();
                rootCg.alpha = 0f;
                rootCg.interactable = false;
                rootCg.blocksRaycasts = false;

                // Clean up previous children completely for a pristine rebuild
                for (int i = rootGo.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(rootGo.transform.GetChild(i).gameObject);
                }

                // 1. Dim Backdrop
                var backdropGo = new GameObject("SettingsController_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                backdropGo.layer = rootGo.layer;
                backdropGo.transform.SetParent(rootGo.transform, false);
                var backdropRt = backdropGo.GetComponent<RectTransform>();
                backdropRt.anchorMin = Vector2.zero;
                backdropRt.anchorMax = Vector2.one;
                backdropRt.offsetMin = Vector2.zero;
                backdropRt.offsetMax = Vector2.zero;
                var backdropImg = backdropGo.GetComponent<Image>();
                backdropImg.color = new Color(0f, 0f, 0f, 0.65f);
                backdropImg.raycastTarget = true;
                var backdropBtn = backdropGo.GetComponent<Button>();
                backdropBtn.transition = Selectable.Transition.None;

                // 2. Modal Container (630x440)
                var containerGo = new GameObject("SettingsController_Container", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                containerGo.layer = rootGo.layer;
                containerGo.transform.SetParent(rootGo.transform, false);
                var containerRt = containerGo.GetComponent<RectTransform>();
                containerRt.anchorMin = new Vector2(0.5f, 0.5f);
                containerRt.anchorMax = new Vector2(0.5f, 0.5f);
                containerRt.pivot = new Vector2(0.5f, 0.5f);
                containerRt.anchoredPosition = Vector2.zero;
                containerRt.sizeDelta = new Vector2(630f, 440f);

                var containerImg = containerGo.GetComponent<Image>();
                containerImg.sprite = modalBg;
                containerImg.type = Image.Type.Sliced;
                containerImg.color = Color.white;
                containerImg.raycastTarget = true;

                // 2a. Domino Watermark (Top-Left)
                if (watermarkSprite != null)
                {
                    var wmGo = new GameObject("Watermark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    wmGo.layer = rootGo.layer;
                    wmGo.transform.SetParent(containerGo.transform, false);
                    var wmRt = wmGo.GetComponent<RectTransform>();
                    wmRt.anchorMin = new Vector2(0f, 1f);
                    wmRt.anchorMax = new Vector2(0f, 1f);
                    wmRt.pivot = new Vector2(0f, 1f);
                    wmRt.anchoredPosition = new Vector2(0f, 0f);
                    wmRt.sizeDelta = new Vector2(250f, 250f);
                    var wmImg = wmGo.GetComponent<Image>();
                    wmImg.sprite = watermarkSprite;
                    wmImg.preserveAspect = true;
                    wmImg.color = new Color(1f, 1f, 1f, 0.26f);
                    wmImg.raycastTarget = false;
                }

                // 2b. Close Button (Top-Right)
                var closeBtnGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeBtnGo.layer = rootGo.layer;
                closeBtnGo.transform.SetParent(containerGo.transform, false);
                var closeBtnRt = closeBtnGo.GetComponent<RectTransform>();
                closeBtnRt.anchorMin = new Vector2(1f, 1f);
                closeBtnRt.anchorMax = new Vector2(1f, 1f);
                closeBtnRt.pivot = new Vector2(1f, 1f);
                closeBtnRt.anchoredPosition = new Vector2(-22f, -22f);
                closeBtnRt.sizeDelta = new Vector2(38f, 38f);

                var closeBtnImg = closeBtnGo.GetComponent<Image>();
                closeBtnImg.sprite = closeBtnBg;
                closeBtnImg.type = Image.Type.Sliced;
                closeBtnImg.color = Color.white;
                closeBtnImg.raycastTarget = true;

                var closeBtn = closeBtnGo.GetComponent<Button>();
                closeBtn.targetGraphic = closeBtnImg;
                closeBtn.transition = Selectable.Transition.SpriteSwap;
                var cbState = closeBtn.spriteState;
                cbState.highlightedSprite = btnHoverBg;
                cbState.pressedSprite = btnHoverBg;
                closeBtn.spriteState = cbState;

                if (closeIconSprite != null)
                {
                    var closeIconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    closeIconGo.layer = rootGo.layer;
                    closeIconGo.transform.SetParent(closeBtnGo.transform, false);
                    var ciRt = closeIconGo.GetComponent<RectTransform>();
                    ciRt.anchorMin = new Vector2(0.5f, 0.5f);
                    ciRt.anchorMax = new Vector2(0.5f, 0.5f);
                    ciRt.pivot = new Vector2(0.5f, 0.5f);
                    ciRt.anchoredPosition = Vector2.zero;
                    ciRt.sizeDelta = new Vector2(16f, 16f);
                    var ciImg = closeIconGo.GetComponent<Image>();
                    ciImg.sprite = closeIconSprite;
                    ciImg.color = Hex("#E2E8F0");
                    ciImg.preserveAspect = true;
                    ciImg.raycastTarget = false;
                }

                // 2c. Title & Subtitle
                var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.layer = rootGo.layer;
                titleGo.transform.SetParent(containerGo.transform, false);
                var titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -36f);
                titleRt.sizeDelta = new Vector2(500f, 34f);

                var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
                titleTmp.text = "Settings";
                titleTmp.font = fBold;
                titleTmp.fontSize = 26f;
                titleTmp.color = Color.white;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.raycastTarget = false;

                var subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                subGo.layer = rootGo.layer;
                subGo.transform.SetParent(containerGo.transform, false);
                var subRt = subGo.GetComponent<RectTransform>();
                subRt.anchorMin = new Vector2(0f, 1f);
                subRt.anchorMax = new Vector2(1f, 1f);
                subRt.pivot = new Vector2(0.5f, 1f);
                subRt.anchoredPosition = new Vector2(0f, -74f);
                subRt.sizeDelta = new Vector2(500f, 22f);

                var subTmp = subGo.GetComponent<TextMeshProUGUI>();
                subTmp.text = "Start playing ProDomino with friends & random opponents.";
                subTmp.font = fRegular;
                subTmp.fontSize = 13f;
                subTmp.color = Hex("#94A3B8");
                subTmp.alignment = TextAlignmentOptions.Center;
                subTmp.raycastTarget = false;

                // 2d. Audio Card Container (550x84)
                var audioCardGo = new GameObject("AudioCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                audioCardGo.layer = rootGo.layer;
                audioCardGo.transform.SetParent(containerGo.transform, false);
                var acRt = audioCardGo.GetComponent<RectTransform>();
                acRt.anchorMin = new Vector2(0.5f, 1f);
                acRt.anchorMax = new Vector2(0.5f, 1f);
                acRt.pivot = new Vector2(0.5f, 1f);
                acRt.anchoredPosition = new Vector2(0f, -118f);
                acRt.sizeDelta = new Vector2(550f, 84f);

                var acImg = audioCardGo.GetComponent<Image>();
                acImg.sprite = sliderCardBg;
                acImg.type = Image.Type.Sliced;
                acImg.color = Color.white;
                acImg.raycastTarget = true;

                // Audio Card: Speaker Button
                var speakerBtnGo = new GameObject("SpeakerButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                speakerBtnGo.layer = rootGo.layer;
                speakerBtnGo.transform.SetParent(audioCardGo.transform, false);
                var spkRt = speakerBtnGo.GetComponent<RectTransform>();
                spkRt.anchorMin = new Vector2(0f, 0.5f);
                spkRt.anchorMax = new Vector2(0f, 0.5f);
                spkRt.pivot = new Vector2(0f, 0.5f);
                spkRt.anchoredPosition = new Vector2(24f, 0f);
                spkRt.sizeDelta = new Vector2(36f, 36f);

                var spkBtnImg = speakerBtnGo.GetComponent<Image>();
                spkBtnImg.color = Color.clear; // invisible click surface
                spkBtnImg.raycastTarget = true;

                var spkBtn = speakerBtnGo.GetComponent<Button>();
                spkBtn.transition = Selectable.Transition.None;

                var spkIconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                spkIconGo.layer = rootGo.layer;
                spkIconGo.transform.SetParent(speakerBtnGo.transform, false);
                var spkIconRt = spkIconGo.GetComponent<RectTransform>();
                spkIconRt.anchorMin = new Vector2(0.5f, 0.5f);
                spkIconRt.anchorMax = new Vector2(0.5f, 0.5f);
                spkIconRt.pivot = new Vector2(0.5f, 0.5f);
                spkIconRt.anchoredPosition = Vector2.zero;
                spkIconRt.sizeDelta = new Vector2(30f, 30f);

                var spkIconImg = spkIconGo.GetComponent<Image>();
                spkIconImg.sprite = speakerOnSprite;
                spkIconImg.preserveAspect = true;
                spkIconImg.color = Color.white;
                spkIconImg.raycastTarget = false;

                // Audio Card: Percentage Text
                var pctGo = new GameObject("PercentageText", typeof(RectTransform), typeof(TextMeshProUGUI));
                pctGo.layer = rootGo.layer;
                pctGo.transform.SetParent(audioCardGo.transform, false);
                var pctRt = pctGo.GetComponent<RectTransform>();
                pctRt.anchorMin = new Vector2(1f, 0.5f);
                pctRt.anchorMax = new Vector2(1f, 0.5f);
                pctRt.pivot = new Vector2(1f, 0.5f);
                pctRt.anchoredPosition = new Vector2(-24f, 0f);
                pctRt.sizeDelta = new Vector2(60f, 32f);

                var pctTmp = pctGo.GetComponent<TextMeshProUGUI>();
                pctTmp.text = "30%";
                pctTmp.font = fBold;
                pctTmp.fontSize = 20f;
                pctTmp.color = Hex("#FFA800");
                pctTmp.alignment = TextAlignmentOptions.Right;
                pctTmp.raycastTarget = false;

                // Audio Card: Master Slider
                var sliderGo = new GameObject("MasterSlider", typeof(RectTransform), typeof(Slider));
                sliderGo.layer = rootGo.layer;
                sliderGo.transform.SetParent(audioCardGo.transform, false);
                var sliderRt = sliderGo.GetComponent<RectTransform>();
                sliderRt.anchorMin = new Vector2(0f, 0.5f);
                sliderRt.anchorMax = new Vector2(1f, 0.5f);
                sliderRt.pivot = new Vector2(0.5f, 0.5f);
                sliderRt.offsetMin = new Vector2(76f, -14f);
                sliderRt.offsetMax = new Vector2(-96f, 14f);

                var slider = sliderGo.GetComponent<Slider>();
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0.3f;
                slider.wholeNumbers = false;

                // Slider Track (18px height)
                var trackGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                trackGo.layer = rootGo.layer;
                trackGo.transform.SetParent(sliderGo.transform, false);
                var trackRt = trackGo.GetComponent<RectTransform>();
                trackRt.anchorMin = new Vector2(0f, 0.5f);
                trackRt.anchorMax = new Vector2(1f, 0.5f);
                trackRt.pivot = new Vector2(0.5f, 0.5f);
                trackRt.anchoredPosition = Vector2.zero;
                trackRt.sizeDelta = new Vector2(0f, 18f);

                var trackImg = trackGo.GetComponent<Image>();
                trackImg.sprite = sliderTrackBg;
                trackImg.type = Image.Type.Sliced;
                trackImg.color = Color.white;
                trackImg.raycastTarget = true;

                // Fill Area
                var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
                fillAreaGo.layer = rootGo.layer;
                fillAreaGo.transform.SetParent(sliderGo.transform, false);
                var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
                fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
                fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
                fillAreaRt.pivot = new Vector2(0.5f, 0.5f);
                fillAreaRt.anchoredPosition = Vector2.zero;
                fillAreaRt.sizeDelta = new Vector2(0f, 18f);

                var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGo.layer = rootGo.layer;
                fillGo.transform.SetParent(fillAreaGo.transform, false);
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;

                var fillImg = fillGo.GetComponent<Image>();
                fillImg.sprite = sliderFillBg;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = Color.white;
                fillImg.raycastTarget = false;

                // Handle Slide Area
                var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleAreaGo.layer = rootGo.layer;
                handleAreaGo.transform.SetParent(sliderGo.transform, false);
                var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
                handleAreaRt.anchorMin = new Vector2(0f, 0f);
                handleAreaRt.anchorMax = new Vector2(1f, 1f);
                handleAreaRt.offsetMin = new Vector2(5f, 0f);
                handleAreaRt.offsetMax = new Vector2(-5f, 0f);

                var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handleGo.layer = rootGo.layer;
                handleGo.transform.SetParent(handleAreaGo.transform, false);
                var handleRt = handleGo.GetComponent<RectTransform>();
                handleRt.anchorMin = new Vector2(0.3f, 0.5f);
                handleRt.anchorMax = new Vector2(0.3f, 0.5f);
                handleRt.pivot = new Vector2(0.5f, 0.5f);
                handleRt.sizeDelta = new Vector2(10f, 24f);

                var handleImg = handleGo.GetComponent<Image>();
                handleImg.sprite = sliderHandleBg;
                handleImg.type = Image.Type.Sliced;
                handleImg.color = Color.white;
                handleImg.raycastTarget = true;

                slider.targetGraphic = handleImg;
                slider.fillRect = fillRt;
                slider.handleRect = handleRt;

                // 2e. Action Buttons Row (550x52)
                var actRowGo = new GameObject("ActionsRow", typeof(RectTransform));
                actRowGo.layer = rootGo.layer;
                actRowGo.transform.SetParent(containerGo.transform, false);
                var actRowRt = actRowGo.GetComponent<RectTransform>();
                actRowRt.anchorMin = new Vector2(0.5f, 1f);
                actRowRt.anchorMax = new Vector2(0.5f, 1f);
                actRowRt.pivot = new Vector2(0.5f, 1f);
                actRowRt.anchoredPosition = new Vector2(0f, -224f);
                actRowRt.sizeDelta = new Vector2(550f, 52f);

                // Button 1: "Learning to Play"
                var learnBtnGo = CreateActionButton(actRowGo.transform, "LearningButton", "Learning to Play",
                    new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-8f, 0f));

                // Button 2: "EULA Agreement"
                var eulaBtnGo = CreateActionButton(actRowGo.transform, "EulaButton", "EULA Agreement",
                    new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(0f, 0f));

                // 2f. Version Footer
                var verGo = new GameObject("VersionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                verGo.layer = rootGo.layer;
                verGo.transform.SetParent(containerGo.transform, false);
                var verRt = verGo.GetComponent<RectTransform>();
                verRt.anchorMin = new Vector2(0.5f, 0f);
                verRt.anchorMax = new Vector2(0.5f, 0f);
                verRt.pivot = new Vector2(0.5f, 0f);
                verRt.anchoredPosition = new Vector2(0f, 26f);
                verRt.sizeDelta = new Vector2(300f, 20f);

                var verTmp = verGo.GetComponent<TextMeshProUGUI>();
                verTmp.text = "VERSION 0.7015";
                verTmp.font = fMedium;
                verTmp.fontSize = 11.5f;
                verTmp.characterSpacing = 2f;
                verTmp.color = Hex("#64748B");
                verTmp.alignment = TextAlignmentOptions.Center;
                verTmp.raycastTarget = false;

                // Wire Serialized Properties to SettingsController
                var so = new SerializedObject(settingsCtrl);
                so.FindProperty("rootCanvasGroup").objectReferenceValue = rootCg;
                so.FindProperty("masterSlider").objectReferenceValue = slider;
                so.FindProperty("volumePercentLabel").objectReferenceValue = pctTmp;
                so.FindProperty("speakerButton").objectReferenceValue = spkBtn;
                so.FindProperty("speakerIcon").objectReferenceValue = spkIconImg;
                so.FindProperty("speakerOnSprite").objectReferenceValue = speakerOnSprite;
                so.FindProperty("speakerMuteSprite").objectReferenceValue = speakerMuteSprite;
                so.FindProperty("learningToPlayButton").objectReferenceValue = learnBtnGo.GetComponent<Button>();
                so.FindProperty("eulaAgreementButton").objectReferenceValue = eulaBtnGo.GetComponent<Button>();
                so.FindProperty("closeButton").objectReferenceValue = closeBtn;
                so.FindProperty("backgroundCloseButton").objectReferenceValue = backdropBtn;
                so.FindProperty("gameVersionLabel").objectReferenceValue = verTmp;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(rootGo, SettingsPrefabPath);
                Debug.Log($"[SettingsRestyler] Saved updated prefab to {SettingsPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootGo);
            }
        }

        private static GameObject CreateActionButton(Transform parent, string goName, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var btnGo = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.layer = parent.gameObject.layer;
            btnGo.transform.SetParent(parent, false);

            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var img = btnGo.GetComponent<Image>();
            img.sprite = btnNormalBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var st = btn.spriteState;
            st.highlightedSprite = btnHoverBg;
            st.pressedSprite = btnHoverBg;
            btn.spriteState = st;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = parent.gameObject.layer;
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = fSemiBold;
            tmp.fontSize = 15f;
            tmp.color = Hex("#E2E8F0");
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btnGo;
        }

        private static void EnsureInMiddleScreen()
        {
            var middleGo = PrefabUtility.LoadPrefabContents(MiddleScreenPrefabPath);
            try
            {
                // Verify or update the SettingsController_PopUp instance under MiddleScreen_Scalable
                var popUpInstance = middleGo.transform.Find("MiddleScreen_Scalable/SettingsController_PopUp")
                                 ?? PdUiKit.FindDeep(middleGo.transform, "SettingsController_PopUp");

                if (popUpInstance != null)
                {
                    var rt = popUpInstance.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    Debug.Log("[SettingsRestyler] Verified SettingsController_PopUp instance in MiddleScreen_Scalable");
                }

                PrefabUtility.SaveAsPrefabAsset(middleGo, MiddleScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleGo);
            }
        }

        private static void RenderScreenshots()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            string outPath = Path.Combine(outDir, "screen_settings.png");

            SidebarRestyler.RenderCanvas(outPath, 1920, 1080, true, root =>
            {
                // Show SettingsController_PopUp over the dashboard
                var settingsPopUp = PdUiKit.FindDeep(root.transform, "SettingsController_PopUp");
                if (settingsPopUp != null)
                {
                    settingsPopUp.gameObject.SetActive(true);
                    var cg = settingsPopUp.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 1f;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    }

                    var ctrl = settingsPopUp.GetComponent<SettingsController>();
                    if (ctrl != null)
                    {
                        ctrl.LoadValues();
                    }
                }
                else
                {
                    Debug.LogWarning("[SettingsRestyler] Could not find SettingsController_PopUp in rendered canvas root!");
                }
            });

            Debug.Log($"[SettingsRestyler] Rendered settings screenshot to {outPath}");
        }
    }
}
