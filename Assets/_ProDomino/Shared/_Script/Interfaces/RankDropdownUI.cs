using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class RankDropdownUI : MonoBehaviour
    {
        [Header("Referencias del UI")]
        public Button headerButton;        
        public GameObject contentPanel;     
        public Animator arrowAnimator;      
        [SerializeField] private Transform rankLayoutGroupTransform;

        [SerializeField] bool selectTransformParent = false;

        private bool isOpen = false;

        void Start()
        {
            if (contentPanel != null)
                contentPanel.SetActive(false);

            if (headerButton != null)
                headerButton.onClick.AddListener(ToggleContent);
        }

        void ToggleContent()
        {
            isOpen = !isOpen;

            if (contentPanel != null)
                contentPanel.SetActive(isOpen);

            if (arrowAnimator != null)
                arrowAnimator.SetBool("IsOpen", isOpen);

            RefreshLayout();

            //RefreshPositionOfFollowingButtons();
        }

        public void RefreshLayout()
        {

            if(!selectTransformParent)
            {
                var parent = rankLayoutGroupTransform ?? contentPanel?.transform.parent ?? contentPanel?.transform;
                if (parent != null)
                    parent.RefreshLayoutGroupsImmediateAndRecursive();
            }
            else
            {
                contentPanel?.transform.parent.RefreshLayoutGroupsImmediateAndRecursive();   
            }
        }

        /*private void RefreshPositionOfFollowingButtons()
        {
            int index = transform.GetSiblingIndex();

            transform.parent.GetChild(index+1).GetComponent<RankDropdownUI>().RefreshLayout();
        }*/
    }
}
