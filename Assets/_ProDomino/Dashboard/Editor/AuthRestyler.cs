using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.SidebarRestyler;

namespace ProDomino.Dashboard.Editor
{
    // Login / Create Account / Forgot Password screens restyled to the Figma design
    // (Design System → Popups: 57:359, 57:358, 102:6949).
    // Every existing input field, toggle and button object is kept and only re-skinned and
    // repositioned, so the authentication logic (Credentials_AuthUI) keeps working.
    internal static class AuthRestyler
    {
        private const string AuthPath = "Assets/_ProDomino/Authentication/Prefabs/AuthUI.prefab";
        private const string GeneratedDir = "Assets/_ProDomino/Dashboard/Generated";
        private const string LogoPath = "Assets/_ProDomino/_UI/Client_Resources/Prodomino_Small_Logo.png";
        private const string ClosePath = "Assets/_ProDomino/_UI/Icons/Icons_Base_64/X_icon.png";
        private const string CheckPath = "Assets/_ProDomino/_UI/Icons/Icons_Base_64/Check_Icon.png";

        // Design colours
        private static readonly Color PanelTop = Hex("#27272C"), PanelBottom = Hex("#01010C");
        private static readonly Color PanelBorder = Hex("#37373D");
        private static readonly Color FieldFill = Hex("#212129"), FieldBorder = Hex("#2E2E38");
        private static readonly Color LabelColor = Hex("#E6E6E7");
        private static readonly Color Muted = Hex("#B0B0B4");
        private static readonly Color Placeholder = Hex("#55555C");
        private static readonly Color Accent = Hex("#FDC553");
        private static readonly Color OnPrimary = Hex("#01010C");
        private static readonly Color Danger = Hex("#FF6B6B");

        // Panel sizes from the design
        private const float PanelW = 880f;
        private const float SignInH = 860f, SignUpH = 1024f, RecoveryH = 704f;
        private const float Pad = 80f, ContentW = 720f;

        private static Sprite panelBg, fieldBg, socialBg, checkboxBg, closeBg, outlineBtn, badgeSprite, primaryBtn, lockSprite;
        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;

        [MenuItem("ProDomino/Dashboard/Restyle Login + Register + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            panelBg = MakePanelSprite("Auth_PanelBg", 48, 96, 12, PanelTop, PanelBottom, PanelBorder, 1);
            fieldBg = MakePanelSprite("Auth_FieldBg", 32, 32, 10, FieldFill, FieldFill, FieldBorder, 1);
            socialBg = MakePanelSprite("Auth_SocialBg", 32, 32, 10, FieldFill, FieldFill, new Color(0, 0, 0, 0), 0);
            checkboxBg = MakePanelSprite("Auth_CheckboxBg", 24, 24, 4, new Color(0.204f, 0.204f, 0.239f, 0.3f), new Color(0.204f, 0.204f, 0.239f, 0.3f), FieldBorder, 1);
            closeBg = MakePanelSprite("Auth_CloseBg", 24, 24, 8, Hex("#34343D"), Hex("#34343D"), Hex("#3E3E3E"), 1);
            outlineBtn = MakePanelSprite("Auth_OutlineBtn", 32, 32, 10, new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), Accent, 2);
            badgeSprite = MakePanelSprite("Auth_Badge", 80, 80, 32, Color.white, Color.white, new Color(0, 0, 0, 0), 0);
            // Design: left-to-right #FFA501 -> #FDC653 with a 2 px lighter rim.
            primaryBtn = MakePanelSprite("Auth_PrimaryBtn", 190, 56, 10, Hex("#FFA501"), Hex("#FDC653"), Hex("#FFD98A"), 2, vertical: false);
            lockSprite = MakeLockSprite("Auth_Lock");

            RestyleAuthPrefab();
            RevertStaleAuthOverrides();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RenderAuthScreens();
            Debug.Log("AUTH_RESTYLE_DONE");
        }

        // MiddleScreen_Scalable stores old layout-driven values (anchors/sizes/font sizes) for the
        // auth screens. They would override the new fixed layout, so they are reverted; script
        // fields and their event targets are left untouched.
        private static readonly Type[] LayoutTypes =
        {
            typeof(RectTransform), typeof(Image), typeof(TextMeshProUGUI), typeof(AspectRatioFitter),
            typeof(LayoutElement), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup), typeof(HorizontalLayoutGroup),
        };

