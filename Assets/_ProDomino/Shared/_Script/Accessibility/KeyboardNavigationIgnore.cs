using UnityEngine;

namespace ProDomino.Shared.Accessibility
{
    /// <summary>
    /// Excludes this element and all of its children from Tab / Shift+Tab navigation
    /// (see <see cref="KeyboardNavigationController"/>). Mouse and touch are not affected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyboardNavigationIgnore : MonoBehaviour { }
}
