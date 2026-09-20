using TMPro;
using UnityEngine;

namespace ProDomino.Shared
{
    public class QuestionInHelpScreen : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text description;

        public void SetInfo(string auxTitle, string auxDescription)
        {
            title.text = auxTitle;
            description.text = auxDescription;
        }
    }
}
