using UnityEngine;
using TMPro;
using System.Collections;
using Ezereal;

namespace RacingUI
{
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance;

        [Header("UI References")]
        public TMP_Text countdownText;
        public GameObject countdownPanel;

        [Header("Participants")]
        public EzerealCarController playerCar;
        public EzerealCarController aiCar;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            // Сразу замораживаем машины и запускаем отсчет 3-2-1
            FreezeCars(true);
            if (countdownPanel != null) countdownPanel.SetActive(false);
            
            // ЗАПУСКАЕМ ОТСЧЕТ АВТОМАТИЧЕСКИ ПРИ ЗАГРУЗКЕ СЦЕНЫ
            StartRaceSequence();
        }

        public void StartRaceSequence()
        {
            StartCoroutine(CountdownRoutine());
        }

        IEnumerator CountdownRoutine()
        {
            if (countdownPanel != null) countdownPanel.SetActive(true);
            
            // Замораживаем управление перед стартом
            FreezeCars(true);

            int count = 3;
            while (count > 0)
            {
                if (countdownText != null) countdownText.text = count.ToString();
                yield return new WaitForSeconds(1f);
                count--;
            }

            if (countdownText != null) countdownText.text = "GO!";
            
            // РАЗРЕШАЕМ ЕХАТЬ
            FreezeCars(false);
            
            yield return new WaitForSeconds(1f);
            if (countdownPanel != null) countdownPanel.SetActive(false);
        }

        public void FreezeCars(bool freeze)
        {
            if (playerCar != null) 
            {
                playerCar.enabled = !freeze;
                // Принудительно заводим двигатель при старте
                if (!freeze) playerCar.isStarted = true; 
            }
            if (aiCar != null) 
            {
                aiCar.enabled = !freeze;
                if (!freeze) aiCar.isStarted = true;
            }

            Debug.Log(freeze ? "--- МАШИНЫ ЗАМОРОЖЕНЫ ---" : "--- СТАРТ! УПРАВЛЕНИЕ ВКЛЮЧЕНО ---");
        }
    }
}
