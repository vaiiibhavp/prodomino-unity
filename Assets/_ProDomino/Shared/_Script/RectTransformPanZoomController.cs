using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using System;
using NUnit.Framework.Constraints;
using System.Collections;

namespace ProDomino.Shared
{
    /// <summary>
    /// Handles panning (drag) and zooming of a target RectTransform inside a given viewport.
    /// Supports both mouse (desktop/WebGL) and touch (mobile) inputs.
    /// Zoom and drag speeds can be shaped using animation curves for smoother motion.
    /// Now includes renewable inertia for smoother stopping and chaining of inputs.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RectTransformPanZoomController : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The RectTransform that will be moved and zoomed.")]
        [SerializeField] private RectTransform targetRect;
        [Tooltip("The RectTransform that defines the viewport bounds for panning and zooming.")]
        [SerializeField] private RectTransform boardViewport;

        [Header("Drag Settings")]
        [Tooltip("If true, inverts the drag direction on the X axis.")]
        [SerializeField] private bool invertX = false;
        [Tooltip("If true, inverts the drag direction on the Y axis.")]
        [SerializeField] private bool invertY = false;
        [Tooltip("Speed multiplier for automatic movement.")]
        [SerializeField] private float automaticMoveSpeed = 1f;
        [Tooltip("Friction applied to drag velocity when no input (higher = stops faster).")]
        [SerializeField] private float dragFriction = 5f;
        [Tooltip("Input Action Reference for drag button (New Input System).")]
        [SerializeField] private InputActionReference dragButton;
        [Tooltip("Animation curve to smooth drag movement. Value is a normalized multiplier.")]
        [SerializeField] private AnimationCurve dragAnimationCurve;

        [Header("Zoom Settings")]
        [Tooltip("Minimum zoom as a percentage of the board size.")]
        [Range(0.05f, 1f)][SerializeField] private float minZoomPercent = 0.6f;
        [Tooltip("Maximum zoom as a percentage of the board size.")]
        [Range(0.05f, 5f)][SerializeField] private float maxZoomPercent = 2.5f;
        [Tooltip("Speed multiplier for zoom.")]
        [SerializeField] private float zoomSpeed = 1f;
        [Tooltip("Friction applied to zoom velocity when no input (higher = stops faster).")]
        [SerializeField] private float zoomFriction = 5f;
        [Tooltip("If true, uses smoothed zoom based distance move instead of direct position change.")]
        [SerializeField] private bool useSmoothedZoom;
        [Tooltip("If true, constrains the targetRect to the viewport bounds. Otherwise, it can exceed them.")]
        [SerializeField] private bool constrainstsOnViewport;

        [Header("Bounds Settings")]
        [Tooltip("Extra percentage offset beyond board bounds to allow visible margin.")]
        [SerializeField] private float visibleOffsetPercent = 0.1f;

        [Header("Double Click Settings")]
        [Tooltip("Time interval in seconds to detect a double click.")]
        [SerializeField] private float doubleClickTime = 0.3f;

        [Header("Curve Normalization References")]
        [Tooltip("Reference delta magnitude (pixels/frame) used to normalize drag input for the curve.")]
        [SerializeField] private float dragDeltaReference = 50f;
        [Tooltip("Reference scroll delta (mouse wheel units/frame) used to normalize zoom input for the curve.")]
        [SerializeField] private float zoomScrollReference = 5f;
        [Tooltip("Reference pinch delta (pixels change in finger distance/frame) for the zoom curve.")]
        [SerializeField] private float pinchDeltaReference = 10f;

        private readonly float tolerance = 0.001f; // ~3 decimal precision
        private byte blockedCount = 0; // For tracking nested blocks
        private bool isOutOfBounds = false; // For tracking if the targetRect is out of bounds
        private bool isExceedingSize = false; // For tracking if the targetRect exceeds the viewport size
        private float currentLimitZoomScale;
        private Bounds tilesBounds;

        private Canvas rootCanvas;
        private Coroutine displaceCoroutine;
        private Coroutine dragCoroutine;
        private Coroutine scaleCoroutine;

