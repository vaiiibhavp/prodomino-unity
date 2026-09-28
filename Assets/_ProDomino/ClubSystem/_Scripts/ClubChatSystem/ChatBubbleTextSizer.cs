using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Reports a capped preferred width for a TMP label to the parent layout group, so a chat
    /// bubble hugs short messages and wraps long ones at a fixed maximum width.
    /// uGUI has no max-width concept, so the cap is applied through <see cref="ILayoutElement"/>
    /// with a higher priority than the one TMP itself provides.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ChatBubbleTextSizer : UIBehaviour, ILayoutElement
    {
        private const float UnboundedWidth = 32767f;

        [SerializeField] private TMP_Text label;
        [SerializeField] private float maxWidth = 380f;

        private float cachedPreferredWidth;
        private float cachedPreferredHeight;

        public float minWidth => -1f;
        public float preferredWidth => cachedPreferredWidth;
        public float flexibleWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => cachedPreferredHeight;
        public float flexibleHeight => -1f;
        public int layoutPriority => 2;

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveLabel();
            SetDirty();
        }

        protected override void OnDisable()
        {
            SetDirty();
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveLabel();

            // OnValidate runs while the inspector is drawing, so defer the rebuild instead of
            // triggering it in the middle of that pass
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this)
                    SetDirty();
            };
        }
#endif

        public void CalculateLayoutInputHorizontal()
        {
            ResolveLabel();

            if (!label)
            {
                cachedPreferredWidth = 0f;
                return;
            }

            var unwrappedWidth = label.GetPreferredValues(label.text, UnboundedWidth, UnboundedWidth).x;
            cachedPreferredWidth = Mathf.Min(unwrappedWidth, maxWidth);
        }

        public void CalculateLayoutInputVertical()
        {
            if (!label)
            {
                cachedPreferredHeight = 0f;
                return;
            }

            // The parent layout grants the width reported above, so measure the wrapped
            // height against that same value instead of the not yet updated rect.
            var width = cachedPreferredWidth > 0f ? cachedPreferredWidth : ((RectTransform)transform).rect.width;
            cachedPreferredHeight = label.GetPreferredValues(label.text, width, UnboundedWidth).y;
        }

        private void ResolveLabel()
        {
            if (!label)
                label = GetComponent<TMP_Text>();
        }

        private void SetDirty()
        {
            if (IsActive())
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }
    }
}