        private static void RevertStaleAuthOverrides()
        {
            const string middlePath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
            var root = PrefabUtility.LoadPrefabContents(middlePath);
            try
            {
                var auth = FindDeep(root.transform, "AuthUI");
                if (auth == null) { Debug.LogWarning("AUTH: AuthUI instance not found in MiddleScreen."); return; }

                int reverted = RevertLayoutOverrides(auth);

                // Unity never reverts a prefab instance's own root placement, so it is set here:
                // the pop-up root has to fill the screen area for the card to centre in it.
                Stretch((RectTransform)auth);
                auth.localScale = Vector3.one;
                auth.localRotation = Quaternion.identity;

                PrefabUtility.SaveAsPrefabAsset(root, middlePath);
                Debug.Log($"AUTH: reverted stale layout overrides on {reverted} components in MiddleScreen_Scalable.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // The canvas and the scene record their own copies of those layout values (stored
            // against the MiddleScreen instance), which would win over the prefab.
            RevertInCanvasAndScene();
        }

        private static int RevertLayoutOverrides(Transform root)
        {
            int reverted = 0;
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || !LayoutTypes.Contains(comp.GetType())) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(comp)) continue;
                var it = new SerializedObject(comp).GetIterator();
                bool overridden = false;
                while (it.Next(true)) if (it.prefabOverride) { overridden = true; break; }
                if (!overridden) continue;
                PrefabUtility.RevertObjectOverride(comp, InteractionMode.AutomatedAction);
                reverted++;
            }
            return reverted;
        }

        private static void RevertInCanvasAndScene()
        {
            const string canvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
            const string scenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";

            var canvas = PrefabUtility.LoadPrefabContents(canvasPath);
            try
            {
                var auth = FindDeep(canvas.transform, "AuthUI");
                if (auth != null)
                {
                    int n = RevertLayoutOverrides(auth);
                    PrefabUtility.SaveAsPrefabAsset(canvas, canvasPath);
                    Debug.Log($"AUTH: reverted {n} auth layout overrides in ProDomino_MainCanvas.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(canvas); }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var sceneAuth = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "AuthUI");
            if (sceneAuth != null)
            {
                int n = RevertLayoutOverrides(sceneAuth);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log($"AUTH: reverted {n} auth layout overrides in MainSceneDomDemo.");
            }
        }

        [MenuItem("ProDomino/Dashboard/Render Login + Register To PNG")]
        public static void RenderAuthScreens()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pd_renders");
            System.IO.Directory.CreateDirectory(outDir);

            foreach (var screen in new[] { "SignIn_Container", "SignUp_Container", "Recovery_Container" })
            {
                var file = System.IO.Path.Combine(outDir, $"auth_{screen.Replace("_Container", "").ToLower()}.png");
                RenderStandalone(screen, file);
            }
        }

        // The pop-up sits on its own nested Canvas, which an off-screen render of the whole game
        // canvas does not draw. Rendering the prefab on its own (its Canvas becomes the root one)
        // shows exactly what the screen looks like.
        private static void RenderStandalone(string screenName, string outPath)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

            var cam = new GameObject("RenderCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0E0E13");
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AuthPath));
            inst.SetActive(true);
            var canvas = inst.GetComponent<Canvas>() ?? inst.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = false;
            var scaler = inst.GetComponent<CanvasScaler>() ?? inst.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (inst.TryGetComponent<CanvasGroup>(out var cg)) { cg.alpha = 1f; cg.blocksRaycasts = true; }
            var container = inst.transform.Find("Auth_Container");
            foreach (var name in new[] { "SignIn_Container", "SignUp_Container", "Recovery_Container" })
            {
                var t = container ? container.Find(name) : null;
                if (t && t.TryGetComponent<CanvasGroup>(out var scg)) scg.alpha = name == screenName ? 1f : 0f;
            }

            // Buttons start non-interactable (until the form is valid) and would render with their
            // greyed-out tint; show them enabled so the preview reflects the real colours.
            foreach (var s in inst.GetComponentsInChildren<Selectable>(true))
            {
                s.interactable = true;
                if (s.targetGraphic && s.targetGraphic.canvasRenderer) s.targetGraphic.canvasRenderer.SetColor(Color.white);
            }

            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();
            }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log($"RENDERED: {outPath}");
        }

        // Makes the auth pop-up and one of its screens visible for the render.
        private static void ShowAuthScreen(GameObject canvasRoot, string screenName)
        {
            var auth = FindDeep(canvasRoot.transform, "AuthUI");
            if (auth == null) { Debug.LogWarning("RENDER: AuthUI not found in canvas."); return; }
            auth.gameObject.SetActive(true);
            Stretch((RectTransform)auth);   // instance root placement is not part of the prefab
            if (auth.TryGetComponent<CanvasGroup>(out var acg)) { acg.alpha = 1f; acg.blocksRaycasts = true; }

            var container = auth.Find("Auth_Container");
            if (container && container.TryGetComponent<CanvasGroup>(out var ccg)) ccg.alpha = 1f;

            foreach (var name in new[] { "SignIn_Container", "SignUp_Container", "Recovery_Container" })
            {
                var t = container ? container.Find(name) : null;
                if (t && t.TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = name == screenName ? 1f : 0f;
            }

            for (var p = auth; p != null; p = p.parent)
            {
                var rt = p as RectTransform;
                var cg = p.GetComponent<CanvasGroup>();
                Debug.Log($"AUTHDIAG: {p.name} active={p.gameObject.activeInHierarchy} rect={(rt ? rt.rect.size.ToString() : "-")} " +
                          $"pos={(rt ? rt.position.ToString() : "-")} alpha={(cg ? cg.alpha.ToString("0.00") : "-")}");
                if (p.name == "ProDomino_MainCanvas") break;
            }
            var signIn = container ? container.Find(screenName) : null;
            if (signIn is RectTransform srt)
            {
                var scg = srt.GetComponent<CanvasGroup>();
                Debug.Log($"AUTHDIAG: {screenName} rect={srt.rect.size} pos={srt.position} children={srt.childCount} alpha={(scg ? scg.alpha : -1f)}");
                foreach (var g in srt.GetComponentsInChildren<Graphic>(true).Take(4))
                    Debug.Log($"AUTHDIAG:   graphic {g.name} canvas={(g.canvas ? g.canvas.name : "NULL")} cull={(g.canvasRenderer ? g.canvasRenderer.cull.ToString() : "-")} " +
                              $"crAlpha={(g.canvasRenderer ? g.canvasRenderer.GetAlpha().ToString("0.00") : "-")} materials={(g.canvasRenderer ? g.canvasRenderer.materialCount : -1)} " +
                              $"enabled={g.enabled} active={g.gameObject.activeInHierarchy} rect={((RectTransform)g.transform).rect.size}");
            }
            // The pop-up has its own nested Canvas, which this off-screen render doesn't draw.
            // Removing it in the throwaway render copy makes it part of the main canvas instead.
            foreach (var raycaster in auth.GetComponents<GraphicRaycaster>())
                UnityEngine.Object.DestroyImmediate(raycaster);
            foreach (var nested in auth.GetComponents<Canvas>())
                UnityEngine.Object.DestroyImmediate(nested);
            auth.SetAsLastSibling();

            // Graphics under a CanvasGroup that started at alpha 0 stay culled until they are
            // rebuilt, which the editor doesn't do on its own for this off-screen render.
            foreach (var g in auth.GetComponentsInChildren<Graphic>(true))
            {
                // toggling re-registers the graphic with the canvas it now belongs to
                if (g.enabled) { g.enabled = false; g.enabled = true; }
                if (g.canvasRenderer) g.canvasRenderer.cull = false;
                g.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void RestyleAuthPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(AuthPath);
            try
            {
                // The prefab root is a zero-sized point (the old layout groups sized the children),
                // so it has to fill the screen area before anything can be centred in it.
                Stretch((RectTransform)root.transform);

                var container = Need(root.transform, "Auth_Container");
                // The popup is a fixed 880-wide card centred on screen.
                var crt = (RectTransform)container;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = new Vector2(PanelW, SignUpH);

                var dim = root.transform.Find("Panel")?.GetComponent<Image>();
                if (dim) dim.color = new Color(0f, 0f, 0f, 0.72f);

                BuildSignIn(Need(container, "SignIn_Container"));
                BuildSignUp(Need(container, "SignUp_Container"));
                BuildRecovery(Need(container, "Recovery_Container"));

                PrefabUtility.SaveAsPrefabAsset(root, AuthPath);
                Debug.Log("AUTH: AuthUI prefab saved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ screens

        private static void BuildSignIn(Transform screen)
        {
            var card = Card(screen, SignInH);
            Header(card, "Welcome Back!", "Login to continue playing ProDomino with friends & random opponents.");
            Close(card, FindDeep(screen, "SignIn_Close_Button"));

            Field(card, FindDeep(screen, "SignIn_Username_InputField (TMP)"), "Email", "Enter Email ID", 308f);
            Field(card, FindDeep(screen, "SignIn_Password_InputField (TMP)"), "Password", "Enter Password", 408f);
            Feedback(card, FindDeep(screen, "SignIn_Feedback"), 492f);

            var remember = FindDeep(screen, "SignIn_RememberMe_Toggle");
            CheckRow(card, remember, "Remember Me", 508f, 260f, Color.white);
            var forgot = FindDeep(screen, "SignIn_ForgotPassword_Button");
            if (forgot)
            {
                Reparent(forgot, card);
                TL((RectTransform)forgot, Pad + ContentW - 240f, 508f, 240f, 24f);
                Transparent(forgot);
                var t = forgot.GetComponentInChildren<TextMeshProUGUI>(true);
                Text(t, "Forgot Password?", fMedium, 16f, Accent, TextAlignmentOptions.MidlineRight);
            }

            PrimaryButton(card, FindDeep(screen, "SignIn_Button"), "Log In", 572f);
            Social(card, FindDeep(screen, "SignIn_Google_Button"), "Login with Google", Pad, 660f);
            Social(card, FindDeep(screen, "SignIn_Facebook_Button"), "Login with Facebook", Pad + 370f, 660f);
            LinkRow(card, FindDeep(screen, "SignUp_Mail_Button"), "Don't have an account? ", "Create an Account", 740f);

            // Not part of the new design.
            Hide(screen, "Or_Container", "SignIn_Description_2_Text", "SignIn_Apple_Button");
        }

        private static void BuildSignUp(Transform screen)
        {
            var card = Card(screen, SignUpH);
            Header(card, "Create your Account", "Start playing ProDomino with friends & random opponents.");
            Close(card, FindDeep(screen, "SignIn_Close_Button"));

            Field(card, FindDeep(screen, "SignUp_Username_InputField (TMP)"), "Username", "Enter Username", 308f);
            Feedback(card, FindDeep(screen, "SignUp_Username_Feedback"), 392f);
            Field(card, FindDeep(screen, "SignUp_Email_InputField (TMP)"), "Email", "Enter Email ID", 408f);
            Feedback(card, FindDeep(screen, "SignUp_Email_Feedback"), 492f);
            Field(card, FindDeep(screen, "SignUp_Password_InputField (TMP)"), "Password", "Enter Password", 508f);
            Feedback(card, FindDeep(screen, "SignIn_Feedback"), 592f);
            Field(card, FindDeep(screen, "SignUp_RepeatPassword_InputField (TMP)"), "Confirm Password", "Enter Password", 608f);
            Feedback(card, FindDeep(screen, "SignUp_RepeatPassword_Feedback"), 692f);

            CheckRow(card, FindDeep(screen, "SignUp_TermAndConditions_Toggle"), null, 708f, ContentW, Muted);
            CheckRow(card, FindDeep(screen, "SignUp_DataTreatment_Toggle"), null, 752f, ContentW, Muted);

            PrimaryButton(card, FindDeep(screen, "SignUp_Button"), "Create an Account", 816f);
            LinkRow(card, FindDeep(screen, "SignUp_BackContainer"), "Already have an account? ", "Login", 904f);
        }

        private static void BuildRecovery(Transform screen)
        {
            // Laid out like the Figma "forgot password" card. Only the widgets this flow uses are
            // placed: the older verification-code block is already inactive and stays untouched.
            var card = Card(screen, RecoveryH);
            Close(card, FindDeep(screen, "SignIn_Close_Button"));

            var badge = GetOrCreate(card, "Recovery_Badge", () => MakeImage(card, "Recovery_Badge", badgeSprite, Color.white, Image.Type.Sliced).transform);
            TL((RectTransform)badge, 385f, 100f, 110f, 110f);
            var badgeImg = GetOrAdd<Image>(badge);
            badgeImg.sprite = badgeSprite; badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(0.996f, 0.580f, 0.580f, 0.2f);   // #FE9494 at 20%
            badgeImg.pixelsPerUnitMultiplier = 1f;
            badgeImg.raycastTarget = false;

            var lockIcon = GetOrCreate(badge, "Icon", () => MakeImage(badge, "Icon", lockSprite, Color.white, Image.Type.Simple).transform);
            var lrt = (RectTransform)lockIcon;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(50f, 50f);
            var lockImg = GetOrAdd<Image>(lockIcon);
            lockImg.sprite = lockSprite; lockImg.type = Image.Type.Simple;
            lockImg.color = Color.white; lockImg.preserveAspect = true; lockImg.raycastTarget = false;

            Header(card, "Forgot Password?", "Please enter your email address to receive a verification code.",
                showLogo: false, titleY: 222f, subtitleY: 270f, subtitleW: 490f);

            Field(card, FindDeep(screen, "Recovery_Email_InputField (TMP)"), "Email", "Enter Email ID", 330f);
            PrimaryButton(card, FindDeep(screen, "Recovery_Button"), "Send Reset Link", 450f);

            // "Back to Login": the outlined secondary button from the design. Looked up through its
            // container, because the inactive confirm step holds a button of the same name.
            var backContainer = FindDeep(screen, "Recovery_BackContainer");
            var back = FindDeep(screen, "Recovery_BackToLogin_Button")            // already moved
                       ?? (backContainer ? backContainer.Find("Recovery_Back_Button") : null);
            if (back)
            {
                back.name = "Recovery_BackToLogin_Button";   // the confirm step has a same-named one
                Reparent(back, card);
                TL((RectTransform)back, Pad, 548f, ContentW, 57f);
                var backImg = GetOrAdd<Image>(back);
                backImg.sprite = outlineBtn; backImg.type = Image.Type.Sliced;
                backImg.color = Color.white; backImg.pixelsPerUnitMultiplier = 1f;
                NeutralTint(back, backImg);
                var backLabel = back.GetComponentInChildren<TextMeshProUGUI>(true);
                if (backLabel)
                {
                    Stretch((RectTransform)backLabel.transform);
                    Text(backLabel, "Back to Login", fMedium, 24f, Accent, TextAlignmentOptions.Center);
                }
            }

            // Replaced by the subtitle above.
            var oldDescription = FindDeep(screen, "Recovery_Description_1_Text");
            if (oldDescription) oldDescription.gameObject.SetActive(false);
        }

        // Measures the built screens and compares them with the Figma rects (card-local, top-left
        // origin). Logs PASS/FAIL per element so differences can't go unnoticed.
        [MenuItem("ProDomino/Dashboard/Verify Login + Register Against Figma")]
        public static void VerifyAgainstDesign()
        {
            var expected = new (string screen, string name, float x, float y, float w, float h)[]
            {
                // Login (Figma 57:359, card 880x860)
                ("SignIn_Container", "Auth_Logo",                          280, 100, 320,  40),
                ("SignIn_Container", "SignIn_Header_Text",                  80, 180, 720,  44),
                ("SignIn_Container", "SignIn_Description_Text",            266, 232, 348,  44),
                ("SignIn_Container", "SignIn_Username_InputField (TMP)",    80, 340, 720,  48),
                ("SignIn_Container", "SignIn_Password_InputField (TMP)",    80, 440, 720,  48),
                ("SignIn_Container", "SignIn_RememberMe_Toggle",            80, 508, 260,  24),
                ("SignIn_Container", "SignIn_Button",                       80, 572, 720,  56),
                ("SignIn_Container", "SignIn_Google_Button",                80, 660, 350,  48),
                ("SignIn_Container", "SignIn_Facebook_Button",             450, 660, 350,  48),
                ("SignIn_Container", "SignUp_Mail_Button",                  80, 740, 720,  24),
                ("SignIn_Container", "SignIn_Close_Button",                810,  20,  50,  50),
                // Create Account (Figma 57:358, card 880x1024)
                ("SignUp_Container", "Auth_Logo",                          280, 100, 320,  40),
                ("SignUp_Container", "SignUp_Header_Text",                  80, 180, 720,  44),
                ("SignUp_Container", "SignUp_Description_Text",            266, 232, 348,  44),
                ("SignUp_Container", "SignUp_Username_InputField (TMP)",    80, 340, 720,  48),
                ("SignUp_Container", "SignUp_Email_InputField (TMP)",       80, 440, 720,  48),
                ("SignUp_Container", "SignUp_Password_InputField (TMP)",    80, 540, 720,  48),
                ("SignUp_Container", "SignUp_RepeatPassword_InputField (TMP)", 80, 640, 720, 48),
                ("SignUp_Container", "SignUp_TermAndConditions_Toggle",     80, 708, 720,  24),
                ("SignUp_Container", "SignUp_DataTreatment_Toggle",         80, 752, 720,  24),
                ("SignUp_Container", "SignUp_Button",                       80, 816, 720,  56),
                ("SignUp_Container", "SignUp_BackContainer",                80, 904, 720,  24),
                // Forgot Password (Figma 57:360, card 880x704)
                ("Recovery_Container", "Recovery_Badge",                   385, 100, 110, 110),
                ("Recovery_Container", "Recovery_Header_Text",              80, 222, 720,  44),
                ("Recovery_Container", "Recovery_Description_Text",        195, 270, 490,  44),
                ("Recovery_Container", "Recovery_Email_InputField (TMP)",   80, 362, 720,  48),
                ("Recovery_Container", "Recovery_Button",                   80, 450, 720,  57),
                ("Recovery_Container", "Recovery_BackToLogin_Button",       80, 548, 720,  57),
                ("Recovery_Container", "SignIn_Close_Button",              810,  20,  50,  50),
            };

            var root = PrefabUtility.LoadPrefabContents(AuthPath);
            try
            {
                int pass = 0, fail = 0;
                foreach (var e in expected)
                {
                    var card = FindDeep(root.transform, e.screen);
                    var t = card ? FindDeep(card, e.name) as RectTransform : null;
                    if (t == null) { Debug.Log($"VERIFY: MISSING {e.screen}/{e.name}"); fail++; continue; }

                    var cardRt = (RectTransform)card;
                    var wc = new Vector3[4];
                    t.GetWorldCorners(wc);
                    var topLeft = cardRt.InverseTransformPoint(wc[1]);
                    var bottomRight = cardRt.InverseTransformPoint(wc[3]);
                    float x = topLeft.x + cardRt.rect.width / 2f;
                    float y = cardRt.rect.height / 2f - topLeft.y;
                    float w = bottomRight.x - topLeft.x;
                    float h = topLeft.y - bottomRight.y;

                    bool ok = Mathf.Abs(x - e.x) <= 1f && Mathf.Abs(y - e.y) <= 1f
                              && Mathf.Abs(w - e.w) <= 1f && Mathf.Abs(h - e.h) <= 1f;
                    if (ok) pass++; else fail++;
                    Debug.Log($"VERIFY: {(ok ? "PASS" : "FAIL")} {e.screen}/{e.name} " +
                              $"got=({x:0},{y:0},{w:0},{h:0}) want=({e.x:0},{e.y:0},{e.w:0},{e.h:0})");
                }
                Debug.Log($"VERIFY_DONE pass={pass} fail={fail}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Image signInBg(Transform auth, string screenName)
        {
            var screen = FindDeep(auth, screenName);
            return screen ? screen.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name.EndsWith("_Background")) : null;
        }

        // ------------------------------------------------------------------ pieces

        // Screen card: fixed size, gradient background, no layout groups driving children.
        private static Transform Card(Transform screen, float height)
        {
            KillLayout(screen);
            var rt = (RectTransform)screen;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PanelW, height);

            var bg = screen.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.transform.parent == screen && i.name.EndsWith("_Background"));
            if (bg)
            {
                Stretch((RectTransform)bg.transform);
                bg.sprite = panelBg; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f;
                bg.transform.SetAsFirstSibling();
            }
            return screen;
        }

        private static void Header(Transform card, string title, string subtitle, bool showLogo = true, float titleY = 180f, float subtitleY = 232f, float subtitleW = 348f)
        {
            var logo = GetOrCreate(card, "Auth_Logo", () => MakeImage(card, "Auth_Logo", null, Color.white, Image.Type.Simple).transform);
            var logoImg = GetOrAdd<Image>(logo);
            logoImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            logoImg.color = Color.white;
            logoImg.type = Image.Type.Simple;
            if (!logoImg.sprite) Debug.LogWarning($"AUTH: logo sprite not found at {LogoPath}");
            var lrt = (RectTransform)logo;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -100f);
            lrt.sizeDelta = new Vector2(320f, 40f);
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            logo.gameObject.SetActive(showLogo);

            var titleT = card.GetComponentsInChildren<TextMeshProUGUI>(true)
                             .FirstOrDefault(t => t && t.name.EndsWith("_Header_Text"))?.transform
                         ?? (FindDeep(card, "Title_Container") is Transform tc && tc.childCount > 0 ? tc.GetChild(0) : null);
            if (titleT)
            {
                Reparent(titleT, card);
                TLCentered((RectTransform)titleT, titleY, ContentW, 44f);
                Text(titleT.GetComponent<TextMeshProUGUI>(), title, fSemiBold, 36f, Color.white, TextAlignmentOptions.Center);
            }

            var sub = card.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t && t.name.EndsWith("_Description_Text"));
            if (!sub)
                sub = MakeText(card, $"{card.name.Replace("_Container", "")}_Description_Text", subtitle, fRegular, 16f, Muted);
            Reparent(sub.transform, card);
            TLCentered((RectTransform)sub.transform, subtitleY, subtitleW, 44f);
            Text(sub, subtitle, fRegular, 16f, Muted, TextAlignmentOptions.Top);
            sub.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void Close(Transform card, Transform button)
        {
            if (!button) return;
            Reparent(button, card);
            var rt = (RectTransform)button;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(50f, 50f);

            var img = button.GetComponent<Image>();
            var xSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ClosePath);
            if (img) { img.sprite = closeBg; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f; }

            var icon = GetOrCreate(button, "Icon", () => MakeImage(button, "Icon", xSprite, Color.white, Image.Type.Simple).transform);
            var irt = (RectTransform)icon;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(20f, 20f);
            var iconImg = icon.GetComponent<Image>();
            if (xSprite) iconImg.sprite = xSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        // Label above a 48 px input field, both spanning the content column.
        private static void Field(Transform card, Transform field, string label, string placeholder, float y)
        {
            if (!field) return;
            Reparent(field, card);
            TL((RectTransform)field, Pad, y + 32f, ContentW, 48f);

            var bg = field.GetComponent<Image>();
            if (bg) { bg.sprite = fieldBg; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f; }

            var labelT = GetOrCreate(card, $"{field.name}_Label", () => MakeText(card, $"{field.name}_Label", label, fMedium, 16f, LabelColor).transform);
            TL((RectTransform)labelT, Pad, y, ContentW, 20f);
            Text(labelT.GetComponent<TextMeshProUGUI>(), label, fMedium, 16f, LabelColor, TextAlignmentOptions.MidlineLeft);

            var area = field.Find("Text Area");
            if (area)
            {
                var art = (RectTransform)area;
                art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
                art.offsetMin = new Vector2(20f, 8f); art.offsetMax = new Vector2(-20f, -8f);
            }
            if (field.TryGetComponent<TMP_InputField>(out var input))
            {
                if (input.placeholder is TextMeshProUGUI ph)
                {
                    Stretch((RectTransform)ph.transform);
                    Text(ph, placeholder, fRegular, 16f, Placeholder, TextAlignmentOptions.MidlineLeft);
                }
                if (input.textComponent is TextMeshProUGUI txt)
                {
                    Stretch((RectTransform)txt.transform);
                    Text(txt, txt.text, fRegular, 16f, Color.white, TextAlignmentOptions.MidlineLeft);
                }
                input.caretColor = Accent;
                input.customCaretColor = true;
                input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
            }
        }

        private static void Feedback(Transform card, Transform feedback, float y)
        {
            if (!feedback) return;
            Reparent(feedback, card);
            TL((RectTransform)feedback, Pad, y, ContentW, 16f);
            // Cleared here: validation fills these in at runtime, the prefab held placeholder text.
            var t = feedback.GetComponent<TextMeshProUGUI>();
            if (t) Text(t, string.Empty, fRegular, 13f, Danger, TextAlignmentOptions.MidlineLeft);
        }

        // 24x24 check box plus its label.
        private static void CheckRow(Transform card, Transform toggle, string label, float y, float width, Color labelColor)
        {
            if (!toggle) return;
            Reparent(toggle, card);
            TL((RectTransform)toggle, Pad, y, width, 24f);

            var box = toggle.Find("Background");
            if (box)
            {
                var brt = (RectTransform)box;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = new Vector2(24f, 24f);
                var fit = box.GetComponent<AspectRatioFitter>();
                if (fit) fit.enabled = false;
                var bimg = box.GetComponent<Image>();
                if (bimg) { bimg.sprite = checkboxBg; bimg.type = Image.Type.Sliced; bimg.color = Color.white; bimg.pixelsPerUnitMultiplier = 1f; }

                var check = box.Find("Checkmark");
                if (check)
                {
                    var crt = (RectTransform)check;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(16f, 16f);
                    var cimg = check.GetComponent<Image>();
                    var checkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CheckPath);
                    if (cimg) { if (checkSprite) cimg.sprite = checkSprite; cimg.color = Accent; cimg.preserveAspect = true; cimg.type = Image.Type.Simple; }
                }
            }

            var text = toggle.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (text)
            {
                var trt = (RectTransform)text.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.pivot = new Vector2(0f, 0.5f);
                trt.offsetMin = new Vector2(36f, 0f); trt.offsetMax = Vector2.zero;
                Text(text, label ?? text.text, label != null ? fMedium : fRegular, 16f, labelColor, TextAlignmentOptions.MidlineLeft);
            }
        }

        private static void PrimaryButton(Transform card, Transform button, string label, float y)
        {
            if (!button) return;
            Reparent(button, card);
            TL((RectTransform)button, Pad, y, ContentW, 56f);
            var img = GetOrAdd<Image>(button);
            img.sprite = primaryBtn; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f;
            NeutralTint(button, img);
            var shadow = GetOrAdd<Shadow>(button);      // design drop shadow under the button
            shadow.effectColor = new Color(0.63f, 0.35f, 0f, 1f);
            shadow.effectDistance = new Vector2(0f, -2f);
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text)
            {
                Stretch((RectTransform)text.transform);
                Text(text, label, fBold, 24f, OnPrimary, TextAlignmentOptions.Center);
            }
        }

        // Google / Facebook button: icon + label centred as a group.
        private static void Social(Transform card, Transform button, string label, float x, float y)
        {
            if (!button) return;
            Reparent(button, card);
            TL((RectTransform)button, x, y, 350f, 48f);
            var img = GetOrAdd<Image>(button);
            img.sprite = socialBg; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f;
            NeutralTint(button, img);

            var hlg = GetOrAdd<HorizontalLayoutGroup>(button);
            hlg.enabled = true;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var icon = button.Find("Icon");
            if (icon)
            {
                var fit = icon.GetComponent<AspectRatioFitter>();
                if (fit) fit.enabled = false;
                ((RectTransform)icon).sizeDelta = new Vector2(20f, 20f);
                var le = GetOrAdd<LayoutElement>(icon);
                le.minWidth = 20f; le.preferredWidth = 20f; le.minHeight = 20f; le.preferredHeight = 20f;
                var iimg = icon.GetComponent<Image>();
                if (iimg) { iimg.color = Color.white; iimg.preserveAspect = true; }
            }
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text)
            {
                Text(text, label, fRegular, 16f, Muted, TextAlignmentOptions.Midline);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var tle = GetOrAdd<LayoutElement>(text.transform);
                tle.preferredWidth = -1f;   // let the label report its own width
                tle.minWidth = -1f;
                tle.preferredHeight = 20f;
            }
        }

        // Footer line: "Don't have an account? Create an Account" (the CTA part is highlighted).
        private static void LinkRow(Transform card, Transform button, string prefix, string cta, float y)
        {
            if (!button) return;
            Reparent(button, card);
            TL((RectTransform)button, Pad, y, ContentW, 24f);
            Transparent(button);
            foreach (var child in button.GetComponentsInChildren<Image>(true))
                if (child.transform != button) child.enabled = false;

            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!text)
                text = MakeText(button, "Label", "", fMedium, 16f, Muted);
            Stretch((RectTransform)text.transform);
            text.richText = true;
            Text(text, $"{prefix}<color=#FDC553>{cta}</color>", fMedium, 16f, Muted, TextAlignmentOptions.Center);
            if (!button.GetComponent<Button>()) button.gameObject.AddComponent<Button>();
        }

        // ------------------------------------------------------------------ helpers

        // The old buttons tint their graphic with dark "normal" colours, which would hide the new
        // sprites; keep white as the base and only darken on hover/press.
        private static void NeutralTint(Transform button, Graphic target)
        {
            if (!button.TryGetComponent<Selectable>(out var selectable)) return;
            selectable.targetGraphic = target;
            selectable.transition = Selectable.Transition.ColorTint;
            var c = selectable.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.92f, 0.92f, 0.95f, 1f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            c.fadeDuration = 0.08f;
            selectable.colors = c;
        }

        private static void KillLayout(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (c is VerticalLayoutGroup or HorizontalLayoutGroup or ContentSizeFitter or AspectRatioFitter)
                    ((Behaviour)c).enabled = false;
            }
        }

        private static void Reparent(Transform t, Transform parent)
        {
            if (t.parent != parent) t.SetParent(parent, false);
            t.localScale = Vector3.one;
        }

        private static void TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void TLCentered(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Text(TextMeshProUGUI t, string value, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            if (!t) return;
            if (value != null) t.text = value;
            t.font = font; t.fontSharedMaterial = font.material;
            t.fontSize = size; t.enableAutoSizing = false;
            t.fontStyle = FontStyles.Normal;
            t.color = color;
            t.alignment = align;
            t.margin = Vector4.zero;
            t.lineSpacing = 0f;
        }

        private static void Transparent(Transform t)
        {
            var img = GetOrAdd<Image>(t);
            img.sprite = null; img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = true;
        }

        private static void Hide(Transform screen, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeep(screen, n);
                if (t) t.gameObject.SetActive(false);
            }
        }

        private static Transform GetOrCreate(Transform parent, string name, Func<Transform> create)
        {
            var existing = parent.Find(name);
            return existing ? existing : create();
        }

        private static T GetOrAdd<T>(Transform t) where T : Component =>
            t.TryGetComponent<T>(out var c) ? c : t.gameObject.AddComponent<T>();

        // Rounded sprite with an optional vertical gradient and a border drawn inside the edge.
        private static Sprite MakePanelSprite(string name, int w, int h, int radius, Color top, Color bottom, Color border, float borderWidth, bool vertical = true)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // texture y is bottom-up; a vertical design gradient runs top -> bottom
                float t = vertical
                    ? (h > 1 ? 1f - (float)y / (h - 1) : 0f)
                    : (w > 1 ? (float)x / (w - 1) : 0f);
                var fill = Color.Lerp(top, bottom, t);

                // Distance from the shape's edge, positive inside (rounded-rect SDF).
                float sd = radius - Distance(x + 0.5f, y + 0.5f, w, h, radius);
                float inside = Mathf.Clamp01(sd + 0.5f);
                var c = fill;
                if (borderWidth > 0f && border.a > 0f)
                {
                    float borderMask = Mathf.Clamp01(borderWidth + 0.5f - sd) * border.a;
                    c = Color.Lerp(fill, border, borderMask);
                    c.a = Mathf.Max(fill.a, borderMask);
                }
                c.a *= inside;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            WritePng(path, tex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = 100;
            int b = radius + 2;
            imp.spriteBorder = new Vector4(b, b, b, b);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // The padlock from the design: shackle arc over a rounded body, red vertical gradient.
        // Drawn here because the Figma export of that icon is not available offline.
        private static Sprite MakeLockSprite(string name, int size = 200)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float scale = size / 50f;                    // the icon is 50 x 50 design units
            Color top = Hex("#FF0000"), bottom = Hex("#FF6E6E");
            const float ringCx = 25f, ringCy = 13.5f, ringOuter = 13.5f, ringInner = 8.5f;
            const float bodyX = 3f, bodyY = 16f, bodyW = 44f, bodyH = 34f, bodyR = 6f;

            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / scale;
                float y = 50f - (py + 0.5f) / scale;     // texture is bottom-up, the design top-down

                // Shackle: the part of the ring above the body (its legs tuck in behind it).
                float ring = -99f;
                if (y <= bodyY + 2f)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(ringCx, ringCy));
                    ring = Mathf.Min(ringOuter - d, d - ringInner);
                }

                // Body: rounded rectangle, positive inside.
                float body = bodyR - Distance(x - bodyX, y - bodyY, (int)bodyW, (int)bodyH, bodyR);

                float sd = Mathf.Max(ring, body);
                var c = Color.Lerp(top, bottom, Mathf.Clamp01(y / 50f));
                c.a = Mathf.Clamp01(sd * scale + 0.5f);
                tex.SetPixel(px, py, c);
            }
            tex.Apply();
            WritePng(path, tex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = 100;
            imp.spriteBorder = Vector4.zero;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Distance from the rounded-rect corner centre (0 in the straight parts).
        private static float Distance(float px, float py, int w, int h, float r)
        {
            float cx = Mathf.Clamp(px, r, w - r);
            float cy = Mathf.Clamp(py, r, h - r);
            return Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
        }
    }
}
