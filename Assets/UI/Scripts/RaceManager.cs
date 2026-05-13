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
        public GameObject finishPanel; 
        public TMP_Text resultText;

        [Header("Participants")]
        public EzerealCarController playerCar;
        public EzerealCarController aiCar;

        [Header("Race State")]
        public bool isRaceStarted = false;
        public bool isRaceFinished = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (countdownPanel != null) countdownPanel.SetActive(false);
            if (finishPanel != null) finishPanel.SetActive(false);
            
            StartRaceSequence();
        }

        public void StartRaceSequence()
        {
            StartCoroutine(RaceStartRoutine());
        }

        IEnumerator RaceStartRoutine()
        {
            FreezeCars(true);
            if (countdownPanel != null) countdownPanel.SetActive(true);
            
            if (countdownText != null)
            {
                countdownText.text = "3";
                yield return new WaitForSeconds(1);
                countdownText.text = "2";
                yield return new WaitForSeconds(1);
                countdownText.text = "1";
                yield return new WaitForSeconds(1);
                countdownText.text = "GO!";
            }

            FreezeCars(false);
            isRaceStarted = true;
            
            yield return new WaitForSeconds(1);
            if (countdownPanel != null) countdownPanel.SetActive(false);
        }

        private void Update()
        {
            // Навигация геймпадом на экране финиша
            if (isRaceFinished && finishPanel != null && finishPanel.activeSelf)
            {
                var gamepad = UnityEngine.InputSystem.Gamepad.current;
                if (gamepad != null)
                {
                    if (gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame)
                    {
                        GameObject current = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                        
                        // Ищем кнопки более надежным способом (даже если они внутри доп. объектов)
                        UnityEngine.UI.Button[] buttons = finishPanel.GetComponentsInChildren<UnityEngine.UI.Button>();
                        if (buttons.Length >= 2)
                        {
                            GameObject btn1 = buttons[0].gameObject;
                            GameObject btn2 = buttons[1].gameObject;

                            if (current == btn1) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btn2);
                            else UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(btn1);
                            
                            Debug.Log("RaceManager: Переключил кнопку геймпадом.");
                        }
                    }
                }
            }
        }

        public void FinishRace(int rank)
        {
            Debug.Log("RaceManager: Функция FinishRace вызвана! Текущий статус isRaceFinished: " + isRaceFinished);

            if (isRaceFinished) 
            {
                Debug.Log("RaceManager: Гонка уже была завершена ранее. Выхожу.");
                return;
            }
            
            isRaceFinished = true;
            isRaceStarted = false;

            Debug.Log("RaceManager: Пытаюсь остановить машины...");
            FreezeCars(true);

            // ВЫКЛЮЧАЕМ HUD игрока
            if (playerCar != null)
            {
                // Ищем Канвас именно у объекта Игрока
                Transform playerRoot = playerCar.transform.root;
                Canvas[] allCanvases = playerRoot.GetComponentsInChildren<Canvas>(true);
                
                foreach (Canvas c in allCanvases)
                {
                    // Выключаем все Канвасы, которые принадлежат игроку (но не тот, где скрипт RaceManager)
                    if (c.gameObject != this.gameObject)
                    {
                        Debug.Log("RaceManager: Выключаю найденный Канвас игрока: " + c.name);
                        c.gameObject.SetActive(false);
                    }
                }
            }

            // Показываем экран результатов
            if (finishPanel != null) 
            {
                Debug.Log("RaceManager: Включаю объект: " + finishPanel.name);
                finishPanel.SetActive(true);
                
                // Автоматически выделяем кнопку Restart, чтобы можно было сразу нажать A/Enter
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                // Ищем кнопку Restart внутри панели и выделяем её
                GameObject restartBtn = finishPanel.GetComponentInChildren<UnityEngine.UI.Button>()?.gameObject;
                if (restartBtn != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(restartBtn);
            }
            else 
            {
                Debug.LogError("RaceManager ОШИБКА: Объект finishPanel НЕ ПРИВЯЗАН в инспекторе!");
            }

            if (resultText != null)
            {
                resultText.text = "ВАШЕ МЕСТО: " + rank;
            }
            else
            {
                Debug.LogWarning("RaceManager: Текст результата не привязан.");
            }

            // Показываем курсор
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void RestartRace()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public void ExitToMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Menu");
        }

        public void FreezeCars(bool freeze)
        {
            // ЗАМОРОЗКА ИГРОКА
            if (playerCar != null) 
            {
                playerCar.enabled = !freeze;
                playerCar.isStarted = !freeze;
                
                if (playerCar.vehicleRB != null)
                {
                    // Жесткая заморозка физики только если гонка ОКОНЧЕНА
                    // На старте (freeze=true, но finish=false) просто обнуляем скорость
                    playerCar.vehicleRB.isKinematic = (freeze && isRaceFinished); 
                    
                    if (freeze)
                    {
                        playerCar.vehicleRB.velocity = Vector3.zero;
                        playerCar.vehicleRB.angularVelocity = Vector3.zero;
                    }
                }
            }

            // ЗАМОРОЗКА БОТА
            if (aiCar != null) 
            {
                aiCar.enabled = !freeze;
                aiCar.isStarted = !freeze;

                if (aiCar.vehicleRB != null)
                {
                    aiCar.vehicleRB.isKinematic = (freeze && isRaceFinished);
                    if (freeze)
                    {
                        aiCar.vehicleRB.velocity = Vector3.zero;
                        aiCar.vehicleRB.angularVelocity = Vector3.zero;
                    }
                }

                // AI скрипты выключаем всегда при заморозке
                MonoBehaviour[] allScripts = aiCar.GetComponentsInChildren<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != aiCar && script.gameObject.activeInHierarchy)
                    {
                        script.enabled = !freeze;
                    }
                }
            }
        }
    }
}
