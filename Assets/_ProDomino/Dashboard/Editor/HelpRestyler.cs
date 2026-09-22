using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.Shared;
using Timba.Database;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles Help_Screen.prefab and accordion elements to strictly match
    /// the Figma reference (media_1790084239067.png):
    /// - Header Card:
    ///     - Rounded midnight card with subtle 1px border (#1E2538)
    ///     - Domino watermark pattern on top-left
    ///     - Centered "Help Center" title + "Do you need help for something or do you have some questions" subtitle
    /// - Accordion FAQ List:
    ///     - Collapsed: Sleek dark navy card (#0D121F), subtle border, white title text, '+' toggle badge
    ///     - Expanded: Vibrant gold gradient (#FFA800 -> #FFC107), dark text (#0F172A), dark toggle badge with '−' icon, answer body text
    /// </summary>
    public static class HelpRestyler
    {
        private const string HelpPrefabPath = "Assets/_ProDomino/Prefabs/UI/Help_Screen.prefab";
        private const string HelpItemPrefabPath = "Assets/_ProDomino/Prefabs/UI/HelpAccordionItem.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string DatabasePath = "Assets/_ProDomino/Shared/ScriptableObjects/HelpScreenDatabase.asset";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;
        private static Sprite screenCardBg, cardNormalBg, cardActiveBg, toggleNormalBg, toggleActiveBg, watermarkSprite;

        [MenuItem("ProDomino/Dashboard/Restyle Help Screen + Render")]
        public static void ApplyAndRender()
        {
            Debug.Log("[HelpRestyler] Starting Help Center UI restyle...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            PrepareAssets();
            UpdateHelpDatabase();
            var itemPrefab = BuildHelpAccordionItemPrefab();
            BuildCleanHelpScreenPrefab(itemPrefab);
            EnsureInMiddleScreen(itemPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderScreenshots();
            Debug.Log("[HelpRestyler] SUCCESS: Help Center screen restyled and rendered successfully!");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            screenCardBg = GetOrCreateScreenCardSprite();

            // Sliced sprites for Accordion Cards
            cardNormalBg = MakePanelSprite("Help_Card_Normal", 48, 48, 10, Hex("#0D121F"), Hex("#080B14"), Hex("#1E2538"), 1f);
            cardActiveBg = MakePanelSprite("Help_Card_Active", 48, 48, 10, Hex("#FFB800"), Hex("#FFA000"), Hex("#FFD54F"), 1f, true);

            // Sliced sprites for Toggle Buttons (32x32)
            toggleNormalBg = MakePanelSprite("Help_Toggle_Normal", 32, 32, 8, Hex("#161E30"), Hex("#101624"), Hex("#25314C"), 1f);
            toggleActiveBg = MakePanelSprite("Help_Toggle_Active", 32, 32, 8, Hex("#0B121E"), Hex("#080D17"), Hex("#1E293B"), 1f);

            // Domino Watermark
            string watermarkPath = $"{GeneratedDir}/Rules_Domino_Watermark.png";
            watermarkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(watermarkPath);
        }

        private static void UpdateHelpDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<HelpScreenDatabase>(DatabasePath);
            if (db == null)
            {
                Debug.LogError($"[HelpRestyler] Database not found at {DatabasePath}");
                return;
            }

            var so = new SerializedObject(db);
            var itemsProp = so.FindProperty("items");
            if (itemsProp == null)
            {
                Debug.LogError("[HelpRestyler] Items property not found on HelpScreenDatabase!");
                return;
            }

            // Save existing EULA description if present
            string eulaDescEn = "";
            string eulaDescEs = "";
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var el = itemsProp.GetArrayElementAtIndex(i);
                var t = el.FindPropertyRelative("title_en")?.stringValue ?? "";
                if (t.IndexOf("EULA", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Licence", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    eulaDescEn = el.FindPropertyRelative("description_en")?.stringValue ?? "";
                    eulaDescEs = el.FindPropertyRelative("description_es")?.stringValue ?? "";
                    break;
                }
            }

            var faqList = new List<(string tEn, string dEn, string tEs, string dEs)>
            {
                (
                    "How do I start a match?",
                    "Select a game mode, choose your preferred match type, and tap Play to enter matchmaking.",
                    "¿Cómo empiezo una partida?",
                    "Selecciona un modo de juego, elige tu tipo de partida preferido y toca Jugar para ingresar al emparejamiento."
                ),
                (
                    "How do I add friends?",
                    "Go to the Friends List from the sidebar, search for your friend by username or Player ID, and tap Add Friend.",
                    "¿Cómo agrego amigos?",
                    "Ve a la Lista de Amigos desde la barra lateral, busca a tu amigo por nombre de usuario o ID de jugador, y toca Agregar Amigo."
                ),
                (
                    "How do I create a party?",
                    "Open the Party tab from the sidebar, select Create Party, and invite your online friends or share your party code.",
                    "¿Cómo creo un grupo?",
                    "Abre la pestaña Grupo desde la barra lateral, selecciona Crear Grupo e invita a tus amigos conectados o comparte tu código de grupo."
                ),
                (
                    "How do I join a club?",
                    "Go to the Club section, browse open clubs or search by name, and tap Join to become a member.",
                    "¿Cómo me uno a un club?",
                    "Ve a la sección Club, explora los clubes abiertos o busca por nombre, y toca Unirse para convertirte en miembro."
                ),
                (
                    "How do I claim rewards?",
                    "Visit the Achievements or Tournament tab to check completed milestones and tap Claim on any unlocked rewards.",
                    "¿Cómo reclamo recompensas?",
                    "Visita la pestaña Logros o Torneos para ver los objetivos completados y toca Reclamar en las recompensas desbloqueadas."
                ),
                (
                    "What happens if I disconnect during a match?",
                    "The game will attempt to reconnect you automatically. If the connection cannot be restored in time, an AI bot may temporarily take your turn.",
                    "¿Qué sucede si me desconecto durante una partida?",
                    "El juego intentará reconectarte automáticamente. Si la conexión no se puede restablecer a tiempo, un bot de IA puede tomar tu turno temporalmente."
                )
            };

            if (!string.IsNullOrEmpty(eulaDescEn))
            {
                faqList.Add((
                    "End User Licence Agreement (EULA)",
                    eulaDescEn,
                    "Acuerdo de licencia de usuario final (EULA)",
                    eulaDescEs
                ));
            }

            itemsProp.ClearArray();
            for (int i = 0; i < faqList.Count; i++)
            {
                itemsProp.InsertArrayElementAtIndex(i);
                var el = itemsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("title_en").stringValue = faqList[i].tEn;
                el.FindPropertyRelative("description_en").stringValue = faqList[i].dEn;
                el.FindPropertyRelative("title_es").stringValue = faqList[i].tEs;
                el.FindPropertyRelative("description_es").stringValue = faqList[i].dEs;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            Debug.Log($"[HelpRestyler] Updated HelpScreenDatabase with {faqList.Count} FAQs.");
        }

        private static GameObject BuildHelpAccordionItemPrefab()
        {
            var go = new GameObject("HelpAccordionItem", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1616f, 52f);

            var cardImg = go.AddComponent<Image>();
            cardImg.sprite = cardNormalBg;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Color.white;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var itemComp = go.AddComponent<HelpAccordionItem>();

            // Header Row (Height 52px)
            var headerGo = CreateExplicitRect(rt, "Header_Row", 0f, 0f, 1f, 1f);
            var headerLe = headerGo.gameObject.AddComponent<LayoutElement>();
            headerLe.minHeight = 52f;
            headerLe.preferredHeight = 52f;
            headerLe.flexibleHeight = 0f;

            var headerBtn = headerGo.gameObject.AddComponent<Button>();
            headerBtn.transition = Selectable.Transition.None;
            var headerTransImg = headerGo.gameObject.AddComponent<Image>();
            headerTransImg.color = Color.clear;
            headerTransImg.raycastTarget = true;

            // Title Label
            var titleTmp = CreateExplicitText(headerGo, "Title_Text", "Question Title", fSemiBold, 16f, Color.white, TextAlignmentOptions.MidlineLeft);
            var ttRt = titleTmp.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0f, 0f);
            ttRt.anchorMax = new Vector2(1f, 1f);
            ttRt.offsetMin = new Vector2(24f, 0f);
            ttRt.offsetMax = new Vector2(-60f, 0f);

            // Toggle Button (32x32px on right)
            var toggleBox = CreateExplicitRect(headerGo, "Toggle_Button", 1f, 0.5f, 1f, 0.5f);
            toggleBox.pivot = new Vector2(1f, 0.5f);
            toggleBox.sizeDelta = new Vector2(32f, 32f);
            toggleBox.anchoredPosition = new Vector2(-16f, 0f);

            var toggleImg = toggleBox.gameObject.AddComponent<Image>();
            toggleImg.sprite = toggleNormalBg;
            toggleImg.type = Image.Type.Sliced;
            toggleImg.raycastTarget = false;

            var toggleIconTmp = CreateExplicitText(toggleBox, "Icon_Text", "+", fBold, 18f, Color.white, TextAlignmentOptions.Center);
            var tiRt = toggleIconTmp.GetComponent<RectTransform>();
            tiRt.anchorMin = Vector2.zero;
            tiRt.anchorMax = Vector2.one;
            tiRt.offsetMin = Vector2.zero;
            tiRt.offsetMax = Vector2.zero;

            // Answer Body Container
            var answerGo = CreateExplicitRect(rt, "Answer_Container", 0f, 0f, 1f, 1f);
            var answerVlg = answerGo.gameObject.AddComponent<VerticalLayoutGroup>();
            answerVlg.padding = new RectOffset(24, 60, 2, 18);
            answerVlg.spacing = 0f;
            answerVlg.childControlWidth = true;
            answerVlg.childControlHeight = true;
            answerVlg.childForceExpandWidth = true;
            answerVlg.childForceExpandHeight = false;

            var answerCsf = answerGo.gameObject.AddComponent<ContentSizeFitter>();
            answerCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            answerCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var answerTmp = CreateExplicitText(answerGo, "Answer_Text", "Answer description text goes here.", fMedium, 13.5f, Hex("#1E293B"), TextAlignmentOptions.TopLeft);
            answerTmp.textWrappingMode = TextWrappingModes.Normal;
            answerTmp.lineSpacing = 12f;

            var aCsf = answerTmp.gameObject.AddComponent<ContentSizeFitter>();
            aCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            aCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            answerGo.gameObject.SetActive(false);

            // Wire serialized properties on HelpAccordionItem
            var so = new SerializedObject(itemComp);
            so.FindProperty("headerButton").objectReferenceValue = headerBtn;
            so.FindProperty("cardBackgroundImage").objectReferenceValue = cardImg;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("toggleButtonImage").objectReferenceValue = toggleImg;
            so.FindProperty("toggleIconText").objectReferenceValue = toggleIconTmp;

            so.FindProperty("answerContainer").objectReferenceValue = answerGo;
            so.FindProperty("answerText").objectReferenceValue = answerTmp;

            so.FindProperty("normalCardSprite").objectReferenceValue = cardNormalBg;
            so.FindProperty("activeCardSprite").objectReferenceValue = cardActiveBg;
            so.FindProperty("normalToggleSprite").objectReferenceValue = toggleNormalBg;
            so.FindProperty("activeToggleSprite").objectReferenceValue = toggleActiveBg;

            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, HelpItemPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[HelpRestyler] Created item prefab at {HelpItemPrefabPath}");
            return prefab;
        }

        private static void BuildCleanHelpScreenPrefab(GameObject itemPrefab)
        {
            var db = AssetDatabase.LoadAssetAtPath<HelpScreenDatabase>(DatabasePath);

            var root = new GameObject("Help_Screen", typeof(RectTransform));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(1616f, 706f);
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var helpUI = root.AddComponent<HelpScreenUI>();

            // MainContent container (stretched)
            var mainContent = CreateExplicitRect(rootRt, "MainContent", 0f, 0f, 1f, 1f);
            mainContent.offsetMin = Vector2.zero;
            mainContent.offsetMax = Vector2.zero;

            // 1. Header Card (Height 120px)
            var headerCard = CreateExplicitRect(mainContent, "Header_Card", 0f, 1f, 1f, 1f);
            headerCard.pivot = new Vector2(0.5f, 1f);
            headerCard.sizeDelta = new Vector2(0f, 120f);
            headerCard.anchoredPosition = new Vector2(0f, 0f);

            var hCardImg = headerCard.gameObject.AddComponent<Image>();
            hCardImg.sprite = screenCardBg;
            hCardImg.type = Image.Type.Sliced;
            hCardImg.color = Color.white;

            // Watermark
            if (watermarkSprite != null)
            {
                var watermarkGo = CreateExplicitRect(headerCard, "Watermark", 0f, 0f, 0f, 1f);
                watermarkGo.pivot = new Vector2(0f, 0.5f);
                watermarkGo.sizeDelta = new Vector2(300f, 0f);
                watermarkGo.anchoredPosition = new Vector2(0f, 0f);
                var wImg = watermarkGo.gameObject.AddComponent<Image>();
                wImg.sprite = watermarkSprite;
                wImg.preserveAspect = true;
                wImg.raycastTarget = false;
                wImg.color = Color.white;
            }

            // Header Texts
            var titleText = CreateExplicitText(headerCard, "TitleText", "Help Center", fBold, 25f, Color.white, TextAlignmentOptions.Center);
            var tRt = titleText.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0.5f);
            tRt.anchorMax = new Vector2(1f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = new Vector2(0f, 12f);
            tRt.sizeDelta = new Vector2(0f, 30f);

            var subText = CreateExplicitText(headerCard, "SubtitleText", "Do you need help for something or do you have some questions", fRegular, 13f, Hex("#94A3B8"), TextAlignmentOptions.Center);
            var sRt = subText.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(0f, -14f);
            sRt.sizeDelta = new Vector2(0f, 24f);

            // 2. Scrollable Section
            var scrollGo = CreateExplicitRect(mainContent, "Scroll_View_Help", 0f, 0f, 1f, 1f);
            scrollGo.offsetMin = new Vector2(0f, 0f);
            scrollGo.offsetMax = new Vector2(0f, -135f);

            var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;

            var viewport = CreateExplicitRect(scrollGo, "Viewport", 0f, 0f, 1f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            var content = CreateExplicitRect(viewport, "Content", 0f, 1f, 1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var contentVlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 12f;
            contentVlg.padding = new RectOffset(0, 0, 2, 20);
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var contentCsf = content.gameObject.AddComponent<ContentSizeFitter>();
            contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = content;

            // Pre-populate items in the prefab for visual inspection / offline preview
            if (db != null && itemPrefab != null)
            {
                var accordionPrefabComp = itemPrefab.GetComponent<HelpAccordionItem>();
                for (int i = 0; i < db.Items.Count; i++)
                {
                    var data = db.Items[i];
                    var itemInstance = (GameObject)PrefabUtility.InstantiatePrefab(itemPrefab, content);
                    itemInstance.name = $"FAQ_{i}_{data.title_en}";
                    var accComp = itemInstance.GetComponent<HelpAccordionItem>();
                    bool startExpanded = (i == 0); // 1st item expanded in Figma mockup!

                    accComp.Setup(
                        i,
                        data.title_en,
                        data.description_en,
                        cardNormalBg,
                        cardActiveBg,
                        toggleNormalBg,
                        toggleActiveBg,
                        null,
                        startExpanded
                    );
                }
            }

            // Wire HelpScreenUI serialized fields
            var uiSo = new SerializedObject(helpUI);
            var cgProp = uiSo.FindProperty("<RootCanvasGroup>k__BackingField") ?? uiSo.FindProperty("RootCanvasGroup");
            if (cgProp != null) cgProp.objectReferenceValue = cg;

            var dbProp = uiSo.FindProperty("helpScreenDatabase");
            if (dbProp != null) dbProp.objectReferenceValue = db;

            var prefabProp = uiSo.FindProperty("helpAccordionItemPrefab");
            if (prefabProp != null && itemPrefab != null)
            {
                prefabProp.objectReferenceValue = itemPrefab.GetComponent<HelpAccordionItem>();
            }

            var qpProp = uiSo.FindProperty("questionParent");
            if (qpProp != null) qpProp.objectReferenceValue = content;

            var nCardProp = uiSo.FindProperty("normalCardSprite");
            if (nCardProp != null) nCardProp.objectReferenceValue = cardNormalBg;

            var aCardProp = uiSo.FindProperty("activeCardSprite");
            if (aCardProp != null) aCardProp.objectReferenceValue = cardActiveBg;

            var nTogProp = uiSo.FindProperty("normalToggleSprite");
            if (nTogProp != null) nTogProp.objectReferenceValue = toggleNormalBg;

            var aTogProp = uiSo.FindProperty("activeToggleSprite");
            if (aTogProp != null) aTogProp.objectReferenceValue = toggleActiveBg;

            uiSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HelpPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[HelpRestyler] Successfully saved {HelpPrefabPath}");
        }

        private static void EnsureInMiddleScreen(GameObject itemPrefab)
        {
            if (!File.Exists(MiddleScreenPath)) return;

            var middleScreen = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
            try
            {
                var innerScreen = middleScreen.transform.Find("InnerScreen");
                if (innerScreen != null)
                {
                    // Find any existing Help_Screen
                    Transform helpScreenTr = null;
                    for (int i = 0; i < innerScreen.childCount; i++)
                    {
                        var child = innerScreen.GetChild(i);
                        if (child.name.IndexOf("Help", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            helpScreenTr = child;
                            break;
                        }
                    }

                    if (helpScreenTr != null)
                    {
                        UnityEngine.Object.DestroyImmediate(helpScreenTr.gameObject);
                    }

                    var helpAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HelpPrefabPath);
                    if (helpAsset != null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(helpAsset, innerScreen);
                        instance.name = "Help_Screen";
                        instance.SetActive(false);

                        var iRt = (RectTransform)instance.transform;
                        iRt.anchorMin = Vector2.zero;
                        iRt.anchorMax = Vector2.one;
                        iRt.offsetMin = Vector2.zero;
                        iRt.offsetMax = Vector2.zero;

                        var cg = instance.GetComponent<CanvasGroup>() ?? instance.AddComponent<CanvasGroup>();
                        cg.alpha = 0f;
                        cg.interactable = false;
                        cg.blocksRaycasts = false;

                        Debug.Log("[HelpRestyler] Connected new Help_Screen in MiddleScreen_Scalable.prefab");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(middleScreen, MiddleScreenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleScreen);
            }
        }

        private static void RenderScreenshots()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "pd_renders"));
            Directory.CreateDirectory(outDir);

            // 1. Help Screen with 1st item expanded (exact match to Figma media_1790084239067.png)
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_help.png"), 1920, 1080, true, root =>
            {
                ActivateHelpScreen(root, 0);
            });

            // 2. Help Screen with all items collapsed
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_help_collapsed.png"), 1920, 1080, true, root =>
            {
                ActivateHelpScreen(root, -1);
            });

            Debug.Log($"[HelpRestyler] Rendered screenshots to {outDir}");
        }

        private static void ActivateHelpScreen(GameObject canvasRoot, int expandedIndex)
        {
            // Deactivate Dashboard Content
            var dash = PdUiKit.FindDeep(canvasRoot.transform, "Dashboard_Content");
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

            // Activate Help_Screen under InnerScreen
            var inner = PdUiKit.FindDeep(canvasRoot.transform, "InnerScreen");
            if (inner != null)
            {
                for (int i = 0; i < inner.childCount; i++)
                {
                    var child = inner.GetChild(i);
                    bool isHelp = child.name.IndexOf("Help", StringComparison.OrdinalIgnoreCase) >= 0;
                    child.gameObject.SetActive(isHelp);

                    foreach (var cg in child.GetComponentsInChildren<CanvasGroup>(true))
                    {
                        cg.alpha = isHelp ? 1f : 0f;
                        cg.interactable = isHelp;
                        cg.blocksRaycasts = isHelp;
                    }

                    if (isHelp)
                    {
                        var items = child.GetComponentsInChildren<HelpAccordionItem>(true);
                        for (int k = 0; k < items.Length; k++)
                        {
                            items[k].SetExpanded(k == expandedIndex);
                        }
                    }
                }
            }

            // Highlight "Help" option on sidebar and deselect all others
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CustomButtonUI")).FirstOrDefault(t => t != null);
            if (type != null)
            {
                var preview = type.GetMethod("PreviewVisualState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var idProp = type.GetProperty("CustomButtonID");

                foreach (var b in canvasRoot.GetComponentsInChildren(type, true))
                {
                    string id = (string)idProp?.GetValue(b);
                    bool isHelpBtn = string.Equals(id, "HelpScreen", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(id, "Help", StringComparison.OrdinalIgnoreCase);
                    preview?.Invoke(b, new object[] { isHelpBtn });
                }
            }
        }

        private static RectTransform CreateExplicitRect(Transform parent, string name, float axMin, float ayMin, float axMax, float ayMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text CreateExplicitText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
