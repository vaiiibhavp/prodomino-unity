using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Controls the visual status of terms and conditions and data treatment agreement toggles by adjusting the
    /// transparency of their associated text elements.
    /// </summary>
    public class ToogleCheckStatusBehavior : MonoBehaviour
    {
        [SerializeField]
        private Toggle termAndConditions;
        [SerializeField]
        private TMP_Text termAndConditionsText;

        [SerializeField]
        private Toggle dataTreatment;
        [SerializeField]
        private TMP_Text dataTreatmentText;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            termAndConditions.onValueChanged.AddListener(ChangeTermAndConditionsTextStatusBehavior);
            dataTreatment.onValueChanged.AddListener(ChangeDataTreatmentTextTextStatusBehavior);

            Color color = termAndConditionsText.color;
            color.a = 127f / 255f;
            termAndConditionsText.color = color;

            color = dataTreatmentText.color;
            color.a = 127f / 255f;
            dataTreatmentText.color = color;
        }

        /// <summary>
        /// Sets the alpha transparency of the terms and conditions text based on the specified status.
        /// </summary>
        /// <param name="isOn">If true, sets the text to fully opaque; if false, sets it to semi-transparent.</param>
        private void ChangeTermAndConditionsTextStatusBehavior(bool isOn)
        {
            Color color = termAndConditionsText.color;

             if (isOn)
            {
                color.a = 255f / 255f;
            }
            else
            {
                color.a = 127f / 255f;
            }

            termAndConditionsText.color = color;
        }

        /// <summary>
        /// Sets the alpha transparency of the data treatment text color based on the specified status.
        /// </summary>
        /// <param name="isOn">If true, sets the text to fully opaque; if false, sets it to semi-transparent.</param>
        private void ChangeDataTreatmentTextTextStatusBehavior(bool isOn)
        {
            Color color = dataTreatmentText.color;

             if (isOn)
            {
                color.a = 255f / 255f;
            }
            else
            {
                color.a = 127f / 255f;
            }

            dataTreatmentText.color = color;
        }
    }
}
