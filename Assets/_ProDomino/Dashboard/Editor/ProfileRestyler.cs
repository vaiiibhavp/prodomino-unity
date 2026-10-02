using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProDomino.AccountSystem;
using static ProDomino.Dashboard.Editor.PdUiKit;
using Object = UnityEngine.Object;

namespace ProDomino.Dashboard.Editor
{
    // Rebuilds the "My Profile" screen (AccountData_PopUp) to the dashboard design: header, avatar column
    // with four stat cards, then a framed section with game tabs, the Elo card, mini stats and the
    // per-mode records list. Every reference AccountDataController / LeaderboardAccountEntry use is rewired.
    public static class ProfileRestyler
    {
        private const string PrefabPath = "Assets/_ProDomino/AccountSystem/Prefabs/AccountData_PopUp.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/AccountSystem/Prefabs/LeaderboardAccount_Entry.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string MainCanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
        private const string InstanceName = "AccountData_PopUp";

        private const string TrophyIcon = "Assets/_ProDomino/_Art/Dashboard/QuickMatch_Competitive_Trophy.png";
        private const string CoinIcon = "Assets/_ProDomino/_Art/Dashboard/Icon_Coin_Raster.png";
        private const string TilesIcon = "Assets/_ProDomino/_UI/Icons/Icons_Achievement/1000_tiles_Achiev_Icon.png";
        private const string HornIcon = "Assets/_ProDomino/Dashboard/Generated/Achiev_Horn_Gold.png";
        private const string QuestIcon = "Assets/_ProDomino/Dashboard/Generated/Achiev_Quest_Badge.png";
        private const string StarIcon = "Assets/_ProDomino/Dashboard/Generated/Achiev_Star_Gold.png";
        private const string ChevronIcon = "Assets/_ProDomino/Dashboard/Generated/Chevron_Down.png";
        private const string CloseIcon = "Assets/_ProDomino/Dashboard/Generated/Shop_Close_Btn.png";
        private const string DefaultAvatar = "Assets/_ProDomino/_Art/Dashboard/Header_ProfileAvatar_Default.png";

        private static readonly Color TextSoft = Hex("#8A8FA3");
        private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.06f);

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite sectionBg, statCardBg, tabBg, tabActiveBg, circle, goldBtn, dangerBtn,
            rowBg, pillBg, iconBoxBg, headerMark, trashIcon, divider;

        [MenuItem("ProDomino/Dashboard/Restyle Profile Screen")]
        public static void Apply()
        {
            PrepareAssets();
            StyleEntryPrefab();
            StylePrefab();
            CleanInstance(MiddleScreenPath);
            CleanInstance(MainCanvasPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProfileRestyler] SUCCESS: Profile screen restyled.");
        }

