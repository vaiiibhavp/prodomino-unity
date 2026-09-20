using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// Makes a larger area (e.g. a header chip background) behave like a click on another button,
    /// so the whole chip is clickable while the original button keeps its existing listeners.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ForwardClick : MonoBehaviour
    {
        [SerializeField] private Button target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (target && target.isActiveAndEnabled && target.IsInteractable())
                target.onClick.Invoke();
        }
    }
}
