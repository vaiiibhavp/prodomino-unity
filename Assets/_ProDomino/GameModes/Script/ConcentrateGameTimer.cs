using UnityEngine;
using TMPro;

namespace ProDomino.GameModes
{
    public class ConcentrateGameTimer : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText; // Arrastra un Text (UI) aquí desde el inspector

        private float elapsedTime = 0f;
        private bool isRunning = false;

        void Update()
        {
            if (!isRunning) return;

            // Incrementar el tiempo
            elapsedTime += Time.deltaTime;

            // Calcular horas, minutos y segundos
            int hours = Mathf.FloorToInt(elapsedTime / 3600);
            int minutes = Mathf.FloorToInt((elapsedTime % 3600) / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);

            // Mostrar en formato 00:00:00
            timerText.text = string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        public void StartTimer()
        {
            elapsedTime = 0f;
            isRunning = true;
        }

        public void StopTimer()
        {
            isRunning = false;
        }

        public void ResumeTimer()
        {
            isRunning = true;
        }

        public void ResetTimer()
        {
            elapsedTime = 0f;
            timerText.text = "00:00:00";
        }

        public void DisableTimer()
        {
            isRunning = false;
            elapsedTime = 0f;
            timerText.text = "";
        }

        // Si quieres obtener el tiempo en segundos para guardarlo
        public float GetElapsedTime()
        {
            return elapsedTime;
        }
    }
}
