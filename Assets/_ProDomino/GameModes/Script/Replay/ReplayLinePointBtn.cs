using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.GameModes
{
    public class ReplayLinePointBtn : MonoBehaviour
    {
        [SerializeField]
        private int lineIndex = -1;
        public int LineIndex => lineIndex;

        [SerializeField]
        private Button lineBtn;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        /*void Start()
        {
            lineBtn = gameObject.GetComponent<Button>();
        }*/

        public void SetLineBtnAction(Action<int> action)
        {
            lineBtn.onClick.AddListener(() => action(lineIndex));
        }

        public void SetLineIndex(int turnIndex)
        {
            lineIndex = turnIndex;
        }
    }
}