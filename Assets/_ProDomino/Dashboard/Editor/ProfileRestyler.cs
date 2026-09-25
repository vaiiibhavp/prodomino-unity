using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProDomino.AccountSystem;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    public static class ProfileRestyler
    {
        private const string PrefabPath = "Assets/_ProDomino/AccountSystem/Prefabs/AccountData_PopUp.prefab";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite cardBg, statCardBg, tabBg, tabActiveBg, avatarRingBg, inputBg;
        private static Sprite goldBtnNormal, goldBtnHover, dangerBtnBg;

        [MenuItem("ProDomino/Dashboard/Restyle Profile Screen")]
        public static void Apply()
        {
            Debug.Log("[ProfileRestyler] Starting Profile screen restyle...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            PrepareAssets();
            StylePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProfileRestyler] SUCCESS: Profile screen restyled.");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            cardBg = MakePanelSprite("Profile_CardBg", 64, 64, 14, Hex("#0D1120"), Hex("#070A14"), Hex("#1E2538"), 1.2f, true);
            statCardBg = MakePanelSprite("Profile_StatCardBg", 48, 48, 10, Hex("#111828"), Hex("#0A0E1C"), Hex("#1E2538"), 1f, true);
            tabBg = MakeRoundedSprite("Profile_TabBg", 48, 48, 24, Hex("#111828"), Hex("#111828"));
            tabActiveBg = MakeRoundedSprite("Profile_TabActiveBg", 200, 48, 24, Hex("#3B82F6"), Hex("#2563EB"));
            avatarRingBg = MakeCircleSprite("Profile_AvatarRing", 128);
            inputBg = MakePanelSprite("Profile_InputBg", 48, 48, 10, Hex("#111625"), Hex("#0E1320"), Hex("#222B3D"), 1.2f, true);
            goldBtnNormal = MakeRoundedSprite("Profile_GoldBtn", 200, 56, 28, AccentStart, AccentEnd);
            goldBtnHover = MakeRoundedSprite("Profile_GoldBtnHover", 200, 56, 28, Hex("#FFB300"), Hex("#FFCA28"));
            dangerBtnBg = MakePanelSprite("Profile_DangerBtn", 48, 48, 10, Hex("#7F1D1D"), Hex("#991B1B"), Hex("#EF4444"), 1f, true);
        }

        private static void StylePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab, PrefabPath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rootRt = (RectTransform)root.transform;
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                // Panel = dark overlay
                var panel = root.transform.Find("Panel");
                if (panel != null)
                {
                    var panelImg = panel.GetComponent<Image>();
                    if (panelImg != null)
                    {
                        panelImg.color = new Color(0f, 0f, 0f, 0.6f);
                        panelImg.sprite = null;
                    }
                }

                // ProfileInfo_Scalable = the content card
                var info = root.transform.Find("ProfileInfo_Scalable");
                if (info == null)
                {
                    Debug.LogError("[ProfileRestyler] ProfileInfo_Scalable not found!");
                    PrefabUtility.UnloadPrefabContents(root);
                    return;
                }

                var infoRt = (RectTransform)info;

                // Reanchor content card to be centered, sized to fill the screen card area
                infoRt.anchorMin = new Vector2(0.17f, 0.05f);
                infoRt.anchorMax = new Vector2(0.98f, 0.95f);
                infoRt.offsetMin = Vector2.zero;
                infoRt.offsetMax = Vector2.zero;
                infoRt.pivot = new Vector2(0.5f, 0.5f);

                // Add screen card background to content area
                var infoBg = GetOrAdd<Image>(info);
                infoBg.sprite = GetOrCreateScreenCardSprite();
                infoBg.type = Image.Type.Sliced;
                infoBg.color = Color.white;
                infoBg.raycastTarget = true;

                // Destroy existing children of ProfileInfo_Scalable to rebuild
                for (int i = info.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(info.GetChild(i).gameObject);

                // ==================== HEADER ====================
                var header = MakeNode(info, "Header");
                TL((RectTransform)header, 28f, 24f, 300f, 36f);

                var headerIcon = MakeImg(header, "Icon", null, Hex("#FFA800"), 22f, 22f);
                MiddleLeft((RectTransform)headerIcon.transform, 0f, 22f, 22f);

                var headerTitle = MakeText(header, "TitleText", "My Profile", fBold, 24f, Color.white);
                var titleRt = (RectTransform)headerTitle.transform;
                titleRt.anchorMin = new Vector2(0f, 0f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(34f, 0f);
                titleRt.offsetMax = Vector2.zero;

                // ==================== AVATAR INFO CARD (Left) ====================
                var avatarCard = MakeNode(info, "AvatarInfoCard");
                TL((RectTransform)avatarCard, 28f, 76f, 260f, 320f);

                var avatarCardBg = GetOrAdd<Image>(avatarCard);
                avatarCardBg.sprite = statCardBg;
                avatarCardBg.type = Image.Type.Sliced;
                avatarCardBg.color = Color.white;

                // Gold Member badge
                var badgeBg = MakeImg(avatarCard, "BadgeBg", goldBtnNormal, Color.white, 120f, 26f);
                TL((RectTransform)badgeBg.transform, 20f, 12f, 120f, 26f);
                var badgeText = MakeText(avatarCard, "BadgeText", "Gold Member", fSemiBold, 11f, OnAccent);
                TL((RectTransform)badgeText.transform, 20f, 12f, 120f, 26f);
                badgeText.alignment = TextAlignmentOptions.Center;

                // Avatar circle
                var avatarHolder = MakeNode(avatarCard, "AvatarHolder");
                TLCentered((RectTransform)avatarHolder, 48f, 100f, 100f);

                var avatarRing = MakeImg(avatarHolder, "AvatarRing", avatarRingBg, Hex("#E67E22"), 100f, 100f);
                Stretch((RectTransform)avatarRing.transform);

                var avatarMask = MakeNode(avatarHolder, "AvatarMask");
                var maskRt = (RectTransform)avatarMask;
                maskRt.anchorMin = new Vector2(0.05f, 0.05f);
                maskRt.anchorMax = new Vector2(0.95f, 0.95f);
                maskRt.offsetMin = Vector2.zero;
                maskRt.offsetMax = Vector2.zero;
                var mask = avatarMask.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var maskImg = avatarMask.gameObject.AddComponent<Image>();
                maskImg.sprite = avatarRingBg;
                maskImg.color = Color.white;

                var profileImg = MakeImg(avatarMask, "ProfileImage", null, Color.white, 90f, 90f);
                Stretch((RectTransform)profileImg.transform);

                // Username
                var userName = MakeText(avatarCard, "Text_User_Name", "Martin", fBold, 20f, Color.white);
                TLCentered((RectTransform)userName.transform, 158f, 200f, 28f);
                userName.alignment = TextAlignmentOptions.Center;

                // Email / ID
                var userId = MakeText(avatarCard, "Text_ID", "martin123@gmail.com", fRegular, 12f, TextMuted);
                TLCentered((RectTransform)userId.transform, 186f, 200f, 18f);
                userId.alignment = TextAlignmentOptions.Center;

                // Edit Profile button
                var editBtnGo = MakeNode(avatarCard, "EditProfileBtn");
                TLCentered((RectTransform)editBtnGo, 216f, 140f, 36f);
                var editBtnImg = editBtnGo.gameObject.AddComponent<Image>();
                editBtnImg.sprite = goldBtnNormal;
                editBtnImg.type = Image.Type.Sliced;
                editBtnImg.color = Color.white;
                var editBtn = editBtnGo.gameObject.AddComponent<Button>();
                NeutralTint(editBtnGo, editBtnImg);
                var editBtnText = MakeText(editBtnGo, "BtnText", "Edit Profile", fSemiBold, 13f, OnAccent);
                Stretch((RectTransform)editBtnText.transform);
                editBtnText.alignment = TextAlignmentOptions.Center;

                // Delete button (small red)
                var delBtnGo = MakeNode(avatarCard, "DeleteBtn");
                TL((RectTransform)delBtnGo, 190f, 216f, 36f, 36f);
                var delBtnImg = delBtnGo.gameObject.AddComponent<Image>();
                delBtnImg.sprite = dangerBtnBg;
                delBtnImg.type = Image.Type.Sliced;
                delBtnImg.color = Color.white;
                delBtnGo.gameObject.AddComponent<Button>();
                var delIcon = MakeText(delBtnGo, "DelIcon", "✖", fBold, 14f, Color.white);
                Stretch((RectTransform)delIcon.transform);
                delIcon.alignment = TextAlignmentOptions.Center;

                // ==================== STATS CARDS ROW (Right of avatar) ====================
                var statsRow = MakeNode(info, "StatsRow");
                var statsRt = (RectTransform)statsRow;
                statsRt.anchorMin = new Vector2(0f, 1f);
                statsRt.anchorMax = new Vector2(1f, 1f);
                statsRt.pivot = new Vector2(0f, 1f);
                statsRt.anchoredPosition = new Vector2(300f, -76f);
                statsRt.sizeDelta = new Vector2(-328f, 120f);

                var hlg = statsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 12f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.padding = new RectOffset(0, 0, 0, 0);

                BuildStatCard(statsRow, "CompletedAchievement_Entry", "12", "Completed\nAchievements");
                BuildStatCard(statsRow, "AchievementsPoints_Entry", "380", "Achievement\nPoints");
                BuildStatCard(statsRow, "TotalMatches_Entry", "120", "Total Matches\nPlayed");
                BuildStatCard(statsRow, "TotalGameTime_Entry", "43m 8s", "Total Game\nTime");

                // ==================== TAB BAR ====================
                var tabBar = MakeNode(info, "TabBar");
                var tabBarRt = (RectTransform)tabBar;
                tabBarRt.anchorMin = new Vector2(0f, 1f);
                tabBarRt.anchorMax = new Vector2(1f, 1f);
                tabBarRt.pivot = new Vector2(0f, 1f);
                tabBarRt.anchoredPosition = new Vector2(28f, -220f);
                tabBarRt.sizeDelta = new Vector2(-56f, 50f);

                var tabBarBg = GetOrAdd<Image>(tabBar);
                tabBarBg.sprite = statCardBg;
                tabBarBg.type = Image.Type.Sliced;
                tabBarBg.color = Color.white;

                var tabHlg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                tabHlg.spacing = 4f;
                tabHlg.padding = new RectOffset(4, 4, 4, 4);
                tabHlg.childAlignment = TextAnchor.MiddleCenter;
                tabHlg.childControlWidth = true;
                tabHlg.childControlHeight = true;
                tabHlg.childForceExpandWidth = true;
                tabHlg.childForceExpandHeight = true;

                BuildTab(tabBar, "BlockTab", "Block Game", true);
                BuildTab(tabBar, "ConcentrateTab", "Concentrate Game", false);

                // ==================== BOTTOM CONTENT ====================
                // Left: Elo Rating card
                var eloCard = MakeNode(info, "EloRatingCard");
                var eloRt = (RectTransform)eloCard;
                eloRt.anchorMin = new Vector2(0f, 0f);
                eloRt.anchorMax = new Vector2(0.48f, 1f);
                eloRt.pivot = new Vector2(0f, 1f);
                eloRt.offsetMin = new Vector2(28f, 28f);
                eloRt.offsetMax = new Vector2(0f, -284f);

                var eloCardBg = GetOrAdd<Image>(eloCard);
                eloCardBg.sprite = statCardBg;
                eloCardBg.type = Image.Type.Sliced;
                eloCardBg.color = Color.white;

                var eloTitle = MakeText(eloCard, "EloTitle", "Elo Rating", fBold, 16f, Color.white);
                TL((RectTransform)eloTitle.transform, 20f, 16f, 120f, 24f);

                var eloDropdown = MakeText(eloCard, "EloDropdown", "All Time ▾", fMedium, 12f, TextMuted);
                var edRt = (RectTransform)eloDropdown.transform;
                edRt.anchorMin = new Vector2(1f, 1f);
                edRt.anchorMax = new Vector2(1f, 1f);
                edRt.pivot = new Vector2(1f, 1f);
                edRt.anchoredPosition = new Vector2(-16f, -18f);
                edRt.sizeDelta = new Vector2(80f, 20f);
                eloDropdown.alignment = TextAlignmentOptions.Right;

                // Placeholder for chart area
                var chartArea = MakeNode(eloCard, "ChartArea");
                var chartRt = (RectTransform)chartArea;
                chartRt.anchorMin = new Vector2(0f, 0f);
                chartRt.anchorMax = new Vector2(1f, 1f);
                chartRt.offsetMin = new Vector2(16f, 20f);
                chartRt.offsetMax = new Vector2(-16f, -48f);

                // Right column: Stats + Achievements
                var rightCol = MakeNode(info, "RightColumn");
                var rcRt = (RectTransform)rightCol;
                rcRt.anchorMin = new Vector2(0.48f, 0f);
                rcRt.anchorMax = new Vector2(1f, 1f);
                rcRt.pivot = new Vector2(0f, 1f);
                rcRt.offsetMin = new Vector2(12f, 28f);
                rcRt.offsetMax = new Vector2(-28f, -284f);

                var rcVlg = rightCol.gameObject.AddComponent<VerticalLayoutGroup>();
                rcVlg.spacing = 12f;
                rcVlg.padding = new RectOffset(0, 0, 0, 0);
                rcVlg.childAlignment = TextAnchor.UpperLeft;
                rcVlg.childControlWidth = true;
                rcVlg.childControlHeight = false;
                rcVlg.childForceExpandWidth = true;
                rcVlg.childForceExpandHeight = false;

                // Stats row (Elo, Wins, Placement)
                var miniStatsRow = MakeNode(rightCol, "MiniStatsRow");
                var msLe = miniStatsRow.gameObject.AddComponent<LayoutElement>();
                msLe.preferredHeight = 80f;
                var msHlg = miniStatsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                msHlg.spacing = 12f;
                msHlg.childControlWidth = true;
                msHlg.childControlHeight = true;
                msHlg.childForceExpandWidth = true;
                msHlg.childForceExpandHeight = true;

                BuildMiniStatCard(miniStatsRow, "EloStat", "1200", "Elo Rating");
                BuildMiniStatCard(miniStatsRow, "WinsStat", "12", "Wins");
                BuildMiniStatCard(miniStatsRow, "PlacementStat", "100 / 50 / 50", "2nd / 3rd / 4th");

                // Achievements section
                var achieveCard = MakeNode(rightCol, "AchievementsSection");
                var acLe = achieveCard.gameObject.AddComponent<LayoutElement>();
                acLe.flexibleHeight = 1f;
                acLe.preferredHeight = 200f;

                var acBg = GetOrAdd<Image>(achieveCard);
                acBg.sprite = statCardBg;
                acBg.type = Image.Type.Sliced;
                acBg.color = Color.white;

                var acTitle = MakeText(achieveCard, "AchievementsTitle", "Achievements", fBold, 16f, Color.white);
                TL((RectTransform)acTitle.transform, 20f, 16f, 200f, 24f);

                // Leaderboard entries parent (used by AccountDataController)
                var entriesParent = MakeNode(achieveCard, "EntriesParent");
                var epRt = (RectTransform)entriesParent;
                epRt.anchorMin = new Vector2(0f, 0f);
                epRt.anchorMax = new Vector2(1f, 1f);
                epRt.offsetMin = new Vector2(12f, 12f);
                epRt.offsetMax = new Vector2(-12f, -48f);

                var epVlg = entriesParent.gameObject.AddComponent<VerticalLayoutGroup>();
                epVlg.spacing = 8f;
                epVlg.childControlWidth = true;
                epVlg.childControlHeight = false;
                epVlg.childForceExpandWidth = true;
                epVlg.childForceExpandHeight = false;

                // ==================== BACK BUTTON ====================
                var backBtn = MakeNode(info, "BackButton");
                var bbRt = (RectTransform)backBtn;
                bbRt.anchorMin = new Vector2(1f, 1f);
                bbRt.anchorMax = new Vector2(1f, 1f);
                bbRt.pivot = new Vector2(1f, 1f);
                bbRt.anchoredPosition = new Vector2(-20f, -20f);
                bbRt.sizeDelta = new Vector2(36f, 36f);

                var bbImg = backBtn.gameObject.AddComponent<Image>();
                bbImg.sprite = statCardBg;
                bbImg.type = Image.Type.Sliced;
                bbImg.color = Color.white;
                var bbButton = backBtn.gameObject.AddComponent<Button>();
                NeutralTint(backBtn, bbImg);

                var bbX = MakeText(backBtn, "XText", "✕", fBold, 18f, TextMuted);
                Stretch((RectTransform)bbX.transform);
                bbX.alignment = TextAlignmentOptions.Center;

                // ==================== PAGE LABEL (bottom center) ====================
                var pageLabel = MakeText(info, "PageLabel", "1/1", fMedium, 12f, TextMuted);
                var plRt = (RectTransform)pageLabel.transform;
                plRt.anchorMin = new Vector2(0.5f, 0f);
                plRt.anchorMax = new Vector2(0.5f, 0f);
                plRt.pivot = new Vector2(0.5f, 0f);
                plRt.anchoredPosition = new Vector2(0f, 8f);
                plRt.sizeDelta = new Vector2(60f, 20f);
                pageLabel.alignment = TextAlignmentOptions.Center;

                // ==================== REWIRE AccountDataController ====================
                RewireController(root, info);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RewireController(GameObject root, Transform info)
        {
            var ctrl = root.GetComponent<AccountDataController>();
            if (ctrl == null)
            {
                Debug.LogWarning("[ProfileRestyler] AccountDataController not found, skipping rewire.");
                return;
            }

            var so = new SerializedObject(ctrl);

            WireTmp(so, "usernameLabel", info, "Text_User_Name");
            WireTmp(so, "userIDLabel", info, "Text_ID");
            WireTmp(so, "currentPageLabel", info, "PageLabel");

            // Stats cards value labels
            WireTmp(so, "completedAchievementsLabel", info, "CompletedAchievement_Entry/ValueText");
            WireTmp(so, "achievementPointsLabel", info, "AchievementsPoints_Entry/ValueText");
            WireTmp(so, "totalMatchPlayedLabel", info, "TotalMatches_Entry/ValueText");
            WireTmp(so, "totalGameTimeLabel", info, "TotalGameTime_Entry/ValueText");

            // Profile image
            var profileImgT = FindDeep(info, "ProfileImage");
            if (profileImgT != null)
            {
                var prop = so.FindProperty("profileImage");
                if (prop != null)
                    prop.objectReferenceValue = profileImgT.GetComponent<Image>();
            }

            // Leaderboard entries parent
            var entriesParent = FindDeep(info, "EntriesParent");
            if (entriesParent != null)
            {
                var prop = so.FindProperty("leaderboardAccountParent");
                if (prop != null)
                    prop.objectReferenceValue = entriesParent;
            }

            // Back button
            var backBtnT = FindDeep(info, "BackButton");
            if (backBtnT != null)
            {
                var prop = so.FindProperty("backButton");
                if (prop != null)
                    prop.objectReferenceValue = backBtnT.GetComponent<Button>();
            }

            // Arrow buttons (pagination) - hidden for now
            var leftProp = so.FindProperty("leftArrow");
            if (leftProp != null) leftProp.objectReferenceValue = null;
            var rightProp = so.FindProperty("rightArrow");
            if (rightProp != null) rightProp.objectReferenceValue = null;

            // CanvasGroup
            var cgProp = so.FindProperty("rootCanvasGroup");
            if (cgProp != null)
                cgProp.objectReferenceValue = root.GetComponent<CanvasGroup>();

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireTmp(SerializedObject so, string propName, Transform root, string path)
        {
            var target = FindDeepPath(root, path);
            if (target == null) return;

            var tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return;

            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = tmp;
        }

        private static Transform FindDeepPath(Transform root, string path)
        {
            var parts = path.Split('/');
            var current = root;
            foreach (var part in parts)
            {
                current = FindDeep(current, part);
                if (current == null) return null;
            }
            return current;
        }

        // ==================== BUILDERS ====================

        private static void BuildStatCard(Transform parent, string name, string value, string label)
        {
            var card = MakeNode(parent, name);

            var bg = GetOrAdd<Image>(card);
            bg.sprite = statCardBg;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var valText = MakeText(card, "ValueText", value, fExtraBold, 32f, Color.white);
            var valRt = (RectTransform)valText.transform;
            valRt.anchorMin = new Vector2(0f, 0.5f);
            valRt.anchorMax = new Vector2(1f, 1f);
            valRt.offsetMin = new Vector2(48f, 0f);
            valRt.offsetMax = new Vector2(-8f, -8f);
            valText.alignment = TextAlignmentOptions.Left;
            valText.enableAutoSizing = true;
            valText.fontSizeMin = 16f;
            valText.fontSizeMax = 32f;

            var lblText = MakeText(card, "LabelText", label, fRegular, 11f, TextMuted);
            var lblRt = (RectTransform)lblText.transform;
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0.5f);
            lblRt.offsetMin = new Vector2(48f, 8f);
            lblRt.offsetMax = new Vector2(-8f, 0f);
            lblText.alignment = TextAlignmentOptions.Left;
            lblText.textWrappingMode = TextWrappingModes.Normal;
            lblText.enableAutoSizing = true;
            lblText.fontSizeMin = 8f;
            lblText.fontSizeMax = 11f;
        }

        private static void BuildMiniStatCard(Transform parent, string name, string value, string label)
        {
            var card = MakeNode(parent, name);

            var bg = GetOrAdd<Image>(card);
            bg.sprite = statCardBg;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var valText = MakeText(card, "ValueText", value, fExtraBold, 28f, Color.white);
            TLCentered((RectTransform)valText.transform, 14f, 150f, 34f);
            valText.alignment = TextAlignmentOptions.Center;
            valText.enableAutoSizing = true;
            valText.fontSizeMin = 14f;
            valText.fontSizeMax = 28f;

            var lblText = MakeText(card, "LabelText", label, fRegular, 10f, TextMuted);
            var lblRt = (RectTransform)lblText.transform;
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.pivot = new Vector2(0.5f, 0f);
            lblRt.anchoredPosition = new Vector2(0f, 8f);
            lblRt.sizeDelta = new Vector2(-8f, 20f);
            lblText.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildTab(Transform parent, string name, string label, bool active)
        {
            var tab = MakeNode(parent, name);

            var bg = GetOrAdd<Image>(tab);
            bg.sprite = active ? tabActiveBg : tabBg;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var btn = tab.gameObject.AddComponent<Button>();
            NeutralTint(tab, bg);

            var text = MakeText(tab, "TabText", label, active ? fSemiBold : fMedium, 15f, active ? Color.white : TextMuted);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Center;
        }

        // ==================== HELPERS ====================

        private static Transform MakeNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return go.transform;
        }

        private static GameObject MakeImg(Transform parent, string name, Sprite sprite, Color color, float w, float h)
        {
            var go = MakeImage(parent, name, sprite, color, Image.Type.Simple);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }
    }
}