        // Inertia state
        private Vector2 dragVelocity;
        private Vector2 zoomMovementVelocity;
        private float zoomScalingVelocity;

        // For pinch zoom
        private Vector2 lastTouchPos1;
        private Vector2 lastTouchPos2;

        // Original state for reset
        private Vector3 originalPosition;
        private Vector3 originalScale;

        // Double click tracking
        private float lastClickTime = -1f;

        // Pan state for reset
        private Vector3 originalPanTargetPosition;
        private Vector3 originalPanMousePosition;

        /// <summary>
        /// Gets direct children (depth 1) of the target RectTransform.
        /// </summary>
        public IReadOnlyList<RectTransform> ChildrenHandlers => targetRect.GetChildrenAtDepth(1)?.ToArray();

        /// <summary>
        /// Gets the main target RectTransform being panned and zoomed.
        /// </summary>
        public RectTransform TargetRect => targetRect;

        /// <summary>
        /// True if pan/zoom is currently blocked.
        /// </summary>
        public bool IsBlocked 
        { 
            get => blockedCount > 0; 
            private set 
            {
                blockedCount = (byte)Math.Clamp(
                    value ? blockedCount + 1 : blockedCount - 1,
                    0,
                    byte.MaxValue
                );
            } 
        }

        private void Awake()
        {
            if (targetRect == null)
                targetRect = GetComponent<RectTransform>();

            if (dragButton is not null and { action: not null and { enabled: false } })
                dragButton.action.Enable();

            originalPosition = targetRect.localPosition;
            originalScale = Vector3.one;
            rootCanvas = transform.root.GetComponentInChildren<Canvas>();
        }

        private void LateUpdate()
        {
            if (!targetRect || IsBlocked)
                return;

            if (targetRect.childCount is 0)
            {
                if (targetRect.localPosition != originalPosition)
                    ResetController();
                return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandlePCDrag();
            HandlePCZoom();
#else
            HandleTouchInput();
#endif
            ClampRectPosition();
        }

        private void OnDrawGizmos()
        {
            if (!boardViewport || targetRect is null or { childCount: 0 })
                return;

            Gizmos.color = IsBlocked ? Color.red : Color.blue;
            Gizmos.DrawWireCube(tilesBounds.center, tilesBounds.size);
        }

        public void Initialize() => FitToViewport();

        public void SetPanZoomBlockStatus(bool isBlocked) => IsBlocked = isBlocked;

        /// <summary>
        /// Calculates bounds of all child RectTransforms and updates size/scale limits.
        /// </summary>
        public void UpdateTilesBounds(bool isCenteringPosition = false, bool isIgnoringBlock = false)
        {
            if (targetRect is null or { childCount: 0 })
                return;

            if (!isIgnoringBlock && IsBlocked)
                return;

            // If we are centering the position, we need to reset the position first (this should be done before bounds calculation)
            if (isCenteringPosition)
            {
                // If we are centering, we need to adjust the position of the targetRect to simulate centering and get the target position
                // First, get the original position of the targetRect
                originalPosition = targetRect.position;
                targetRect.position = boardViewport.position;
            }
            
            // Calculate bounds of all children
            BoundsCalculations();


            var localSize = targetRect.InverseTransformVector(tilesBounds.size);
            targetRect.sizeDelta = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));

            // Check if the target will adjust its position to center the viewport
            if (isCenteringPosition)
            {
                // Calculate the difference between the center of the tiles bounds and the current position of the targetRect
                var difference = tilesBounds.center - boardViewport.position;

                // Adjust the targetRect position by the difference
                targetRect.position -= difference;

                // Once again, recalculate bounds after centering to get always correct bounds
                BoundsCalculations();

                // Register the new position as the target position
                var targetPosition = targetRect.position;

                // Now we can set the targetRect position back to the original position to start the movementfrom the original position
                targetRect.position = originalPosition;

                // Check if with the new bounds the targetRect is out of bounds
                ClampRectPosition
                    (isUpdatingTilesBounds: false,
                    isIgnoringBlock: true,
                    isForcingConstrainstsToViewport: true);

                // Try to center the targetRect according to the new bounds inside the viewport
                if (targetRect.position != targetPosition)
                    DisplaceToPosition(targetPosition, alternativeSpeedMultiplier: automaticMoveSpeed);
            }