        // The open game scene keeps its own overrides on the popup subtree; drop them so the prefab drives it.
        [MenuItem("ProDomino/Dashboard/Restyle Profile Screen (Clean Scene Overrides)")]
        public static void CleanSceneOverrides()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning($"[ProfileRestyler] Active scene is {scene.path}, expected {ScenePath}.");
                return;
            }
            var canvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ProDomino_MainCanvas");
            var popup = canvas ? FindDeep(canvas.transform, InstanceName) : null;
            if (!popup) { Debug.LogError("[ProfileRestyler] Popup not found in scene."); return; }

            int reverted = 0;
            foreach (var t in popup.GetComponentsInChildren<Transform>(true))
            {
                if (t == popup) continue; // the root keeps its active-state / canvas-group values
                if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                foreach (var c in t.GetComponents<Component>())
                {
                    if (!c) continue;
                    PrefabUtility.RevertObjectOverride(c, InteractionMode.AutomatedAction);
                    reverted++;
                }
                PrefabUtility.RevertObjectOverride(t.gameObject, InteractionMode.AutomatedAction);
            }
            CopyRectFromSibling((RectTransform)popup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ProfileRestyler] Scene overrides cleaned ({reverted} components).");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            sectionBg = MakePanelSprite("Profile_SectionBg", 64, 64, 14, Hex("#0B1020"), Hex("#060914"), Hex("#1C2438"), 1.2f);
            statCardBg = MakePanelSprite("Profile_StatCardBg", 48, 48, 10, Hex("#121A2C"), Hex("#0A0F1D"), Hex("#232C42"), 1f);
            tabBg = MakePanelSprite("Profile_TabBarBg", 48, 48, 8, Hex("#0E1424"), Hex("#0E1424"), Hex("#1E2638"), 1f);
            tabActiveBg = MakeRoundedSprite("Profile_TabActiveBg", 200, 40, 8, Hex("#3B6FD8"), Hex("#6FA2F5"));
            circle = MakeCircleSprite("Profile_AvatarRing", 128);
            goldBtn = MakeRoundedSprite("Profile_GoldBtn", 120, 32, 6, AccentStart, AccentEnd);
            dangerBtn = MakeRoundedSprite("Profile_DangerBtn", 32, 32, 6, Hex("#E5484D"), Hex("#C9343A"));
            rowBg = MakePanelSprite("Profile_RowBg", 48, 48, 8, Hex("#141B2D"), Hex("#0D1322"), Hex("#232C42"), 1f);
            pillBg = MakePanelSprite("Profile_PillBg", 32, 32, 6, Hex("#141B2D"), Hex("#141B2D"), Hex("#2A3450"), 1f);
            iconBoxBg = MakePanelSprite("Profile_IconBoxBg", 32, 32, 6, Hex("#2A1F12"), Hex("#1A140C"), Hex("#4A3418"), 1f);
            headerMark = MakeRoundedSprite("Profile_HeaderMark", 24, 24, 4, AccentStart, AccentEnd);
            divider = MakeRoundedSprite("Profile_Divider", 4, 4, 0, Color.white, Color.white);
            trashIcon = MakeTrashIcon();
        }

        // ------------------------------------------------------------------ popup
        private static void StylePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Stretch((RectTransform)root.transform);

                // The screen sits inside the content area like the other sections: no dim overlay.
                var panel = root.transform.Find("Panel");
                if (panel && panel.TryGetComponent<Image>(out var panelImg))
                {
                    panelImg.sprite = null;
                    panelImg.color = new Color(0f, 0f, 0f, 0f);
                }

                var info = root.transform.Find("ProfileInfo_Scalable");
                Require(info, "ProfileInfo_Scalable");
                var infoRt = (RectTransform)info;
                Stretch(infoRt);
                infoRt.pivot = new Vector2(0.5f, 0.5f);
                infoRt.localScale = Vector3.one;
                var infoBg = GetOrAdd<Image>(info);
                infoBg.sprite = GetOrCreateScreenCardSprite();
                infoBg.type = Image.Type.Sliced;
                infoBg.color = Color.white;
                infoBg.raycastTarget = true;
                foreach (var c in info.GetComponents<LayoutGroup>()) Object.DestroyImmediate(c);
                foreach (var c in info.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(c);

                for (int i = info.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(info.GetChild(i).gameObject);

                BuildHeader(info);
                BuildTopRow(info);
                BuildSection(info);

                RewireController(root, info);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildHeader(Transform info)
        {
            var header = Node(info, "Header");
            TopBand(header, 24f, 22f, 24f, 36f);

            var mark = Img(header, "Icon", headerMark, Color.white, Image.Type.Sliced);
            MiddleLeft(mark, 0f, 20f, 20f);

            var title = MakeText(header, "TitleText", "My Profile", fBold, 24f, Color.white);
            Fill(title.rectTransform, 32f, 0f, 0f, 0f);

            // Back / close button, top-right of the card.
            var back = Node(info, "BackButton");
            back.anchorMin = back.anchorMax = Vector2.one;
            back.pivot = Vector2.one;
            back.anchoredPosition = new Vector2(-22f, -22f);
            back.sizeDelta = new Vector2(36f, 36f);
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = Load<Sprite>(CloseIcon);
            backImg.preserveAspect = true;
            backImg.color = Color.white;
            back.gameObject.AddComponent<Button>();
            NeutralTint(back, backImg);
        }

        private static void BuildTopRow(Transform info)
        {
            var row = Node(info, "TopRow");
            TopBand(row, 24f, 72f, 24f, 150f);

            // ---- avatar column
            var avatarCard = Node(row, "AvatarInfoCard");
            avatarCard.anchorMin = new Vector2(0f, 0f);
            avatarCard.anchorMax = new Vector2(0f, 1f);
            avatarCard.pivot = new Vector2(0f, 0.5f);
            avatarCard.anchoredPosition = Vector2.zero;
            avatarCard.sizeDelta = new Vector2(190f, 0f);

            var holder = Node(avatarCard, "AvatarHolder");
            TLCentered(holder, 6f, 72f, 72f);
            var ring = Img(holder, "AvatarRing", circle, Hex("#F28C28"), Image.Type.Simple);
            Stretch(ring);
            var maskRt = Node(holder, "AvatarMask");
            maskRt.anchorMin = new Vector2(0.06f, 0.06f);
            maskRt.anchorMax = new Vector2(0.94f, 0.94f);
            maskRt.offsetMin = maskRt.offsetMax = Vector2.zero;
            var maskImg = maskRt.gameObject.AddComponent<Image>();
            maskImg.sprite = circle;
            maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var profile = Img(maskRt, "ProfileImage", Load<Sprite>(DefaultAvatar), Color.white, Image.Type.Simple);
            Stretch(profile);
            profile.GetComponent<Image>().preserveAspect = true;

            // "Gold Member" badge overlaps the top-left of the avatar.
            var badge = Img(avatarCard, "Badge", goldBtn, Color.white, Image.Type.Sliced);
            TLCentered(badge, 0f, 86f, 18f);
            badge.anchoredPosition = new Vector2(-36f, 0f);
            var badgeText = MakeText(badge, "BadgeText", "Gold Member", fBold, 9f, OnAccent);
            Stretch(badgeText.rectTransform);
            badgeText.alignment = TextAlignmentOptions.Center;

            var name = MakeText(avatarCard, "Text_User_Name", "Martin", fBold, 16f, Color.white);
            TLCentered(name.rectTransform, 82f, 186f, 20f);
            Label(name, fBold, 16f, Color.white).alignment = TextAlignmentOptions.Center;

            var id = MakeText(avatarCard, "Text_ID", "martin123@gmail.com", fRegular, 11f, TextSoft);
            TLCentered(id.rectTransform, 103f, 186f, 16f);
            Label(id, fRegular, 11f, TextSoft).alignment = TextAlignmentOptions.Center;

            var buttons = Node(avatarCard, "Buttons");
            TLCentered(buttons, 124f, 118f, 24f);
            var edit = Node(buttons, "EditProfileBtn");
            TL(edit, 0f, 0f, 88f, 24f);
            var editImg = edit.gameObject.AddComponent<Image>();
            editImg.sprite = goldBtn; editImg.type = Image.Type.Sliced;
            edit.gameObject.AddComponent<Button>();
            NeutralTint(edit, editImg);
            var editText = MakeText(edit, "BtnText", "Edit Profile", fBold, 10f, OnAccent);
            Stretch(editText.rectTransform);
            editText.alignment = TextAlignmentOptions.Center;

            var del = Node(buttons, "DeleteBtn");
            TL(del, 94f, 0f, 24f, 24f);
            var delImg = del.gameObject.AddComponent<Image>();
            delImg.sprite = dangerBtn; delImg.type = Image.Type.Sliced;
            del.gameObject.AddComponent<Button>();
            NeutralTint(del, delImg);
            var delIcon = Img(del, "Icon", trashIcon, Color.white, Image.Type.Simple);
            delIcon.anchorMin = delIcon.anchorMax = new Vector2(0.5f, 0.5f);
            delIcon.sizeDelta = new Vector2(14f, 14f);

            // ---- separator
            var sep = Img(row, "Separator", divider, new Color(1f, 1f, 1f, 0.08f), Image.Type.Simple);
            sep.anchorMin = new Vector2(0f, 0.12f);
            sep.anchorMax = new Vector2(0f, 0.88f);
            sep.pivot = new Vector2(0f, 0.5f);
            sep.anchoredPosition = new Vector2(206f, 0f);
            sep.sizeDelta = new Vector2(1f, 0f);

            // ---- four stat cards
            var stats = Node(row, "StatsRow");
            stats.anchorMin = new Vector2(0f, 0.5f);
            stats.anchorMax = new Vector2(1f, 0.5f);
            stats.pivot = new Vector2(0f, 0.5f);
            stats.offsetMin = new Vector2(224f, -40f);
            stats.offsetMax = new Vector2(-48f, 40f);
            var hlg = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

            StatCard(stats, "CompletedAchievement_Entry", TrophyIcon, "0", "Completed\nAchievements");
            StatCard(stats, "AchievementsPoints_Entry", CoinIcon, "0", "Achievement\nPoints");
            StatCard(stats, "TotalMatches_Entry", TilesIcon, "0", "Total Matches\nPlayed");
            StatCard(stats, "TotalGameTime_Entry", HornIcon, "0s", "Total Game\nTime");
        }

        private static void BuildSection(Transform info)
        {
            var section = Img(info, "ProfileSection", sectionBg, Color.white, Image.Type.Sliced);
            Fill(section, 24f, 24f, 24f, 238f);

            // ---- tab bar
            var tabBar = Img(section, "TabBar", tabBg, Color.white, Image.Type.Sliced);
            TopBand(tabBar, 18f, 18f, 18f, 44f);
            var tabHlg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabHlg.padding = new RectOffset(4, 4, 4, 4);
            tabHlg.spacing = 4f;
            tabHlg.childControlWidth = tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = tabHlg.childForceExpandHeight = true;
            Tab(tabBar, "BlockTab", "Block Game", true);
            Tab(tabBar, "ConcentrateTab", "Concentrate Game", false);

            // ---- Elo card (left 46%)
            var elo = Img(section, "EloRatingCard", statCardBg, Color.white, Image.Type.Sliced);
            elo.anchorMin = Vector2.zero;
            elo.anchorMax = new Vector2(0.46f, 1f);
            elo.offsetMin = new Vector2(18f, 18f);
            elo.offsetMax = new Vector2(-8f, -76f);
            BuildEloCard(elo);

            // ---- right column
            var right = Node(section, "RightColumn");
            right.anchorMin = new Vector2(0.46f, 0f);
            right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(8f, 18f);
            right.offsetMax = new Vector2(-18f, -76f);

            var mini = Node(right, "MiniStatsRow");
            TopBand(mini, 0f, 0f, 0f, 84f);
            var miniHlg = mini.gameObject.AddComponent<HorizontalLayoutGroup>();
            miniHlg.spacing = 12f;
            miniHlg.childControlWidth = miniHlg.childControlHeight = true;
            miniHlg.childForceExpandWidth = miniHlg.childForceExpandHeight = true;
            MiniStat(mini, "EloStat", "1200", "Elo Rating", 1f);
            MiniStat(mini, "WinsStat", "12", "Wins", 1f);
            MiniStat(mini, "PlacementStat", "100 / 50 / 50", "2nd / 3rd / 4th", 2f);

            var ach = Img(right, "AchievementsSection", statCardBg, Color.white, Image.Type.Sliced);
            Fill(ach, 0f, 0f, 0f, 96f);
            var achTitle = MakeText(ach, "AchievementsTitle", "Achievements", fBold, 16f, Color.white);
            TopBand(achTitle.rectTransform, 18f, 14f, 120f, 24f);

            // Pagination (wired to AccountDataController's arrows; shown only with more than one page).
            var pager = Node(ach, "Pager");
            pager.anchorMin = pager.anchorMax = Vector2.one;
            pager.pivot = Vector2.one;
            pager.anchoredPosition = new Vector2(-14f, -12f);
            pager.sizeDelta = new Vector2(108f, 28f);
            PagerArrow(pager, "LeftArrow", true);
            var page = MakeText(pager, "PageLabel", "1/1", fMedium, 12f, TextSoft);
            page.rectTransform.anchorMin = page.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            page.rectTransform.sizeDelta = new Vector2(44f, 28f);
            page.alignment = TextAlignmentOptions.Center;
            PagerArrow(pager, "RightArrow", false);

            var entries = Node(ach, "EntriesParent");
            Fill(entries, 14f, 14f, 14f, 50f);
            var vlg = entries.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        private static void BuildEloCard(RectTransform card)
        {
            var title = MakeText(card, "EloTitle", "Elo Rating", fBold, 16f, Color.white);
            TopBand(title.rectTransform, 18f, 14f, 160f, 24f);

            var pill = Img(card, "EloDropdown", pillBg, Color.white, Image.Type.Sliced);
            pill.anchorMin = pill.anchorMax = Vector2.one;
            pill.pivot = Vector2.one;
            pill.anchoredPosition = new Vector2(-14f, -12f);
            pill.sizeDelta = new Vector2(92f, 28f);
            var pillText = MakeText(pill, "Text", "All Time", fMedium, 11f, TextMuted);
            Fill(pillText.rectTransform, 10f, 0f, 22f, 0f);
            var chev = Img(pill, "Chevron", Load<Sprite>(ChevronIcon), TextMuted, Image.Type.Simple);
            MiddleRight(chev, 8f, 10f, 10f);
            chev.GetComponent<Image>().preserveAspect = true;

            // Axis grid; the rating history itself has no backing data yet.
            var chart = Node(card, "ChartArea");
            Fill(chart, 56f, 18f, 18f, 56f);
            string[] yLabels = { "1600", "1500", "1400", "1300", "1200", "1100", "1000" };
            for (int i = 0; i < yLabels.Length; i++)
            {
                float t = 1f - i / (float)(yLabels.Length - 1);
                var line = Img(chart, $"Grid_{yLabels[i]}", divider, GridLine, Image.Type.Simple);
                line.anchorMin = new Vector2(0f, t);
                line.anchorMax = new Vector2(1f, t);
                line.sizeDelta = new Vector2(0f, 1f);
                line.anchoredPosition = Vector2.zero;

                var lbl = MakeText(chart, $"Y_{yLabels[i]}", yLabels[i], fRegular, 10f, TextSoft);
                lbl.rectTransform.anchorMin = lbl.rectTransform.anchorMax = new Vector2(0f, t);
                lbl.rectTransform.pivot = new Vector2(1f, 0.5f);
                lbl.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
                lbl.rectTransform.sizeDelta = new Vector2(40f, 14f);
                lbl.alignment = TextAlignmentOptions.MidlineRight;
            }
            var xTitle = MakeText(card, "XAxisTitle", "Match Number", fRegular, 10f, TextSoft);
            xTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            xTitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            xTitle.rectTransform.pivot = new Vector2(0.5f, 0f);
            xTitle.rectTransform.anchoredPosition = new Vector2(19f, 18f);
            xTitle.rectTransform.sizeDelta = new Vector2(-74f, 16f);
            xTitle.alignment = TextAlignmentOptions.Center;

            var empty = MakeText(chart, "EmptyText", "No rating history yet", fMedium, 12f, TextSoft);
            Stretch(empty.rectTransform);
            empty.alignment = TextAlignmentOptions.Center;
        }

        private static void StatCard(Transform parent, string name, string iconPath, string value, string label)
        {
            var card = Img(parent, name, statCardBg, Color.white, Image.Type.Sliced);

            var icon = Img(card, "Icon", Load<Sprite>(iconPath), Color.white, Image.Type.Simple);
            MiddleLeft(icon, 16f, 36f, 36f);
            icon.GetComponent<Image>().preserveAspect = true;

            var val = MakeText(card, "ValueText", value, fBold, 26f, Color.white);
            val.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            val.rectTransform.anchorMax = new Vector2(1f, 1f);
            val.rectTransform.offsetMin = new Vector2(64f, -2f);
            val.rectTransform.offsetMax = new Vector2(-10f, -8f);
            Label(val, fBold, 26f, Color.white).alignment = TextAlignmentOptions.BottomLeft;

            var lbl = MakeText(card, "LabelText", label, fRegular, 11f, TextSoft);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            lbl.rectTransform.offsetMin = new Vector2(64f, 6f);
            lbl.rectTransform.offsetMax = new Vector2(-10f, -2f);
            lbl.alignment = TextAlignmentOptions.TopLeft;
            lbl.textWrappingMode = TextWrappingModes.Normal;
            lbl.lineSpacing = -8f;
        }

        private static void MiniStat(Transform parent, string name, string value, string label, float flex)
        {
            var card = Img(parent, name, statCardBg, Color.white, Image.Type.Sliced);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = flex;

            var val = MakeText(card, "ValueText", value, fBold, 22f, Color.white);
            val.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            val.rectTransform.anchorMax = Vector2.one;
            val.rectTransform.offsetMin = new Vector2(16f, 0f);
            val.rectTransform.offsetMax = new Vector2(-12f, -10f);
            Label(val, fBold, 22f, Color.white).alignment = TextAlignmentOptions.BottomLeft;

            var lbl = MakeText(card, "LabelText", label, fRegular, 11f, TextSoft);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            lbl.rectTransform.offsetMin = new Vector2(16f, 10f);
            lbl.rectTransform.offsetMax = new Vector2(-12f, -4f);
            Label(lbl, fRegular, 11f, TextSoft).alignment = TextAlignmentOptions.TopLeft;
        }

        private static void Tab(Transform parent, string name, string label, bool active)
        {
            var tab = Node(parent, name);
            var img = tab.gameObject.AddComponent<Image>();
            img.sprite = active ? tabActiveBg : null;
            img.type = Image.Type.Sliced;
            img.color = active ? Color.white : new Color(1f, 1f, 1f, 0f);
            tab.gameObject.AddComponent<Button>();
            NeutralTint(tab, img);
            var text = MakeText(tab, "TabText", label, active ? fSemiBold : fMedium, 15f, active ? Color.white : TextMuted);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void PagerArrow(Transform pager, string name, bool left)
        {
            var btn = Img(pager, name, pillBg, Color.white, Image.Type.Sliced);
            btn.anchorMin = btn.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            btn.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            btn.anchoredPosition = Vector2.zero;
            btn.sizeDelta = new Vector2(28f, 28f);
            var img = btn.GetComponent<Image>();
            img.raycastTarget = true;
            btn.gameObject.AddComponent<Button>();
            NeutralTint(btn, img);
            var chev = Img(btn, "Chevron", Load<Sprite>(ChevronIcon), TextMuted, Image.Type.Simple);
            chev.anchorMin = chev.anchorMax = new Vector2(0.5f, 0.5f);
            chev.sizeDelta = new Vector2(10f, 10f);
            chev.localEulerAngles = new Vector3(0f, 0f, left ? -90f : 90f);
            chev.GetComponent<Image>().preserveAspect = true;
        }

        private static void RewireController(GameObject root, Transform info)
        {
            var ctrl = root.GetComponent<AccountDataController>();
            Require(ctrl, nameof(AccountDataController));
            var so = new SerializedObject(ctrl);

            Ref(so, "usernameLabel", Tmp(info, "Text_User_Name"));
            Ref(so, "userIDLabel", Tmp(info, "Text_ID"));
            Ref(so, "currentPageLabel", Tmp(info, "PageLabel"));
            Ref(so, "completedAchievementsLabel", Tmp(FindDeep(info, "CompletedAchievement_Entry"), "ValueText"));
            Ref(so, "achievementPointsLabel", Tmp(FindDeep(info, "AchievementsPoints_Entry"), "ValueText"));
            Ref(so, "totalMatchPlayedLabel", Tmp(FindDeep(info, "TotalMatches_Entry"), "ValueText"));
            Ref(so, "totalGameTimeLabel", Tmp(FindDeep(info, "TotalGameTime_Entry"), "ValueText"));
            Ref(so, "profileImage", FindDeep(info, "ProfileImage").GetComponent<Image>());
            Ref(so, "leaderboardAccountParent", FindDeep(info, "EntriesParent"));
            Ref(so, "backButton", FindDeep(info, "BackButton").GetComponent<Button>());
            Ref(so, "leftArrow", FindDeep(info, "LeftArrow").GetComponent<Button>());
            Ref(so, "rightArrow", FindDeep(info, "RightArrow").GetComponent<Button>());
            Ref(so, "rootCanvasGroup", root.GetComponent<CanvasGroup>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ records row
        // One row per game mode: icon, mode name, best 1v1 / 1v3 tier and score.
        private static void StyleEntryPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(EntryPrefabPath);
            try
            {
                var rt = (RectTransform)root.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 58f);
                rt.localScale = Vector3.one;
                foreach (var c in root.GetComponents<LayoutGroup>()) Object.DestroyImmediate(c);
                foreach (var c in root.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(c);
                var le = GetOrAdd<LayoutElement>(root.transform);
                le.preferredHeight = 58f; le.minHeight = 58f; le.flexibleHeight = -1f;
                le.preferredWidth = -1f; le.flexibleWidth = 1f;

                var bg = GetOrAdd<Image>(root.transform);
                bg.sprite = rowBg; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.raycastTarget = false;

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var box = Img(root.transform, "IconBox", iconBoxBg, Color.white, Image.Type.Sliced);
                MiddleLeft(box, 12f, 36f, 36f);
                var icon = Img(box, "Icon", Load<Sprite>(QuestIcon), Color.white, Image.Type.Simple);
                Fill(icon, 5f, 5f, 5f, 5f);
                icon.GetComponent<Image>().preserveAspect = true;

                var mode = MakeText(root.transform, "Text_GameMode", "Block", fBold, 14f, Color.white);
                mode.rectTransform.anchorMin = new Vector2(0f, 0f);
                mode.rectTransform.anchorMax = new Vector2(0.38f, 1f);
                mode.rectTransform.offsetMin = new Vector2(60f, 0f);
                mode.rectTransform.offsetMax = new Vector2(0f, 0f);
                Label(mode, fBold, 14f, Color.white).fontStyle = FontStyles.UpperCase;

                var one = Record(root.transform, "OneVsOne", "1 vs 1", 0.38f, 0.69f);
                var three = Record(root.transform, "OneVsThree", "1 vs 3", 0.69f, 1f);

                var entry = root.GetComponent(FindType("ProDomino.AccountSystem.LeaderboardAccountEntry"));
                Require(entry, "LeaderboardAccountEntry");
                var so = new SerializedObject(entry);
                Ref(so, "gameModeLabel", mode);
                Ref(so, "oneVsOneLabel", one);
                Ref(so, "oneVsThreeLabel", three);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TextMeshProUGUI Record(Transform row, string name, string caption, float xMin, float xMax)
        {
            var col = Node(row, name);
            col.anchorMin = new Vector2(xMin, 0f);
            col.anchorMax = new Vector2(xMax, 1f);
            col.offsetMin = new Vector2(8f, 8f);
            col.offsetMax = new Vector2(-12f, -8f);

            var star = Img(col, "Star", Load<Sprite>(StarIcon), Color.white, Image.Type.Simple);
            MiddleLeft(star, 0f, 16f, 16f);
            star.GetComponent<Image>().preserveAspect = true;

            var cap = MakeText(col, "Caption", caption, fRegular, 10f, TextSoft);
            cap.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            cap.rectTransform.anchorMax = Vector2.one;
            cap.rectTransform.offsetMin = new Vector2(22f, 0f);
            cap.rectTransform.offsetMax = Vector2.zero;
            cap.alignment = TextAlignmentOptions.BottomLeft;

            var val = MakeText(col, "Value", "No Record", fSemiBold, 12f, Accent);
            val.rectTransform.anchorMin = Vector2.zero;
            val.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            val.rectTransform.offsetMin = new Vector2(22f, 0f);
            val.rectTransform.offsetMax = Vector2.zero;
            Label(val, fSemiBold, 12f, Accent).alignment = TextAlignmentOptions.TopLeft;
            return val;
        }

        // ------------------------------------------------------------------ host prefabs
        // Reverts rect/visual overrides on the popup subtree and gives the root the Friends List rect so it
        // fills the content area next to the sidebar like the other sections.
        private static void CleanInstance(string prefabPath)
        {
            var host = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var popup = FindDeep(host.transform, InstanceName);
                if (!popup) { Debug.LogWarning($"[ProfileRestyler] {InstanceName} not in {prefabPath}"); return; }

                foreach (var t in popup.GetComponentsInChildren<Transform>(true))
                {
                    if (t == popup || !PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                    foreach (var c in t.GetComponents<Component>())
                        if (c) PrefabUtility.RevertObjectOverride(c, InteractionMode.AutomatedAction);
                    PrefabUtility.RevertObjectOverride(t.gameObject, InteractionMode.AutomatedAction);
                }
                CopyRectFromSibling((RectTransform)popup);

                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(popup.gameObject);
                if (instanceRoot)
                    PrefabUtility.RemoveUnusedOverrides(new[] { instanceRoot }, InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(host, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(host);
            }
        }

        private static void CopyRectFromSibling(RectTransform rt)
        {
            var reference = rt.parent ? rt.parent.Find("FriendList_Popup") as RectTransform : null;
            if (reference)
            {
                rt.anchorMin = reference.anchorMin; rt.anchorMax = reference.anchorMax;
                rt.pivot = reference.pivot;
                rt.offsetMin = reference.offsetMin; rt.offsetMax = reference.offsetMax;
                rt.localScale = reference.localScale;
            }
            else
            {
                Stretch(rt);
                rt.localScale = Vector3.one;
            }
        }

        // ------------------------------------------------------------------ helpers
        private static Sprite MakeTrashIcon()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // texture y is bottom-up: body 4..22, lid 24..26, handle 27..29
                bool body = y >= 4 && y <= 22 && x >= 9 && x <= 22 && !(y >= 8 && y <= 18 && (x == 13 || x == 18));
                bool lid = y >= 24 && y <= 26 && x >= 6 && x <= 25;
                bool handle = y >= 27 && y <= 29 && x >= 13 && x <= 18;
                var c = Color.white;
                c.a = body || lid || handle ? 1f : 0f;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{GeneratedDir}/Profile_TrashIcon.png", tex);
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return (RectTransform)go.transform;
        }

        private static RectTransform Img(Transform parent, string name, Sprite sprite, Color color, Image.Type type) =>
            (RectTransform)MakeImage(parent, name, sprite, color, type).transform;

        // Stretched with insets measured from each edge.
        private static void Fill(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // Full-width band at a fixed distance from the top.
        private static void TopBand(RectTransform rt, float left, float top, float right, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!a) Debug.LogWarning($"[ProfileRestyler] Missing asset {path}");
            return a;
        }

        private static TMP_Text Tmp(Transform root, string name)
        {
            var t = root ? FindDeep(root, name) : null;
            return t ? t.GetComponent<TMP_Text>() : null;
        }

        private static void Ref(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) throw new Exception($"[ProfileRestyler] Property {prop} not found on {so.targetObject.GetType().Name}");
            if (!value) throw new Exception($"[ProfileRestyler] Target for {prop} not found");
            p.objectReferenceValue = value;
        }
    }
}
