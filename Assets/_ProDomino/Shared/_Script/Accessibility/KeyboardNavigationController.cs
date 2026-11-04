using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ProDomino.Shared.Accessibility
{
    /// <summary>
    /// Global keyboard accessibility for uGUI. uGUI has no built-in Tab navigation, so this adds:
    /// <list type="bullet">
    /// <item>Tab / Shift+Tab: move focus to the next / previous visible, interactable element, in reading order
    /// (grouped by hierarchy, then top-to-bottom, left-to-right).</item>
    /// <item>Space: activates the focused button/toggle/dropdown (Enter is already sent as Submit by the
    /// InputSystemUIInputModule).</item>
    /// <item>Enter/Space never reach an element that is hidden or covered by a popup.</item>
    /// <item>A focus ring around the focused element, shown only while the keyboard is being used, so mouse and
    /// touch visuals stay untouched.</item>
    /// </list>
    /// Elements are skipped when inactive, not interactable, inside a hidden CanvasGroup (alpha 0 / not blocking
    /// raycasts / not interactable), inside a disabled Canvas, or covered by another UI element (e.g. a modal popup).
    /// Add <see cref="KeyboardNavigationIgnore"/> to exclude an element (and its children) from Tab order.
    /// Created automatically at startup, no scene setup needed.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Run before the EventSystem so Enter/Space checks happen before Submit is sent.
    [DisallowMultipleComponent]
    public sealed class KeyboardNavigationController : MonoBehaviour
    {
        public static KeyboardNavigationController Instance { get; private set; }

        /// <summary>True while the user is navigating with the keyboard (cleared by any pointer/touch press).</summary>
        public static bool IsKeyboardMode { get; private set; }

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Vector2[] SamplePoints = { new(.5f, .5f), new(.25f, .25f), new(.75f, .25f), new(.25f, .75f), new(.75f, .75f) };

        private readonly List<RaycastResult> raycastResults = new();
        private readonly List<Item> items = new();

        private KeyboardFocusHighlight highlight;

        private Selectable pendingTarget;
        private bool pendingIsSubmit;
        private int pendingFrame = -1;

        #region Bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            IsKeyboardMode = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(KeyboardNavigationController));
            DontDestroyOnLoad(go);
            go.AddComponent<KeyboardNavigationController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            highlight = KeyboardFocusHighlight.Create(transform);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Moves focus to <paramref name="target"/> on the next frame. Safe to call from TMP_InputField.onSubmit
        /// (the input field finishes its own Enter handling first).
        /// </summary>
        public static void FocusNextFrame(Selectable target) => Instance?.Schedule(target, false);

        /// <summary>
        /// On the next frame, presses <paramref name="button"/> if it is usable; otherwise moves focus forward from
        /// the current element like Tab does. Meant for "Enter in the last field submits the form".
        /// </summary>
        public static void SubmitOrAdvanceNextFrame(Selectable button) => Instance?.Schedule(button, true);

        /// <summary>Moves focus to the next (or previous) navigable element, exactly like Tab / Shift+Tab.</summary>
        public static void MoveFocus(bool backwards) => Instance?.Move(backwards);
        #endregion

        private void Schedule(Selectable target, bool isSubmit)
        {
            pendingTarget = target;
            pendingIsSubmit = isSubmit;
            pendingFrame = Time.frameCount;
        }

        private void Update()
        {
            var eventSystem = EventSystem.current;

            RunPendingAction(eventSystem);

#if ENABLE_INPUT_SYSTEM
            UpdateInputMode();

            var keyboard = Keyboard.current;
            if (keyboard == null || eventSystem == null)
                return;

            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool otherModifier = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
                || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed
                || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed;

            if (keyboard.tabKey.wasPressedThisFrame && !otherModifier)
            {
                IsKeyboardMode = true;
                Move(shift);
                return;
            }

            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            bool space = keyboard.spaceKey.wasPressedThisFrame;

            if (enter || space)
            {
                var selected = eventSystem.currentSelectedGameObject;

                // Text fields handle Enter (onSubmit) and Space (typing) themselves.
                if (selected == null || IsTextInput(selected))
                    return;

                // Never let Enter/Space activate something hidden or behind a popup.
                var selectable = selected.GetComponent<Selectable>();
                if (selectable != null && !IsReachable(selectable, eventSystem))
                {
                    eventSystem.SetSelectedGameObject(null);
                    return;
                }

                // Space only while navigating with the keyboard, so a stray Space press never re-clicks the
                // button that was last clicked with the mouse (uGUI keeps it selected).
                if (space && IsKeyboardMode && !otherModifier && !IsSpaceBoundToSubmit(eventSystem))
                    ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            }
#endif
        }

        private void LateUpdate()
        {
            var eventSystem = EventSystem.current;
            GameObject target = null;

            if (IsKeyboardMode && eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected != null)
                {
                    var selectable = selected.GetComponent<Selectable>();
                    if (selectable == null || IsCandidate(selectable, out _))
                        target = selected;
                }
            }

            highlight.Follow(target != null ? target.transform as RectTransform : null);
        }

#if ENABLE_INPUT_SYSTEM
        private static void UpdateInputMode()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame))
                IsKeyboardMode = true;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                IsKeyboardMode = false;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                IsKeyboardMode = false;
        }

        /// <summary>Avoids pressing twice when the project's UI actions already map Space to Submit.</summary>
        private static bool IsSpaceBoundToSubmit(EventSystem eventSystem)
        {
            if (eventSystem.currentInputModule is not InputSystemUIInputModule module)
                return false;

            var action = module.submit?.action;
            var space = Keyboard.current?.spaceKey;
            if (action == null || space == null)
                return false;

            foreach (var control in action.controls)
                if (control == space)
                    return true;

            return false;
        }