            void BoundsCalculations()
            {
                // Get all direct children of the target RectTransform
                var rects = ChildrenHandlers;

                // Initialize bounds with the first child
                var corners = new Vector3[4];
                rects[0].GetWorldCorners(corners);

                // Create a bounds that starts with the first child's corners
                var bounds = new Bounds(corners[0], Vector3.zero);
                tilesBounds = bounds;

                // Iterate through all children to encapsulate their bounds
                foreach (var rect in rects)
                {
                    rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        tilesBounds.Encapsulate(corner);
                }

                // Adjust bounds to include the visible offset
                if (visibleOffsetPercent != 0)
                {
                    var xOffset = tilesBounds.size.x * visibleOffsetPercent;
                    var yOffset = tilesBounds.size.y * visibleOffsetPercent;
                    tilesBounds.Expand(new Vector3(xOffset, yOffset, 0f));
                }
            }
        }

        /// <summary>
        /// Handles drag with renewable inertia for PC.
        /// </summary>
        private void HandlePCDrag()
        {
            // Make sure targetRect exists
            if (!boardViewport || !targetRect)
            {
                Debug.LogWarning("Board viewport or target RectTransform is not assigned.");
                return;
            } 
            else if (IsBlocked)
                return;

            if (dragButton != null)
            {
                // Detect double click to fit to viewport
                if (dragButton.action.WasPressedThisFrame())
                { 
                    if (Time.time - lastClickTime <= doubleClickTime)
                    {
                        FitToViewport();
                        lastClickTime = -1f;
                    } 
                    else
                    {
                        var worldPos = GetScreenPoint();

                        originalPanMousePosition = worldPos;
                        originalPanTargetPosition = targetRect.position;
                        lastClickTime = Time.time;
                    }
                }
                else if (dragButton.action.WasReleasedThisFrame())
                    originalPanMousePosition = default;

                // Only move while holding the drag button
                else if (dragButton.action.IsPressed())
                {
                    var worldPos = GetScreenPoint();

                    // Apply inversion if needed
                    if (!invertX) worldPos.x = -worldPos.x;
                    if (!invertY) worldPos.y = -worldPos.y;

                    var newPoint = originalPanTargetPosition + (worldPos - originalPanMousePosition);

                    // Set position so the rect follows the cursor
                    DisplaceToPosition(newPoint, false, automaticMoveSpeed);
                }
            }

            Vector3 GetScreenPoint()
            {
                // Get mouse position in screen space
                var screenPos = Mouse.current.position.ReadValue();

                // Convert screen position to local position in the targetRect's parent
                var parentRect = targetRect.parent as RectTransform;
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parentRect,
                    screenPos,
                    rootCanvas.worldCamera, // null means we are using screen space overlay, otherwise pass the camera
                    out worldPos
                );

                return worldPos;
            }
        }

        /// <summary>
        /// Handles zoom with renewable inertia for PC.
        /// Prevents overscaling beyond limits.
        /// Zoom is focused towards mouse position with smooth displacement.
        /// Uses tolerance to avoid floating point jitter.
        /// </summary>
        private void HandlePCZoom()
        {
            if (!boardViewport || !targetRect)
            {
                Debug.LogWarning("Board viewport or target RectTransform is not assigned.");
                return;
            } 
            else if (IsBlocked)
                return;

            var scroll = Mouse.current.scroll.ReadValue().y;

            // Check if the scroll input is significant enough to trigger zoom, and its value is negative or we are not constraining the viewport nor exceeding size
            if (Mathf.Abs(scroll) > 0.01f && (scroll < 0 || !constrainstsOnViewport || !isExceedingSize))
            {
                // Add scroll input to zoom velocity with curve shaping
                zoomScalingVelocity += scroll * zoomSpeed;

                // Get world mouse point
                Vector3 mouseWorldPoint;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    targetRect,
                    Mouse.current.position.ReadValue(),
                    rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                    out mouseWorldPoint
                );

                // If using smoothed zoom, calculate the displacement caused by scaling
                if (useSmoothedZoom)
                { 
                    // Calculate displacement caused by scaling
                    Vector2 deltaOffset = targetRect.position - mouseWorldPoint;

                    // --- SAFETY FIXES ---
                    // Ignore absurd values
                    if (float.IsNaN(deltaOffset.x) || float.IsNaN(deltaOffset.y))
                        deltaOffset = Vector2.zero;

                    // Clamp the offset based on viewport size
                    float maxDistance = Mathf.Max(boardViewport.rect.width, boardViewport.rect.height) * 0.5f;

                    // Normalize the offset so it never exceeds maxDistance
                    if (deltaOffset.magnitude > maxDistance)
                        deltaOffset = deltaOffset.normalized * maxDistance;

                    // Optionally: Lerp offset to make it proportional instead of hard clamp
                    float t = Mathf.InverseLerp(0, maxDistance, deltaOffset.magnitude);
                    deltaOffset = Vector2.Lerp(Vector2.zero, deltaOffset, t);


                    // Add this displacement to velocity
                    zoomMovementVelocity += deltaOffset * zoomSpeed;
                }

                // If not using smoothed zoom, directly displace to the mouse position
                else
                    DisplaceToPosition(mouseWorldPoint, false, zoomSpeed);
            }

            // If we have a non-zero zoom scaling velocity, ant its is negative or we are not constraining the viewport nor exceeding size, set the new scale
            if (Mathf.Abs(zoomScalingVelocity) > 0.01f && (scroll < 0 || !constrainstsOnViewport || !isExceedingSize))
            {
                float currentScale = targetRect.localScale.x;
                float proposedScale = currentScale + zoomScalingVelocity * Time.deltaTime;

                // Clamp against min/max percent
                float newScale = Mathf.Clamp(
                    proposedScale,
                    minZoomPercent,
                    maxZoomPercent
                );

                if (Mathf.Abs(newScale - currentScale) <= tolerance)
                {
                    // Stops the zoom if the scale change is too small
                    zoomScalingVelocity = 0f;
                    zoomMovementVelocity = Vector2.zero;
                    return;
                }

                // Try to apply new scale with tolerance
                targetRect.localScale = new Vector3(newScale, newScale, 1f);

                // Apply smoothed offset to targetRect.position
                if (useSmoothedZoom && zoomMovementVelocity.sqrMagnitude > tolerance)
                {
                    targetRect.position -= (Vector3)zoomMovementVelocity;
                    zoomMovementVelocity = Vector2.Lerp(zoomMovementVelocity, Vector2.zero, zoomFriction * Time.deltaTime);
                }

                // Apply friction to zoom velocity
                zoomScalingVelocity = Mathf.Lerp(zoomScalingVelocity, 0f, zoomFriction * Time.deltaTime);

                UpdateTilesBounds();
            }
        }

        /// <summary>
        /// Handles touch drag (absolute follow) and pinch zoom.
        /// Mirrors PC drag: object sticks to finger instead of velocity-based movement.
        /// </summary>
        private void HandleTouchInput()
        {
            if (Touchscreen.current.touches.Count == 1)
            {
                var touch = Touchscreen.current.touches[0];

                // Solo procesamos si el dedo está en movimiento o mantenido
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved ||
                    touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    // Obtener posición del dedo en pantalla
                    var screenPos = touch.position.ReadValue();

                    // Convertir a posición local en el espacio del parent de targetRect
                    var parentRect = targetRect.parent as RectTransform;
                    Vector2 localPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        screenPos,
                        null, // Si usas Camera en lugar de Screen Space Overlay, cámbialo
                        out localPos
                    );

                    // Aplicar inversión de ejes si corresponde
                    if (!invertX) localPos.x = -localPos.x;
                    if (!invertY) localPos.y = -localPos.y;

                    // Seguir directamente el dedo (sin inercia)
                    targetRect.anchoredPosition = localPos;

                    UpdateTilesBounds();
                }
            } else if (Touchscreen.current.touches.Count >= 2)
            {
                // El pinch-zoom se mantiene igual
                var t1 = Touchscreen.current.touches[0];
                var t2 = Touchscreen.current.touches[1];

                if (t1.isInProgress && t2.isInProgress)
                {
                    var pos1 = t1.position.ReadValue();
                    var pos2 = t2.position.ReadValue();

                    if (t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                        t2.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        lastTouchPos1 = pos1;
                        lastTouchPos2 = pos2;
                    } else
                    {
                        var prevDist = Vector2.Distance(lastTouchPos1, lastTouchPos2);
                        var currDist = Vector2.Distance(pos1, pos2);
                        var diff = currDist - prevDist;

                        float normalizedPinch = Mathf.Clamp01(Mathf.Abs(diff) / pinchDeltaReference);
                        zoomScalingVelocity += diff * zoomSpeed * 0.01f * normalizedPinch;

                        if (Mathf.Abs(zoomScalingVelocity) > 0.001f)
                        {
                            float currentScale = targetRect.localScale.x;
                            float newScale = Mathf.Clamp(
                                currentScale + zoomScalingVelocity * Time.deltaTime,
                                minZoomPercent,
                                maxZoomPercent
                            );

                            targetRect.localScale = new Vector3(newScale, newScale, 1f);
                            zoomScalingVelocity = Mathf.Lerp(zoomScalingVelocity, 0f, zoomFriction * Time.deltaTime);
                            UpdateTilesBounds();
                        }

                        lastTouchPos1 = pos1;
                        lastTouchPos2 = pos2;
                    }
                }
            }
        }

        private void FitToViewport()
        {
            if (!boardViewport || !targetRect)
            {
                Debug.LogWarning("Board viewport or target RectTransform is not assigned.");
                return;
            }

            // If the controller is blocked and we are not updating the tiles bounds, we skip the clamping
            else if (IsBlocked)
                return;

            if (boardViewport == null || targetRect.childCount is 0)
            {
                if (boardViewport == null)
                    Debug.LogWarning("No viewportRect assigned. Using default reset.");
                ResetCameraToOriginal();
                return;
            }

            ScaleTo(currentLimitZoomScale);
            UpdateTilesBounds(true);
        }

        /// <summary>
        /// Clamps the position and scale of the targetRect so it always stays inside the viewport.
        /// Additionally, calculates a dynamic currentLimitZoomScale which represents the maximum
        /// allowed zoom-out scale at this moment.
        /// </summary>
        private void ClampRectPosition
            (bool isUpdatingTilesBounds = true, 
            bool isIgnoringBlock = false, 
            bool isForcingConstrainstsToViewport = false)
        {
            if (!boardViewport || !targetRect)
            {
                Debug.LogWarning("Board viewport or target RectTransform is not assigned.");
                return;
            }

            // If the controller is blocked and we are not updating the tiles bounds, we skip the clamping
            else if (!isIgnoringBlock && IsBlocked)
                return;

            // Get world bounds of the viewport
            var boardCorners = new Vector3[4];
            boardViewport.GetWorldCorners(boardCorners);

            var boardBounds = new Bounds(boardCorners[0], Vector3.zero);
            foreach (var c in boardCorners)
                boardBounds.Encapsulate(c);

            // --- POSITION CLAMP ---
            var minPos = tilesBounds.min;
            var maxPos = tilesBounds.max;
            minPos.z = maxPos.z = targetRect.position.z; // Keep Z position unchanged

            isOutOfBounds = !boardBounds.Contains(minPos) || !boardBounds.Contains(maxPos);
            if (isOutOfBounds && isUpdatingTilesBounds && (isForcingConstrainstsToViewport || constrainstsOnViewport))
            {
                // Current displacement to apply (world space)
                Vector3 displacement = Vector3.zero;

                // --- Clamp on X axis ---
                float leftExcess = boardBounds.min.x - tilesBounds.min.x; // negative if content too far left
                float rightExcess = boardBounds.max.x - tilesBounds.max.x; // positive if content too far right

                if (leftExcess > 0)           // Content passed left boundary
                    displacement.x += leftExcess;
                else if (rightExcess < 0)     // Content passed right boundary
                    displacement.x += rightExcess;

                // --- Clamp on Y axis ---
                float bottomExcess = boardBounds.min.y - tilesBounds.min.y; // negative if content too low
                float topExcess = boardBounds.max.y - tilesBounds.max.y; // positive if content too high

                if (bottomExcess > 0)         // Content passed bottom boundary
                    displacement.y += bottomExcess;
                else if (topExcess < 0)       // Content passed top boundary
                    displacement.y += topExcess;

                // Apply correction in world space
                Vector3 newWorldPos = targetRect.position + displacement;

                // Call your provided function to actually move the RectTransform
                DisplaceToPosition(newWorldPos, false, automaticMoveSpeed);
            }

            // --- SCALE CLAMP ---
            ComputeZoomOutLimit_FromWorldBounds();

            // Check if currently exceeding viewport
            isExceedingSize = tilesBounds.size.x > boardBounds.size.x || tilesBounds.size.y > boardBounds.size.y;

            // If the targetRect is out of bounds, we need to displace it to the new position
            if (isExceedingSize && (isForcingConstrainstsToViewport || constrainstsOnViewport))
                targetRect.localScale = Vector3.one * currentLimitZoomScale;

            // Returns the absolute uniform scale that makes the content fit inside the viewport.
            // Assumes uniform scaling on targetRect (x == y).
            void ComputeZoomOutLimit_FromWorldBounds()
            {
                if (!boardViewport || !targetRect)
                    return;

                // 1) Viewport bounds in world space
                var vc = new Vector3[4];
                boardViewport.GetWorldCorners(vc);
                var viewBounds = new Bounds(vc[0], Vector3.zero);
                for (int i = 1; i < 4; i++)
                    viewBounds.Encapsulate(vc[i]);

                // 2) Current content size in world space (already scaled)
                //    tilesBounds must be up-to-date BEFORE calling this method.
                Vector2 contentWorld = new Vector2(Mathf.Max(tilesBounds.size.x, 1e-6f),
                                                   Mathf.Max(tilesBounds.size.y, 1e-6f));

                // 3) Viewport size in world space
                Vector2 viewWorld = new Vector2(viewBounds.size.x, viewBounds.size.y);

                // 4) Factor needed to fit CURRENT content into the viewport
                float fitFactorX = viewWorld.x / contentWorld.x;
                float fitFactorY = viewWorld.y / contentWorld.y;
                float fitFactor = Mathf.Min(fitFactorX, fitFactorY);

                // 5) Convert factor to ABSOLUTE scale (relative to base), using current uniform scale
                float currentUniformScale = targetRect.localScale.x; // assumes uniform scale
                float limit = currentUniformScale * fitFactor;

                currentLimitZoomScale = limit;
            }
        }

        private void DisplaceToPosition(Vector3 newPosition, bool isBlocking = true, float alternativeSpeedMultiplier = 1)
        {
            if (!targetRect)
                return;

            // Save local blocking state
            var _isBlocking = isBlocking;

            // If there is already a coroutine running, stop it
            if (displaceCoroutine != null)
            {
                StopCoroutine(displaceCoroutine);
                displaceCoroutine = null;
            }

            // Set block state
            if (_isBlocking)
                IsBlocked = true;

            displaceCoroutine = StartCoroutine(IDisplaceToPosition
                (() =>
                {
                    if (_isBlocking)
                        IsBlocked = false;

                    UpdateTilesBounds();
                    displaceCoroutine = null;
                },
                newPosition,
                alternativeSpeedMultiplier));
        }


        IEnumerator IDisplaceToPosition(Action onFinish, Vector3 newPosition, float alternativeSpeedMultiplier = 1)
        {
            var startPosition = targetRect.position;
            var targetPosition = newPosition;

            // Use proper distance check
            var isDestinyReached = new Func<bool>(() => Vector3.Distance(targetRect.position, targetPosition) <= tolerance);

            var elapsed = 0f;
            var interpolation = 0f;
            float maxDuration = 1f; // 1 second timeout
            var lastRegisteredPosition = targetRect.position;

            while (!isDestinyReached() && elapsed < maxDuration)
            {
                elapsed += Time.deltaTime * alternativeSpeedMultiplier;
                interpolation = Mathf.Clamp01(elapsed / maxDuration); // normalized time [0,1]
                lastRegisteredPosition = Vector3.Lerp(startPosition, targetPosition, interpolation);

                targetRect.position = lastRegisteredPosition;
                UpdateTilesBounds();

                yield return null;
            }

            // Ensure final position is exact
            var destinyPosition = targetPosition;
            targetRect.position = destinyPosition;

            if (onFinish != null)
                onFinish.Invoke();
        }

        /// <summary>
        /// Smoothly scales the targetRect to the given new scale using a coroutine.
        /// The animation will stop after reaching the scale or after a 1 second timeout.
        /// </summary>
        private void ScaleTo(float newScale, bool isBlocking = true)
        {
            if (!targetRect)
                return;

            // Save local blocking state
            var _isBlocking = isBlocking;

            // If there is already a coroutine running, stop it
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            // Set block state
            if (_isBlocking)
                IsBlocked = true;

            scaleCoroutine = StartCoroutine(IScaleTo(() =>
            {
                if (_isBlocking)
                    IsBlocked = false;

                UpdateTilesBounds();
                scaleCoroutine = null;
            }));

            IEnumerator IScaleTo(Action onFinish)
            {
                var startScale = targetRect.localScale;
                var targetScale = Vector3.one * Mathf.Clamp(newScale, minZoomPercent, maxZoomPercent);

                // Proper tolerance check for scale
                var isScaleReached = new Func<bool>(() => Vector3.Distance(targetRect.localScale, targetScale) <= tolerance);

                float elapsed = 0f;
                float maxDuration = 1f; // maximum time allowed

                while (!isScaleReached() && elapsed < maxDuration && !isExceedingSize)
                {
                    // Increase elapsed time depending on speed
                    elapsed += Time.deltaTime * automaticMoveSpeed;

                    // Normalize elapsed into [0,1]
                    float t = Mathf.Clamp01(elapsed / maxDuration);

                    // Interpolate scale
                    targetRect.localScale = Vector3.Lerp(startScale, targetScale, t);
                    UpdateTilesBounds();

                    yield return null;
                }

                // Ensure exact scale at the end
                targetRect.localScale = targetScale;

                if (onFinish != null)
                    onFinish.Invoke();
            }
        }

        public void StopCoroutines()
        {
            if (displaceCoroutine != null)
            {
                IsBlocked = false; // Reset block state before stopping (remove 1 count)
                StopCoroutine(displaceCoroutine);
                displaceCoroutine = null;
            }

            if (scaleCoroutine != null)
            {
                IsBlocked = false; // Reset block state before stopping (remove 1 count)
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            if (dragCoroutine != null)
            {
                IsBlocked = false; // Reset block state before stopping (remove 1 count)
                StopCoroutine(dragCoroutine);
                dragCoroutine = null;
            }
        }

        public void RestartFields()
        {
            lastClickTime = -1f;
            blockedCount = 0;
            dragVelocity = Vector2.zero;
            zoomMovementVelocity = Vector2.zero;
            zoomScalingVelocity = 0f;
        }

        private void ResetCameraToOriginal()
        {
            targetRect.localPosition = originalPosition;
            targetRect.localScale = originalScale;
            UpdateTilesBounds();
        }

        public void ResetController()
        {
            for (int i = targetRect.childCount - 1; i >= 0; i--)
                Destroy(targetRect.GetChild(i).gameObject);

            StopCoroutines();
            ResetCameraToOriginal();
            RestartFields();
        }
    }
}
