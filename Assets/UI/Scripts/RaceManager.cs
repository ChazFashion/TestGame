using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Ezereal;

namespace RacingUI
{
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance;

        [System.Serializable]
        public class ParticipantState
        {
            public EzerealCarController car;
            public string name;
            public bool isPlayer;
            public int currentLap = 1;
            public int nextCheckpointIndex = 0;
            public float currentLapTime = 0f;
            public float bestLapTime = float.MaxValue;
            public float totalTime = 0f;
            public bool hasFinished = false;
        }

        [Header("UI References")]
        public TMP_Text countdownText;
        public GameObject countdownPanel;
        public GameObject finishPanel; 
        public TMP_Text resultText;
        
        [Header("Race Gameplay UI")]
        public TMP_Text timerText;       // Текст текущего времени круга/гонки
        public TMP_Text lapText;         // Текст номера круга (например, "LAP 1/3")
        public TMP_Text bestLapText;     // Текст лучшего времени круга

        [Header("Participants")]
        public EzerealCarController playerCar;
        public EzerealCarController aiCar; // Может быть пустым, если гонка одиночная

        [Header("Track Data Settings")]
        public string trackId = "Track_Forest";
        public int totalLaps = 3;

        [Header("Race State")]
        public bool isRaceStarted = false;
        public bool isRaceFinished = false;

        private List<ParticipantState> participants = new List<ParticipantState>();
        private int totalCheckpoints = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (countdownPanel != null) countdownPanel.SetActive(false);
            if (finishPanel != null) finishPanel.SetActive(false);

            // 1. Загружаем параметры трассы из базы данных
            LoadTrackDataFromDatabase();

            // 2. Автоматически находим и настраиваем вейпоинты
            AutoSetupWaypoints();

            // 3. Инициализируем список участников гонки
            InitializeParticipants();

            // 4. Обновляем стартовый интерфейс кругов
            UpdateLapUI();

