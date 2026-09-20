using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class UI_InteractionSound : MonoBehaviour, /*IPointerClickHandler,*/ IPointerEnterHandler
    {
        [Header("Sound IDs (match with SoundManager library)")]
        public IDAudioClip clickSoundId;
        public IDAudioClip hoverSoundId;

        [SerializeField]
        private bool activeSound = false;

        private Button button;

        void Awake()
        {
            button = gameObject.GetComponent<Button>();

            if(button != null)
            {
                button.onClick.AddListener(() =>
                {
                    if (activeSound && button.interactable && clickSoundId != IDAudioClip.none)
                        SoundManager.Instance.PlaySFX(clickSoundId);
                });   
            }
        }

        /*public void OnPointerClick(PointerEventData eventData)
        {
            if (clickSoundId != IDAudioClip.none)
                SoundManager.Instance.PlaySFX(clickSoundId);
        }*/

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (activeSound && hoverSoundId != IDAudioClip.none)
                SoundManager.Instance.PlaySFX(hoverSoundId);
        }
    }
}