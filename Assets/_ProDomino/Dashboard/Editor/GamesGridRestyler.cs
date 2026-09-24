using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Restyles GameModeSelectUI.prefab (the "Games" nav panel) per the reference mockups:
    // a big-card grid (Block/Concentrate playable now, French/Draw/Five as "Coming Soon" until
    // their art arrives) replaces the old inline "Most Popular Game Modes" icon row, and the
    // existing mode/type/players/difficulty selector -- left completely alone functionally --
    // is reparented into a centered popup modal instead of showing inline.
    //
    // GameModeConfig lives in the ProDomino.GameModes assembly, which this Editor assembly does
    // not reference, so it's driven the same way SidebarRestyler drives DashboardController:
    // by type name via reflection, and persistent UnityEvent listeners wired by method name so
    // no compile-time reference is needed at all.
    internal static class GamesGridRestyler
    {
        private const string PrefabPath = "Assets/_ProDomino/Prefabs/UI/GameModeSelectUI.prefab";
        private const string ArtDir = "Assets/_ProDomino/_Art/Dashboard";
        private const string GameModeIconDir = "Assets/_ProDomino/_UI/Game_Modes";

        private static readonly (string id, string title, string illustration, Color colorA, Color colorB)[] PlayableCards =
        {
            ("block", "Block", "PlayGames_Block_Illustration", Hex("#99015B"), Hex("#FF0197")),
            ("concentrate", "Concentrate", "PlayGames_Concentrate_Illustration", Hex("#520062"), Hex("#A700C8")),
        };

        // (id, title, icon file under GameModeIconDir) -- shown dimmed with no Play button until
        // real card art is supplied.
        private static readonly (string id, string title, string icon)[] ComingSoonCards =
        {
            ("french", "French", "Frenck_512"),
            ("draw", "Draw", "Draw_512"),
            ("five", "Five", "Five_512"),
        };

        [MenuItem("ProDomino/Dashboard/Restyle Games Grid + Modal")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var gameModeConfigType = FindType("GameModeConfig");
                var gameModeConfig = root.GetComponent(gameModeConfigType);
                Require(gameModeConfig, "GameModeConfig on GameModeSelectUI root");

                var scrollView = FindDeep(root.transform, "Scroll View_SelectionUI");
                Require(scrollView, "Scroll View_SelectionUI in GameModeSelectUI");
                var selectionUI = FindDeep(scrollView, "SelectionUI");
                Require(selectionUI, "SelectionUI under Scroll View_SelectionUI");

                // Safely reparent scrollView back to root before cleaning up any old modals!
                scrollView.SetParent(root.transform, false);

                // Destroy ALL existing GamesGrid_Root and GamesModal_Root duplicates
                var toDestroy = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    if (child.name == "GamesGrid_Root" || child.name == "GamesModal_Root")
                        toDestroy.Add(child.gameObject);
                }
                foreach (var go in toDestroy)
                    UnityEngine.Object.DestroyImmediate(go);

                // The old top row ("Most Popular Game Modes" + mode icons) is superseded by the
                // grid below; picking a mode now happens by tapping a card's Play button instead.
                Hide(selectionUI, "UIContainer_GameMode");

                // Grid first, modal second: sibling order is paint order, and the modal (which
                // starts inactive) must render on top of the grid once it's opened.
                var gridCanvasGroup = BuildGrid(root.transform, gameModeConfigType, gameModeConfig);
                var modalRoot = BuildModal(root.transform, scrollView, gameModeConfigType, gameModeConfig);

                var so = new SerializedObject(gameModeConfig);
                so.FindProperty("gamesModalRoot").objectReferenceValue = modalRoot.gameObject;
                so.FindProperty("gamesModalTitle").objectReferenceValue = FindDeep(modalRoot, "Title")?.GetComponent<TextMeshProUGUI>();
                so.FindProperty("gamesGridCanvasGroup").objectReferenceValue = gridCanvasGroup;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("GAMES_GRID_RESTYLE_DONE");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // Also clean up any duplicate roots in the active scene if open
            var sceneNav = GameObject.Find("GameModeSelectUI_NavPanel");
            if (sceneNav != null)
            {
                var sceneToDestroy = new System.Collections.Generic.List<GameObject>();
                int gCount = 0;
                int mCount = 0;
                for (int i = 0; i < sceneNav.transform.childCount; i++)
                {
                    var child = sceneNav.transform.GetChild(i);
                    if (child.name == "GamesGrid_Root")
                    {
                        gCount++;
                        if (gCount > 1) sceneToDestroy.Add(child.gameObject);
                    }
                    else if (child.name == "GamesModal_Root")
                    {
                        mCount++;
                        if (mCount > 1) sceneToDestroy.Add(child.gameObject);
                    }
                }
                foreach (var go in sceneToDestroy)
                    UnityEngine.Object.DestroyImmediate(go);

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sceneNav.scene);
            }
        }

        // ------------------------------------------------------------------ modal

        // Moves the existing (untouched) selector into a centered popup: a dim full-screen
        // background, a dark rounded panel, a title, and a close button. Starts inactive --
        // GameModeConfig.OpenGameModal/CloseGameModal toggle it.
        private static Transform BuildModal(Transform parent, Transform scrollView, Type gameModeConfigType, Component gameModeConfig)
        {
            var modal = new GameObject("GamesModal_Root", typeof(RectTransform)).transform;
            modal.SetParent(parent, false);
            Stretch((RectTransform)modal);
            modal.gameObject.SetActive(false);

            var dim = MakeImage(modal, "DimBackground", null, new Color(0f, 0f, 0f, 0.75f), Image.Type.Simple);
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().raycastTarget = true;
            var dimButton = dim.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            WireVoidClick(dimButton.onClick, gameModeConfigType, gameModeConfig, "CloseGameModal");

            var panelSprite = MakePanelSprite("PD_GamesModalPanel", 48, 48, (int)CardRadius * 2, Hex("#0B0B16"), Hex("#050509"), Hex("#2A2A38"), 1.5f);
            var panel = MakeImage(modal, "ModalPanel", panelSprite, Color.white, Image.Type.Sliced).transform;
            panel.GetComponent<Image>().raycastTarget = true;
            var panelRt = (RectTransform)panel;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(960f, 515f);

            var title = MakeText(panel, "Title", "Game", ExtraBold, 28f, TextPrimary);
            var titleRt = (RectTransform)title.transform;
            TL(titleRt, 36f, 20f, 600f, 38f);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var close = MakeImage(panel, "CloseButton", null, new Color(1f, 1f, 1f, 0.08f), Image.Type.Sliced);
            close.transform.SetAsLastSibling();
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-20f, -20f);
            closeRt.sizeDelta = new Vector2(36f, 36f);
            var closeLabel = MakeText(close.transform, "Label", "X", ExtraBold, 18f, TextPrimary);
            Stretch((RectTransform)closeLabel.transform);
            closeLabel.alignment = TextAlignmentOptions.Center;
            close.GetComponent<Image>().raycastTarget = true; // MakeImage defaults this off; the close button needs it on to receive clicks
            var closeButton = close.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            WireVoidClick(closeButton.onClick, gameModeConfigType, gameModeConfig, "CloseGameModal");

            // The selector keeps every field/reference GameModeConfig already has on it -- only
            // reparented and resized to fit the panel below the title, instead of rebuilt.
            scrollView.SetParent(panel, false);
            var scrollRt = (RectTransform)scrollView;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(24f, 16f);
            scrollRt.offsetMax = new Vector2(-24f, -62f);

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }

            var scrollbar = FindDeep(scrollView, "Scrollbar Vertical");
            if (scrollbar != null)
            {
                var sbarImg = scrollbar.GetComponent<Image>();
                if (sbarImg != null) sbarImg.color = new Color(1f, 1f, 1f, 0.04f);
                var handle = FindDeep(scrollbar, "Handle")?.GetComponent<Image>();
                if (handle != null) handle.color = new Color(1f, 1f, 1f, 0.15f);
            }

            var selectionUI = FindDeep(scrollView, "SelectionUI");
            if (selectionUI != null)
            {
                var srt = (RectTransform)selectionUI;
                srt.anchorMin = new Vector2(0f, 1f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.anchoredPosition = Vector2.zero;

                var vlg = selectionUI.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.childControlWidth = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 6f;
                    vlg.padding = new RectOffset(16, 16, 6, 6);
                    vlg.childAlignment = TextAnchor.UpperCenter;
                }

                RestyleSection(selectionUI, "UIContainer_GameType", "GameType_ContainerBtns", 98f);
                RestyleSection(selectionUI, "UIContainer_PlayerVs", "PlayerVs_ContainerBtns", 98f);
                RestyleSection(selectionUI, "UIContainer_Difficulty", "Difficulty_ContainerBtns", 98f);
                RestyleSection(selectionUI, "UIContainer_NumberTiles", "NumberTiles_ContainerBtns", 98f);

                var playContainer = FindDeep(selectionUI, "UIContainer_PlayGameBtn");
                if (playContainer != null)
                {
                    var prt = (RectTransform)playContainer;
                    prt.sizeDelta = new Vector2(0f, 65f);

                    for (int i = 0; i < playContainer.childCount; i++)
                    {
                        var child = playContainer.GetChild(i);
                        if (child.name.StartsWith("Dividing_Line"))
                            child.gameObject.SetActive(false);
                    }

                    var playBtn = FindDeep(playContainer, "PlayGameMode_CustomButton");
                    if (playBtn != null)
                    {
                        var brt = (RectTransform)playBtn;
                        brt.anchorMin = new Vector2(0.5f, 0.5f);
                        brt.anchorMax = new Vector2(0.5f, 0.5f);
                        brt.pivot = new Vector2(0.5f, 0.5f);
                        brt.anchoredPosition = Vector2.zero;
                        brt.sizeDelta = new Vector2(240f, 52f);

                        var goldSprite = MakeRoundedSprite("PD_Modal_PlayButton_Bg", 32, 32, 14, AccentStart, AccentEnd);
                        var bgImg = FindDeep(playBtn, "Bg_Image")?.GetComponent<Image>();
                        if (bgImg != null)
                        {
                            bgImg.sprite = goldSprite;
                            bgImg.type = Image.Type.Sliced;
                            bgImg.color = Color.white;
                            bgImg.raycastTarget = false;
                        }

                        var titleTmp = FindDeep(playBtn, "Title_Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                        if (titleTmp != null)
                        {
                            titleTmp.text = "Play";
                            titleTmp.color = OnAccent;
                            titleTmp.font = ExtraBold;
                            titleTmp.fontSize = 20f;
                            titleTmp.fontStyle = FontStyles.Bold;
                            titleTmp.alignment = TextAlignmentOptions.Center;
                            titleTmp.raycastTarget = false;
                        }

                        var customBtn = playBtn.GetComponent<CustomButtonUI>();
                        if (customBtn != null)
                        {
                            customBtn.SetColors(OnAccent, OnAccent, Hex("#1A1A24"));
                            var soBtn = new SerializedObject(customBtn);
                            var desColProp = soBtn.FindProperty("deselectedColor");
                            if (desColProp != null) desColProp.colorValue = OnAccent;
                            var selColProp = soBtn.FindProperty("selectedColor");
                            if (selColProp != null) selColProp.colorValue = OnAccent;
                            var hovColProp = soBtn.FindProperty("hoverColor");
                            if (hovColProp != null) hovColProp.colorValue = Hex("#1A1A24");
                            var isTogProp = soBtn.FindProperty("isToggleable");
                            if (isTogProp != null) isTogProp.boolValue = false;
                            var isDefProp = soBtn.FindProperty("isInteractable");
                            if (isDefProp != null) isDefProp.boolValue = true;
                            soBtn.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }

            return modal;
        }

        private static void RestyleSection(Transform parent, string containerName, string btnGroupName, float height)
        {
            var container = FindDeep(parent, containerName);
            if (container == null) return;

            var crt = (RectTransform)container;
            crt.sizeDelta = new Vector2(0f, height);

            // Title text cleanly positioned at the top of the container
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.name.EndsWith("_TitleText") || child.name.Contains("Title"))
                {
                    var trt = (RectTransform)child;
                    trt.anchorMin = new Vector2(0f, 1f);
                    trt.anchorMax = new Vector2(1f, 1f);
                    trt.pivot = new Vector2(0.5f, 1f);
                    trt.anchoredPosition = new Vector2(0f, -2f);
                    trt.sizeDelta = new Vector2(0f, 22f);
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.alignment = TextAlignmentOptions.Center;
                        tmp.fontSize = 15f;
                        tmp.color = TextMuted;
                    }
                }
            }

            // Buttons centered below the title
            var btns = container.Find(btnGroupName);
            if (btns != null)
            {
                var brt = (RectTransform)btns;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0f, 12f);
                brt.sizeDelta = new Vector2(0f, 50f);
                var hlg = btns.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                    hlg.spacing = 16f;
                }
            }

            // Divider line at the bottom
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.name.StartsWith("Dividing_Line"))
                {
                    var drt = (RectTransform)child;
                    drt.anchorMin = new Vector2(0.04f, 0f);
                    drt.anchorMax = new Vector2(0.96f, 0f);
                    drt.pivot = new Vector2(0.5f, 0f);
                    drt.anchoredPosition = Vector2.zero;
                    drt.sizeDelta = new Vector2(0f, 1.5f);
                    var dimg = child.GetComponent<Image>();
                    if (dimg != null)
                    {
                        dimg.color = new Color(1f, 1f, 1f, 0.08f);
                        dimg.raycastTarget = false;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ grid

        private static CanvasGroup BuildGrid(Transform parent, Type gameModeConfigType, Component gameModeConfig)
        {
            var grid = new GameObject("GamesGrid_Root", typeof(RectTransform)).transform;
            grid.SetParent(parent, false);
            Stretch((RectTransform)grid);
            // Covered/restored by DashboardController via GameModeConfig.SetSelectionUIForceHidden --
            // without this the grid has no way to get out of the way while the dashboard covers
            // this panel, and bleeds through its card gaps exactly like the old raw selector did.
            var gridCanvasGroup = grid.gameObject.AddComponent<CanvasGroup>();

            var layoutGo = new GameObject("CardsGrid", typeof(RectTransform));
            layoutGo.transform.SetParent(grid, false);
            var layoutRt = (RectTransform)layoutGo.transform;
            layoutRt.anchorMin = new Vector2(0f, 1f);
            layoutRt.anchorMax = new Vector2(0f, 1f);
            layoutRt.pivot = new Vector2(0f, 1f);
            layoutRt.anchoredPosition = new Vector2(0f, 0f);
            layoutRt.sizeDelta = new Vector2(1150f, 850f);

            var glg = layoutGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(567f, 253f);
            glg.spacing = new Vector2(16f, 16f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperLeft;

            foreach (var (id, title, illustration, colorA, colorB) in PlayableCards)
                BuildPlayableCard(layoutGo.transform, id, title, illustration, colorA, colorB, gameModeConfigType, gameModeConfig);

            foreach (var (id, title, icon) in ComingSoonCards)
                BuildComingSoonCard(layoutGo.transform, id, title, icon);

            return gridCanvasGroup;
        }

        private static void BuildPlayableCard(Transform parent, string id, string title, string illustration,
            Color colorA, Color colorB, Type gameModeConfigType, Component gameModeConfig)
        {
            var gradient = MakeRoundedSprite($"PD_GameCard_{id}", 64, 64, (int)CardRadius, colorA, colorB);
            var card = MakeImage(parent, $"{title}_GameCard", gradient, Color.white, Image.Type.Sliced);

            var illust = LoadArtSprite(illustration);
            if (illust != null)
            {
                var illustImg = MakeImage(card.transform, "Illustration", illust, Color.white, Image.Type.Simple);
                illustImg.GetComponent<Image>().preserveAspect = true;
                var irt = (RectTransform)illustImg.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(1f, 0.5f);
                irt.pivot = new Vector2(1f, 0.5f);
                irt.anchoredPosition = new Vector2(-16f, 6f);
                irt.sizeDelta = new Vector2(230f, 230f);
            }

            var titleText = MakeText(card.transform, "Title", title, ExtraBold, 44f, TextPrimary);
            PlaceTopLeft((RectTransform)titleText.transform, 32f, -20f, 260f, 60f);

            var playBtn = MakeImage(card.transform, "PlayButton",
                MakeRoundedSprite("PD_GameCard_PlayButton", 32, 32, 14, AccentStart, AccentEnd), Color.white, Image.Type.Sliced);
            playBtn.GetComponent<Image>().raycastTarget = true;
            var playRt = (RectTransform)playBtn.transform;
            playRt.anchorMin = playRt.anchorMax = new Vector2(0f, 0f);
            playRt.pivot = new Vector2(0f, 0f);
            playRt.anchoredPosition = new Vector2(32f, 24f);
            playRt.sizeDelta = new Vector2(112f, 48f);
            var playLabel = MakeText(playBtn.transform, "Label", "Play", ExtraBold, 20f, OnAccent);
            Stretch((RectTransform)playLabel.transform);
            playLabel.alignment = TextAlignmentOptions.Center;

            var button = playBtn.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            WireStringClick(button.onClick, gameModeConfigType, gameModeConfig, "OpenGameModal", id);
        }

        private static void BuildComingSoonCard(Transform parent, string id, string title, string iconName)
        {
            var gradient = MakeRoundedSprite("PD_GameCard_ComingSoon", 64, 64, (int)CardRadius, Hex("#1A1A22"), Hex("#0A0A0E"));
            var card = MakeImage(parent, $"{title}_GameCard_ComingSoon", gradient, Color.white, Image.Type.Sliced);

            var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{GameModeIconDir}/{iconName}.png");
            if (icon != null)
            {
                var iconImg = MakeImage(card.transform, "Icon", icon, new Color(1f, 1f, 1f, 0.35f), Image.Type.Simple);
                iconImg.GetComponent<Image>().preserveAspect = true;
                var irt = (RectTransform)iconImg.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(1f, 0.5f);
                irt.pivot = new Vector2(1f, 0.5f);
                irt.anchoredPosition = new Vector2(-40f, 0f);
                irt.sizeDelta = new Vector2(140f, 140f);
            }

            var titleText = MakeText(card.transform, "Title", title, ExtraBold, 36f, new Color(1f, 1f, 1f, 0.5f));
            PlaceTopLeft((RectTransform)titleText.transform, 32f, -20f, 260f, 50f);

            var comingSoon = MakeText(card.transform, "ComingSoonLabel", "Coming Soon", SemiBold, 18f, TextMuted);
            PlaceTopLeft((RectTransform)comingSoon.transform, 32f, -76f, 200f, 26f);

            var cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = false;
        }

        // ------------------------------------------------------------------ reflection wiring

        private static void WireVoidClick(UnityEvent evt, Type targetType, Component target, string methodName)
        {
            var method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null) { Debug.LogWarning($"GAMES_GRID: {targetType.Name}.{methodName} not found."); return; }
            var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
            UnityEventTools.AddVoidPersistentListener(evt, action);
        }

        private static void WireStringClick(UnityEvent evt, Type targetType, Component target, string methodName, string arg)
        {
            var method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null) { Debug.LogWarning($"GAMES_GRID: {targetType.Name}.{methodName} not found."); return; }
            var action = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), target, method);
            UnityEventTools.AddStringPersistentListener(evt, action, arg);
        }

        // ------------------------------------------------------------------ assets

        private static Sprite LoadArtSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
    }
}
