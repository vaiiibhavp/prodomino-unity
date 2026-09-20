using Timba.Patterns;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AnalyticsSystem
{
    /// <summary>
    /// Manages analytics event subscriptions and dispatches analytic events to registered listeners.
    /// </summary>
    public class AnalyticsManager : SingleInstanceMonoBehaviour<AnalyticsManager>, IService
    {
        public bool IsAlreadyInitialized => true;

        public delegate void AnalyticsEventHandler(AnalyticType type, object data);

        private event AnalyticsEventHandler OnAnalyticsEvent;

        /// <summary>
        /// Registers a listener for analytics events.
        /// </summary>
        /// <param name="listener">The event handler to subscribe to analytics events.</param>
        public void Subscribe(AnalyticsEventHandler listener)
        {
            OnAnalyticsEvent += listener;
        }

        /// <summary>
        /// Removes the specified analytics event handler from the event subscription.
        /// </summary>
        /// <param name="listener">The event handler to remove.</param>
        public void Unsubscribe(AnalyticsEventHandler listener)
        {
            OnAnalyticsEvent -= listener;
        }

        /// <summary>
        /// Triggers an analytics event with the specified type and optional data.
        /// </summary>
        /// <param name="type">The type of analytic event to send.</param>
        /// <param name="data">Optional data associated with the analytic event.</param>
        public void SendAnalytic(AnalyticType type, object data = null)
        {
            OnAnalyticsEvent?.Invoke(type, data);
        }
    }
}