            // 5. Запускаем стартовый отсчет
            StartRaceSequence();
        }

        private void LoadTrackDataFromDatabase()
        {
            DataManager dm = FindObjectOfType<DataManager>();
            if (dm != null)
            {
                var trackInfo = dm.GetTrackInfo(trackId);
                if (trackInfo != null)
                {
                    if (trackInfo.ContainsKey("laps_count"))
                    {
                        totalLaps = System.Convert.ToInt32(trackInfo["laps_count"]);
                    }
                    Debug.Log($"[RaceManager] Данные трассы '{trackId}' загружены из БД: Кругов={totalLaps}");
                }
            }
        }

        private void AutoSetupWaypoints()
        {
            // Ищем контейнер с именем "waypoints" или содержащим "waypoint"
            GameObject container = null;
            foreach (var go in FindObjectsOfType<GameObject>(true))
            {
                if ((go.name.ToLower() == "waypoints" || go.name.ToLower().Contains("waypoint")) && go.transform.parent == null)
                {
                    container = go;
                    break;
                }
            }

            if (container != null)
            {
                int index = 0;
                foreach (Transform child in container.transform)
                {
                    RaceWaypoint wp = child.GetComponent<RaceWaypoint>();
                    if (wp == null)
                    {
                        wp = child.gameObject.AddComponent<RaceWaypoint>();
                    }
                    wp.waypointIndex = index;

                    // Настраиваем коллайдер как триггер
                    Collider col = child.GetComponent<Collider>();
                    if (col != null)
                    {
                        col.isTrigger = true;
                    }
                    else
                    {
                        BoxCollider box = child.gameObject.AddComponent<BoxCollider>();
                        box.isTrigger = true;
                    }

                    // Скрываем видимость кубика в игре (чтобы они были невидимыми триггерами)
                    MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    index++;
                }
                totalCheckpoints = index;
                Debug.Log($"[RaceManager] Автоматически настроено {totalCheckpoints} вейпоинтов из контейнера '{container.name}'");
            }
            else
            {
                // Поиск по сцене, если нет общего родителя
                RaceWaypoint[] wps = FindObjectsOfType<RaceWaypoint>();
                System.Array.Sort(wps, (a, b) => a.waypointIndex.CompareTo(b.waypointIndex));
                totalCheckpoints = wps.Length;
                Debug.LogWarning($"[RaceManager] Контейнер 'waypoints' не найден в корне. Найдено {totalCheckpoints} компонентов RaceWaypoint на сцене.");
            }
        }

        private void InitializeParticipants()
        {
            participants.Clear();

            if (playerCar != null)
            {
                participants.Add(new ParticipantState()
                {
                    car = playerCar,
                    name = "Игрок",
                    isPlayer = true
                });
            }

            if (aiCar != null)
            {
                // Ищем имя бота у AICarDriver
                string botName = "Бот-Соперник";
                var aiDriver = aiCar.GetComponentInChildren<AICarDriver>();
                if (aiDriver != null)
                {
                    botName = aiDriver.botName;
                }

                participants.Add(new ParticipantState()
                {
                    car = aiCar,
                    name = botName,
                    isPlayer = false
                });
            }
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
            // 1. Логика таймера гонки
            if (isRaceStarted && !isRaceFinished)
            {
                float dt = Time.deltaTime;
                foreach (var p in participants)
                {
                    if (!p.hasFinished)
                    {
                        p.currentLapTime += dt;
                        p.totalTime += dt;
                    }
                }

                // Обновляем таймер игрока на экране
                ParticipantState playerState = GetPlayerState();
                if (playerState != null && timerText != null)
                {
                    timerText.text = "ВРЕМЯ: " + FormatTime(playerState.currentLapTime);
                }
            }

            // 2. Навигация геймпадом на экране финиша
            if (isRaceFinished && finishPanel != null && finishPanel.activeSelf)
            {
                var gamepad = UnityEngine.InputSystem.Gamepad.current;
                if (gamepad != null)
                {
                    if (gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame)
                    {
                        GameObject current = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                        
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

        // Вызывается из триггера вейпоинта
        public void OnCarPassedWaypoint(EzerealCarController car, int waypointIndex)
        {
            ParticipantState state = GetParticipantState(car);
            if (state == null || state.hasFinished) return;

            // Если машина пересекает следующий по порядку вейпоинт
            if (waypointIndex == state.nextCheckpointIndex)
            {
                state.nextCheckpointIndex++;
                Debug.Log($"[RaceManager] {state.name} прошел чекпоинт {waypointIndex + 1}/{totalCheckpoints}");
            }
        }

        // Вызывается из финишного триггера
        public void OnCarCrossedFinish(EzerealCarController car)
        {
            if (!isRaceStarted || isRaceFinished) return;

            ParticipantState state = GetParticipantState(car);
            if (state == null || state.hasFinished) return;

            // Проверяем, прошел ли участник все чекпоинты (или если чекпоинтов на сцене нет вообще)
            if (totalCheckpoints == 0 || state.nextCheckpointIndex >= totalCheckpoints)
            {
                float completedLapTime = state.currentLapTime;

                // Защита от спама триггера на старте (игнорируем первые 5 секунд)
                if (state.totalTime < 5f && state.currentLap == 1) return;

                // Записываем лучший круг
                if (completedLapTime < state.bestLapTime)
                {
                    state.bestLapTime = completedLapTime;
                    if (state.isPlayer && bestLapText != null)
                    {
                        bestLapText.text = "ЛУЧШИЙ КРУГ: " + FormatTime(state.bestLapTime);
                    }
                }

                Debug.Log($"[RaceManager] {state.name} завершил круг {state.currentLap}! Время круга: {FormatTime(completedLapTime)}");

                state.currentLap++;
                state.currentLapTime = 0f;
                state.nextCheckpointIndex = 0;

                if (state.isPlayer)
                {
                    UpdateLapUI();
                }

                // Проверка завершения гонки (все круги пройдены)
                if (state.currentLap > totalLaps)
                {
                    state.hasFinished = true;

                    // Вычисляем место
                    int rank = 1;
                    foreach (var p in participants)
                    {
                        if (p != state && p.hasFinished) rank++;
                    }

                    Debug.Log($"[RaceManager] {state.name} ФИНИШИРОВАЛ! Место: {rank}");

                    if (state.isPlayer)
                    {
                        FinishRace(rank);

                        // Сохраняем лучший круг в БД
                        DataManager dm = FindObjectOfType<DataManager>();
                        if (dm != null && state.bestLapTime != float.MaxValue)
                        {
                            dm.SaveRaceRecord(trackId, state.bestLapTime);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[RaceManager] {state.name} пересек линию финиша, но не прошел все чекпоинты! Пройдено: {state.nextCheckpointIndex}/{totalCheckpoints}");
            }
        }

        private void UpdateLapUI()
        {
            ParticipantState playerState = GetPlayerState();
            if (playerState != null)
            {
                if (lapText != null)
                {
                    int displayLap = Mathf.Min(playerState.currentLap, totalLaps);
                    lapText.text = $"КРУГ: {displayLap} / {totalLaps}";
                }
                
                if (bestLapText != null)
                {
                    bestLapText.text = playerState.bestLapTime == float.MaxValue 
                        ? "ЛУЧШИЙ КРУГ: --:--.--" 
                        : "ЛУЧШИЙ КРУГ: " + FormatTime(playerState.bestLapTime);
                }
            }
        }

        private ParticipantState GetPlayerState()
        {
            foreach (var p in participants)
            {
                if (p.isPlayer) return p;
            }
            return null;
        }

        private ParticipantState GetParticipantState(EzerealCarController car)
        {
            foreach (var p in participants)
            {
                if (p.car == car) return p;
            }
            return null;
        }

        public int GetPlayerCheckpointIndex()
        {
            ParticipantState playerState = GetPlayerState();
            return playerState != null ? playerState.nextCheckpointIndex : 0;
        }

        private string FormatTime(float timeInSeconds)
        {
            if (timeInSeconds == float.MaxValue) return "--:--.--";
            int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
            int seconds = Mathf.FloorToInt(timeInSeconds - minutes * 60);
            float fraction = (timeInSeconds * 100) % 100;
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);
        }

        public void FinishRace(int rank)
        {
            if (isRaceFinished) return;
            
            isRaceFinished = true;
            isRaceStarted = false;

            FreezeCars(true);

            // Выключаем стандартный HUD игрока
            if (playerCar != null)
            {
                Transform playerRoot = playerCar.transform.root;
                Canvas[] allCanvases = playerRoot.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas c in allCanvases)
                {
                    if (c.gameObject != this.gameObject)
                    {
                        c.gameObject.SetActive(false);
                    }
                }
            }

            // Показываем экран результатов
            if (finishPanel != null) 
            {
                finishPanel.SetActive(true);
                
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                GameObject restartBtn = finishPanel.GetComponentInChildren<UnityEngine.UI.Button>()?.gameObject;
                if (restartBtn != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(restartBtn);
            }

            if (resultText != null)
            {
                resultText.text = "ВАШЕ МЕСТО: " + rank;
            }

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
            // ИГРОК
            if (playerCar != null) 
            {
                playerCar.enabled = !freeze;
                playerCar.isStarted = !freeze;
                
                if (playerCar.vehicleRB != null)
                {
                    playerCar.vehicleRB.isKinematic = (freeze && isRaceFinished); 
                    if (freeze)
                    {
                        playerCar.vehicleRB.velocity = Vector3.zero;
                        playerCar.vehicleRB.angularVelocity = Vector3.zero;
                    }
                }
            }

            // БОТ
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
