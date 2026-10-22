using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// ScrollRect whose mouse wheel scrolling eases toward the new position instead of jumping a full
    /// step per wheel tick. Dragging, inertia and elasticity are the stock ScrollRect behaviour.
    /// </summary>
    public class SmoothScrollRect : ScrollRect
    {
        [SerializeField] private float wheelSmoothTime = 0.12f;

        private Vector2? wheelTarget;
        private Vector2 wheelVelocity;

        public override void OnScroll(PointerEventData data)
        {
            if (!IsActive() || !content)
                return;

            // Let ScrollRect compute (and clamp) the step from the pending target, so fast wheel spins add up,
            // then put the content back and ease toward that target in LateUpdate
            var current = content.anchoredPosition;
            if (wheelTarget.HasValue)
                SetContentAnchoredPosition(wheelTarget.Value);

            base.OnScroll(data);

            wheelTarget = content.anchoredPosition;
            SetContentAnchoredPosition(current);
            velocity = Vector2.zero;
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            wheelTarget = null;
            base.OnBeginDrag(eventData);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!wheelTarget.HasValue || !content)
                return;

            var position = Vector2.SmoothDamp(content.anchoredPosition, wheelTarget.Value, ref wheelVelocity, wheelSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if ((position - wheelTarget.Value).sqrMagnitude < 0.25f)
            {
                position = wheelTarget.Value;
                wheelTarget = null;
                wheelVelocity = Vector2.zero;
            }

            SetContentAnchoredPosition(position);
            velocity = Vector2.zero;
        }

        protected override void OnDisable()
        {
            wheelTarget = null;
            wheelVelocity = Vector2.zero;
            base.OnDisable();
        }
    }
}
