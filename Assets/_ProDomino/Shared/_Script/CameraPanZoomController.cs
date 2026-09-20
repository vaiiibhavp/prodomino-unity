using UnityEngine;
using UnityEngine.InputSystem;

namespace ProDomino.Shared
{
    /// <summary>
    /// CameraPanZoomController
    /// This component allows the user to pan (drag) and zoom an orthographic camera
    /// within defined bounds calculated from a set of RectTransforms.
    /// - Drag can be inverted per axis.
    /// - Drag on PC requires holding a specific InputAction (dragButton) + mouse movement.
    /// - Supports both PC and mobile (touch) via the Unity Input System.
    /// - Zoom limits are percentage-based relative to the target bounds size.
    /// - Camera position is always clamped to ensure the board remains visible.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraPanZoomController : MonoBehaviour
    {
        [Header("Drag Settings")]
        [Tooltip("If true, dragging direction will be inverted on the X axis.")]
        public bool invertX = false;

        [Tooltip("If true, dragging direction will be inverted on the Y axis.")]
        public bool invertY = false;

        [Tooltip("Base speed multiplier for drag movement.")]
        public float dragSpeed = 1f;

        [Tooltip("InputAction that must be held to drag on PC.")]
        public InputActionReference dragButton;

        [Header("Zoom Settings")]
        [Tooltip("Minimum zoom relative to the board height (1 = full board height, 0.25 = quarter board height).")]
        [Range(0.05f, 1f)]
        public float minZoomPercent = 0.25f;

        [Tooltip("Maximum zoom relative to the board height (1 = full board height, >1 = zoom out beyond full board).")]
        [Range(0.05f, 2f)]
        public float maxZoomPercent = 1.5f;

        [Tooltip("Base speed multiplier for zoom.")]
        public float zoomSpeed = 1f;

        [Header("Bounds Settings")]
        [Tooltip("Extra margin around the board as a percentage of its size, ensuring tiles remain visible.")]
        public float visibleOffsetPercent = 0.1f;

        // Internal references
        private Camera cam;
        private Bounds? boardBounds; // Board bounds in world space
        private float minOrthoSize; // Calculated minimum orthographic size
        private float maxOrthoSize; // Calculated maximum orthographic size

        // For mobile pinch zoom tracking
        private Vector2 lastTouchPos1;
        private Vector2 lastTouchPos2;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>
        /// Initializes the camera limits and zoom range from an array of RectTransforms.
        /// The bounds are calculated to include all rects and expanded by the visibleOffsetPercent.
        /// </summary>
        /// <param name="rects">Array of RectTransforms representing the board elements.</param>
        public void InitializeFromRects(params RectTransform[] rects)
        {
            if (rects == null || rects.Length == 0)
            {
                Debug.LogWarning("InitializeFromRects called with empty rects array");
                boardBounds = null;
                return;
            }

            // Start bounds using the first rect
            Vector3[] corners = new Vector3[4];
            rects[0].GetWorldCorners(corners);
            Bounds bounds = new Bounds(corners[0], Vector3.zero);

            // Encapsulate all rect corners
            foreach (var rect in rects)
            {
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    bounds.Encapsulate(corner);
                }
            }

            // Expand bounds by offset percentage
            float xOffset = bounds.size.x * visibleOffsetPercent;
            float yOffset = bounds.size.y * visibleOffsetPercent;
            bounds.Expand(new Vector3(xOffset, yOffset, 0f));

            boardBounds = bounds;

            // Calculate zoom limits based on the height of the bounds
            float boardHeight = bounds.size.y;
            minOrthoSize = boardHeight * minZoomPercent * 0.5f;
            maxOrthoSize = boardHeight * maxZoomPercent * 0.5f;

            // Set initial camera zoom to fit the board
            //cam.orthographicSize = Mathf.Clamp(boardHeight * 0.5f, minOrthoSize, maxOrthoSize);

            // Clamp initial camera position
            //ClampCameraPosition();
        }

        private void OnEnable()
        {
            if (dragButton != null && dragButton.action != null)
                dragButton.action.Enable();
        }

        private void OnDisable()
        {
            if (dragButton != null && dragButton.action != null)
                dragButton.action.Disable();
        }

        private void Update()
        {
            if (!boardBounds.HasValue)
                return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandlePCDrag();
            //HandlePCZoom();
#else
        HandleTouchInput();
#endif
        }

        /// <summary>
        /// Handles drag on PC using the mouse.
        /// Drag is only active if the defined dragButton is pressed.
        /// </summary>
        private void HandlePCDrag()
        {
            if (dragButton != null && dragButton.action.IsPressed())
            {
                Vector2 delta = Mouse.current.delta.ReadValue();

                // Convert delta from screen space to world units based on zoom
                Vector3 move = new Vector3(
                    delta.x * (invertX ? 1 : -1),
                    delta.y * (invertY ? 1 : -1),
                    0f
                ) * dragSpeed * cam.orthographicSize / Screen.height * 2f;

                transform.position += move;
                //ClampCameraPosition();
            }
        }

        /// <summary>
        /// Handles zoom on PC using the mouse scroll wheel.
        /// Zoom is clamped between the calculated min and max sizes.
        /// </summary>
        private void HandlePCZoom()
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float targetSize = cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime;
                cam.orthographicSize = Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize);
                ClampCameraPosition();
            }
        }

        /// <summary>
        /// Handles drag and pinch-to-zoom on mobile devices using the new Input System.
        /// </summary>
        private void HandleTouchInput()
        {
            if (Touchscreen.current.touches.Count == 1)
            {
                var touch = Touchscreen.current.touches[0];
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 delta = touch.delta.ReadValue();

                    Vector3 move = new Vector3(
                        delta.x * (invertX ? 1 : -1),
                        delta.y * (invertY ? 1 : -1),
                        0f
                    ) * dragSpeed * cam.orthographicSize / Screen.height * 2f;

                    transform.position += move;
                    ClampCameraPosition();
                }
            } else if (Touchscreen.current.touches.Count >= 2)
            {
                var t1 = Touchscreen.current.touches[0];
                var t2 = Touchscreen.current.touches[1];

                if (t1.isInProgress && t2.isInProgress)
                {
                    Vector2 pos1 = t1.position.ReadValue();
                    Vector2 pos2 = t2.position.ReadValue();

                    // Initialize last touch positions when pinch starts
                    if (t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                        t2.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        lastTouchPos1 = pos1;
                        lastTouchPos2 = pos2;
                    } else
                    {
                        // Calculate pinch distance difference
                        float prevDist = Vector2.Distance(lastTouchPos1, lastTouchPos2);
                        float currDist = Vector2.Distance(pos1, pos2);
                        float diff = currDist - prevDist;

                        float targetSize = cam.orthographicSize - diff * zoomSpeed * Time.deltaTime * 0.1f;
                        cam.orthographicSize = Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize);
                        ClampCameraPosition();

                        lastTouchPos1 = pos1;
                        lastTouchPos2 = pos2;
                    }
                }
            }
        }

        /// <summary>
        /// Clamps the camera position so that the visible area never goes outside the board bounds.
        /// This method takes the current zoom into account when clamping.
        /// </summary>
        private void ClampCameraPosition()
        {
            if (!boardBounds.HasValue)
                return; 
            
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;

            Vector3 pos = transform.position;

            float minX = boardBounds.Value.min.x + camWidth / 2f;
            float maxX = boardBounds.Value.max.x - camWidth / 2f;
            float minY = boardBounds.Value.min.y + camHeight / 2f;
            float maxY = boardBounds.Value.max.y - camHeight / 2f;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            transform.position = pos;
        }
    }

}
