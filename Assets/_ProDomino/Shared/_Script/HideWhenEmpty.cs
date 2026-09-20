using TMPro;
using UnityEngine;

namespace ProDomino.Shared
{
    /// <summary>
    /// Switches a text object off while it has nothing to say, so a layout group gives it no room.
    /// Sits on the parent (a form field group) rather than on the text itself, which would stop
    /// running as soon as it hid itself. Used for the auth validation messages: the fields below
    /// move down when a message appears instead of being written over.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HideWhenEmpty : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;

        public TMP_Text Target { get => target; set => target = value; }

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (!target) return;
            bool hasText = !string.IsNullOrWhiteSpace(target.text);
            if (target.gameObject.activeSelf != hasText)
                target.gameObject.SetActive(hasText);
        }
    }
}
