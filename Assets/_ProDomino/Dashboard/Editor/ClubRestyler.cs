using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    public static class ClubRestyler
    {
        private static readonly string[] ScreenPrefabs =
        {
            "Assets/_ProDomino/Prefabs/UI/ClubUI_NavPanel.prefab",
            "Assets/_ProDomino/Prefabs/UI/Search_Club_Screen.prefab",
            "Assets/_ProDomino/Prefabs/UI/Club_Home_Screen.prefab",
            "Assets/_ProDomino/Prefabs/UI/Creating_Club_Screen.prefab",
            "Assets/_ProDomino/Prefabs/UI/Creating_Club_Icon.prefab",
        };

        private static readonly string[] EntryPrefabs =
        {
            "Assets/_ProDomino/Prefabs/UI/Club_Name_List_Container.prefab",
            "Assets/_ProDomino/Prefabs/UI/Top_Club_Name_List_Container.prefab",
            "Assets/_ProDomino/Prefabs/UI/Message_ChatClub_Container.prefab",
            "Assets/_ProDomino/Prefabs/UI/Container_Edit_Club_Rank.prefab",
            "Assets/_ProDomino/Prefabs/UI/Edit_club_rank.prefab",
            "Assets/_ProDomino/Prefabs/UI/Club_Customization_Color.prefab",
            "Assets/_ProDomino/Prefabs/UI/Club_Customization_Selector.prefab",
            "Assets/_ProDomino/Prefabs/UI/Club_Image.prefab",
            "Assets/_ProDomino/Prefabs/UI/Player_info_Club Variant.prefab",
        };

        // Gold action button names (case-insensitive partial match)
        private static readonly string[] GoldButtonNames =
        {
            "create", "save", "confirm", "accept", "join", "invite", "send", "edit"
        };

        // Secondary (dark card) button names
        private static readonly string[] SecondaryButtonNames =
        {
            "back", "cancel", "close", "decline", "remove", "leave", "delete"
        };

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite screenCardBg, cardBg, fieldBg, goldBtnBg, secondaryBtnBg, entryRowBg;
        private static Sprite tabBarBg, tabActiveBg, tableHeaderBg, chatBubbleBg, dangerBtnBg;

        [MenuItem("ProDomino/Dashboard/Restyle Club System")]
        public static void Apply()
        {
            Debug.Log("[ClubRestyler] Starting Club system restyle...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();

            int count = 0;
            foreach (var path in ScreenPrefabs.Concat(EntryPrefabs))
            {
                if (!System.IO.File.Exists(path.Replace('/', '\\')))
                {
                    Debug.LogWarning($"[ClubRestyler] Prefab not found: {path}");
                    continue;
                }
                RestylePrefab(path);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ClubRestyler] SUCCESS: Restyled {count} club prefabs.");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            screenCardBg = GetOrCreateScreenCardSprite();
            cardBg = MakePanelSprite("Club_CardBg", 64, 64, 14, Hex("#0D1120"), Hex("#070A14"), Hex("#1E2538"), 1.2f, true);
            fieldBg = MakePanelSprite("Club_FieldBg", 48, 48, 10, Hex("#111625"), Hex("#0E1320"), Hex("#222B3D"), 1.2f, true);
            goldBtnBg = MakeRoundedSprite("Club_GoldBtn", 200, 56, 28, AccentStart, AccentEnd);
            secondaryBtnBg = MakePanelSprite("Club_SecondaryBtn", 64, 48, 12, Hex("#161B2E"), Hex("#0F1322"), Hex("#2A3148"), 1.2f, true);
            entryRowBg = MakePanelSprite("Club_EntryRowBg", 64, 48, 10, Hex("#0C1020"), Hex("#080D18"), Hex("#1A2035"), 1f, true);
            tabBarBg = MakePanelSprite("Club_TabBarBg", 64, 52, 26, Hex("#111828"), Hex("#0A0E1C"), Hex("#1E2538"), 1f, true);
            tabActiveBg = MakeRoundedSprite("Club_TabActive", 200, 44, 22, Hex("#5B9CF6"), Hex("#3B82F6"));
            tableHeaderBg = MakePanelSprite("Club_TableHeader", 64, 40, 8, Hex("#111828"), Hex("#0D1120"), Hex("#1A2035"), 0.8f, true);
            chatBubbleBg = MakeRoundedSprite("Club_ChatBubble", 64, 40, 12, Hex("#1A2035"), Hex("#141B2E"));
            dangerBtnBg = MakePanelSprite("Club_DangerBtn", 40, 40, 10, Hex("#7F1D1D"), Hex("#991B1B"), Hex("#EF4444"), 1f, true);
        }

        private static void RestylePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (path.Contains("Search_Club_Screen"))
                    RebuildSearchScreen(root);
                else if (path.Contains("Club_Home_Screen"))
                    RebuildHomeScreen(root);
                else
                    RestyleRecursive(root.transform, IsScreen(path));

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[ClubRestyler] Restyled: {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RebuildSearchScreen(GameObject root)
        {
            var rootT = root.transform;

            // Find the ClubSearchController to rewire serialized fields (internal type, use reflection)
            var controllerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "ClubSearchController");
            var controller = controllerType != null ? root.GetComponentInChildren(controllerType, true) : null;
            SerializedObject so = controller != null ? new SerializedObject(controller) : null;

            // --- Background: screen card sprite on root or UI_Base_General ---
            var uiBase = FindDeep(rootT, "UI_Base_General");
            if (uiBase != null)
            {
                var bgImg = GetOrAdd<Image>(uiBase);
                bgImg.sprite = screenCardBg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
                bgImg.raycastTarget = true;
            }

            // --- Restyle the header: "Club" title area ---
            var totalAchText = FindDeep(rootT, "Total_Achievements_Text (TMP)");
            if (totalAchText != null)
            {
                var tmp = totalAchText.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "Club";
                    tmp.font = fBold;
                    tmp.fontSharedMaterial = fBold.material;
                    tmp.fontSize = 24f;
                    tmp.color = Color.white;
                }
            }

            // --- "No clubs found" label —> restyle as hero heading ---
            var noClubsLabel = FindDeep(rootT, "NoClubsFound_Label");
            if (noClubsLabel != null)
            {
                var tmp = noClubsLabel.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "You're Not in a Club Yet";
                    tmp.font = fBold;
                    tmp.fontSharedMaterial = fBold.material;
                    tmp.fontSize = 28f;
                    tmp.color = Color.white;
                    tmp.alignment = TextAlignmentOptions.Center;
                }
            }

            // --- Search input field ---
            var searchInput = root.GetComponentInChildren<TMP_InputField>(true);
            if (searchInput != null)
            {
                var inputImg = searchInput.GetComponent<Image>();
                if (inputImg != null)
                {
                    inputImg.sprite = fieldBg;
                    inputImg.type = Image.Type.Sliced;
                    inputImg.color = Color.white;
                }

                if (searchInput.textComponent != null)
                {
                    searchInput.textComponent.font = fMedium;
                    searchInput.textComponent.fontSharedMaterial = fMedium.material;
                    searchInput.textComponent.color = Color.white;
                }

                if (searchInput.placeholder is TextMeshProUGUI ph)
                {
                    ph.font = fRegular;
                    ph.fontSharedMaterial = fRegular.material;
                    ph.color = TextPlaceholder;
                    ph.text = "Search club...";
                }

                searchInput.caretColor = Accent;
                searchInput.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
            }

            // --- Restyle all CustomButtonUI buttons ---
            var customBtnType = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return System.Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "CustomButtonUI");
            var customButtons = customBtnType != null
                ? root.GetComponentsInChildren(customBtnType, true)
                : new Component[0];
            foreach (var cb in customButtons)
            {
                var cbImg = cb.GetComponent<Image>();
                if (cbImg == null) continue;

                var cbName = cb.gameObject.name.ToLowerInvariant();
                bool isGoldBtn = cbName.Contains("create") || cbName.Contains("search") ||
                                 cbName.Contains("send") || cbName.Contains("join");

                if (isGoldBtn)
                {
                    cbImg.sprite = goldBtnBg;
                    cbImg.type = Image.Type.Sliced;
                    cbImg.color = Color.white;

                    var childTmp = cb.GetComponentInChildren<TextMeshProUGUI>();
                    if (childTmp != null)
                    {
                        childTmp.font = fSemiBold;
                        childTmp.fontSharedMaterial = fSemiBold.material;
                        childTmp.color = OnAccent;
                    }
                }
                else
                {
                    cbImg.sprite = secondaryBtnBg;
                    cbImg.type = Image.Type.Sliced;
                    cbImg.color = Color.white;
                }
            }

            // --- Also restyle standard Button components ---
            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (customBtnType != null && btn.GetComponent(customBtnType) != null) continue;
                var btnImg = btn.GetComponent<Image>();
                if (btnImg == null) continue;

                var btnName = btn.gameObject.name.ToLowerInvariant();
                var childText = btn.GetComponentInChildren<TextMeshProUGUI>();
                string textLower = childText != null ? childText.text.ToLowerInvariant() : "";
                bool isGold = GoldButtonNames.Any(g => btnName.Contains(g) || textLower.Contains(g));

                if (isGold)
                {
                    btnImg.sprite = goldBtnBg;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = Color.white;
                    if (childText != null)
                    {
                        childText.font = fSemiBold;
                        childText.fontSharedMaterial = fSemiBold.material;
                        childText.color = OnAccent;
                    }
                }
                else
                {
                    btnImg.sprite = secondaryBtnBg;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = Color.white;
                    if (childText != null)
                    {
                        childText.font = fSemiBold;
                        childText.fontSharedMaterial = fSemiBold.material;
                        childText.color = Color.white;
                    }
                }
            }

            // --- Restyle header texts: "Top Clubs", "Members", "Ranking" ---
            var topClubsText = FindDeep(rootT, "Top_Clubs_Text (TMP)");
            if (topClubsText != null)
            {
                var tmp = topClubsText.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.font = fSemiBold; tmp.fontSharedMaterial = fSemiBold.material; tmp.color = TextLabel; }
            }

            var membersText = FindDeep(rootT, "Members_Text (TMP)");
            if (membersText != null)
            {
                var tmp = membersText.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material; tmp.color = TextMuted; }
            }

            var rankingText = FindDeep(rootT, "Ranking_Text (TMP)");
            if (rankingText != null)
            {
                var tmp = rankingText.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material; tmp.color = TextMuted; }
            }

            // --- Restyle Panel overlay ---
            var panel = FindDeep(rootT, "Panel");
            if (panel != null)
            {
                var pImg = panel.GetComponent<Image>();
                if (pImg != null)
                    pImg.color = new Color(0.004f, 0.004f, 0.047f, 0.7f);
            }

            // --- Chat_Panel / Titles_Container ---
            foreach (var n in new[] { "Chat_Panel", "Titles_Container", "Top_Club_Container" })
            {
                var t = FindDeep(rootT, n);
                if (t != null)
                {
                    var img = GetOrAdd<Image>(t);
                    img.sprite = cardBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
            }

            // --- Scrollbar ---
            var scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
            foreach (var sb in scrollbars)
                RestyleScrollbar(sb);

            // --- Any remaining TMP texts not yet styled ---
            var allTmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTmps)
            {
                if (tmp.font != null && tmp.font.name.StartsWith("Montserrat"))
                    continue; // Already styled

                tmp.font = fMedium;
                tmp.fontSharedMaterial = fMedium.material;
                if (IsOpaqueBlack(tmp.color))
                    tmp.color = Color.white;
            }

            // --- SendRequest popup ---
            var sendReqPopup = FindDeep(rootT, "SendRequest_PopUp");
            if (sendReqPopup != null)
            {
                var srImg = GetOrAdd<Image>(sendReqPopup);
                srImg.sprite = cardBg;
                srImg.type = Image.Type.Sliced;
                srImg.color = Color.white;
            }

            if (so != null)
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RebuildHomeScreen(GameObject root)
        {
            var rootT = root.transform;

            // --- Screen card background ---
            var uiBase = FindDeep(rootT, "UI_Base_General");
            if (uiBase != null)
            {
                var bgImg = GetOrAdd<Image>(uiBase);
                bgImg.sprite = screenCardBg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }

            // --- Club Header (Club_HomeScreen_DataUI) ---
            var headerData = FindDeep(rootT, "Club_HomeScreen_DataUI");
            if (headerData != null)
            {
                var headerImg = GetOrAdd<Image>(headerData);
                headerImg.sprite = cardBg;
                headerImg.type = Image.Type.Sliced;
                headerImg.color = Color.white;

                // Style all text in header
                var headerTmps = headerData.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in headerTmps)
                {
                    var n = tmp.gameObject.name.ToLowerInvariant();
                    if (n.Contains("name") || n.Contains("title"))
                    {
                        tmp.font = fBold; tmp.fontSharedMaterial = fBold.material;
                        tmp.color = Color.white;
                    }
                    else
                    {
                        tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material;
                        if (IsOpaqueBlack(tmp.color)) tmp.color = Color.white;
                        else if (!IsNearWhite(tmp.color)) tmp.color = TextMuted;
                    }
                }
            }

            // --- Tab bar (HomeScreen_Tabs) ---
            var tabBar = FindDeep(rootT, "HomeScreen_Tabs");
            if (tabBar != null)
            {
                var tbImg = GetOrAdd<Image>(tabBar);
                tbImg.sprite = tabBarBg;
                tbImg.type = Image.Type.Sliced;
                tbImg.color = Color.white;

                // Style tab buttons via CustomButtonUI
                var customBtnType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == "CustomButtonUI");

                if (customBtnType != null)
                {
                    var tabButtons = tabBar.GetComponentsInChildren(customBtnType, true);
                    foreach (var tb in tabButtons)
                    {
                        var tbBtnImg = tb.GetComponent<Image>();
                        if (tbBtnImg != null)
                        {
                            tbBtnImg.sprite = tabActiveBg;
                            tbBtnImg.type = Image.Type.Sliced;
                        }

                        var tbText = tb.GetComponentInChildren<TextMeshProUGUI>();
                        if (tbText != null)
                        {
                            tbText.font = fSemiBold;
                            tbText.fontSharedMaterial = fSemiBold.material;
                            tbText.color = Color.white;
                        }
                    }
                }
            }

            // --- Table header rows (Container_Title) ---
            var containerTitles = new List<Transform>();
            FindAllByName(rootT, "Container_Title", containerTitles);
            foreach (var ct in containerTitles)
            {
                var ctImg = GetOrAdd<Image>(ct);
                ctImg.sprite = tableHeaderBg;
                ctImg.type = Image.Type.Sliced;
                ctImg.color = Color.white;

                var ctTmps = ct.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in ctTmps)
                {
                    tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material;
                    tmp.color = TextMuted;
                }
            }

            // --- Panel overlays ---
            foreach (var panelName in new[] { "Panel", "Recovery_Background" })
            {
                var panel = FindDeep(rootT, panelName);
                if (panel != null)
                {
                    var pImg = panel.GetComponent<Image>();
                    if (pImg != null)
                        pImg.color = new Color(0.004f, 0.004f, 0.047f, 0.7f);
                }
            }

            // --- Club_Panel backgrounds ---
            var clubPanels = new List<Transform>();
            FindAllByName(rootT, "Club_Panel", clubPanels);
            foreach (var cp in clubPanels)
            {
                var cpImg = GetOrAdd<Image>(cp);
                cpImg.sprite = cardBg;
                cpImg.type = Image.Type.Sliced;
                cpImg.color = Color.white;
            }

            // --- Members/Applicant sub-screen backgrounds ---
            foreach (var subName in new[] { "Members_SubScreen", "Applicant_SubScreen", "ClubChat_SubScreen" })
            {
                var sub = FindDeep(rootT, subName);
                if (sub == null) continue;

                // Style sub-screen text
                var subTmps = sub.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in subTmps)
                {
                    if (tmp.font != null && tmp.font.name.StartsWith("Montserrat")) continue;

                    var n = tmp.gameObject.name.ToLowerInvariant();
                    if (n.Contains("title") || n.Contains("name"))
                    {
                        tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material;
                        tmp.color = Color.white;
                    }
                    else if (n.Contains("rank") || n.Contains("status") || n.Contains("date") ||
                             n.Contains("since") || n.Contains("application"))
                    {
                        tmp.font = fRegular; tmp.fontSharedMaterial = fRegular.material;
                        tmp.color = TextMuted;
                    }
                    else
                    {
                        tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material;
                        if (IsOpaqueBlack(tmp.color)) tmp.color = Color.white;
                    }
                }
            }

            // --- Chat input container ---
            var inputContainer = FindDeep(rootT, "Input_Container");
            if (inputContainer != null)
            {
                var icImg = GetOrAdd<Image>(inputContainer);
                icImg.sprite = fieldBg;
                icImg.type = Image.Type.Sliced;
                icImg.color = Color.white;
            }

            // --- Chat input field ---
            var chatInputs = root.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var input in chatInputs)
            {
                var inputImg = input.GetComponent<Image>();
                if (inputImg != null)
                {
                    inputImg.sprite = fieldBg;
                    inputImg.type = Image.Type.Sliced;
                    inputImg.color = Color.white;
                }
                if (input.textComponent != null)
                {
                    input.textComponent.font = fMedium;
                    input.textComponent.fontSharedMaterial = fMedium.material;
                    input.textComponent.color = Color.white;
                }
                if (input.placeholder is TextMeshProUGUI ph)
                {
                    ph.font = fRegular; ph.fontSharedMaterial = fRegular.material;
                    ph.color = TextPlaceholder;
                }
                input.caretColor = Accent;
                input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
            }

            // --- Send button ---
            var sendBtn = FindDeep(rootT, "Send_Button");
            if (sendBtn != null)
            {
                var sbImg = sendBtn.GetComponent<Image>();
                if (sbImg != null)
                {
                    sbImg.sprite = goldBtnBg;
                    sbImg.type = Image.Type.Sliced;
                    sbImg.color = Color.white;
                }
            }

            // --- All standard Buttons: Accept = gold, Decline/Expel/Remove = danger red ---
            var allButtons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                var btnImg = btn.GetComponent<Image>();
                if (btnImg == null) continue;

                var btnName = btn.gameObject.name.ToLowerInvariant();
                var childText = btn.GetComponentInChildren<TextMeshProUGUI>();
                var textLower = childText != null ? childText.text.ToLowerInvariant() : "";

                bool isGold = GoldButtonNames.Any(g => btnName.Contains(g) || textLower.Contains(g));
                bool isDanger = btnName.Contains("expel") || btnName.Contains("remove") ||
                                btnName.Contains("decline") || btnName.Contains("reject") ||
                                btnName.Contains("exclude") || textLower.Contains("remove") ||
                                textLower.Contains("decline");

                if (isDanger)
                {
                    btnImg.sprite = dangerBtnBg;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = Color.white;
                    if (childText != null)
                    {
                        childText.font = fSemiBold; childText.fontSharedMaterial = fSemiBold.material;
                        childText.color = Color.white;
                    }
                }
                else if (isGold)
                {
                    btnImg.sprite = goldBtnBg;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = Color.white;
                    if (childText != null)
                    {
                        childText.font = fSemiBold; childText.fontSharedMaterial = fSemiBold.material;
                        childText.color = OnAccent;
                    }
                }
                else
                {
                    // Generic button: dark card
                    if (!IsDarkColor(btnImg.color) || btnImg.sprite == null)
                    {
                        btnImg.sprite = secondaryBtnBg;
                        btnImg.type = Image.Type.Sliced;
                        btnImg.color = Color.white;
                    }
                    if (childText != null)
                    {
                        childText.font = fSemiBold; childText.fontSharedMaterial = fSemiBold.material;
                        if (IsOpaqueBlack(childText.color)) childText.color = Color.white;
                    }
                }
            }

            // --- CustomButtonUI buttons (Edit Club, Roles, action menus) ---
            var customBtnType2 = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "CustomButtonUI");
            if (customBtnType2 != null)
            {
                var customBtns = root.GetComponentsInChildren(customBtnType2, true);
                foreach (var cb in customBtns)
                {
                    // Skip tab buttons (already handled)
                    if (tabBar != null && cb.transform.IsChildOf(tabBar)) continue;

                    var cbImg = cb.GetComponent<Image>();
                    if (cbImg == null) continue;

                    var cbName = cb.gameObject.name.ToLowerInvariant();
                    bool isGold = cbName.Contains("edit") || cbName.Contains("accept") ||
                                  cbName.Contains("invite") || cbName.Contains("send");

                    if (isGold)
                    {
                        cbImg.sprite = goldBtnBg;
                        cbImg.type = Image.Type.Sliced;
                        cbImg.color = Color.white;
                        var txt = cb.GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) { txt.font = fSemiBold; txt.fontSharedMaterial = fSemiBold.material; txt.color = OnAccent; }
                    }
                    else
                    {
                        cbImg.sprite = secondaryBtnBg;
                        cbImg.type = Image.Type.Sliced;
                        cbImg.color = Color.white;
                        var txt = cb.GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) { txt.font = fSemiBold; txt.fontSharedMaterial = fSemiBold.material; txt.color = Color.white; }
                    }
                }
            }

            // --- Popup backgrounds ---
            foreach (var popupName in new[] { "Expel_Member_PopUp", "DetermineRequest_Applicant_PopUp", "Invite_Player_Club_Popup", "Ranks_based_permission_editor" })
            {
                var popup = FindDeep(rootT, popupName);
                if (popup == null) continue;
                var popImg = GetOrAdd<Image>(popup);
                popImg.sprite = cardBg;
                popImg.type = Image.Type.Sliced;
                popImg.color = Color.white;

                // Style popup text
                var popTmps = popup.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in popTmps)
                {
                    if (tmp.font != null && tmp.font.name.StartsWith("Montserrat")) continue;
                    tmp.font = fMedium; tmp.fontSharedMaterial = fMedium.material;
                    if (IsOpaqueBlack(tmp.color)) tmp.color = Color.white;
                }
            }

            // --- Scrollbars ---
            var scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
            foreach (var sb in scrollbars)
                RestyleScrollbar(sb);

            // --- Any remaining unstyled TMP ---
            var allTmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTmps)
            {
                if (tmp.font != null && tmp.font.name.StartsWith("Montserrat")) continue;
                tmp.font = fMedium;
                tmp.fontSharedMaterial = fMedium.material;
                if (IsOpaqueBlack(tmp.color)) tmp.color = Color.white;
            }
        }

        private static void FindAllByName(Transform root, string name, List<Transform> results)
        {
            if (root.gameObject.name == name) results.Add(root);
            for (int i = 0; i < root.childCount; i++)
                FindAllByName(root.GetChild(i), name, results);
        }

        private static bool IsScreen(string path) => ScreenPrefabs.Any(s => s == path);

        private static void RestyleRecursive(Transform t, bool isScreen)
        {
            var go = t.gameObject;
            var name = go.name;
            var lowerName = name.ToLowerInvariant();

            // ---- Button with Image: gold or secondary styling ----
            var button = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();

            if (button != null && img != null)
            {
                RestyleButton(button, img, lowerName);
            }
            else if (img != null)
            {
                RestyleImage(img, name, lowerName);
            }

            // ---- TMP text ----
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                RestyleText(tmp, name, lowerName, button != null);

            // ---- InputField ----
            var inputField = go.GetComponent<TMP_InputField>();
            if (inputField != null)
                RestyleInputField(inputField);

            // ---- Scrollbar ----
            var scrollbar = go.GetComponent<Scrollbar>();
            if (scrollbar != null)
                RestyleScrollbar(scrollbar);

            for (int i = 0; i < t.childCount; i++)
                RestyleRecursive(t.GetChild(i), isScreen);
        }

        private static void RestyleButton(Button button, Image img, string lowerName)
        {
            bool isGold = GoldButtonNames.Any(g => lowerName.Contains(g));
            bool isSecondary = SecondaryButtonNames.Any(s => lowerName.Contains(s));

            // Check child text for button intent if name doesn't match
            if (!isGold && !isSecondary)
            {
                var childText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (childText != null)
                {
                    var textLower = childText.text.ToLowerInvariant();
                    isGold = GoldButtonNames.Any(g => textLower.Contains(g));
                    isSecondary = !isGold && SecondaryButtonNames.Any(s => textLower.Contains(s));
                }
            }

            if (isGold)
            {
                img.sprite = goldBtnBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;

                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.85f, 0.5f, 1f);
                colors.pressedColor = new Color(0.9f, 0.7f, 0.2f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;

                // Set child text to dark
                var childTmp = button.GetComponentInChildren<TextMeshProUGUI>();
                if (childTmp != null)
                {
                    childTmp.font = fSemiBold;
                    childTmp.fontSharedMaterial = fSemiBold.material;
                    childTmp.color = OnAccent;
                }
            }
            else if (isSecondary)
            {
                img.sprite = secondaryBtnBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                NeutralTint(button.transform, img);

                var childTmp = button.GetComponentInChildren<TextMeshProUGUI>();
                if (childTmp != null)
                {
                    childTmp.font = fSemiBold;
                    childTmp.fontSharedMaterial = fSemiBold.material;
                    childTmp.color = Color.white;
                }
            }
            else
            {
                // Generic button — dark card style
                if (img.sprite == null || IsLightColor(img.color))
                {
                    img.sprite = secondaryBtnBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                    NeutralTint(button.transform, img);
                }
            }
        }

        private static void RestyleImage(Image img, string name, string lowerName)
        {
            // UI_Base_General = main screen background
            if (name == "UI_Base_General")
            {
                img.sprite = screenCardBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // Panel/overlay backgrounds
            if (name == "Panel" || name == "Recovery_Background")
            {
                img.color = new Color(0.004f, 0.004f, 0.047f, 0.7f);
                return;
            }

            // Chat_Panel background
            if (name == "Chat_Panel")
            {
                img.sprite = cardBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // Outline elements
            if (lowerName.Contains("outline"))
            {
                img.color = ScreenCardBorder;
                return;
            }

            // Search container — give it field background
            if (name == "Search_Container" || lowerName == "search_club_container")
            {
                img.sprite = fieldBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // Titles container / header areas
            if (name == "Titles_Container" || name == "Container_Title")
            {
                img.sprite = cardBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // Top club container / list entries
            if (name == "Top_Club_Container" || name == "Top_Club_List_Container" ||
                name == "Club_Name_List" || name == "SendRequest_Scalable" ||
                name == "SendRequest_PopUp")
            {
                if (img.sprite == null)
                {
                    img.sprite = cardBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                return;
            }

            // Club info / edit containers
            if (name == "Club_Info" || name == "Edit_Container" ||
                lowerName.Contains("clubname_container") || lowerName.Contains("clubslogan_container"))
            {
                if (img.sprite == null)
                {
                    img.sprite = cardBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                return;
            }

            // Image container in icon creator
            if (name == "Image_Container" || name == "Texture_Container" ||
                name == "Options_Color_Texture" || name == "Options_Shield" ||
                name == "Options_Texture")
            {
                if (img.sprite == null)
                {
                    img.sprite = cardBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                return;
            }

            // Entry row items (list containers, rank items, player info)
            if (lowerName.Contains("container") && !lowerName.Contains("viewport"))
            {
                if (img.sprite == null && !IsDarkColor(img.color))
                {
                    img.sprite = entryRowBg;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                return;
            }

            // Invite popup
            if (name == "Invite_Player_Club_Popup")
            {
                img.sprite = cardBg;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // Any remaining no-sprite opaque image that looks light — darken
            if (img.sprite == null && IsLightColor(img.color) &&
                !lowerName.Contains("icon") && !lowerName.Contains("shield") &&
                !lowerName.Contains("image") && !lowerName.Contains("selected") &&
                !lowerName.Contains("hover") && !lowerName.Contains("handle") &&
                !lowerName.Contains("emoji") && !lowerName.Contains("mask") &&
                !lowerName.Contains("central") && !lowerName.Contains("texture") &&
                !lowerName.Contains("club_image") && !lowerName.Contains("viewport"))
            {
                img.color = ScreenCardBg;
            }
        }

        private static void RestyleText(TextMeshProUGUI tmp, string name, string lowerName, bool isOnButton)
        {
            // Skip text on buttons (handled by RestyleButton)
            if (isOnButton) return;

            // Also skip if parent is a button (child text handled there)
            if (tmp.transform.parent != null && tmp.transform.parent.GetComponent<Button>() != null)
                return;

            if (lowerName.Contains("title") || lowerName.Contains("header") ||
                lowerName.Contains("club_name") || lowerName.Contains("club name"))
            {
                tmp.font = fBold;
                tmp.fontSharedMaterial = fBold.material;
                tmp.color = Color.white;
            }
            else if (lowerName.Contains("body") || lowerName.Contains("feedback") ||
                     lowerName.Contains("placeholder") || lowerName.Contains("requirements"))
            {
                tmp.font = fRegular;
                tmp.fontSharedMaterial = fRegular.material;
                tmp.color = TextMuted;
            }
            else if (lowerName.Contains("ranking") || lowerName.Contains("members_text") ||
                     lowerName.Contains("top_clubs"))
            {
                tmp.font = fSemiBold;
                tmp.fontSharedMaterial = fSemiBold.material;
                tmp.color = TextLabel;
            }
            else if (lowerName.Contains("noclubs") || lowerName.Contains("no_clubs") ||
                     lowerName.Contains("total_achievements"))
            {
                tmp.font = fBold;
                tmp.fontSharedMaterial = fBold.material;
                tmp.color = Color.white;
            }
            else if (lowerName.Contains("choose_texture"))
            {
                tmp.font = fSemiBold;
                tmp.fontSharedMaterial = fSemiBold.material;
                tmp.color = Color.white;
            }
            else
            {
                tmp.font = fMedium;
                tmp.fontSharedMaterial = fMedium.material;

                if (IsOpaqueBlack(tmp.color))
                    tmp.color = Color.white;
                else if (tmp.color == Color.white || IsNearWhite(tmp.color))
                    tmp.color = Color.white;
                else
                    tmp.color = TextLabel;
            }
        }

        private static void RestyleInputField(TMP_InputField input)
        {
            var bg = input.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = fieldBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }

            if (input.textComponent != null)
            {
                input.textComponent.font = fMedium;
                input.textComponent.fontSharedMaterial = fMedium.material;
                input.textComponent.color = Color.white;
            }

            if (input.placeholder is TextMeshProUGUI ph)
            {
                ph.font = fRegular;
                ph.fontSharedMaterial = fRegular.material;
                ph.color = TextPlaceholder;
            }

            input.caretColor = Accent;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
        }

        private static void RestyleScrollbar(Scrollbar sb)
        {
            var bgImg = sb.GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = new Color(0.05f, 0.07f, 0.12f, 0.5f);

            if (sb.handleRect != null)
            {
                var handleImg = sb.handleRect.GetComponent<Image>();
                if (handleImg != null)
                    handleImg.color = Hex("#2A3148");
            }
        }

        // ==================== COLOR HELPERS ====================

        private static bool IsLightColor(Color c) => c.a > 0.5f && (c.r + c.g + c.b) / 3f > 0.4f;
        private static bool IsDarkColor(Color c) => c.a > 0.5f && (c.r + c.g + c.b) / 3f < 0.15f;
        private static bool IsOpaqueBlack(Color c) => c.a > 0.8f && c.r < 0.15f && c.g < 0.15f && c.b < 0.15f;
        private static bool IsNearWhite(Color c) => c.a > 0.8f && c.r > 0.9f && c.g > 0.9f && c.b > 0.9f;
    }
}
