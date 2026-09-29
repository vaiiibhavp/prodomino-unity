using System;
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
    /// Rebuilds FriendList_PopUp as the full-screen Friends List of the Figma "Friends List" section (115:3033):
    /// - empty state (illustration, title, centered search bar, first-match hint)
    /// - recently played players / search results as cards with "+ Add Friend"
    /// - friends grid with a "..." actions menu per card (Invite to Party, Remove Friend)
    /// - "Player Not Found" state
    /// The root keeps its PartyController, CanvasGroup and outside-click closer; only the visuals are rebuilt.
    /// The cards are new prefabs so FriendEntry / SearchEntry used by the party screen stay untouched.
    /// </summary>
    public static class FriendListPopupRestyler
    {
        private const string PopupPath = "Assets/_ProDomino/FriendSystem/Prefabs/FriendList_PopUp.prefab";
        private const string FriendCardPath = "Assets/_ProDomino/FriendSystem/Prefabs/FriendCard.prefab";
        private const string PlayerCardPath = "Assets/_ProDomino/FriendSystem/Prefabs/PlayerCard.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string DefaultAvatarPath = "Assets/_ProDomino/_Art/Avatars/Man_Avatar_1.png";

        private const float CardWidth = 165f;
        private const float FriendCardHeight = 186f;
        private const float PlayerCardHeight = 234f;
        private const float CardGap = 24f;

        private static Sprite screenBg, cardBg, fieldBg, goldBtn, goldBtnSmall, darkBtn, menuBtnBg, menuPanelBg, menuRowHover;
        private static Sprite circle, avatarRing, dotsIcon, illustration, userIcon, defaultAvatar;

        [MenuItem("ProDomino/Dashboard/Restyle Friend List Popup + Render")]
        public static void ApplyAndRender()
        {
            Apply();
            RenderOnly();
        }

        public static void Apply()
        {
            PrepareAssets();
            var friendCard = BuildFriendCard();
            var playerCard = BuildPlayerCard();
            BuildPopup(friendCard, playerCard);
            CleanMiddleScreenInstance();
            AssetDatabase.SaveAssets();
            Debug.Log("[FriendListPopupRestyler] Done.");
        }

        // ------------------------------------------------------------------ assets
        private static void PrepareAssets()
        {
            screenBg = MakePanelSprite("FriendList_ScreenBg", 48, 48, 10, Hex("#010818"), Hex("#010818"), Hex("#34343D"), 1f);
            cardBg = MakePanelSprite("FriendList_CardBg", 64, 64, 12, Hex("#34343D"), Hex("#000005"), Hex("#46464B"), 2f);
            fieldBg = MakePanelSprite("FriendList_FieldBg", 48, 48, 10, Hex("#11111A"), Hex("#11111A"), Hex("#1B1B29"), 1f);
            goldBtn = MakePanelSprite("FriendList_GoldBtn", 48, 48, 10, AccentStart, AccentEnd, AccentRim, 1.5f, false);
            goldBtnSmall = goldBtn;
            darkBtn = MakePanelSprite("FriendList_DarkBtn", 48, 48, 10, Hex("#212129"), Hex("#212129"), Hex("#34343D"), 2f);
            menuBtnBg = MakePanelSprite("FriendList_MenuBtnBg", 24, 24, 4, Hex("#040614"), Hex("#191A1D"), Hex("#34343D"), 1f);
            menuPanelBg = MakePanelSprite("FriendList_MenuPanelBg", 32, 32, 8, Hex("#040614"), Hex("#191A1D"), Hex("#34343D"), 1f);
            menuRowHover = MakePanelSprite("FriendList_MenuRowHover", 16, 16, 4, Hex("#23232B"), Hex("#23232B"), Color.clear, 0f);
            circle = MakeCircleSprite("FriendList_Circle", 128);
            avatarRing = MakeRingSprite("FriendList_AvatarRing", 128, 3f);
            dotsIcon = MakeDotsSprite("FriendList_Dots", 28, 8);
            illustration = AssetDatabase.LoadAllAssetsAtPath($"{GeneratedDir}/Friends_Empty_Illustration.png").OfType<Sprite>().FirstOrDefault();
            userIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Nav_FriendsList.png");
            defaultAvatar = AssetDatabase.LoadAllAssetsAtPath(DefaultAvatarPath).OfType<Sprite>().FirstOrDefault();
            Require(illustration, "Friends_Empty_Illustration");
            Require(defaultAvatar, "default avatar");
        }

        private static Sprite MakeRingSprite(string name, int size, float width)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d + 0.5f) * Mathf.Clamp01(d - (r - width) + 0.5f);
                var c = Color.Lerp(AccentStart, AccentEnd, (float)x / (size - 1));
                c.a = a;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{GeneratedDir}/{name}.png", tex);
        }

        // Three horizontal dots, the "more" glyph of the friend card.
        private static Sprite MakeDotsSprite(string name, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float rad = h / 2f;
            var centers = new[] { rad, w / 2f, w - rad };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = centers.Max(cx => Mathf.Clamp01(rad - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, rad)) + 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return SaveSprite($"{GeneratedDir}/{name}.png", tex);
        }

        // ------------------------------------------------------------------ friend card
        private static GameObject BuildFriendCard()
        {
            var root = NewRect("FriendCard", null);
            root.gameObject.layer = 5;
            root.sizeDelta = new Vector2(CardWidth, FriendCardHeight);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = cardBg; bg.type = Image.Type.Sliced; bg.raycastTarget = true;

            BuildAvatar(root, out var avatar);
            var name = CardText(root, "Text_Friend_Username", "Robert", Medium, 20f, Color.white, 120f, 24f);
            var status = BuildStatusRow(root, out var dot);

            // Hidden id label, FriendEntry fills it and the design does not show it
            var id = MakeText(root, "Text_User_ID", "", Regular, 10f, TextMuted);
            id.gameObject.SetActive(false);

            // "..." button, top right
            var menuBtn = NewRect("Button_Actions", root);
            TopRight(menuBtn, 12f, 8f, 24f, 24f);
            var menuImg = menuBtn.gameObject.AddComponent<Image>();
            menuImg.sprite = menuBtnBg; menuImg.type = Image.Type.Sliced;
            var menuButton = menuBtn.gameObject.AddComponent<Button>();
            NeutralTint(menuBtn, menuImg);
            var dots = MakeImage(menuBtn, "Dots", dotsIcon, Color.white, Image.Type.Simple).GetComponent<RectTransform>();
            dots.sizeDelta = new Vector2(14f, 4f);

            // Actions menu, drops under the button and stays inside the card so no mask clips it
            var menu = NewRect("ActionsMenu", root);
            TopRight(menu, 8f, 36f, 129f, 0f);
            var menuPanelImg = menu.gameObject.AddComponent<Image>();
            menuPanelImg.sprite = menuPanelBg; menuPanelImg.type = Image.Type.Sliced; menuPanelImg.raycastTarget = true;
            var vlg = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6); vlg.spacing = 2f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = menu.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var invite = MenuRow(menu, "Button_InviteToParty", "Invite to Party");
            var remove = MenuRow(menu, "Button_RemoveFriend", "Remove Friend");

            // Hidden through a CanvasGroup, not SetActive, so the menu buttons run Awake with the card
            var menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
            menuGroup.alpha = 0f; menuGroup.interactable = false; menuGroup.blocksRaycasts = false;

            var entry = AddByName(root, "ProDomino.FriendSystem.FriendEntry");
            var so = new SerializedObject(entry);
            so.FindProperty("friendNameLabel").objectReferenceValue = name;
            so.FindProperty("friendIDLabel").objectReferenceValue = id;
            so.FindProperty("friendStatusLabel").objectReferenceValue = status;
            so.FindProperty("statusIndicatorImage").objectReferenceValue = dot;
            so.FindProperty("inviteButton").objectReferenceValue = invite;
            so.FindProperty("RemoveButton").objectReferenceValue = remove;
            so.FindProperty("avatarImage").objectReferenceValue = avatar;
            so.FindProperty("defaultAvatarSprite").objectReferenceValue = defaultAvatar;
            so.FindProperty("actionsMenuButton").objectReferenceValue = menuButton;
            so.FindProperty("actionsMenuPanel").objectReferenceValue = menu.gameObject;
            so.FindProperty("isTintingStatusLabel").boolValue = true;
            so.FindProperty("onlineStatusLabelColor").colorValue = Hex("#E6E6E7");
            so.FindProperty("otherStatusLabelColor").colorValue = Hex("#8A8A8F");
            so.FindProperty("onlineStatusColor").colorValue = Hex("#6ADF3A");
            so.FindProperty("offlineStatusColor").colorValue = Hex("#8A8A8F");
            so.FindProperty("busyStatusColor").colorValue = Hex("#FFC651");
            so.FindProperty("unknownlineStatusColor").colorValue = Hex("#8A8A8F");
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root.gameObject, FriendCardPath);
        }

        private static CustomButtonUI MenuRow(Transform menu, string name, string label)
        {
            var row = NewRect(name, menu);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 27f;
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = menuRowHover; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, 0f);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            row.gameObject.AddComponent<CanvasGroup>();

            var text = MakeText(row, "Text", label, Medium, 12f, Hex("#8A8A8F"));
            var trt = text.rectTransform; Stretch(trt); trt.offsetMin = new Vector2(12f, 0f);

            var custom = row.gameObject.AddComponent<CustomButtonUI>();
            var so = new SerializedObject(custom);
            so.FindProperty("isToggleable").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return custom;
        }

        // ------------------------------------------------------------------ player card
        private static GameObject BuildPlayerCard()
        {
            var root = NewRect("PlayerCard", null);
            root.gameObject.layer = 5;
            root.sizeDelta = new Vector2(CardWidth, PlayerCardHeight);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = cardBg; bg.type = Image.Type.Sliced; bg.raycastTarget = true;

            BuildAvatar(root, out var avatar);
            var name = CardText(root, "Text_Username", "Robert", Medium, 20f, Color.white, 120f, 24f);

            // The search only returns names, the status line reads as the player tag instead
            var id = MakeText(root, "Text_UserId", "", Regular, 10f, TextMuted);
            id.gameObject.SetActive(false);

            var btn = NewRect("Button_AddFriend", root);
            btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
            btn.pivot = new Vector2(0.5f, 0f);
            btn.anchoredPosition = new Vector2(0f, 18f);
            btn.sizeDelta = new Vector2(133f, 40f);
            var btnImg = btn.gameObject.AddComponent<Image>();
            btnImg.sprite = darkBtn; btnImg.type = Image.Type.Sliced;
            var button = btn.gameObject.AddComponent<Button>();
            NeutralTint(btn, btnImg);
            var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
            var label = MakeText(btn, "Text", "+ Add Friend", SemiBold, 14f, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);

            var entry = AddByName(root, "ProDomino.GameSystem.SearchUserEntry");
            var so = new SerializedObject(entry);
            so.FindProperty("playerImage").objectReferenceValue = avatar;
            so.FindProperty("usernameLabel").objectReferenceValue = name;
            so.FindProperty("userIdLabel").objectReferenceValue = id;
            so.FindProperty("invitePlayerButton").objectReferenceValue = button;
            so.FindProperty("invitePlayerButtonLabel").objectReferenceValue = label;
            so.FindProperty("invitePlayerButtonImage").objectReferenceValue = btnImg;
            so.FindProperty("requestSentSprite").objectReferenceValue = goldBtnSmall;
            so.FindProperty("requestSentLabelColor").colorValue = OnAccent;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root.gameObject, PlayerCardPath);
        }

        private static void BuildAvatar(Transform card, out Image avatar)
        {
            var holder = NewRect("Avatar", card);
            TopCenter(holder, 24f, 80f, 80f);
            var maskImg = holder.gameObject.AddComponent<Image>();
            maskImg.sprite = circle; maskImg.raycastTarget = false;
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            avatar = MakeImage(holder, "Image_Avatar", defaultAvatar, Color.white, Image.Type.Simple).GetComponent<Image>();
            avatar.preserveAspect = true;
            Stretch(avatar.rectTransform);

            var ring = MakeImage(card, "Avatar_Ring", avatarRing, Color.white, Image.Type.Simple).GetComponent<RectTransform>();
            TopCenter(ring, 24f, 80f, 80f);
        }

        private static TMP_Text BuildStatusRow(Transform card, out Image dot)
        {
            var row = NewRect("Status", card);
            TopCenter(row, 147f, 131f, 15f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            dot = MakeImage(row, "Status_Indicator", circle, Hex("#6ADF3A"), Image.Type.Simple).GetComponent<Image>();
            dot.rectTransform.sizeDelta = new Vector2(8f, 8f);
            var le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 8f; le.preferredHeight = 8f;

            var text = MakeText(row, "Text_User_Status", "Online", Medium, 12f, Hex("#E6E6E7"));
            text.rectTransform.sizeDelta = new Vector2(60f, 15f);
            return text;
        }

        private static TMP_Text CardText(Transform card, string name, string value, TMP_FontAsset font, float size, Color color, float y, float h)
        {
            var t = MakeText(card, name, value, font, size, color);
            Label(t, font, size, color);
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.offsetMin = new Vector2(10f, -y - h); t.rectTransform.offsetMax = new Vector2(-10f, -y);
            return t;
        }

        // ------------------------------------------------------------------ popup
        private static void BuildPopup(GameObject friendCard, GameObject playerCard)
        {
            var root = PrefabUtility.LoadPrefabContents(PopupPath);
            try
            {
                var rootRt = (RectTransform)root.transform;
                Stretch(rootRt);
                rootRt.pivot = new Vector2(0.5f, 0.5f);

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                // Full-screen card: also the panel the outside-click closer treats as "inside"
                var screen = NewRect("Screen", root.transform);
                Stretch(screen);
                var screenImg = screen.gameObject.AddComponent<Image>();
                screenImg.sprite = screenBg; screenImg.type = Image.Type.Sliced; screenImg.raycastTarget = true;

                // Header
                var header = NewRect("Header", screen);
                TL(header, 20f, 24f, 400f, 29f);
                var icon = MakeImage(header, "Icon", userIcon, Accent, Image.Type.Simple).GetComponent<Image>();
                icon.preserveAspect = true;
                TL(icon.rectTransform, 0f, 2f, 24f, 24f);
                var title = MakeText(header, "Text_Title", "Friends List", SemiBold, 24f, Color.white);
                TL(title.rectTransform, 34f, 0f, 360f, 29f);

                // Scrollable body
                var scroll = NewRect("Scroll View", screen);
                Stretch(scroll);
                scroll.offsetMin = new Vector2(20f, 24f); scroll.offsetMax = new Vector2(-20f, -93f);
                var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false; scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 30f;
                var viewport = NewRect("Viewport", scroll);
                Stretch(viewport);
                viewport.gameObject.AddComponent<RectMask2D>();
                var vpImg = viewport.gameObject.AddComponent<Image>();
                vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;

                var content = NewRect("Content", viewport);
                content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = content.offsetMax = Vector2.zero;
                var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 40f; vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.padding = new RectOffset(0, 0, 0, 24);
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scrollRect.viewport = viewport; scrollRect.content = content;

                // Empty state hero
                var hero = StateBlock(content, "EmptyState", "Your Friends List is Empty",
                    "Search for ProDomino players to send friend\nrequests and start playing together.", 479f);

                // Search row
                var searchRow = NewRect("SearchRow", content);
                var rowLe = searchRow.gameObject.AddComponent<LayoutElement>();
                rowLe.preferredHeight = 56f; rowLe.preferredWidth = 607f; rowLe.flexibleWidth = 0f;
                var hlg = searchRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 16f;
                hlg.childControlWidth = true; hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
                var input = BuildInput(searchRow);
                var searchButton = BuildSearchButton(searchRow);

                // First-match hint
                var hint = MakeText(content, "Text_FirstMatchHint",
                    "Play your first match to discover players you've competed against and add them as friends.",
                    Medium, 16f, Hex("#FED88C"));
                hint.alignment = TextAlignmentOptions.Center;
                hint.textWrappingMode = TextWrappingModes.Normal;

                // Player not found
                var notFound = StateBlock(content, "PlayerNotFound", "Player Not Found",
                    "The player you're looking for doesn't exist or hasn't registered yet.", 521f);

                // Recently played / search results
                var players = NewRect("PlayersSection", content);
                var playersVlg = players.gameObject.AddComponent<VerticalLayoutGroup>();
                playersVlg.spacing = 28f; playersVlg.childAlignment = TextAnchor.UpperLeft;
                playersVlg.childControlWidth = true; playersVlg.childControlHeight = true;
                playersVlg.childForceExpandWidth = true; playersVlg.childForceExpandHeight = false;
                players.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var playersCg = players.gameObject.AddComponent<CanvasGroup>();
                var playersTitle = MakeText(players, "Text_SectionTitle", "Recently played players", SemiBold, 24f, Color.white);
                var playersGrid = CardGrid(players, "PlayerCards", PlayerCardHeight, 0);

                // Friends grid (the design keeps 20px under the search bar instead of the 40px section gap)
                var friends = NewRect("FriendsSection", content);
                friends.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var friendsLayout = friends.gameObject.AddComponent<VerticalLayoutGroup>();
                friendsLayout.padding = new RectOffset(0, 0, -20, 0);
                friendsLayout.childControlWidth = true; friendsLayout.childControlHeight = true;
                friendsLayout.childForceExpandWidth = true; friendsLayout.childForceExpandHeight = false;
                var friendsGrid = CardGrid(friends, "FriendCards", FriendCardHeight, 0);

                // Default authoring state: empty list with the hint
                notFound.gameObject.SetActive(false);
                players.gameObject.SetActive(false);
                friends.gameObject.SetActive(false);

                WireController(root, friendCard, playerCard, hero.gameObject, hint.gameObject, rowLe, friends.gameObject,
                    players.gameObject, playersTitle, playersCg, notFound.gameObject, friendsGrid, playersGrid, input, searchButton, screen);

                PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform StateBlock(Transform parent, string name, string title, string subtitle, float width)
        {
            var block = NewRect(name, parent);
            var vlg = block.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

            var art = MakeImage(block, "Illustration", illustration, Color.white, Image.Type.Simple).GetComponent<Image>();
            art.preserveAspect = true;
            var artLe = art.gameObject.AddComponent<LayoutElement>();
            artLe.preferredWidth = 140f; artLe.preferredHeight = 140f;

            var texts = NewRect("Texts", block);
            var tv = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 8f; tv.childAlignment = TextAnchor.UpperCenter;
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;
            texts.gameObject.AddComponent<LayoutElement>().preferredWidth = width;

            var t = MakeText(texts, "Text_Title", title, SemiBold, 36f, Color.white);
            t.alignment = TextAlignmentOptions.Center;
            var s = MakeText(texts, "Text_Subtitle", subtitle, Regular, 16f, TextMuted);
            s.alignment = TextAlignmentOptions.Center;
            s.textWrappingMode = TextWrappingModes.Normal;
            return block;
        }

        private static RectTransform CardGrid(Transform parent, string name, float cardHeight, int topPadding)
        {
            var grid = NewRect(name, parent);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(CardWidth, cardHeight);
            g.spacing = new Vector2(CardGap, CardGap);
            g.startCorner = GridLayoutGroup.Corner.UpperLeft;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.childAlignment = TextAnchor.UpperLeft;
            g.padding = new RectOffset(0, 0, topPadding, 0);
            return grid;
        }

        private static TMP_InputField BuildInput(Transform row)
        {
            var go = NewRect("InputField_Search", row);
            go.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var img = go.gameObject.AddComponent<Image>();
            img.sprite = fieldBg; img.type = Image.Type.Sliced;
            var field = go.gameObject.AddComponent<TMP_InputField>();

            var area = NewRect("Text Area", go);
            Stretch(area);
            area.offsetMin = new Vector2(20f, 0f); area.offsetMax = new Vector2(-20f, 0f);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = MakeText(area, "Placeholder", "Search username...", Regular, 16f, TextPlaceholder);
            Stretch(ph.rectTransform);
            var text = MakeText(area, "Text", "", Regular, 16f, Color.white);
            Stretch(text.rectTransform);

            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.fontAsset = Regular;
            field.pointSize = 16f;
            field.caretColor = Accent;
            field.customCaretColor = true;
            field.selectionColor = new Color(AccentStart.r, AccentStart.g, AccentStart.b, 0.35f);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 32;
            return field;
        }

        private static CustomButtonUI BuildSearchButton(Transform row)
        {
            var go = NewRect("Button_Search", row);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 112f; le.minWidth = 112f;
            var img = go.gameObject.AddComponent<Image>();
            img.sprite = goldBtn; img.type = Image.Type.Sliced;
            go.gameObject.AddComponent<Button>();
            NeutralTint(go, img);
            go.gameObject.AddComponent<CanvasGroup>();
            var t = MakeText(go, "Text", "Search", Bold, 20f, OnAccent);
            t.alignment = TextAlignmentOptions.Center;
            Stretch(t.rectTransform);

            var custom = go.gameObject.AddComponent<CustomButtonUI>();
            var so = new SerializedObject(custom);
            so.FindProperty("isToggleable").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return custom;
        }

        private static void WireController(GameObject root, GameObject friendCard, GameObject playerCard, GameObject hero,
            GameObject hint, LayoutElement searchRow, GameObject friends, GameObject players, TMP_Text playersTitle,
            CanvasGroup playersCg, GameObject notFound, Transform friendsGrid, Transform playersGrid,
            TMP_InputField input, CustomButtonUI searchButton, Transform screen)
        {
            var controller = root.GetComponent(FindType("ProDomino.FriendSystem.PartyController"));
            var so = new SerializedObject(controller);
            so.FindProperty("friendListCanvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("friendEntryPrefab").objectReferenceValue = friendCard.GetComponent(FindType("ProDomino.FriendSystem.FriendEntry"));
            so.FindProperty("friendEntriesParent").objectReferenceValue = friendsGrid;
            so.FindProperty("friendsCountLabel").objectReferenceValue = null;
            // Name | Status | Invite | Remove: the design shows no player id on the cards
            so.FindProperty("categoriesShown").intValue = 1 | 4 | 8 | 16;
            so.FindProperty("emptyFriendlistLabel").objectReferenceValue = hero;
            so.FindProperty("orderByToggleGroup").objectReferenceValue = null;
            so.FindProperty("closePopUp").objectReferenceValue = null;
            so.FindProperty("searchUsersCanvasGroup").objectReferenceValue = playersCg;
            so.FindProperty("requestFriendshipButton").objectReferenceValue = searchButton;
            so.FindProperty("requestFriendshipInputfield").objectReferenceValue = input;
            so.FindProperty("searchUserEntriesParent").objectReferenceValue = playersGrid;
            so.FindProperty("searchUserEntryPrefab").objectReferenceValue = playerCard.GetComponent(FindType("ProDomino.GameSystem.SearchUserEntry"));
            so.FindProperty("firstMatchHint").objectReferenceValue = hint;
            so.FindProperty("searchRowLayout").objectReferenceValue = searchRow;
            so.FindProperty("friendsSection").objectReferenceValue = friends;
            so.FindProperty("playersSection").objectReferenceValue = players;
            so.FindProperty("playersSectionTitle").objectReferenceValue = playersTitle;
            so.FindProperty("playerNotFoundState").objectReferenceValue = notFound;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Clicks anywhere on the screen card count as inside; the sidebar/header close it
            var closer = root.GetComponent(FindType("CanvasGroupVisibilityController"));
            if (closer)
            {
                var cso = new SerializedObject(closer);
                cso.FindProperty("panelRectTransform").objectReferenceValue = screen;
                cso.FindProperty("canvasGroupToControl").objectReferenceValue = root.GetComponent<CanvasGroup>();
                cso.FindProperty("isClosingPopUpClickingPanelToo").boolValue = false;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // The MiddleScreen instance carries layout overrides for the old popup children; drop them so
        // the rebuilt layout drives itself, and keep the root filling Static_PopUps.
        private static void CleanMiddleScreenInstance()
        {
            var middle = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
            try
            {
                var popup = FindDeep(middle.transform, "FriendList_Popup");
                if (!popup) throw new Exception("FriendList_Popup not found in MiddleScreen_Scalable.");

                foreach (var t in popup.GetComponentsInChildren<Transform>(true))
                {
                    if (t == popup) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(t))
                        PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
                }

                var rt = (RectTransform)popup;
                PrefabUtility.RevertObjectOverride(rt, InteractionMode.AutomatedAction);
                Stretch(rt);
                rt.localScale = Vector3.one;

                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(popup.gameObject);
                if (instanceRoot)
                    PrefabUtility.RemoveUnusedOverrides(new[] { instanceRoot }, InteractionMode.AutomatedAction);

                PrefabUtility.SaveAsPrefabAsset(middle, MiddleScreenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middle);
            }
        }

        // ------------------------------------------------------------------ helpers
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent)
            {
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
            }
            return (RectTransform)go.transform;
        }

        private static void TopCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void TopRight(RectTransform rt, float fromRight, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-fromRight, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static GameObject Save(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ render
        [MenuItem("ProDomino/Dashboard/Render Friend List Popup")]
        public static void RenderOnly()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            string[] names = { "Robert", "Aubrey", "Max", "Courtney", "Marjorie", "Arlene" };
            bool[] online = { true, false, true, true, true, false };

            Render(Path.Combine(outDir, "friends_empty.png"), (p, s) => { });
            Render(Path.Combine(outDir, "friends_recent.png"), (p, s) =>
            {
                s("EmptyState", true); s("Text_FirstMatchHint", false); s("PlayersSection", true);
                Fill(FindDeep(p, "PlayerCards"), PlayerCardPath, 6, names, online);
            });
            Render(Path.Combine(outDir, "friends_list.png"), (p, s) =>
            {
                s("EmptyState", false); s("Text_FirstMatchHint", false); s("FriendsSection", true);
                var le = FindDeep(p, "SearchRow").GetComponent<LayoutElement>();
                le.preferredWidth = -1f; le.flexibleWidth = 1f;
                Fill(FindDeep(p, "FriendCards"), FriendCardPath, 12, names, online);
                var firstMenu = FindDeep(FindDeep(p, "FriendCards").GetChild(0), "ActionsMenu");
                firstMenu.GetComponent<CanvasGroup>().alpha = 1f;
            });
            Render(Path.Combine(outDir, "friends_not_found.png"), (p, s) =>
            {
                s("EmptyState", false); s("Text_FirstMatchHint", false); s("PlayerNotFound", true);
                var le = FindDeep(p, "SearchRow").GetComponent<LayoutElement>();
                le.preferredWidth = -1f; le.flexibleWidth = 1f;
            });
            Debug.Log($"[FriendListPopupRestyler] Rendered to {outDir}");
        }

        private static void Fill(Transform grid, string cardPath, int count, string[] names, bool[] online)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(cardPath);
            for (int i = grid.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(grid.GetChild(i).gameObject);
            for (int i = 0; i < count; i++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid);
                var n = names[i % names.Length];
                foreach (var t in card.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.name.Contains("Username")) t.text = n;
                    if (t.name == "Text_User_Status")
                    {
                        t.text = online[i % online.Length] ? "Online" : "Offline";
                        t.color = online[i % online.Length] ? Hex("#E6E6E7") : Hex("#8A8A8F");
                    }
                }
                var dot = FindDeep(card.transform, "Status_Indicator");
                if (dot) dot.GetComponent<Image>().color = online[i % online.Length] ? Hex("#6ADF3A") : Hex("#8A8A8F");
            }
        }

        // Renders the game scene's own canvas copy (it keeps its own overrides) without saving the scene.
        [MenuItem("ProDomino/Dashboard/Render Friend List Popup (Scene)")]
        public static void RenderScene()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ProDomino_MainCanvas");
            if (!canvas) throw new Exception("ProDomino_MainCanvas not found in scene.");

            Render(Path.Combine(outDir, "scene_friends_empty.png"), (p, s) => { }, canvas);
        }

        private static void Render(string path, Action<Transform, Action<string, bool>> setup, GameObject existing = null)
        {
            SidebarRestyler.RenderCanvas(path, 1440, 1024, true, root =>
            {
                var dash = FindDeep(root.transform, "Dashboard_Content");
                if (dash && dash.TryGetComponent<CanvasGroup>(out var dcg)) dcg.alpha = 0f;

                var popup = FindDeep(root.transform, "FriendList_Popup");
                var popupCg = popup.GetComponent<CanvasGroup>();
                popupCg.alpha = 1f; popupCg.interactable = true; popupCg.blocksRaycasts = true;
                void Set(string n, bool on) { var t = FindDeep(popup, n); if (t) t.gameObject.SetActive(on); }
                setup(popup, Set);

                var type = FindType("CustomButtonUI");
                var preview = type.GetMethod("PreviewVisualState");
                var idProp = type.GetProperty("CustomButtonID");
                foreach (var b in root.GetComponentsInChildren(type, true))
                {
                    var id = (string)idProp.GetValue(b);
                    if (!string.IsNullOrEmpty(id))
                        preview.Invoke(b, new object[] { id == "FriendsList" });
                }

                foreach (var g in popup.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
            }, existing);
        }
    }
}
