using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.NotificationSystem;
using HelperSharedLibrary;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles the Notification Drawer and Notification Cards to match the Figma reference (media_1790154812121.png):
    /// - Right-side flyout drawer (330x720, #0C101D -> #070A14, border #1E2538, radius 16px, sortingOrder 60)
    /// - Header: Golden bell icon + "Notification" (Montserrat-Bold 17px) + dark square Close button (X icon)
    /// - Section Headers: "Today" and "Older Notification" (Montserrat-SemiBold 12px, #94A3B8)
    /// - Notification Cards (#111625 -> #0D111D, border #1F2639, radius 10px, 52px height):
    ///     - Category label ("Friends Notification", "Club Membership", etc., white 11px)
    ///     - Avatar circular thumbnail (28x28)
    ///     - Message body ("Emma accepted your friend request.", etc., #94A3B8 10.5px)
    ///     - Golden timestamp ("12:00PM", #F59E0B) placed at bottom-right
    /// - Empty State: "No notifications yet" (#64748B)
    /// </summary>
    public static class NotificationRestyler
    {
        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
        private const string NotifPrefabPath = "Assets/_ProDomino/Prefabs/UI/Notifications_Scalable.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/Prefabs/UI/Notif_Container.prefab";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;
        private static Sprite drawerBg, cardBg, closeBtnBg, avatarCircle;
        private static Sprite acceptBtnBg, declineBtnBg;
        private static Sprite bellIcon, closeIcon, defaultAvatar;

        [MenuItem("ProDomino/Dashboard/Restyle Notifications + Render")]
        public static void ApplyAndRender()
        {
            Debug.Log("[NotificationRestyler] Starting Notification screen restyling...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            PrepareAssets();
            RestyleNotificationCardPrefab();
            RestyleNotificationsScalablePrefab();
            ApplyToScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderScreenshots();
            Debug.Log("[NotificationRestyler] SUCCESS: Notification screen restyled and rendered successfully!");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            // Drawer panel background (64x64, radius 16, #0C101D -> #070A14, border #1E2538)
            drawerBg = MakePanelSprite("Notification_Drawer_Bg", 64, 64, 16, Hex("#0C101D"), Hex("#070A14"), Hex("#1E2538"), 1.2f, true);

            // Card background (64x64, radius 10, #111625 -> #0D111D, border #1F2639)
            cardBg = MakePanelSprite("Notification_Card_Bg", 64, 64, 10, Hex("#111625"), Hex("#0D111D"), Hex("#1F2639"), 1.0f, true);

            // Close button (48x48, radius 8, #1E2538, border #2E3A52)
            closeBtnBg = MakePanelSprite("Notification_Close_Btn", 48, 48, 8, Hex("#1E2538"), Hex("#1E2538"), Hex("#2E3A52"), 1.0f);

            // Accept button (golden horizontal gradient #FFA000 -> #FFBD1E, radius 6)
            acceptBtnBg = MakePanelSprite("Notification_Btn_Accept", 48, 24, 6, Hex("#FFA000"), Hex("#FFBD1E"), Hex("#FFD54F"), 0.8f, false);

            // Decline button (dark #1E2538, border #2E3A52, radius 6)
            declineBtnBg = MakePanelSprite("Notification_Btn_Decline", 48, 24, 6, Hex("#1E2538"), Hex("#161D2B"), Hex("#2E3A52"), 0.8f, true);

            // Circular avatar mask/sprite (28x28 circle)
            avatarCircle = MakeCircleSprite("Notification_Avatar_Circle", 28, Hex("#1E2538"), Hex("#334155"), 1.0f);

            // Icons
            bellIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Header_Bell.png");
            closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_ProDomino/_UI/Icons/Icons_Base_64/X_icon.png");
            defaultAvatar = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Nav_FriendsList.png");
        }

        private static Sprite MakeCircleSprite(string name, int size, Color fill, Color border, float borderWidth)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f - 1f;
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float inside = Mathf.Clamp01(radius - dist + 0.5f);
                var c = fill;
                if (borderWidth > 0f)
                {
                    float borderMask = Mathf.Clamp01(borderWidth + 0.5f - (radius - dist));
                    c = Color.Lerp(fill, border, borderMask);
                }
                c.a *= inside;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void RestyleNotificationCardPrefab()
        {
            // Create a completely clean root GameObject to avoid any legacy duplicate/ghost objects
            var rootGo = new GameObject("Notif_Container", typeof(RectTransform));
            try
            {
                var rootRt = rootGo.GetComponent<RectTransform>();
                rootRt.sizeDelta = new Vector2(340f, 54f);
                rootRt.pivot = new Vector2(0f, 1f);
                rootRt.anchorMin = new Vector2(0f, 1f);
                rootRt.anchorMax = new Vector2(1f, 1f);

                var entry = rootGo.AddComponent<NotificationEntry>();

                // Background Image (#111625 -> #0D111D, border #1F2639)
                var bg = rootGo.AddComponent<Image>();
                bg.sprite = cardBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                // LayoutElement
                var le = rootGo.AddComponent<LayoutElement>();
                le.minWidth = 320f;
                le.preferredWidth = 340f;
                le.flexibleWidth = 1f;
                le.minHeight = 54f;
                le.preferredHeight = 54f;
                le.flexibleHeight = 0f;

                // HorizontalLayoutGroup
                var hlg = rootGo.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(10, 12, 8, 8);
                hlg.spacing = 10f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;

                // 1. Avatar Container (28x28 circular container)
                var avatarGo = new GameObject("Avatar_Container", typeof(RectTransform));
                avatarGo.transform.SetParent(rootGo.transform, false);
                var avatarRt = avatarGo.GetComponent<RectTransform>();
                avatarRt.sizeDelta = new Vector2(28f, 28f);
                var avatarLe = avatarGo.AddComponent<LayoutElement>();
                avatarLe.minWidth = avatarLe.preferredWidth = 28f;
                avatarLe.minHeight = avatarLe.preferredHeight = 28f;
                avatarLe.flexibleWidth = avatarLe.flexibleHeight = 0f;

                var avatarMaskImg = avatarGo.AddComponent<Image>();
                avatarMaskImg.sprite = avatarCircle;
                avatarMaskImg.type = Image.Type.Simple;
                avatarMaskImg.color = Color.white;

                var avatarIconGo = new GameObject("Avatar_Image", typeof(RectTransform));
                avatarIconGo.transform.SetParent(avatarGo.transform, false);
                var avatarIconRt = avatarIconGo.GetComponent<RectTransform>();
                avatarIconRt.anchorMin = Vector2.zero;
                avatarIconRt.anchorMax = Vector2.one;
                avatarIconRt.offsetMin = new Vector2(3f, 3f);
                avatarIconRt.offsetMax = new Vector2(-3f, -3f);
                var avatarIconImg = avatarIconGo.AddComponent<Image>();
                avatarIconImg.sprite = defaultAvatar;
                avatarIconImg.color = Hex("#FDC553"); // golden accent
                avatarIconImg.preserveAspect = true;

                // 2. Content Column
                var contentGo = new GameObject("Content_Column", typeof(RectTransform));
                contentGo.transform.SetParent(rootGo.transform, false);
                var contentRt = contentGo.GetComponent<RectTransform>();
                contentRt.sizeDelta = new Vector2(280f, 38f);
                var contentLe = contentGo.AddComponent<LayoutElement>();
                contentLe.minWidth = 200f;
                contentLe.preferredWidth = 280f;
                contentLe.flexibleWidth = 1f;
                contentLe.flexibleHeight = 1f;

                var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(0, 0, 0, 0);
                vlg.spacing = 2f;
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Line 1: Category Text ("Friends Notification", "Club Membership", etc.)
                var catGo = new GameObject("Category_Text", typeof(RectTransform));
                catGo.transform.SetParent(contentGo.transform, false);
                var catTmp = catGo.AddComponent<TextMeshProUGUI>();
                catTmp.font = fSemiBold;
                catTmp.fontSize = 11.5f;
                catTmp.color = Color.white;
                catTmp.text = "Friends Notification";
                catTmp.alignment = TextAlignmentOptions.MidlineLeft;
                var catLe = catGo.AddComponent<LayoutElement>();
                catLe.minHeight = catLe.preferredHeight = 16f;

                // Line 2: Bottom Row (Message on left + Timestamp on right)
                var bottomRowGo = new GameObject("Bottom_Row", typeof(RectTransform));
                bottomRowGo.transform.SetParent(contentGo.transform, false);
                var bottomHlg = bottomRowGo.AddComponent<HorizontalLayoutGroup>();
                bottomHlg.spacing = 6f;
                bottomHlg.childAlignment = TextAnchor.MiddleLeft;
                bottomHlg.childControlWidth = true;
                bottomHlg.childControlHeight = true;
                bottomHlg.childForceExpandWidth = false;
                bottomHlg.childForceExpandHeight = true;
                var bottomLe = bottomRowGo.AddComponent<LayoutElement>();
                bottomLe.minHeight = bottomLe.preferredHeight = 16f;

                // Message Text
                var msgGo = new GameObject("Message_Text", typeof(RectTransform));
                msgGo.transform.SetParent(bottomRowGo.transform, false);
                var msgTmp = msgGo.AddComponent<TextMeshProUGUI>();
                msgTmp.font = fRegular;
                msgTmp.fontSize = 10f;
                msgTmp.color = Hex("#94A3B8");
                msgTmp.text = "Emma accepted your friend request.";
                msgTmp.textWrappingMode = TextWrappingModes.NoWrap;
                msgTmp.overflowMode = TextOverflowModes.Ellipsis;
                msgTmp.alignment = TextAlignmentOptions.MidlineLeft;
                var msgLe = msgGo.AddComponent<LayoutElement>();
                msgLe.minWidth = 140f;
                msgLe.preferredWidth = 210f;
                msgLe.flexibleWidth = 1f;

                // Timestamp Text
                var timeGo = new GameObject("Timestamp_Text", typeof(RectTransform));
                timeGo.transform.SetParent(bottomRowGo.transform, false);
                var timeTmp = timeGo.AddComponent<TextMeshProUGUI>();
                timeTmp.font = fMedium;
                timeTmp.fontSize = 9.5f;
                timeTmp.color = Hex("#F59E0B"); // Golden amber
                timeTmp.text = "12:00PM";
                timeTmp.alignment = TextAlignmentOptions.MidlineRight;
                var timeLe = timeGo.AddComponent<LayoutElement>();
                timeLe.minWidth = 54f;
                timeLe.preferredWidth = 54f;
                timeLe.flexibleWidth = 0f;

                // Actions Row (for Friend Requests requiring Accept/Decline, inactive by default)
                var actionsGo = new GameObject("Actions_Row", typeof(RectTransform));
                actionsGo.transform.SetParent(contentGo.transform, false);
                var actionsHlg = actionsGo.AddComponent<HorizontalLayoutGroup>();
                actionsHlg.spacing = 6f;
                actionsHlg.childAlignment = TextAnchor.MiddleLeft;
                actionsHlg.childControlWidth = false;
                actionsHlg.childControlHeight = true;
                actionsHlg.childForceExpandWidth = false;
                actionsHlg.childForceExpandHeight = true;

                // Confirm / Accept Button
                var confirmGo = new GameObject("Confirm_Button", typeof(RectTransform));
                confirmGo.transform.SetParent(actionsGo.transform, false);
                var confirmRt = confirmGo.GetComponent<RectTransform>();
                confirmRt.sizeDelta = new Vector2(62f, 18f);
                var confirmImg = confirmGo.AddComponent<Image>();
                confirmImg.sprite = acceptBtnBg;
                confirmImg.type = Image.Type.Sliced;
                var confirmBtn = confirmGo.AddComponent<Button>();
                confirmBtn.targetGraphic = confirmImg;

                var confirmTxtGo = new GameObject("Text", typeof(RectTransform));
                confirmTxtGo.transform.SetParent(confirmGo.transform, false);
                var confirmTxt = confirmTxtGo.AddComponent<TextMeshProUGUI>();
                Stretch((RectTransform)confirmTxtGo.transform);
                confirmTxt.font = fBold;
                confirmTxt.fontSize = 9.5f;
                confirmTxt.color = Hex("#01010C");
                confirmTxt.text = "Accept";
                confirmTxt.alignment = TextAlignmentOptions.Center;

                // Decline Button
                var declineGo = new GameObject("Decline_Button", typeof(RectTransform));
                declineGo.transform.SetParent(actionsGo.transform, false);
                var declineRt = declineGo.GetComponent<RectTransform>();
                declineRt.sizeDelta = new Vector2(62f, 18f);
                var declineImg = declineGo.AddComponent<Image>();
                declineImg.sprite = declineBtnBg;
                declineImg.type = Image.Type.Sliced;
                var declineBtn = declineGo.AddComponent<Button>();
                declineBtn.targetGraphic = declineImg;

                var declineTxtGo = new GameObject("Text", typeof(RectTransform));
                declineTxtGo.transform.SetParent(declineGo.transform, false);
                var declineTxt = declineTxtGo.AddComponent<TextMeshProUGUI>();
                Stretch((RectTransform)declineTxtGo.transform);
                declineTxt.font = fMedium;
                declineTxt.fontSize = 9.5f;
                declineTxt.color = Hex("#94A3B8");
                declineTxt.text = "Decline";
                declineTxt.alignment = TextAlignmentOptions.Center;

                actionsGo.SetActive(false);

                // Serialized fields binding
                var so = new SerializedObject(entry);
                so.FindProperty("iconObject").objectReferenceValue = avatarMaskImg;
                so.FindProperty("iconImage").objectReferenceValue = avatarIconImg;
                so.FindProperty("avatarImage").objectReferenceValue = avatarIconImg;
                so.FindProperty("headerLabel").objectReferenceValue = catTmp;
                so.FindProperty("categoryLabel").objectReferenceValue = catTmp;
                so.FindProperty("bodyLabel").objectReferenceValue = msgTmp;
                so.FindProperty("timestampLabel").objectReferenceValue = timeTmp;
                so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
                so.FindProperty("declineButton").objectReferenceValue = declineBtn;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(rootGo, EntryPrefabPath);
                Debug.Log("[NotificationRestyler] Saved clean EntryPrefab from scratch.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootGo);
            }
        }

        private static void RestyleNotificationsScalablePrefab()
        {
            var rootGo = PrefabUtility.LoadPrefabContents(NotifPrefabPath);
            try
            {
                var notifUi = rootGo.transform.Find("Notifications_UI");
                if (!notifUi)
                {
                    Debug.LogError("[NotificationRestyler] Notifications_UI not found in Notifications_Scalable prefab!");
                    return;
                }

                var controller = notifUi.GetComponent<NotificationController>();
                if (!controller) controller = notifUi.gameObject.AddComponent<NotificationController>();

                // Pressed_NotifUI is the flyout drawer
                var pressed = notifUi.Find("Pressed_NotifUI");
                if (!pressed)
                {
                    Debug.LogError("[NotificationRestyler] Pressed_NotifUI not found!");
                    return;
                }

                var pressedRt = (RectTransform)pressed;
                pressedRt.anchorMin = pressedRt.anchorMax = new Vector2(1f, 0f);
                pressedRt.pivot = new Vector2(1f, 1f);
                pressedRt.anchoredPosition = new Vector2(319f, -14f);
                pressedRt.sizeDelta = new Vector2(360f, 760f);

                // Disable any AspectRatioFitter on pressed
                if (pressed.TryGetComponent<AspectRatioFitter>(out var pressedArf))
                    pressedArf.enabled = false;

                // Ensure Canvas overrideSorting so drawer floats above all other layers
                var pressedCanvas = GetOrAdd<Canvas>(pressed.gameObject);
                pressedCanvas.overrideSorting = true;
                pressedCanvas.sortingOrder = 60;
                GetOrAdd<GraphicRaycaster>(pressed.gameObject);

                // Ensure CanvasGroup on Pressed_NotifUI
                var pressedCg = GetOrAdd<CanvasGroup>(pressed.gameObject);
                pressedCg.alpha = 0f;
                pressedCg.interactable = false;
                pressedCg.blocksRaycasts = false;

                // Setup Background
                var bg = pressed.GetComponent<Image>();
                if (!bg) bg = pressed.gameObject.AddComponent<Image>();
                bg.sprite = drawerBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                // Clean ALL children of Pressed_NotifUI
                for (int i = pressed.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(pressed.GetChild(i).gameObject);

                // Setup Structure inside Pressed_NotifUI:
                // 1. Header (48px high)
                var headerGo = new GameObject("Drawer_Header", typeof(RectTransform));
                headerGo.transform.SetParent(pressed, false);
                var headerRt = headerGo.GetComponent<RectTransform>();
                headerRt.anchorMin = new Vector2(0f, 1f);
                headerRt.anchorMax = new Vector2(1f, 1f);
                headerRt.pivot = new Vector2(0.5f, 1f);
                headerRt.anchoredPosition = Vector2.zero;
                headerRt.sizeDelta = new Vector2(0f, 48f);

                // Golden Bell Icon
                var bellGo = new GameObject("Bell_Icon", typeof(RectTransform));
                bellGo.transform.SetParent(headerGo.transform, false);
                var bellRt = bellGo.GetComponent<RectTransform>();
                bellRt.anchorMin = bellRt.anchorMax = bellRt.pivot = new Vector2(0f, 0.5f);
                bellRt.anchoredPosition = new Vector2(16f, 0f);
                bellRt.sizeDelta = new Vector2(20f, 20f);
                var bellImg = bellGo.AddComponent<Image>();
                bellImg.sprite = bellIcon;
                bellImg.color = Hex("#FDC553"); // Golden tint
                bellImg.preserveAspect = true;

                // Title "Notification"
                var titleGo = new GameObject("Title_Text", typeof(RectTransform));
                titleGo.transform.SetParent(headerGo.transform, false);
                var titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 0f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(44f, 0f);
                titleRt.offsetMax = new Vector2(-48f, 0f);
                var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
                titleTmp.font = fBold;
                titleTmp.fontSize = 16.5f;
                titleTmp.color = Color.white;
                titleTmp.text = "Notification";
                titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

                // Close Button (28x28 square, X icon)
                var closeGo = new GameObject("Close_Button", typeof(RectTransform));
                closeGo.transform.SetParent(headerGo.transform, false);
                var closeRt = closeGo.GetComponent<RectTransform>();
                closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1f, 0.5f);
                closeRt.anchoredPosition = new Vector2(-14f, 0f);
                closeRt.sizeDelta = new Vector2(28f, 28f);
                var closeImg = closeGo.AddComponent<Image>();
                closeImg.sprite = closeBtnBg;
                closeImg.type = Image.Type.Sliced;
                var closeBtn = closeGo.AddComponent<Button>();
                closeBtn.targetGraphic = closeImg;

                var closeIconGo = new GameObject("Icon", typeof(RectTransform));
                closeIconGo.transform.SetParent(closeGo.transform, false);
                var closeIconRt = closeIconGo.GetComponent<RectTransform>();
                closeIconRt.anchorMin = closeIconRt.anchorMax = closeIconRt.pivot = new Vector2(0.5f, 0.5f);
                closeIconRt.anchoredPosition = Vector2.zero;
                closeIconRt.sizeDelta = new Vector2(12f, 12f);
                var closeIconImg = closeIconGo.AddComponent<Image>();
                closeIconImg.sprite = closeIcon;
                closeIconImg.color = Color.white;
                closeIconImg.raycastTarget = false;

                // 2. Divider line (1px)
                var divGo = new GameObject("Drawer_Divider", typeof(RectTransform));
                divGo.transform.SetParent(pressed, false);
                var divRt = divGo.GetComponent<RectTransform>();
                divRt.anchorMin = new Vector2(0f, 1f);
                divRt.anchorMax = new Vector2(1f, 1f);
                divRt.pivot = new Vector2(0.5f, 1f);
                divRt.anchoredPosition = new Vector2(0f, -48f);
                divRt.sizeDelta = new Vector2(0f, 1f);
                var divImg = divGo.AddComponent<Image>();
                divImg.color = Hex("#1E2538");

                // 3. Scroll View
                var scrollGo = new GameObject("Scroll_View", typeof(RectTransform));
                scrollGo.transform.SetParent(pressed, false);
                var scrollRt = scrollGo.GetComponent<RectTransform>();
                scrollRt.anchorMin = Vector2.zero;
                scrollRt.anchorMax = Vector2.one;
                scrollRt.offsetMin = new Vector2(10f, 10f);
                scrollRt.offsetMax = new Vector2(-10f, -50f);

                var scrollRect = scrollGo.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                var viewportGo = new GameObject("Viewport", typeof(RectTransform));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                var viewportRt = viewportGo.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero;
                viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = Vector2.zero;
                viewportRt.offsetMax = Vector2.zero;
                viewportRt.pivot = new Vector2(0f, 1f);
                viewportGo.AddComponent<RectMask2D>();
                scrollRect.viewport = viewportRt;

                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewportGo.transform, false);
                var contentRt = contentGo.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(0f, 300f);
                scrollRect.content = contentRt;

                var contentVlg = contentGo.AddComponent<VerticalLayoutGroup>();
                contentVlg.padding = new RectOffset(0, 0, 6, 6);
                contentVlg.spacing = 8f;
                contentVlg.childAlignment = TextAnchor.UpperLeft;
                contentVlg.childControlWidth = true;
                contentVlg.childControlHeight = true;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;

                var contentCsf = contentGo.AddComponent<ContentSizeFitter>();
                contentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Today Section Header
                var todayGo = new GameObject("Today_Section_Header", typeof(RectTransform));
                todayGo.transform.SetParent(contentRt, false);
                var todayRt = todayGo.GetComponent<RectTransform>();
                todayRt.sizeDelta = new Vector2(340f, 22f);
                todayRt.pivot = new Vector2(0f, 1f);
                var todayLe = todayGo.AddComponent<LayoutElement>();
                todayLe.minHeight = todayLe.preferredHeight = 22f;
                todayLe.flexibleWidth = 1f;
                var todayTmp = todayGo.AddComponent<TextMeshProUGUI>();
                todayTmp.font = fSemiBold;
                todayTmp.fontSize = 12f;
                todayTmp.color = Hex("#94A3B8");
                todayTmp.text = "Today";
                todayTmp.alignment = TextAlignmentOptions.MidlineLeft;

                // Older Section Header
                var olderGo = new GameObject("Older_Section_Header", typeof(RectTransform));
                olderGo.transform.SetParent(contentRt, false);
                var olderRt = olderGo.GetComponent<RectTransform>();
                olderRt.sizeDelta = new Vector2(340f, 30f);
                olderRt.pivot = new Vector2(0f, 1f);
                var olderLe = olderGo.AddComponent<LayoutElement>();
                olderLe.minHeight = olderLe.preferredHeight = 30f;
                olderLe.flexibleWidth = 1f;
                var olderTmp = olderGo.AddComponent<TextMeshProUGUI>();
                olderTmp.font = fSemiBold;
                olderTmp.fontSize = 12f;
                olderTmp.color = Hex("#94A3B8");
                olderTmp.text = "Older Notification";
                olderTmp.alignment = TextAlignmentOptions.BottomLeft;

                // 4. Empty State (No Notifications)
                var emptyGo = new GameObject("NoNotifications", typeof(RectTransform));
                emptyGo.transform.SetParent(pressed, false);
                var emptyRt = emptyGo.GetComponent<RectTransform>();
                Stretch(emptyRt);
                var emptyCg = emptyGo.AddComponent<CanvasGroup>();
                emptyCg.alpha = 0f;
                emptyCg.interactable = false;
                emptyCg.blocksRaycasts = false;

                var emptyLabelGo = new GameObject("Empty_Text", typeof(RectTransform));
                emptyLabelGo.transform.SetParent(emptyGo.transform, false);
                var emptyLabelRt = emptyLabelGo.GetComponent<RectTransform>();
                emptyLabelRt.anchorMin = emptyLabelRt.anchorMax = emptyLabelRt.pivot = new Vector2(0.5f, 0.5f);
                emptyLabelRt.anchoredPosition = new Vector2(0f, -10f);
                emptyLabelRt.sizeDelta = new Vector2(260f, 40f);
                var emptyTmp = emptyLabelGo.AddComponent<TextMeshProUGUI>();
                emptyTmp.font = fMedium;
                emptyTmp.fontSize = 13.5f;
                emptyTmp.color = Hex("#64748B");
                emptyTmp.text = "No notifications yet";
                emptyTmp.alignment = TextAlignmentOptions.Center;

                // Bind NotificationController fields
                var entryPrefab = AssetDatabase.LoadAssetAtPath<NotificationEntry>(EntryPrefabPath);
                var so = new SerializedObject(controller);
                so.FindProperty("popUpCanvasGroup").objectReferenceValue = pressedCg;
                so.FindProperty("noNotificationsCanvasGroup").objectReferenceValue = emptyCg;
                so.FindProperty("notificationEntryPrefab").objectReferenceValue = entryPrefab;
                so.FindProperty("notificationEntriesParent").objectReferenceValue = contentRt;
                so.FindProperty("closePopUpButton").objectReferenceValue = closeBtn;
                so.FindProperty("todaySectionHeader").objectReferenceValue = todayGo;
                so.FindProperty("olderSectionHeader").objectReferenceValue = olderGo;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(rootGo, NotifPrefabPath);
                Debug.Log("[NotificationRestyler] Saved updated Notifications_Scalable prefab cleanly.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootGo);
            }
        }

        private static void ApplyToScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var notif = GameObject.Find("Notifications_Scalable");
            if (!notif)
            {
                Debug.LogWarning("[NotificationRestyler] Notifications_Scalable not found in scene!");
                return;
            }

            var notifUi = notif.transform.Find("Notifications_UI");
            if (!notifUi) return;

            var controller = notifUi.GetComponent<NotificationController>();
            var pressed = notifUi.Find("Pressed_NotifUI");
            if (!pressed) return;

            var pressedRt = (RectTransform)pressed;
            pressedRt.anchorMin = pressedRt.anchorMax = new Vector2(1f, 0f);
            pressedRt.pivot = new Vector2(1f, 1f);
            pressedRt.anchoredPosition = new Vector2(319f, -14f);
            pressedRt.sizeDelta = new Vector2(360f, 760f);

            var pressedCanvas = GetOrAdd<Canvas>(pressed.gameObject);
            pressedCanvas.overrideSorting = true;
            pressedCanvas.sortingOrder = 60;
            GetOrAdd<GraphicRaycaster>(pressed.gameObject);

            // Re-apply prefabs into scene
            var prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(NotifPrefabPath);
            if (prefabObj)
            {
                var prefabNotifUi = prefabObj.transform.Find("Notifications_UI");
                if (prefabNotifUi)
                {
                    var prefabPressed = prefabNotifUi.Find("Pressed_NotifUI");
                    if (prefabPressed)
                    {
                        // Clean existing children of pressed in scene
                        for (int i = pressed.childCount - 1; i >= 0; i--)
                            UnityEngine.Object.DestroyImmediate(pressed.GetChild(i).gameObject);

                        // Instantiate fresh children from prefab
                        foreach (Transform child in prefabPressed)
                        {
                            var clone = UnityEngine.Object.Instantiate(child.gameObject, pressed);
                            clone.name = child.name;
                        }

                        // Clean any stray children under content
                        var content = pressed.Find("Scroll_View/Viewport/Content");
                        if (content)
                        {
                            for (int i = content.childCount - 1; i >= 0; i--)
                            {
                                var ch = content.GetChild(i).gameObject;
                                if (ch.name != "Today_Section_Header" && ch.name != "Older_Section_Header")
                                    UnityEngine.Object.DestroyImmediate(ch);
                            }
                        }

                        // Rebind controller
                        var pressedCg = GetOrAdd<CanvasGroup>(pressed.gameObject);
                        var bg = GetOrAdd<Image>(pressed.gameObject);
                        bg.sprite = drawerBg;
                        bg.type = Image.Type.Sliced;
                        bg.color = Color.white;

                        var closeBtn = pressed.Find("Drawer_Header/Close_Button")?.GetComponent<Button>();
                        var emptyCg = pressed.Find("NoNotifications")?.GetComponent<CanvasGroup>();
                        var todayGo = content?.Find("Today_Section_Header")?.gameObject;
                        var olderGo = content?.Find("Older_Section_Header")?.gameObject;
                        var entryPrefab = AssetDatabase.LoadAssetAtPath<NotificationEntry>(EntryPrefabPath);

                        var so = new SerializedObject(controller);
                        so.FindProperty("popUpCanvasGroup").objectReferenceValue = pressedCg;
                        so.FindProperty("noNotificationsCanvasGroup").objectReferenceValue = emptyCg;
                        so.FindProperty("notificationEntryPrefab").objectReferenceValue = entryPrefab;
                        so.FindProperty("notificationEntriesParent").objectReferenceValue = content;
                        so.FindProperty("closePopUpButton").objectReferenceValue = closeBtn;
                        so.FindProperty("todaySectionHeader").objectReferenceValue = todayGo;
                        so.FindProperty("olderSectionHeader").objectReferenceValue = olderGo;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NotificationRestyler] Saved MainSceneDomDemo scene changes cleanly.");
        }

        public static void RenderScreenshots()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var notif = GameObject.Find("Notifications_Scalable");
            if (!notif) return;

            var pressed = notif.transform.Find("Notifications_UI/Pressed_NotifUI");
            if (!pressed) return;

            var pressedCg = pressed.GetComponent<CanvasGroup>();
            var content = pressed.Find("Scroll_View/Viewport/Content");
            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);

            // Clean any existing children under content
            if (content)
            {
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var ch = content.GetChild(i).gameObject;
                    if (ch.name != "Today_Section_Header" && ch.name != "Older_Section_Header")
                        UnityEngine.Object.DestroyImmediate(ch);
                }
            }

            // Populate sample notification cards to verify layout matching Figma
            var sampleCards = new List<GameObject>();
            if (content && entryPrefab)
            {
                var todayHeader = content.Find("Today_Section_Header");
                if (todayHeader) todayHeader.SetSiblingIndex(0);

                // Card 1: Emma accepted your friend request
                var card1 = UnityEngine.Object.Instantiate(entryPrefab, content);
                card1.name = "Sample_Card_1";
                card1.transform.SetSiblingIndex(1);
                card1.SetActive(true);
                SetCardContent(card1, "Friends Notification", "12:00PM", "Emma accepted your friend request.");
                sampleCards.Add(card1);

                // Card 2: Alex sent you a friend request
                var card2 = UnityEngine.Object.Instantiate(entryPrefab, content);
                card2.name = "Sample_Card_2";
                card2.transform.SetSiblingIndex(2);
                card2.SetActive(true);
                SetCardContent(card2, "Friends Notification", "12:00PM", "Alex sent you a friend request.");
                sampleCards.Add(card2);

                // Older section
                var olderHeader = content.Find("Older_Section_Header");
                if (olderHeader) olderHeader.SetSiblingIndex(3);

                // Card 3: Club membership accepted
                var card3 = UnityEngine.Object.Instantiate(entryPrefab, content);
                card3.name = "Sample_Card_3";
                card3.transform.SetSiblingIndex(4);
                card3.SetActive(true);
                SetCardContent(card3, "Club Membership", "12:00PM", "Your request to join Domino Masters has been accepted.");
                sampleCards.Add(card3);

                // Card 4: Removed from Domino Masters
                var card4 = UnityEngine.Object.Instantiate(entryPrefab, content);
                card4.name = "Sample_Card_4";
                card4.transform.SetSiblingIndex(5);
                card4.SetActive(true);
                SetCardContent(card4, "Club Membership", "12:00PM", "You were removed from Domino Masters.");
                sampleCards.Add(card4);

                // Card 5: Party invitation
                var card5 = UnityEngine.Object.Instantiate(entryPrefab, content);
                card5.name = "Sample_Card_5";
                card5.transform.SetSiblingIndex(6);
                card5.SetActive(true);
                SetCardContent(card5, "Party Notifications", "12:00PM", "Emma accepted your party invitation.");
                sampleCards.Add(card5);
            }

            // Open notification drawer
            if (pressedCg)
            {
                pressedCg.alpha = 1f;
                pressedCg.interactable = true;
                pressedCg.blocksRaycasts = true;
            }

            if (content)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            }
            Canvas.ForceUpdateCanvases();

            // Render camera
            var cam = GameObject.Find("PD_Main_Camera")?.GetComponent<Camera>();
            if (cam)
            {
                RenderCameraToPng(cam, "C:/Users/Admin/.gemini/antigravity/brain/ca407b30-6a21-4bdd-a191-1ba20fc87220/screen_notification_open.png");
            }

            // Clean up sample cards
            foreach (var card in sampleCards)
                UnityEngine.Object.DestroyImmediate(card);

            // Close notification drawer
            if (pressedCg)
            {
                pressedCg.alpha = 0f;
                pressedCg.interactable = false;
                pressedCg.blocksRaycasts = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetCardContent(GameObject card, string category, string time, string message)
        {
            var catTmp = card.transform.Find("Content_Column/Category_Text")?.GetComponent<TextMeshProUGUI>();
            if (catTmp) catTmp.text = category;

            var msgTmp = card.transform.Find("Content_Column/Bottom_Row/Message_Text")?.GetComponent<TextMeshProUGUI>();
            if (msgTmp) msgTmp.text = message;

            var timeTmp = card.transform.Find("Content_Column/Bottom_Row/Timestamp_Text")?.GetComponent<TextMeshProUGUI>();
            if (timeTmp) timeTmp.text = time;
        }

        private static void RenderCameraToPng(Camera cam, string outputPath)
        {
            int w = 1920;
            int h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(outputPath, bytes);
            Debug.Log($"[NotificationRestyler] Rendered screenshot to: {outputPath}");
        }

        private static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            var child = parent.transform.Find(name);
            if (child) return child.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (!comp) comp = go.AddComponent<T>();
            return comp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
