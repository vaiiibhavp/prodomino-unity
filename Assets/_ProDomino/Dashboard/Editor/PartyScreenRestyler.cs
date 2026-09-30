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
    /// Rebuilds Form_party_Screen (PartyUI_NavPanel in Static_PopUps) as the full-screen "Create Party" section:
    /// - current party members as cards (PartyMembers + PartyCard slots, empty slots hidden, local player shown as "You")
    /// - friends list as the same FriendCard grid of the Friends List screen, without the search bar
    /// The root keeps its PartyController, CanvasGroup and outside-click closer; only the visuals are rebuilt.
    /// Run FriendListPopupRestyler first, the friends grid reuses its FriendCard prefab.
    /// </summary>
    public static class PartyScreenRestyler
    {
        private const string ScreenPath = "Assets/_ProDomino/FriendSystem/Prefabs/Form_party_Screen.prefab";
        private const string PartyCardPath = "Assets/_ProDomino/FriendSystem/Prefabs/PartyCard.prefab";
        private const string FriendCardPath = "Assets/_ProDomino/FriendSystem/Prefabs/FriendCard.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string MainCanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string DefaultAvatarPath = "Assets/_ProDomino/_Art/Avatars/Man_Avatar_1.png";
        private const string InstanceName = "PartyUI_NavPanel";

        private const int PartySlots = 4;
        private const float CardWidth = 165f;
        private const float PartyCardHeight = 209f;
        private const float FriendCardHeight = 186f;
        private const float CardGap = 24f;

        private static Sprite screenBg, cardBg, chipBg, circle, avatarRing, userIcon, defaultAvatar;

        [MenuItem("ProDomino/Dashboard/Restyle Party Screen + Render")]
        public static void ApplyAndRender()
        {
            Apply();
            RenderOnly();
        }

        public static void Apply()
        {
            PrepareAssets();
            var partyCard = BuildPartyCard();
            var friendCard = AssetDatabase.LoadAssetAtPath<GameObject>(FriendCardPath);
            Require(friendCard, "FriendCard prefab (run FriendListPopupRestyler first)");
            BuildScreen(partyCard, friendCard);
            CleanMiddleScreenInstance();
            AssetDatabase.SaveAssets();
            Debug.Log("[PartyScreenRestyler] Done.");
        }

        // ------------------------------------------------------------------ assets
        private static void PrepareAssets()
        {
            // Same generated sprites as the Friends List screen, so both sections match
            screenBg = MakePanelSprite("FriendList_ScreenBg", 48, 48, 10, Hex("#010818"), Hex("#010818"), Hex("#34343D"), 1f);
            cardBg = MakePanelSprite("FriendList_CardBg", 64, 64, 12, Hex("#34343D"), Hex("#000005"), Hex("#46464B"), 2f);
            chipBg = MakePanelSprite("Party_ChipBg", 24, 24, 6, AccentStart, AccentEnd, Color.clear, 0f, false);
            circle = MakeCircleSprite("FriendList_Circle", 128);
            avatarRing = AssetDatabase.LoadAllAssetsAtPath($"{GeneratedDir}/FriendList_AvatarRing.png").OfType<Sprite>().FirstOrDefault();
            userIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Nav_FriendsList.png");
            defaultAvatar = AssetDatabase.LoadAllAssetsAtPath(DefaultAvatarPath).OfType<Sprite>().FirstOrDefault();
            Require(avatarRing, "FriendList_AvatarRing (run FriendListPopupRestyler first)");
            Require(defaultAvatar, "default avatar");
        }

        // ------------------------------------------------------------------ party card
        private static GameObject BuildPartyCard()
        {
            var root = NewRect("PartyCard", null);
            root.gameObject.layer = 5;
            root.sizeDelta = new Vector2(CardWidth, PartyCardHeight);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = cardBg; bg.type = Image.Type.Sliced; bg.raycastTarget = true;

            // Everything of an occupied slot lives here, PartyEntry toggles it with the slot state
            var member = NewRect("PartyMember_Object", root);
            Stretch(member);

            var holder = NewRect("Avatar", member);
            TopCenter(holder, 24f, 80f, 80f);
            var maskImg = holder.gameObject.AddComponent<Image>();
            maskImg.sprite = circle; maskImg.raycastTarget = false;
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var avatar = MakeImage(holder, "Image_Avatar", defaultAvatar, Color.white, Image.Type.Simple).GetComponent<Image>();
            avatar.preserveAspect = true;
            Stretch(avatar.rectTransform);
            var ring = MakeImage(member, "Avatar_Ring", avatarRing, Color.white, Image.Type.Simple).GetComponent<RectTransform>();
            TopCenter(ring, 24f, 80f, 80f);

            var name = MakeText(member, "Text_Username", "Robert", Medium, 20f, Color.white);
            Label(name, Medium, 20f, Color.white);
            name.alignment = TextAlignmentOptions.Center;
            name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0.5f, 1f);
            name.rectTransform.offsetMin = new Vector2(10f, -144f); name.rectTransform.offsetMax = new Vector2(-10f, -120f);

            // Status + leader chip, hidden on the local player's card which only reads "You"
            var other = NewRect("OtherMember_Object", member);
            Stretch(other);

            var status = NewRect("Status", other);
            TopCenter(status, 147f, 131f, 15f);
            var hlg = status.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var dot = MakeImage(status, "Status_Indicator", circle, Hex("#6ADF3A"), Image.Type.Simple).GetComponent<Image>();
            dot.rectTransform.sizeDelta = new Vector2(8f, 8f);
            var dle = dot.gameObject.AddComponent<LayoutElement>();
            dle.preferredWidth = 8f; dle.preferredHeight = 8f;
            var statusText = MakeText(status, "Text_User_Status", "Online", Medium, 12f, Hex("#E6E6E7"));
            statusText.rectTransform.sizeDelta = new Vector2(60f, 15f);

            var leader = NewRect("Leader_Chip", other);
            TopCenter(leader, 170f, 72f, 18f);
            var chipImg = leader.gameObject.AddComponent<Image>();
            chipImg.sprite = chipBg; chipImg.type = Image.Type.Sliced; chipImg.raycastTarget = false;
            var chipText = MakeText(leader, "Text", "Leader", SemiBold, 11f, OnAccent);
            chipText.alignment = TextAlignmentOptions.Center;
            Stretch(chipText.rectTransform);

            // PartyEntry fills the id and toggles the host marker; the design shows neither
            var id = MakeText(member, "Text_User_ID", "", Regular, 10f, TextMuted);
            id.gameObject.SetActive(false);
            var host = NewRect("HostMarker", root);

            var entry = AddByName(root, "ProDomino.FriendSystem.PartyEntry");
            var so = new SerializedObject(entry);
            so.FindProperty("turningOffDisocuppy").boolValue = true;
            so.FindProperty("profileIcon").objectReferenceValue = avatar;
            so.FindProperty("usernameLabel").objectReferenceValue = name;
            so.FindProperty("userIDLabel").objectReferenceValue = id;
            so.FindProperty("promAsLeaderButton").objectReferenceValue = null;
            so.FindProperty("kickButton").objectReferenceValue = null;
            so.FindProperty("leaderObject").objectReferenceValue = leader.gameObject;
            so.FindProperty("hostingMemberObject").objectReferenceValue = host.gameObject;
            so.FindProperty("partyMemberObject").objectReferenceValue = member.gameObject;
            so.FindProperty("availableSlotObject").objectReferenceValue = null;
            so.FindProperty("localPlayerLabel").stringValue = "You";
            so.FindProperty("otherMemberObject").objectReferenceValue = other.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PartyCardPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // ------------------------------------------------------------------ screen
        private static void BuildScreen(GameObject partyCard, GameObject friendCard)
        {
            var root = PrefabUtility.LoadPrefabContents(ScreenPath);
            try
            {
                var rootRt = (RectTransform)root.transform;
                Stretch(rootRt);
                rootRt.pivot = new Vector2(0.5f, 0.5f);

                // The root image was the old dimmed backdrop; the screen card below is the only visual now
                if (root.TryGetComponent<Image>(out var rootImg)) rootImg.color = new Color(0f, 0f, 0f, 0f);

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var screen = NewRect("Screen", root.transform);
                Stretch(screen);
                var screenImg = screen.gameObject.AddComponent<Image>();
                screenImg.sprite = screenBg; screenImg.type = Image.Type.Sliced; screenImg.raycastTarget = true;

                var scroll = NewRect("Scroll View", screen);
                Stretch(scroll);
                scroll.offsetMin = new Vector2(20f, 24f); scroll.offsetMax = new Vector2(-20f, -24f);
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
                vlg.spacing = 40f; vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.padding = new RectOffset(0, 0, 0, 24);
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scrollRect.viewport = viewport; scrollRect.content = content;

                // Create Party: title + current members (PartyMembers hides the row while no one is in the party)
                var party = Section(content, "PartySection", "Create Party");
                var partyGrid = CardGrid(party, "PartyCards", PartyCardHeight);
                for (int i = 0; i < PartySlots; i++)
                {
                    var slot = (GameObject)PrefabUtility.InstantiatePrefab(partyCard, partyGrid);
                    slot.name = $"PartyCard_{i + 1}";
                }
                var partyCg = partyGrid.gameObject.AddComponent<CanvasGroup>();
                var members = AddByName(partyGrid, "ProDomino.FriendSystem.PartyMembers");
                var mso = new SerializedObject(members);
                mso.FindProperty("partyEntryParent").objectReferenceValue = partyGrid;
                mso.FindProperty("partyCanvasGroup").objectReferenceValue = partyCg;
                mso.ApplyModifiedPropertiesWithoutUndo();

                // Friends List: same cards as the Friends List screen, no search bar
                var friends = Section(content, "FriendsSection", "Friends List");
                var friendsGrid = CardGrid(friends, "FriendCards", FriendCardHeight);
                var empty = MakeText(friends, "Text_EmptyFriends",
                    "You have no friends yet. Add players from the Friends List to invite them to your party.",
                    Medium, 16f, Hex("#FED88C"));
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.gameObject.SetActive(false);

                WireController(root, friendCard, friendsGrid, empty.gameObject, members, screen);

                PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform Section(Transform parent, string name, string title)
        {
            var section = NewRect(name, parent);
            var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 40f; vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var header = NewRect("Header", section);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 29f;
            var icon = MakeImage(header, "Icon", userIcon, Accent, Image.Type.Simple).GetComponent<Image>();
            icon.preserveAspect = true;
            TL(icon.rectTransform, 0f, 2f, 24f, 24f);
            var text = MakeText(header, "Text_Title", title, SemiBold, 24f, Color.white);
            TL(text.rectTransform, 34f, 0f, 400f, 29f);
            return section;
        }

        private static RectTransform CardGrid(Transform parent, string name, float cardHeight)
        {
            var grid = NewRect(name, parent);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(CardWidth, cardHeight);
            g.spacing = new Vector2(CardGap, CardGap);
            g.startCorner = GridLayoutGroup.Corner.UpperLeft;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.childAlignment = TextAnchor.UpperLeft;
            return grid;
        }

        private static void WireController(GameObject root, GameObject friendCard, Transform friendsGrid,
            GameObject empty, Component members, Transform screen)
        {
            var controller = root.GetComponent(FindType("ProDomino.FriendSystem.PartyController"));
            var so = new SerializedObject(controller);
            so.FindProperty("friendListCanvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("friendEntryPrefab").objectReferenceValue = friendCard.GetComponent(FindType("ProDomino.FriendSystem.FriendEntry"));
            so.FindProperty("friendEntriesParent").objectReferenceValue = friendsGrid;
            so.FindProperty("friendsCountLabel").objectReferenceValue = null;
            // Name | Status | Invite | Remove, same as the Friends List cards
            so.FindProperty("categoriesShown").intValue = 1 | 4 | 8 | 16;
            so.FindProperty("emptyFriendlistLabel").objectReferenceValue = empty;
            so.FindProperty("orderByToggleGroup").objectReferenceValue = null;
            so.FindProperty("closePopUp").objectReferenceValue = null;
            so.FindProperty("searchUsersCanvasGroup").objectReferenceValue = null;
            so.FindProperty("requestFriendshipButton").objectReferenceValue = null;
            so.FindProperty("requestFriendshipInputfield").objectReferenceValue = null;
            so.FindProperty("searchUserEntriesParent").objectReferenceValue = null;
            so.FindProperty("searchUserEntryPrefab").objectReferenceValue = null;
            so.FindProperty("mainPartyMembers").objectReferenceValue = members;
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

        // ------------------------------------------------------------------ navigation
        // Sidebar "Party" opens the screen as an overlay nav panel, like Friends List.
        [MenuItem("ProDomino/Dashboard/Wire Party Navigation")]
        public static void WireNavigation()
        {
            var root = PrefabUtility.LoadPrefabContents(ScreenPath);
            try
            {
                var cg = root.GetComponent<CanvasGroup>();
                cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
                var nav = root.GetComponent(FindType("ProDomino.FriendSystem.PartyNavPanel"))
                    ?? AddByName(root.transform, "ProDomino.FriendSystem.PartyNavPanel");
                var so = new SerializedObject(nav);
                so.FindProperty("partyController").objectReferenceValue = root.GetComponent(FindType("ProDomino.FriendSystem.PartyController"));
                so.FindProperty("rootCanvasGroup").objectReferenceValue = cg;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var canvas = PrefabUtility.LoadPrefabContents(MainCanvasPath);
            try
            {
                WirePartyButton(canvas.transform);
                PrefabUtility.SaveAsPrefabAsset(canvas, MainCanvasPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvas);
            }

            // The game scene keeps its own overrides on the canvas instance
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var sceneCanvas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ProDomino_MainCanvas");
            if (sceneCanvas)
            {
                WirePartyButton(sceneCanvas.transform);
                var panel = FindDeep(sceneCanvas.transform, InstanceName);
                if (panel && panel.TryGetComponent<CanvasGroup>(out var pcg))
                {
                    pcg.alpha = 0f; pcg.interactable = false; pcg.blocksRaycasts = false;
                }
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PartyScreenRestyler] Navigation wired.");
        }

        private static void WirePartyButton(Transform canvas)
        {
            var btn = FindDeep(canvas, "NavegationPanel_Party");
            if (!btn) throw new Exception("NavegationPanel_Party not found.");
            btn.gameObject.SetActive(true);

            // The original canvas removed Image, Button, CanvasGroup and CustomButtonUI from this instance,
            // so the entry could never be clicked or highlighted. Bring them back from the button prefab.
            foreach (var removed in PrefabUtility.GetRemovedComponents(btn.gameObject).ToArray())
                if (removed.containingInstanceGameObject == btn.gameObject || removed.containingInstanceGameObject.transform.IsChildOf(btn))
                    removed.Revert(InteractionMode.AutomatedAction);

            var type = FindType("CustomButtonUI");
            // Keep one CustomButtonUI: prefer the prefab's own, drop duplicates added on the instance
            var all = btn.GetComponents(type);
            var custom = all.FirstOrDefault(c => !PrefabUtility.IsAddedComponentOverride(c)) ?? all.FirstOrDefault();
            foreach (var extra in all.Where(c => c != custom))
                UnityEngine.Object.DestroyImmediate(extra, true);
            if (!custom) throw new Exception("CustomButtonUI missing on NavegationPanel_Party after restore.");
            // Drop stale overrides on the restored component so it links to this button's own children
            if (PrefabUtility.IsPartOfPrefabInstance(custom))
                PrefabUtility.RevertObjectOverride(custom, InteractionMode.AutomatedAction);
            foreach (var b in btn.GetComponentsInChildren<Behaviour>(true))
                if (b is Button || b.GetType() == type) b.enabled = true;

            // Highlight/hover graphics were deactivated on this instance; CustomButtonUI drives them by alpha
            foreach (var n in new[] { "NPButton_ToggleIndicator", "NPButton_Hoverindicator" })
            {
                var t = FindDeep(btn, n);
                if (t) t.gameObject.SetActive(true);
            }

            // Same sidebar styling as the Club entry (same button prefab), keeping the Party icon
            var club = FindDeep(canvas, "NavegationPanel_Club_Button");
            if (club) CopyNavStyle(club, btn, type);

            var so = new SerializedObject(custom);
            // Link the state graphics to this button's own children (an earlier copy left them on Friends List's)
            var toggleInd = FindDeep(btn, "NPButton_ToggleIndicator");
            var hoverInd = FindDeep(btn, "NPButton_Hoverindicator");
            var toggleGfx = FindDeep(btn, "NPButton_ToggleGraphic").GetComponent<Image>();
            var iconImg = FindDeep(btn, "NPButton_Icon").GetComponent<Image>();
            var label = FindDeep(btn, "NPButton_Text (TMP)").GetComponent<TMP_Text>();
            var tooltip = btn.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("Tooltip"));
            so.FindProperty("tooltipContainer").objectReferenceValue = tooltip ? tooltip.gameObject : null;
            so.FindProperty("canvasGroup_toggle").objectReferenceValue = toggleInd.GetComponent<CanvasGroup>();
            so.FindProperty("canvasGroup_hover").objectReferenceValue = hoverInd.GetComponent<CanvasGroup>();
            var texts = so.FindProperty("textElements_toggle");
            texts.arraySize = 1; texts.GetArrayElementAtIndex(0).objectReferenceValue = label;
            var images = so.FindProperty("imageElements_toggle");
            images.arraySize = 1; images.GetArrayElementAtIndex(0).objectReferenceValue = iconImg;
            var states = so.FindProperty("imageStates");
            for (int i = 0; i < states.arraySize; i++)
                states.GetArrayElementAtIndex(i).FindPropertyRelative("image").objectReferenceValue = i == 0 ? iconImg : toggleGfx;

            so.FindProperty("toggleID").stringValue = "Party";
            so.FindProperty("isInteractable").boolValue = true;
            so.FindProperty("isToggleable").boolValue = true;
            so.FindProperty("blockClickHandler").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (btn.TryGetComponent<Button>(out var button)) button.interactable = true;
            if (btn.TryGetComponent<CanvasGroup>(out var bcg))
            {
                bcg.alpha = 1f; bcg.interactable = true; bcg.blocksRaycasts = true;
            }
        }

        private static void CopyNavStyle(Transform src, Transform dst, Type customType)
        {
            if (src.TryGetComponent<Image>(out var sRoot) && dst.TryGetComponent<Image>(out var dRoot))
            {
                dRoot.sprite = sRoot.sprite; dRoot.type = sRoot.type; dRoot.color = sRoot.color; dRoot.enabled = sRoot.enabled;
                dRoot.raycastTarget = sRoot.raycastTarget;
            }

            foreach (var n in new[] { "NPButton_ToggleIndicator", "NPButton_Hoverindicator" })
            {
                var s = FindDeep(src, n); var d = FindDeep(dst, n);
                if (!s || !d) continue;
                CopyRect((RectTransform)s, (RectTransform)d);
                if (s.TryGetComponent<CanvasGroup>(out var scg) && d.TryGetComponent<CanvasGroup>(out var dcg)) dcg.alpha = scg.alpha;
            }
            foreach (var n in new[] { "NPButton_ToggleGraphic", "NPButton_HoverGraphic", "NPButton_Icon" })
            {
                var s = FindDeep(src, n); var d = FindDeep(dst, n);
                if (!s || !d) continue;
                CopyRect((RectTransform)s, (RectTransform)d);
                var si = s.GetComponent<Image>(); var di = d.GetComponent<Image>();
                if (n != "NPButton_Icon") { di.sprite = si.sprite; di.type = si.type; }
                di.color = si.color; di.preserveAspect = si.preserveAspect;
                di.pixelsPerUnitMultiplier = si.pixelsPerUnitMultiplier; di.raycastTarget = si.raycastTarget;
                if (s.TryGetComponent<AspectRatioFitter>(out var sf) && d.TryGetComponent<AspectRatioFitter>(out var df)) df.enabled = sf.enabled;
            }
            var st = FindDeep(src, "NPButton_Text (TMP)"); var dt = FindDeep(dst, "NPButton_Text (TMP)");
            if (st && dt)
            {
                CopyRect((RectTransform)st, (RectTransform)dt);
                var a = st.GetComponent<TMP_Text>(); var b = dt.GetComponent<TMP_Text>();
                b.font = a.font; b.fontSharedMaterial = a.fontSharedMaterial; b.color = a.color;
                b.enableAutoSizing = a.enableAutoSizing; b.fontSizeMin = a.fontSizeMin; b.fontSizeMax = a.fontSizeMax;
                b.fontSize = a.fontSize; b.alignment = a.alignment; b.fontStyle = a.fontStyle;
            }

            // State colors: both buttons come from the same prefab, so the lists line up by index
            var from = new SerializedObject(src.GetComponent(customType));
            var to = new SerializedObject(dst.GetComponent(customType));
            foreach (var p in new[] { "selectedColor", "deselectedColor", "hoverColor" })
                to.FindProperty(p).colorValue = from.FindProperty(p).colorValue;
            var fs = from.FindProperty("imageStates"); var ts = to.FindProperty("imageStates");
            for (int i = 0; fs != null && ts != null && i < Mathf.Min(fs.arraySize, ts.arraySize); i++)
                foreach (var c in new[] { "selectedStateColor", "deselectedStateColor", "hoverStateColor" })
                    ts.GetArrayElementAtIndex(i).FindPropertyRelative(c).colorValue =
                        fs.GetArrayElementAtIndex(i).FindPropertyRelative(c).colorValue;
            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyRect(RectTransform s, RectTransform d)
        {
            d.anchorMin = s.anchorMin; d.anchorMax = s.anchorMax; d.pivot = s.pivot;
            d.anchoredPosition = s.anchoredPosition; d.sizeDelta = s.sizeDelta;
        }

        // The MiddleScreen instance carries layout overrides for the old popup children; drop them so
        // the rebuilt layout drives itself, and keep the root filling Static_PopUps.
        private static void CleanMiddleScreenInstance()
        {
            CleanInstance(MiddleScreenPath);
            CleanInstance(MainCanvasPath);
        }

        // Reverts the rect overrides of the panel subtree and gives the root the same rect as the Friends List
        // screen, so both sections fill the same area next to the sidebar.
        private static void CleanInstance(string prefabPath)
        {
            var host = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var panel = FindDeep(host.transform, InstanceName);
                if (!panel) throw new Exception($"{InstanceName} not found in {prefabPath}.");

                foreach (var t in panel.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(t))
                        PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);

                var rt = (RectTransform)panel;
                var reference = FindDeep(host.transform, "FriendList_Popup") as RectTransform;
                if (reference && reference.parent == rt.parent)
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

                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(panel.gameObject);
                if (instanceRoot)
                    PrefabUtility.RemoveUnusedOverrides(new[] { instanceRoot }, InteractionMode.AutomatedAction);

                PrefabUtility.SaveAsPrefabAsset(host, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(host);
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

        // ------------------------------------------------------------------ render
        [MenuItem("ProDomino/Dashboard/Render Party Screen")]
        public static void RenderOnly()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            Render(Path.Combine(outDir, "party_no_party.png"), p => FillFriends(p, 6));
            Render(Path.Combine(outDir, "party_with_members.png"), p =>
            {
                FillFriends(p, 6);
                string[] names = { "Robert", "Max", "Courtney", "You" };
                var grid = FindDeep(p, "PartyCards");
                for (int i = 0; i < grid.childCount; i++)
                {
                    var card = grid.GetChild(i);
                    card.gameObject.SetActive(true);
                    FindDeep(card, "Text_Username").GetComponent<TMP_Text>().text = names[i];
                    FindDeep(card, "Leader_Chip").gameObject.SetActive(i == 0);
                    FindDeep(card, "OtherMember_Object").gameObject.SetActive(i < 3);
                }
            });
            Debug.Log($"[PartyScreenRestyler] Rendered to {outDir}");
        }

        private static void FillFriends(Transform popup, int count)
        {
            string[] names = { "Robert", "Aubrey", "Max", "Courtney", "Marjorie", "Arlene" };
            bool[] online = { true, false, true, true, true, false };
            var grid = FindDeep(popup, "FriendCards");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FriendCardPath);
            for (int i = grid.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(grid.GetChild(i).gameObject);
            for (int i = 0; i < count; i++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid);
                FindDeep(card.transform, "Text_Friend_Username").GetComponent<TMP_Text>().text = names[i % names.Length];
                var on = online[i % online.Length];
                var status = FindDeep(card.transform, "Text_User_Status").GetComponent<TMP_Text>();
                status.text = on ? "Online" : "Offline";
                status.color = on ? Hex("#E6E6E7") : Hex("#8A8A8F");
                FindDeep(card.transform, "Status_Indicator").GetComponent<Image>().color = on ? Hex("#6ADF3A") : Hex("#8A8A8F");
            }
        }

        // Renders the game scene's own canvas copy (it keeps its own overrides) without saving the scene.
        [MenuItem("ProDomino/Dashboard/Render Party Screen (Scene)")]
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

            Render(Path.Combine(outDir, "scene_party.png"), p => FillFriends(p, 6), canvas);
        }

        private static void Render(string path, Action<Transform> setup, GameObject existing = null)
        {
            SidebarRestyler.RenderCanvas(path, 1440, 1024, true, root =>
            {
                var dash = FindDeep(root.transform, "Dashboard_Content");
                if (dash && dash.TryGetComponent<CanvasGroup>(out var dcg)) dcg.alpha = 0f;

                var popup = FindDeep(root.transform, InstanceName);
                var popupCg = popup.GetComponent<CanvasGroup>();
                popupCg.alpha = 1f; popupCg.interactable = true; popupCg.blocksRaycasts = true;
                setup(popup);

                var type = FindType("CustomButtonUI");
                var preview = type.GetMethod("PreviewVisualState");
                var idProp = type.GetProperty("CustomButtonID");
                foreach (var b in root.GetComponentsInChildren(type, true))
                {
                    var id = (string)idProp.GetValue(b);
                    if (!string.IsNullOrEmpty(id))
                        preview.Invoke(b, new object[] { id.Contains("Party") });
                }

                foreach (var g in popup.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
            }, existing);
        }
    }
}