#endif

        private void RunPendingAction(EventSystem eventSystem)
        {
            if (pendingFrame < 0 || Time.frameCount <= pendingFrame)
                return;

            var target = pendingTarget;
            bool isSubmit = pendingIsSubmit;
            pendingTarget = null;
            pendingFrame = -1;

            if (eventSystem == null)
                return;

            if (target != null && IsCandidate(target, out _))
            {
                if (isSubmit)
                    ExecuteEvents.Execute(target.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
                else
                    Select(target, eventSystem);
                return;
            }

            if (isSubmit)
                Move(false);
        }

        #region Tab navigation
        private void Move(bool backwards)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            BuildOrderedCandidates();
            if (items.Count == 0)
                return;

            var current = eventSystem.currentSelectedGameObject;
            int currentIndex = -1;
            if (current != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Selectable.gameObject == current)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int count = items.Count;
            int step = backwards ? -1 : 1;
            int start = currentIndex >= 0 ? currentIndex + step : (backwards ? count - 1 : 0);

            for (int n = 0; n < count; n++)
            {
                int index = ((start + n * step) % count + count) % count;
                if (index == currentIndex)
                    continue;

                var candidate = items[index].Selectable;
                if (IsReachable(candidate, eventSystem))
                {
                    Select(candidate, eventSystem);
                    return;
                }
            }
        }

        private static void Select(Selectable target, EventSystem eventSystem)
        {
            ScrollIntoView(target.transform as RectTransform);

            if (eventSystem.currentSelectedGameObject == target.gameObject)
            {
                // Re-selecting the same input field should still put the caret back in it.
                if (target is TMP_InputField tmp && !tmp.isFocused) tmp.ActivateInputField();
                else if (target is InputField legacy && !legacy.isFocused) legacy.ActivateInputField();
                return;
            }

            eventSystem.SetSelectedGameObject(target.gameObject);
        }

        private struct Item
        {
            public Selectable Selectable;
            public Rect Rect;
        }

        private sealed class Node
        {
            public Transform Transform;
            public Selectable Selectable;
            public Rect Rect;
            public bool HasRect;
            public readonly List<Node> Children = new();

            public void Encapsulate(Rect r)
            {
                if (!HasRect)
                {
                    Rect = r;
                    HasRect = true;
                    return;
                }
                Rect = Rect.MinMaxRect(Mathf.Min(Rect.xMin, r.xMin), Mathf.Min(Rect.yMin, r.yMin),
                    Mathf.Max(Rect.xMax, r.xMax), Mathf.Max(Rect.yMax, r.yMax));
            }
        }

        /// <summary>
        /// Builds the Tab order. Candidates are grouped by hierarchy (like DOM order on the web), and siblings are
        /// ordered visually: rows top-to-bottom, then left-to-right inside a row. This keeps a sidebar's buttons
        /// together instead of interleaving them with the content next to it, while still following the layout
        /// even when the hierarchy order differs from what is on screen.
        /// </summary>
        private void BuildOrderedCandidates()
        {
            items.Clear();

            var root = new Node();
            var nodes = new Dictionary<Transform, Node>();
            var path = new List<Transform>();

            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (!IsCandidate(selectable, out var rect))
                    continue;

                path.Clear();
                for (var t = selectable.transform; t != null; t = t.parent)
                    path.Add(t);

                var parent = root;
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    var t = path[i];
                    if (!nodes.TryGetValue(t, out var node))
                    {
                        node = new Node { Transform = t };
                        nodes.Add(t, node);
                        parent.Children.Add(node);
                    }
                    node.Encapsulate(rect);
                    parent = node;
                }

                parent.Selectable = selectable;
            }

            Flatten(root);
        }

        private void Flatten(Node node)
        {
            if (node.Selectable != null)
                items.Add(new Item { Selectable = node.Selectable, Rect = node.Rect });

            if (node.Children.Count == 0)
                return;

            var ordered = new List<Node>(node.Children);
            ordered.Sort((a, b) =>
            {
                int byTop = b.Rect.yMax.CompareTo(a.Rect.yMax); // screen space: larger y is higher
                return byTop != 0 ? byTop : a.Transform.GetSiblingIndex().CompareTo(b.Transform.GetSiblingIndex());
            });

            // Group into rows: an item joins the current row when it overlaps the row's first item vertically
            // by at least half of the smaller height.
            int rowStart = 0;
            while (rowStart < ordered.Count)
            {
                var reference = ordered[rowStart].Rect;
                int rowEnd = rowStart + 1;
                while (rowEnd < ordered.Count && SameRow(reference, ordered[rowEnd].Rect))
                    rowEnd++;

                ordered.Sort(rowStart, rowEnd - rowStart, Comparer<Node>.Create((a, b) =>
                {
                    int byLeft = a.Rect.xMin.CompareTo(b.Rect.xMin);
                    if (Mathf.Abs(a.Rect.xMin - b.Rect.xMin) < 1f) byLeft = 0;
                    return byLeft != 0 ? byLeft : a.Transform.GetSiblingIndex().CompareTo(b.Transform.GetSiblingIndex());
                }));

                rowStart = rowEnd;
            }

            foreach (var child in ordered)
                Flatten(child);
        }

        private static bool SameRow(Rect a, Rect b)
        {
            float overlap = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            float minHeight = Mathf.Max(1f, Mathf.Min(a.height, b.height));
            return overlap >= minHeight * .5f;
        }
        #endregion

        #region Visibility checks
        /// <summary>
        /// Cheap checks (no raycasts): active, interactable, not hidden by CanvasGroup/Canvas, has a size on screen.
        /// </summary>
        private static bool IsCandidate(Selectable selectable, out Rect screenRect)
        {
            screenRect = default;

            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
                return false;

            if (selectable is Scrollbar)
                return false;

            if (selectable.GetComponentInParent<KeyboardNavigationIgnore>() != null)
                return false;

            float alpha = 1f;
            bool ignoreParentGroups = false;
            for (var t = selectable.transform; t != null; t = t.parent)
            {
                if (!ignoreParentGroups && t.TryGetComponent<CanvasGroup>(out var group) && group.enabled)
                {
                    if (!group.blocksRaycasts)
                        return false;
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups)
                        ignoreParentGroups = true;
                }

                if (t.TryGetComponent<Canvas>(out var canvas) && !canvas.enabled)
                    return false;
            }

            if (alpha < .01f)
                return false;

            if (selectable.transform is not RectTransform rectTransform || !TryGetScreenRect(rectTransform, out screenRect))
                return false;

            if (screenRect.width < 1f || screenRect.height < 1f)
                return false;

            // Off-screen elements are skipped, unless they live in a scroll view (they get scrolled into view).
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            if (!screen.Overlaps(screenRect) && selectable.GetComponentInParent<ScrollRect>() == null)
                return false;

            return true;
        }

        /// <summary>
        /// Full check: candidate and not covered by another UI element (e.g. a modal popup or its dimmed background).
        /// </summary>
        private bool IsReachable(Selectable selectable, EventSystem eventSystem)
        {
            if (!IsCandidate(selectable, out var rect))
                return false;

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var pointer = new PointerEventData(eventSystem);
            bool testedAnyPoint = false;

            foreach (var sample in SamplePoints)
            {
                var point = new Vector2(rect.x + rect.width * sample.x, rect.y + rect.height * sample.y);
                if (!screen.Contains(point))
                    continue;

                testedAnyPoint = true;
                pointer.position = point;
                raycastResults.Clear();
                eventSystem.RaycastAll(pointer, raycastResults);

                GameObject top = null;
                foreach (var result in raycastResults)
                {
                    if (result.module is GraphicRaycaster)
                    {
                        top = result.gameObject;
                        break;
                    }
                }

                if (top == null || IsPartOf(top.transform, selectable))
                    return true;
            }

            // Fully off-screen inside a scroll view: reachable, it will be scrolled into view.
            return !testedAnyPoint;
        }

        /// <summary>Whether the top-most UI hit belongs to the element itself rather than something covering it.</summary>
        private static bool IsPartOf(Transform hit, Selectable selectable)
        {
            var self = selectable.transform;

            // The element or one of its children (label, icon...).
            if (hit == self || hit.IsChildOf(self))
                return true;

            // Its own container (nothing on top of it at this point).
            if (self.IsChildOf(hit))
                return true;

            // A small decorative sibling drawn over it (label/icon placed next to it in the hierarchy).
            // Large siblings, like a popup's full-screen blocker, still count as covering.
            var parent = self.parent;
            if (parent != null && hit.IsChildOf(parent) && hit is RectTransform hitRect && self is RectTransform selfRect
                && TryGetScreenRect(hitRect, out var hitScreen) && TryGetScreenRect(selfRect, out var selfScreen))
            {
                return hitScreen.width * hitScreen.height <= selfScreen.width * selfScreen.height * 1.5f;
            }

            return false;
        }

        internal static bool TryGetScreenRect(RectTransform rectTransform, out Rect rect)
        {
            rect = default;
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;

            var rootCanvas = canvas.rootCanvas;
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            rectTransform.GetWorldCorners(Corners);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, Corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private static bool IsTextInput(GameObject go) =>
            go.GetComponent<TMP_InputField>() != null || go.GetComponent<InputField>() != null;
        #endregion

        /// <summary>Scrolls the closest ScrollRect so the element is fully visible.</summary>
        private static void ScrollIntoView(RectTransform target)
        {
            if (target == null)
                return;

            var scrollRect = target.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null)
                return;

            var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            var content = scrollRect.content;
            if (!target.IsChildOf(content))
                return;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            var view = viewport.rect;
            var delta = Vector2.zero;

            if (scrollRect.vertical)
            {
                if (bounds.max.y > view.yMax) delta.y = bounds.max.y - view.yMax;
                else if (bounds.min.y < view.yMin) delta.y = bounds.min.y - view.yMin;
            }

            if (scrollRect.horizontal)
            {
                if (bounds.max.x > view.xMax) delta.x = bounds.max.x - view.xMax;
                else if (bounds.min.x < view.xMin) delta.x = bounds.min.x - view.xMin;
            }

            if (delta == Vector2.zero)
                return;

            // Convert the viewport-space offset into the content parent's space.
            var parent = content.parent;
            if (parent != null && parent != viewport)
            {
                var worldDelta = viewport.TransformVector(delta);
                delta = parent.InverseTransformVector(worldDelta);
            }

            scrollRect.StopMovement();
            content.anchoredPosition -= delta;
        }
    }
}
