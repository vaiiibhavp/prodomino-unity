using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.Shared;
using ProDomino.FriendSystem;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles and builds the Friends List screen (FriendsList_Screen.prefab) matching the Figma reference (media_1790149930827.png):
    /// - Framed Screen Card (#070A14, border #1E2538, radius 16px)
    /// - Header: Golden user icon (Nav_FriendsList.png) + "Friends List" (Montserrat-Bold 24px)
    /// - Empty State:
    ///     - Circular avatar illustration (Friends_Empty_Illustration.png, 84x84)
    ///     - "Your Friends List is Empty" (Montserrat-Bold 30px)
    ///     - Subtitle (Montserrat-Regular 14.5px, #94A3B8)
    ///     - Search bar: Input field (360x50, #111625, border #222B3D) + Golden Search Button (116x50, "Search")
    ///     - Amber hint text: "Play your first match to discover players you've competed against and add them as friends." (#EAB308)
    /// - Wires sidebar button NavegationPanel_FriendsList_Button with toggleID "FriendsList"
    /// </summary>
    public static class FriendsListRestyler
    {
        private const string FriendsPrefabPath = "Assets/_ProDomino/Prefabs/UI/FriendsList_Screen.prefab";
        private const string MiddleScreenPrefabPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string MainCanvasPrefabPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;
        private static Sprite searchInputBg, searchBtnNormalBg, searchBtnHoverBg;
        private static Sprite emptyIllustration, friendsHeaderIcon;

        [MenuItem("ProDomino/Dashboard/Restyle Friends List + Render")]
        public static void ApplyAndRender()
        {
            Debug.Log("[FriendsListRestyler] Starting Friends List screen restyling...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            PrepareAssets();
            var screenPrefab = BuildFriendsScreenPrefab();
            EnsureInMiddleScreen(screenPrefab);
            EnsureSidebarButtonConfigured();
            EnsureInScene(screenPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderScreenshots();
            Debug.Log("[FriendsListRestyler] SUCCESS: Friends List screen restyled and rendered successfully!");
        }

        [MenuItem("ProDomino/Dashboard/Test Click Friends List Button")]
        public static void TestClickFriendsList()
        {
            var btn = GameObject.Find("NavegationPanel_FriendsList_Button");
            if (btn != null)
            {
                var customBtn = btn.GetComponent<CustomButtonUI>();
                if (customBtn != null)
                {
                    Debug.Log("[TestClickFriendsList] Calling customBtn.Select()...");
                    customBtn.Select();
                }
                else
                {
                    var uBtn = btn.GetComponent<Button>();
                    uBtn?.onClick?.Invoke();
                }
            }
            else
            {
                Debug.LogWarning("[TestClickFriendsList] NavegationPanel_FriendsList_Button not found in active scene!");
            }
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            // Input field background (48x48, radius 10, #111625 -> #0E1320, border #222B3D)
            searchInputBg = MakePanelSprite("Friends_Search_Input_Bg", 48, 48, 10, Hex("#111625"), Hex("#0E1320"), Hex("#222B3D"), 1.2f, true);

            // Search button background (golden horizontal gradient #FFA000 -> #FFBD1E, border #FFD54F)
            searchBtnNormalBg = MakePanelSprite("Friends_Search_Btn_Normal", 48, 48, 10, Hex("#FFA000"), Hex("#FFBD1E"), Hex("#FFD54F"), 1f, false);
            searchBtnHoverBg = MakePanelSprite("Friends_Search_Btn_Hover", 48, 48, 10, Hex("#FFB300"), Hex("#FFCA28"), Hex("#FFE082"), 1f, false);

            // Illustrations & Icons
            emptyIllustration = AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedDir}/Friends_Empty_Illustration.png");
            friendsHeaderIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Nav_FriendsList.png");
        }

        private static GameObject BuildFriendsScreenPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FriendsPrefabPath));

            var rootGo = new GameObject("FriendsList_Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var screenCardImg = rootGo.GetComponent<Image>();
            screenCardImg.sprite = GetOrCreateScreenCardSprite();
            screenCardImg.type = Image.Type.Sliced;
            screenCardImg.color = Color.white;
            screenCardImg.raycastTarget = true;

            var rootCg = rootGo.GetComponent<CanvasGroup>();
            rootCg.alpha = 0f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;

            var friendsUi = rootGo.AddComponent<FriendsListUI>();

            // 1. Header (Top-Left)
            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(rootGo.transform, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(0f, 1f);
            headerRt.pivot = new Vector2(0f, 1f);
            headerRt.anchoredPosition = new Vector2(28f, -24f);
            headerRt.sizeDelta = new Vector2(300f, 40f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(headerGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0f, 0f);
            iconRt.sizeDelta = new Vector2(22f, 22f);

            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = friendsHeaderIcon;
            iconImg.color = Hex("#FFA800"); // Golden person icon
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(34f, 0f);
            titleRt.sizeDelta = new Vector2(250f, 32f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Friends List";
            titleTmp.font = fBold;
            titleTmp.fontSize = 24f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;
            titleTmp.raycastTarget = false;

            // 2. Empty State Container
            var emptyGo = new GameObject("EmptyStateContainer", typeof(RectTransform));
            emptyGo.transform.SetParent(rootGo.transform, false);
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0.5f, 1f);
            emptyRt.anchorMax = new Vector2(0.5f, 1f);
            emptyRt.pivot = new Vector2(0.5f, 1f);
            emptyRt.anchoredPosition = new Vector2(0f, -140f);
            emptyRt.sizeDelta = new Vector2(700f, 500f);

            // 2a. Avatar Illustration (84x84)
            var illusGo = new GameObject("EmptyIllustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            illusGo.transform.SetParent(emptyGo.transform, false);
            var illusRt = illusGo.GetComponent<RectTransform>();
            illusRt.anchorMin = new Vector2(0.5f, 1f);
            illusRt.anchorMax = new Vector2(0.5f, 1f);
            illusRt.pivot = new Vector2(0.5f, 1f);
            illusRt.anchoredPosition = new Vector2(0f, 0f);
            illusRt.sizeDelta = new Vector2(84f, 84f);

            var illusImg = illusGo.GetComponent<Image>();
            illusImg.sprite = emptyIllustration;
            illusImg.preserveAspect = true;
            illusImg.raycastTarget = false;

            // 2b. Headline Text
            var headGo = new GameObject("HeadlineText", typeof(RectTransform), typeof(TextMeshProUGUI));
            headGo.transform.SetParent(emptyGo.transform, false);
            var headRt = headGo.GetComponent<RectTransform>();
            headRt.anchorMin = new Vector2(0.5f, 1f);
            headRt.anchorMax = new Vector2(0.5f, 1f);
            headRt.pivot = new Vector2(0.5f, 1f);
            headRt.anchoredPosition = new Vector2(0f, -108f);
            headRt.sizeDelta = new Vector2(600f, 40f);

            var headTmp = headGo.GetComponent<TextMeshProUGUI>();
            headTmp.text = "Your Friends List is Empty";
            headTmp.font = fBold;
            headTmp.fontSize = 30f;
            headTmp.color = Color.white;
            headTmp.alignment = TextAlignmentOptions.Center;
            headTmp.raycastTarget = false;

            // 2c. Subtitle Text
            var subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(emptyGo.transform, false);
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -156f);
            subRt.sizeDelta = new Vector2(550f, 42f);

            var subTmp = subGo.GetComponent<TextMeshProUGUI>();
            subTmp.text = "Search for ProDomino players to send friend\nrequests and start playing together.";
            subTmp.font = fRegular;
            subTmp.fontSize = 14.5f;
            subTmp.lineSpacing = 4f;
            subTmp.color = Hex("#94A3B8");
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.raycastTarget = false;

            // 2d. Search Bar Row (490x50)
            var searchRowGo = new GameObject("SearchBarRow", typeof(RectTransform));
            searchRowGo.transform.SetParent(emptyGo.transform, false);
            var srRt = searchRowGo.GetComponent<RectTransform>();
            srRt.anchorMin = new Vector2(0.5f, 1f);
            srRt.anchorMax = new Vector2(0.5f, 1f);
            srRt.pivot = new Vector2(0.5f, 1f);
            srRt.anchoredPosition = new Vector2(0f, -225f);
            srRt.sizeDelta = new Vector2(490f, 50f);

            // Input Field (360x50)
            var inputGo = new GameObject("SearchInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(searchRowGo.transform, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(0f, 1f);
            inputRt.pivot = new Vector2(0f, 0.5f);
            inputRt.anchoredPosition = Vector2.zero;
            inputRt.sizeDelta = new Vector2(360f, 0f);

            var inputImg = inputGo.GetComponent<Image>();
            inputImg.sprite = searchInputBg;
            inputImg.type = Image.Type.Sliced;
            inputImg.color = Color.white;
            inputImg.raycastTarget = true;

            var inputField = inputGo.GetComponent<TMP_InputField>();

            // Text Area
            var textAreaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textAreaGo.transform.SetParent(inputGo.transform, false);
            var taRt = textAreaGo.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(16f, 0f);
            taRt.offsetMax = new Vector2(-16f, 0f);

            // Placeholder Text
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            phGo.transform.SetParent(textAreaGo.transform, false);
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;

            var phTmp = phGo.GetComponent<TextMeshProUGUI>();
            phTmp.text = "Search username...";
            phTmp.font = fRegular;
            phTmp.fontSize = 15f;
            phTmp.color = Hex("#64748B");
            phTmp.alignment = TextAlignmentOptions.Left;
            phTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

            // Input Text
            var itGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            itGo.transform.SetParent(textAreaGo.transform, false);
            var itRt = itGo.GetComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;

            var itTmp = itGo.GetComponent<TextMeshProUGUI>();
            itTmp.text = "";
            itTmp.font = fRegular;
            itTmp.fontSize = 15f;
            itTmp.color = Color.white;
            itTmp.alignment = TextAlignmentOptions.Left;
            itTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

            inputField.textViewport = taRt;
            inputField.textComponent = itTmp;
            inputField.placeholder = phTmp;

            // Search Button (116x50)
            var searchBtnGo = new GameObject("SearchButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            searchBtnGo.transform.SetParent(searchRowGo.transform, false);
            var sbRt = searchBtnGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.anchoredPosition = Vector2.zero;
            sbRt.sizeDelta = new Vector2(116f, 0f);

            var sbImg = searchBtnGo.GetComponent<Image>();
            sbImg.sprite = searchBtnNormalBg;
            sbImg.type = Image.Type.Sliced;
            sbImg.color = Color.white;
            sbImg.raycastTarget = true;

            var searchBtn = searchBtnGo.GetComponent<Button>();
            searchBtn.targetGraphic = sbImg;
            searchBtn.transition = Selectable.Transition.SpriteSwap;
            var st = searchBtn.spriteState;
            st.highlightedSprite = searchBtnHoverBg;
            st.pressedSprite = searchBtnHoverBg;
            searchBtn.spriteState = st;

            var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextGo.transform.SetParent(searchBtnGo.transform, false);
            var btRt = btnTextGo.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;

            var btTmp = btnTextGo.GetComponent<TextMeshProUGUI>();
            btTmp.text = "Search";
            btTmp.font = fBold;
            btTmp.fontSize = 16f;
            btTmp.color = Hex("#01010C"); // Dark label on gold
            btTmp.alignment = TextAlignmentOptions.Center;
            btTmp.raycastTarget = false;

            // 2e. Hint Text
            var hintGo = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(emptyGo.transform, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 1f);
            hintRt.anchorMax = new Vector2(0.5f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, -305f);
            hintRt.sizeDelta = new Vector2(700f, 30f);

            var hintTmp = hintGo.GetComponent<TextMeshProUGUI>();
            hintTmp.text = "Play your first match to discover players you've competed against and add them as friends.";
            hintTmp.font = fMedium;
            hintTmp.fontSize = 13.5f;
            hintTmp.color = Hex("#EAB308"); // Warm amber hint color matching Figma
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.raycastTarget = false;

            // Wire Serialized Properties to FriendsListUI
            var so = new SerializedObject(friendsUi);
            so.FindProperty("rootCanvasGroup").objectReferenceValue = rootCg;
            so.FindProperty("emptyStateContainer").objectReferenceValue = emptyGo;
            so.FindProperty("searchInputField").objectReferenceValue = inputField;
            so.FindProperty("searchButton").objectReferenceValue = searchBtn;
            so.FindProperty("screenTitleLabel").objectReferenceValue = titleTmp;
            so.FindProperty("screenTitleIcon").objectReferenceValue = iconImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, FriendsPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);
            Debug.Log($"[FriendsListRestyler] Created prefab at {FriendsPrefabPath}");
            return prefab;
        }

        private static void EnsureInMiddleScreen(GameObject screenPrefab)
        {
            var middleGo = PrefabUtility.LoadPrefabContents(MiddleScreenPrefabPath);
            try
            {
                var innerScreen = middleGo.transform.Find("InnerScreen");
                if (innerScreen != null)
                {
                    // Destroy any old FriendsList_Screen instance
                    for (int i = innerScreen.childCount - 1; i >= 0; i--)
                    {
                        var child = innerScreen.GetChild(i);
                        if (child.name.IndexOf("Friends", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                        }
                    }

                    // Instantiate new clean prefab instance
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, innerScreen);
                    inst.name = "FriendsList_Screen";
                    inst.SetActive(false);

                    var rt = (RectTransform)inst.transform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    var cg = inst.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }

                    Debug.Log("[FriendsListRestyler] Connected FriendsList_Screen inside MiddleScreen_Scalable.prefab");
                }

                PrefabUtility.SaveAsPrefabAsset(middleGo, MiddleScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleGo);
            }
        }

        private static void ConfigureSidebarButton(Transform friendsBtn, Sprite iconSprite)
        {
            if (friendsBtn == null) return;

            // Set label and remove LocalizeStringEvent so localization does not overwrite text at runtime
            var label = FindDeep(friendsBtn, "NPButton_Text (TMP)");
            if (label != null)
            {
                var tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "Friends List";
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                }

                var lse = label.GetComponent("LocalizeStringEvent");
                if (lse != null) UnityEngine.Object.DestroyImmediate(lse);
            }

            // Set icon
            if (iconSprite != null)
            {
                var iconTr = FindDeep(friendsBtn, "Image") ?? FindDeep(friendsBtn, "Icon");
                if (iconTr != null && iconTr.TryGetComponent<Image>(out var img))
                {
                    img.sprite = iconSprite;
                    img.color = new Color(0.69f, 0.69f, 0.706f, 1f);
                }
            }

            var customBtn = friendsBtn.GetComponent<CustomButtonUI>();
            if (customBtn != null)
            {
                var so = new SerializedObject(customBtn);
                so.FindProperty("toggleID").stringValue = "FriendsList";
                so.FindProperty("isInteractable").boolValue = true;
                so.FindProperty("isToggleable").boolValue = true;
                so.FindProperty("blockClickHandler").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var btn = friendsBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
            }
        }

        private static void ConfigureGamesButton(Transform gamesBtn)
        {
            if (gamesBtn == null) return;

            var label = FindDeep(gamesBtn, "NPButton_Text (TMP)");
            if (label != null)
            {
                var tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "Games";
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                }

                var lse = label.GetComponent("LocalizeStringEvent");
                if (lse != null) UnityEngine.Object.DestroyImmediate(lse);
            }

            var customBtn = gamesBtn.GetComponent<CustomButtonUI>();
            if (customBtn != null)
            {
                var so = new SerializedObject(customBtn);
                so.FindProperty("toggleID").stringValue = "Games";
                so.FindProperty("isInteractable").boolValue = true;
                so.FindProperty("isToggleable").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var btn = gamesBtn.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
            }
        }

        private static void EnsureSidebarButtonConfigured()
        {
            var canvasGo = PrefabUtility.LoadPrefabContents(MainCanvasPrefabPath);
            try
            {
                var friendsBtn = FindDeep(canvasGo.transform, "NavegationPanel_FriendsList_Button");
                ConfigureSidebarButton(friendsBtn, friendsHeaderIcon);

                var gamesBtn = FindDeep(canvasGo.transform, "NavegationPanel_Games_Button");
                ConfigureGamesButton(gamesBtn);

                PrefabUtility.SaveAsPrefabAsset(canvasGo, MainCanvasPrefabPath);
                Debug.Log("[FriendsListRestyler] Configured sidebar buttons in ProDomino_MainCanvas.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(canvasGo);
            }
        }

        private static void EnsureInScene(GameObject screenPrefab)
        {
            const string scenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var roots = scene.GetRootGameObjects();
            Transform canvasRoot = null;
            foreach (var r in roots)
            {
                if (r.name == "ProDomino_MainCanvas")
                {
                    canvasRoot = r.transform;
                    break;
                }
            }

            if (canvasRoot != null)
            {
                // Configure buttons in the scene's sidebar
                var friendsBtn = FindDeep(canvasRoot, "NavegationPanel_FriendsList_Button");
                ConfigureSidebarButton(friendsBtn, friendsHeaderIcon);

                var gamesBtn = FindDeep(canvasRoot, "NavegationPanel_Games_Button");
                ConfigureGamesButton(gamesBtn);

                // Put FriendsList_Screen into InnerScreen in the scene
                var innerScreen = FindDeep(canvasRoot, "InnerScreen");
                if (innerScreen != null)
                {
                    var existing = innerScreen.Find("FriendsList_Screen");
                    if (existing != null)
                    {
                        UnityEngine.Object.DestroyImmediate(existing.gameObject);
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, innerScreen);
                    instance.name = "FriendsList_Screen";

                    var rt = instance.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;

                    var cg = instance.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                    instance.SetActive(false);

                    Debug.Log("[FriendsListRestyler] Successfully instantiated FriendsList_Screen into InnerScreen in MainSceneDomDemo.unity!");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[FriendsListRestyler] Successfully saved MainSceneDomDemo.unity!");
            }
            else
            {
                Debug.LogWarning("[FriendsListRestyler] ProDomino_MainCanvas root not found in MainSceneDomDemo.unity!");
            }
        }

        private static void RenderScreenshots()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            string outPath = Path.Combine(outDir, "screen_friends_empty.png");

            SidebarRestyler.RenderCanvas(outPath, 1920, 1080, true, root =>
            {
                // Deactivate Dashboard Content
                var dash = FindDeep(root.transform, "Dashboard_Content");
                if (dash != null)
                {
                    dash.gameObject.SetActive(false);
                    if (dash.TryGetComponent<CanvasGroup>(out var dcg))
                    {
                        dcg.alpha = 0f;
                        dcg.interactable = false;
                        dcg.blocksRaycasts = false;
                    }
                }

                // Activate FriendsList_Screen under InnerScreen
                var inner = FindDeep(root.transform, "InnerScreen");
                if (inner != null)
                {
                    for (int i = 0; i < inner.childCount; i++)
                    {
                        var child = inner.GetChild(i);
                        bool isFriends = child.name.IndexOf("Friends", StringComparison.OrdinalIgnoreCase) >= 0;
                        child.gameObject.SetActive(isFriends);

                        foreach (var cg in child.GetComponentsInChildren<CanvasGroup>(true))
                        {
                            cg.alpha = isFriends ? 1f : 0f;
                            cg.interactable = isFriends;
                            cg.blocksRaycasts = isFriends;
                        }

                        if (isFriends)
                        {
                            var ui = child.GetComponent<FriendsListUI>();
                            if (ui != null)
                                ui.RefreshUI();
                        }
                    }
                }

                // Highlight "Friends List" option on sidebar and deselect all others
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("CustomButtonUI")).FirstOrDefault(t => t != null);
                if (type != null)
                {
                    var preview = type.GetMethod("PreviewVisualState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    var idProp = type.GetProperty("CustomButtonID");

                    foreach (var b in root.GetComponentsInChildren(type, true))
                    {
                        string id = (string)idProp?.GetValue(b);
                        bool isFriends = string.Equals(id, "FriendsList", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(id, "Friends", StringComparison.OrdinalIgnoreCase);
                        preview?.Invoke(b, new object[] { isFriends });
                    }
                }
            });

            Debug.Log($"[FriendsListRestyler] Rendered screenshot to {outPath}");
        }
    }
}
