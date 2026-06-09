using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

            // Список чекпоинтов конкретно для этого участника в нужном порядке прохождения
            [System.NonSerialized]
            public List<Transform> checkpoints = new List<Transform>();
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

        [Header("Checkpoints (Containers)")]
        [Tooltip("Контейнер чекпоинтов для игрока (перетащить сюда родительский объект с кубами)")]
        public Transform playerWaypointsContainer;
        [Tooltip("Контейнер чекпоинтов для бота (перетащить сюда родительский объект с кубами)")]
        public Transform aiWaypointsContainer;
        [Tooltip("Контейнер общих чекпоинтов, которые должны проходить и игрок, и бот")]
        public Transform commonWaypointsContainer;

        [Header("Track Data Settings")]
        public string trackId = "Track_Forest";
        public int totalLaps = 3;

        [Header("Race State")]
        public bool isRaceStarted = false;
        public bool isRaceFinished = false;

        [Header("Pause Settings")]
        [Tooltip("Панель меню паузы. Если не назначена, будет создана автоматически.")]
        public GameObject pausePanel;
        [HideInInspector] public bool isPaused = false;

        private List<ParticipantState> participants = new List<ParticipantState>();

        [Header("Dynamic Spawning")]
        [Tooltip("Точка спавна игрока (если не задана машина на сцене)")]
        public Transform playerSpawnPoint;
        [Tooltip("Точка спавна бота (если не задана машина на сцене)")]
        public Transform aiSpawnPoint;
        [Tooltip("Префаб по умолчанию для игрока")]
        public GameObject defaultPlayerCarPrefab;
        [Tooltip("Имя бота-соперника для спавна")]
        public string opponentBotName = "Бот Сергей";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            SpawnCars();
        }

        private void SpawnCars()
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning("[RaceManager] DataManager.Instance не найден! Пропускаем динамический спавн.");
                return;
            }

            // --- 1. СПАВН ИГРОКА ---
            Vector3 playerPos = Vector3.zero;
            Quaternion playerRot = Quaternion.identity;
            bool hasPlayerSpawn = false;

            // Если машина игрока уже перетащена в инспекторе, используем её координаты и уничтожаем
            if (playerCar != null)
            {
                playerPos = playerCar.transform.position;
                playerRot = playerCar.transform.rotation;
                hasPlayerSpawn = true;
                DestroyImmediate(playerCar.gameObject);
                playerCar = null;
            }
            else if (playerSpawnPoint != null)
            {
                playerPos = playerSpawnPoint.position;
                playerRot = playerSpawnPoint.rotation;
                hasPlayerSpawn = true;
            }

            if (hasPlayerSpawn)
            {
                int activeCarId = DataManager.Instance.GetActiveCarId();
                var carData = DataManager.Instance.GetCarGarageState(activeCarId);
                
                if (carData != null && carData.ContainsKey("prefab_name"))
                {
                    string prefabName = carData["prefab_name"].ToString();
                    var mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == prefabName);
                    
                    GameObject prefabToSpawn = mapping.gameplayPrefab;
                    if (prefabToSpawn == null && defaultPlayerCarPrefab != null)
                    {
                        prefabToSpawn = defaultPlayerCarPrefab;
                    }

                    if (prefabToSpawn != null)
                    {
                        playerPos.y += 0.25f; // Приподнимаем над землей, чтобы исключить застревание
                        GameObject spawned = Instantiate(prefabToSpawn, playerPos, playerRot);
                        spawned.name = "PlayerCar_" + prefabName;
                        RemoveEmbeddedUI(spawned);
                        playerCar = spawned.GetComponent<EzerealCarController>();
                        if (playerCar == null) playerCar = spawned.GetComponentInChildren<EzerealCarController>();

                        // Привязываем UI-элементы сцены гонки к контроллеру игрока
                        GameObject speedObj = GameObject.Find("Current Speed TMP");
                        GameObject gearObj = GameObject.Find("Current Gear TMP");
                        TMPro.TMP_Text speedText = speedObj != null ? speedObj.GetComponent<TMPro.TMP_Text>() : null;
                        TMPro.TMP_Text gearText = gearObj != null ? gearObj.GetComponent<TMPro.TMP_Text>() : null;
                        if (playerCar != null)
                        {
                            playerCar.SetUITextReferences(speedText, gearText);
                        }

                        // Если используем стандартную машину-заполнитель, подменяем её 3D-модель на нужную из БД
                        if (prefabToSpawn == defaultPlayerCarPrefab && mapping.menuShowcasePrefab != null)
                        {
                            SwapVisualModel(playerCar, mapping.menuShowcasePrefab);
                        }

                        Debug.Log($"[RaceManager] Успешно заспавнена машина игрока: {spawned.name}");
                        ApplyUpgradesToCar(playerCar, activeCarId);
                        
                        // Добавляем контроллер Нитро на машину игрока
                        spawned.AddComponent<NitroController>();



                        // Находим камеру на сцене со скриптом SmoothCameraFollow и привязываем её к игроку
                        SmoothCameraFollow followCam = FindAnyObjectByType<SmoothCameraFollow>();
                        if (followCam != null)
                        {
                            if (playerCar != null)
                            {
                                followCam.target = playerCar.vehicleRB != null ? playerCar.vehicleRB.transform : playerCar.transform;
                            }
                            else
                            {
                                Debug.LogError("[RaceManager] Не удалось установить камеру: компонент EzerealCarController не найден на заспавненной машине игрока!");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"[RaceManager] Не найден геймплейный префаб для {prefabName}!");
                    }
                }
            }

            // --- 2. СПАВН БОТА ---
            Vector3 aiPos = Vector3.zero;
            Quaternion aiRot = Quaternion.identity;
            bool hasAiSpawn = false;

            if (aiCar != null)
            {
                aiPos = aiCar.transform.position;
                aiRot = aiCar.transform.rotation;
                hasAiSpawn = true;

                var oldDriver = aiCar.GetComponentInChildren<AICarDriver>();
                if (oldDriver != null)
                {
                    opponentBotName = oldDriver.botName;
                }

                DestroyImmediate(aiCar.gameObject);
                aiCar = null;
            }
            else if (aiSpawnPoint != null)
            {
                aiPos = aiSpawnPoint.position;
                aiRot = aiSpawnPoint.rotation;
                hasAiSpawn = true;
            }

            if (hasAiSpawn)
            {
                string botName = opponentBotName;
                if (DataManager.Instance != null)
                {
                    botName = DataManager.Instance.selectedOpponentBotName;
                }

                var botProfile = DataManager.Instance.GetBotProfileByName(botName);
                if (botProfile != null && botProfile.ContainsKey("prefab"))
                {
                    string botPrefabName = botProfile["prefab"].ToString();
                    var mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == botPrefabName);
                    
                    GameObject botPrefab = mapping.gameplayPrefab;
                    if (botPrefab == null && defaultPlayerCarPrefab != null)
                    {
                        botPrefab = defaultPlayerCarPrefab;
                    }

                    if (botPrefab != null)
                    {
                        aiPos.y += 0.25f; // Приподнимаем над землей, чтобы исключить застревание
                        GameObject spawned = Instantiate(botPrefab, aiPos, aiRot);
                        spawned.name = "AICar_" + botName;
                        RemoveEmbeddedUI(spawned);
                        aiCar = spawned.GetComponent<EzerealCarController>();
                        if (aiCar == null) aiCar = spawned.GetComponentInChildren<EzerealCarController>();

                        // Подменяем 3D-модель для ИИ
                        if (botPrefab == defaultPlayerCarPrefab && mapping.menuShowcasePrefab != null)
                        {
                            SwapVisualModel(aiCar, mapping.menuShowcasePrefab);
                        }

                        var aiDriver = spawned.GetComponentInChildren<AICarDriver>();
                        if (aiDriver == null)
                        {
                            // Добавляем ИИ-водителя на тот же объект, где висит Rigidbody (чтобы transform двигался)
                            Rigidbody aiRb = spawned.GetComponentInChildren<Rigidbody>();
                            if (aiRb != null)
                            {
                                aiDriver = aiRb.gameObject.AddComponent<AICarDriver>();
                            }
                            else
                            {
                                aiDriver = spawned.AddComponent<AICarDriver>();
                            }
                        }

                        if (aiDriver != null)
                        {
                            aiDriver.botName = botName;

                            // Назначаем контейнер вейпоинтов для бота (для вождения)
                            Transform drivingWaypoints = aiWaypointsContainer;
                            if (drivingWaypoints == null)
                            {
                                GameObject wpObj = GameObject.Find("Waypoints");
                                if (wpObj != null)
                                {
                                    drivingWaypoints = wpObj.transform;
                                }
                            }

                            if (drivingWaypoints != null)
                            {
                                var wpContainer = drivingWaypoints.GetComponent<WaypointContainer>();
                                if (wpContainer == null)
                                {
                                    wpContainer = drivingWaypoints.gameObject.AddComponent<WaypointContainer>();
                                    int count = drivingWaypoints.childCount;
                                    wpContainer.waypoints = new Transform[count];
                                    for (int i = 0; i < count; i++)
                                    {
                                        wpContainer.waypoints[i] = drivingWaypoints.GetChild(i);
                                    }
                                }
                                aiDriver.waypointContainer = wpContainer;
                            }
                            
                            // Отключаем PlayerInput у машины бота
                            var playerInput = spawned.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                            if (playerInput == null) playerInput = spawned.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>();
                            if (playerInput != null) playerInput.enabled = false;
                        }

                        // Настраиваем базовые характеристики бота
                        if (botProfile != null && botProfile.ContainsKey("car_id"))
                        {
                            int botCarId = System.Convert.ToInt32(botProfile["car_id"]);
                            ConfigureAiCarStats(aiCar, botCarId);
                        }

                        Debug.Log($"[RaceManager] Успешно заспавнена машина бота: {spawned.name}");
                    }
                    else
                    {
                        Debug.LogError($"[RaceManager] Не найден геймплейный префаб бота для: {botPrefabName}");
                    }
                }
            }
        }

        private void RemoveEmbeddedUI(GameObject carGo)
        {
            if (carGo == null) return;
            Canvas[] canvases = carGo.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                Debug.Log($"[RaceManager] Удален встроенный UI Canvas: {canvas.name} на объекте {carGo.name}");
                Destroy(canvas.gameObject);
            }
        }

        private void ApplyUpgradesToCar(EzerealCarController car, int carId)
        {
            if (car == null) return;
            if (DataManager.Instance == null) return;

            var levels = DataManager.Instance.GetCarUpgradeLevels(carId);
            int engine = levels["engine"];
            int handling = levels["handling"];

            // 1. Считываем базовые характеристики в зависимости от модели
            float baseHp = 100f;
            float baseSpeed = 120f;
            float baseMass = 1200f;

            var carData = DataManager.Instance.GetCarGarageState(carId);
            if (carData != null)
            {
                if (carData.ContainsKey("base_hp")) baseHp = System.Convert.ToSingle(carData["base_hp"]);
                if (carData.ContainsKey("base_speed")) baseSpeed = System.Convert.ToSingle(carData["base_speed"]);
                if (carData.ContainsKey("base_weight")) baseMass = System.Convert.ToSingle(carData["base_weight"]);

                if (carData.ContainsKey("prefab_name"))
                {
                    string prefabName = carData["prefab_name"].ToString();
                    var mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == prefabName);

                    // Переопределяем параметры из инспектора, если они заданы пользователем
                    if (mapping.baseHorsePower > 0f) baseHp = mapping.baseHorsePower;
                    if (mapping.baseMaxSpeed > 0f) baseSpeed = mapping.baseMaxSpeed;
                    if (mapping.baseMass > 0f) baseMass = mapping.baseMass;
                }
            }

            // Устанавливаем базовые параметры на контроллер
            car.horsePower = baseHp;
            car.maxForwardSpeed = baseSpeed;

            Rigidbody rb = car.vehicleRB;
            if (rb == null) rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.mass = baseMass;
            }

            // 2. Двигатель (Прокачка)
            float engineMultiplier = 1f + (engine - 1) * 0.15f;
            car.horsePower = car.horsePower * engineMultiplier;
            car.maxForwardSpeed = car.maxForwardSpeed * (1f + (engine - 1) * 0.08f);

            // 3. Управляемость (Облегчение массы)
            if (rb != null)
            {
                float massMultiplier = 1f - (handling - 1) * 0.05f;
                rb.mass = rb.mass * massMultiplier;
            }

            Debug.Log($"[RaceManager] Настроены параметры авто ID={carId}: БазоваяМощность={baseHp:F1} HP, ИтоговаяМощность={car.horsePower:F1} HP, Масса={rb?.mass:F1} кг");
        }

        private void ConfigureAiCarStats(EzerealCarController car, int carId)
        {
            if (car == null || DataManager.Instance == null) return;

            // Считываем базовые характеристики в зависимости от модели
            float baseHp = 100f;
            float baseSpeed = 120f;
            float baseMass = 1200f;

            var carData = DataManager.Instance.GetCarGarageState(carId);
            if (carData != null)
            {
                if (carData.ContainsKey("base_hp")) baseHp = System.Convert.ToSingle(carData["base_hp"]);
                if (carData.ContainsKey("base_speed")) baseSpeed = System.Convert.ToSingle(carData["base_speed"]);
                if (carData.ContainsKey("base_weight")) baseMass = System.Convert.ToSingle(carData["base_weight"]);

                if (carData.ContainsKey("prefab_name"))
                {
                    string prefabName = carData["prefab_name"].ToString();
                    var mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == prefabName);

                    // Переопределяем параметры из инспектора, если они заданы пользователем
                    if (mapping.baseHorsePower > 0f) baseHp = mapping.baseHorsePower;
                    if (mapping.baseMaxSpeed > 0f) baseSpeed = mapping.baseMaxSpeed;
                    if (mapping.baseMass > 0f) baseMass = mapping.baseMass;
                }
            }

            // Устанавливаем базовые параметры на контроллер бота
            car.horsePower = baseHp;
            car.maxForwardSpeed = baseSpeed;

            Rigidbody rb = car.vehicleRB;
            if (rb == null) rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.mass = baseMass;
            }

            Debug.Log($"[RaceManager] Инициализированы базовые параметры бота ID={carId}: HP={baseHp:F1}, MaxSpeed={baseSpeed:F1}, Mass={rb?.mass:F1}");
        }

        private void SwapVisualModel(EzerealCarController car, GameObject visualPrefab)
        {
            if (car == null || visualPrefab == null) return;

            // 1. Скрываем все оригинальные MeshRenderer и SkinnedMeshRenderer (визуал грузовика) на всей машине (от корня)
            MeshRenderer[] originalMeshRenderers = car.transform.root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in originalMeshRenderers)
            {
                renderer.enabled = false;
            }
            SkinnedMeshRenderer[] originalSkinnedRenderers = car.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in originalSkinnedRenderers)
            {
                renderer.enabled = false;
            }

            // Отключаем внутренние приборные панели (Canvas) грузовика, чтобы убрать наложение передач, но сохраняем Overlay Canvas для игрока
            Canvas[] originalCanvases = car.transform.root.GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in originalCanvases)
            {
                if (canvas.gameObject.name == "Overlay Canvas")
                {
                    canvas.gameObject.SetActive(car == playerCar);
                }
                else
                {
                    canvas.enabled = false;
                }
            }

            // 2. Создаем визуальную модель советской машины как дочерний объект для КОРНЯ машины
            GameObject visualInstance = Instantiate(visualPrefab, car.transform.root);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            // Выключаем Static у новой модели и всех её дочерних объектов, чтобы она двигалась
            visualInstance.isStatic = false;
            foreach (Transform t in visualInstance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = false;
            }

            // 3. Удаляем физику и коллайдеры с новой визуальной модели
            Rigidbody visualRb = visualInstance.GetComponent<Rigidbody>();
            if (visualRb == null) visualRb = visualInstance.GetComponentInChildren<Rigidbody>();
            if (visualRb != null) Destroy(visualRb);

            Collider[] visualColliders = visualInstance.GetComponentsInChildren<Collider>(true);
            foreach (var col in visualColliders)
            {
                Destroy(col);
            }

            // 4. Поиск колес в новой визуальной модели по именам (Lada/Volga)
            Transform flWheel = null;
            Transform frWheel = null;
            Transform rlWheel = null;
            Transform rrWheel = null;

            Transform[] children = visualInstance.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                string name = child.name.ToLower();
                if (name.Contains("wheel") || name.Contains("whl") || name.Contains("koleso"))
                {
                    if (name.Contains("f_l") || (name.Contains("front") && name.Contains("left")) || name.Contains("_fl"))
                    {
                        flWheel = child;
                    }
                    else if (name.Contains("f_r") || (name.Contains("front") && name.Contains("right")) || name.Contains("_fr"))
                    {
                        frWheel = child;
                    }
                    else if (name.Contains("r_l") || (name.Contains("rear") && name.Contains("left")) || name.Contains("back_l") || name.Contains("_rl"))
                    {
                        rlWheel = child;
                    }
                    else if (name.Contains("r_r") || (name.Contains("rear") && name.Contains("right")) || name.Contains("back_r") || name.Contains("_rr"))
                    {
                        rrWheel = child;
                    }
                }
            }

            // 5. Переназначаем колесные меши в EzerealCarController через Рефлексию и сдвигаем WheelColliders в мировом пространстве
            var controllerType = typeof(EzerealCarController);
            if (flWheel != null)
            {
                controllerType.GetField("frontLeftWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, flWheel);
                if (car.frontLeftWheelCollider != null)
                {
                    Vector3 targetPos = new Vector3(flWheel.position.x, car.frontLeftWheelCollider.transform.position.y, flWheel.position.z);
                    car.frontLeftWheelCollider.transform.position = targetPos;
                }
            }
            if (frWheel != null)
            {
                controllerType.GetField("frontRightWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, frWheel);
                if (car.frontRightWheelCollider != null)
                {
                    Vector3 targetPos = new Vector3(frWheel.position.x, car.frontRightWheelCollider.transform.position.y, frWheel.position.z);
                    car.frontRightWheelCollider.transform.position = targetPos;
                }
            }
            if (rlWheel != null)
            {
                controllerType.GetField("rearLeftWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, rlWheel);
                if (car.rearLeftWheelCollider != null)
                {
                    Vector3 targetPos = new Vector3(rlWheel.position.x, car.rearLeftWheelCollider.transform.position.y, rlWheel.position.z);
                    car.rearLeftWheelCollider.transform.position = targetPos;
                }
            }
            if (rrWheel != null)
            {
                controllerType.GetField("rearRightWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, rrWheel);
                if (car.rearRightWheelCollider != null)
                {
                    Vector3 targetPos = new Vector3(rrWheel.position.x, car.rearRightWheelCollider.transform.position.y, rrWheel.position.z);
                    car.rearRightWheelCollider.transform.position = targetPos;
                }
            }

            Debug.Log($"[RaceManager] Визуал {visualPrefab.name} успешно интегрирован в EzerealCarController!");
        }

        private void Start()
        {
            if (countdownPanel != null) countdownPanel.SetActive(false);
            if (finishPanel != null) finishPanel.SetActive(false);

            // 1. Загружаем параметры трассы из базы данных
            LoadTrackDataFromDatabase();

            // 2. Инициализируем список участников гонки
            InitializeParticipants();

            // 3. Настраиваем и сортируем индивидуальные чекпоинты для каждого участника
            InitializeCheckpoints();

            // 4. Обновляем стартовый интерфейс кругов
            UpdateLapUI();

            // 5. Инициализируем панель паузы, если она не задана вручную
            if (pausePanel == null)
            {
                CreateDefaultPausePanel();
            }
            else
            {
                pausePanel.SetActive(false);
            }

            // 6. Запускаем стартовый отсчет
            StartRaceSequence();
        }

        private void LoadTrackDataFromDatabase()
        {
            if (DataManager.Instance != null)
            {
                totalLaps = DataManager.Instance.selectedLapsCount;
                Debug.Log($"[RaceManager] Количество кругов установлено из настроек: {totalLaps}");
                return;
            }

            DataManager dm = FindAnyObjectByType<DataManager>();
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

        private void InitializeCheckpoints()
        {
            foreach (var p in participants)
            {
                Transform specificContainer = p.isPlayer ? playerWaypointsContainer : aiWaypointsContainer;
                InitializeCheckpointsForState(p, specificContainer);
            }
        }

        private void InitializeCheckpointsForState(ParticipantState state, Transform specificContainer)
        {
            state.checkpoints.Clear();

            // 1. Добавляем специфичные чекпоинты из контейнера
            if (specificContainer != null)
            {
                foreach (Transform child in specificContainer)
                {
                    state.checkpoints.Add(child);
                    ConfigureCheckpointTrigger(child);
                }
            }

            // 2. Добавляем общие чекпоинты
            if (commonWaypointsContainer != null)
            {
                foreach (Transform child in commonWaypointsContainer)
                {
                    state.checkpoints.Add(child);
                    ConfigureCheckpointTrigger(child);
                }
            }

            // 3. Если никакие контейнеры не перетащены, ищем все вейпоинты на сцене в качестве резервной логики
            if (state.checkpoints.Count == 0)
            {
                RaceWaypoint[] allWps = FindObjectsByType<RaceWaypoint>(FindObjectsSortMode.None);
                foreach (var wp in allWps)
                {
                    state.checkpoints.Add(wp.transform);
                }
            }

            // 4. Сортируем чекпоинты по индексу/имени, чтобы порядок прохождения был верным
            state.checkpoints.Sort((a, b) => GetWaypointSortIndex(a).CompareTo(GetWaypointSortIndex(b)));

            // 5. Убеждаемся, что на каждом чекпоинте висит скрипт RaceWaypoint
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < state.checkpoints.Count; i++)
            {
                var t = state.checkpoints[i];
                RaceWaypoint rw = t.GetComponent<RaceWaypoint>();
                if (rw == null) rw = t.gameObject.AddComponent<RaceWaypoint>();
                rw.waypointIndex = i;
                
                Collider col = t.GetComponent<Collider>();
                string colInfo = col != null ? $"Коллайдер (Trigger={col.isTrigger})" : "НЕТ КОЛЛАЙДЕРА";
                sb.Append($"- [{i}] {t.name}: {colInfo}, Позиция: {t.position}\n");
            }

            Debug.Log($"[RaceManager] Для {state.name} инициализировано {state.checkpoints.Count} чекпоинтов:\n{sb.ToString()}");
        }

        private void ConfigureCheckpointTrigger(Transform t)
        {
            // Настраиваем коллайдер как триггер
            Collider col = t.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
            else
            {
                BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }

            // Скрываем видимость кубика в игре (чтобы они были невидимыми триггерами)
            MeshRenderer renderer = t.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private int GetWaypointSortIndex(Transform t)
        {
            // Пытаемся извлечь число из имени объекта (например, "Waypoint (5)" -> 5)
            string name = t.name;
            string digits = "";
            foreach (char c in name)
            {
                if (char.IsDigit(c)) digits += c;
            }
            if (!string.IsNullOrEmpty(digits))
            {
                if (int.TryParse(digits, out int index)) return index;
            }

            // Если чисел нет, возвращаем его индекс в иерархии родителя
            return t.GetSiblingIndex();
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
            // 0. Обработка клавиши паузы (через новый Input System)
            if (!isRaceFinished)
            {
                bool pausePressed = false;

                // Клавиатура: Escape или P
                if (Keyboard.current != null)
                {
                    pausePressed = Keyboard.current.escapeKey.wasPressedThisFrame 
                                || Keyboard.current.pKey.wasPressedThisFrame;
                }

                // Геймпад: кнопка Start
                if (!pausePressed && Gamepad.current != null)
                {
                    pausePressed = Gamepad.current.startButton.wasPressedThisFrame;
                }

                if (pausePressed)
                {
                    if (isPaused)
                    {
                        ResumeRace();
                    }
                    else
                    {
                        PauseRace();
                    }
                }
            }

            if (isPaused) return;

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
        public void OnCarPassedWaypoint(EzerealCarController car, Transform waypointTransform)
        {
            ParticipantState state = GetParticipantState(car);
            if (state == null || state.hasFinished) return;

            // Если машина пересекает следующий по порядку вейпоинт из своего списка
            if (state.nextCheckpointIndex < state.checkpoints.Count)
            {
                Transform expected = state.checkpoints[state.nextCheckpointIndex];
                if (waypointTransform == expected)
                {
                    state.nextCheckpointIndex++;
                    Debug.Log($"[RaceManager] {state.name} прошел чекпоинт {state.nextCheckpointIndex}/{state.checkpoints.Count}");
                }
            }
        }

        // Вызывается из финишного триггера
        public void OnCarCrossedFinish(EzerealCarController car)
        {
            if (!isRaceStarted || isRaceFinished) return;

            ParticipantState state = GetParticipantState(car);
            if (state == null || state.hasFinished) return;

            // Проверяем, прошел ли участник все чекпоинты (или если чекпоинтов на сцене нет вообще)
            if (state.checkpoints.Count == 0 || state.nextCheckpointIndex >= state.checkpoints.Count)
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

                    // Пополняем бак Нитро при завершении круга
                    if (playerCar != null)
                    {
                        var nitroCtrl = playerCar.GetComponent<NitroController>();
                        if (nitroCtrl == null) nitroCtrl = playerCar.GetComponentInParent<NitroController>();
                        if (nitroCtrl == null) nitroCtrl = playerCar.GetComponentInChildren<NitroController>();
                        
                        if (nitroCtrl != null)
                        {
                            nitroCtrl.RefillNitro();
                        }
                    }
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
                        DataManager dm = FindAnyObjectByType<DataManager>();
                        if (dm != null && state.bestLapTime != float.MaxValue)
                        {
                            dm.SaveRaceRecord(trackId, state.bestLapTime);
                        }
                    }
                    else
                    {
                        // Если бот финишировал первым, гонка сразу завершается поражением игрока
                        if (rank == 1)
                        {
                            FinishRace(2);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[RaceManager] {state.name} пересек линию финиша, но не прошел все чекпоинты! Пройдено: {state.nextCheckpointIndex}/{state.checkpoints.Count}");
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

        // Возвращает текущий активный вейпоинт для игрока
        public Transform GetPlayerTargetCheckpoint()
        {
            ParticipantState playerState = GetPlayerState();
            if (playerState != null && playerState.nextCheckpointIndex < playerState.checkpoints.Count)
            {
                return playerState.checkpoints[playerState.nextCheckpointIndex];
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

            // Расчет награды: 100 монет за круг при победе, 50 монет за круг при поражении
            int coinsPerLap = (rank == 1) ? 100 : 50;
            int rewardCoins = totalLaps * coinsPerLap;

            // Сохранение награды в базу данных
            if (DataManager.Instance != null)
            {
                DataManager.Instance.AddCoins(rewardCoins);
                Debug.Log($"[RaceManager] Начислено монет за финиш на месте #{rank}: {rewardCoins} (кругов в гонке: {totalLaps})");
            }

            if (resultText != null)
            {
                string statusText = (rank == 1) ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
                resultText.text = $"{statusText}\nНАГРАДА: +{rewardCoins} монет";
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void RestartRace()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public void ExitToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
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
                        playerCar.vehicleRB.linearVelocity = Vector3.zero;
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
                        aiCar.vehicleRB.linearVelocity = Vector3.zero;
                        aiCar.vehicleRB.angularVelocity = Vector3.zero;
                    }
                }

                MonoBehaviour[] allScripts = aiCar.GetComponentsInChildren<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != aiCar && script.gameObject.activeInHierarchy)
                    {
                        if (script.GetType().Name == "PlayerInput") continue;
                        script.enabled = !freeze;
                    }
                }
            }
        }

        private void CreateDefaultPausePanel()
        {
            // 1. Создаем ОТДЕЛЬНЫЙ Canvas для паузы с гарантированными настройками
            GameObject canvasObj = new GameObject("PauseCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Поверх всех остальных Canvas на сцене

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 2. Создаем затемняющий задний фон (Overlay)
            GameObject overlayObj = new GameObject("PauseOverlay");
            overlayObj.transform.SetParent(canvas.transform, false);
            
            UnityEngine.UI.Image overlayImage = overlayObj.AddComponent<UnityEngine.UI.Image>();
            overlayImage.color = new Color(0.02f, 0.02f, 0.03f, 0.65f); // Полупрозрачный темный фон
            
            RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            // 3. Создаем карточку меню (Menu Card) по центру
            GameObject cardObj = new GameObject("PauseCard");
            cardObj.transform.SetParent(overlayObj.transform, false);

            UnityEngine.UI.Image cardImage = cardObj.AddComponent<UnityEngine.UI.Image>();
            cardImage.color = new Color(0.07f, 0.08f, 0.1f, 0.97f); // Темная тема с легким синим оттенком

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(380, 280);
            cardRect.anchoredPosition = Vector2.zero;

            // Добавляем красивую неоновую полоску сверху карточки
            GameObject accentLine = new GameObject("AccentLine");
            accentLine.transform.SetParent(cardObj.transform, false);
            UnityEngine.UI.Image accentImage = accentLine.AddComponent<UnityEngine.UI.Image>();
            accentImage.color = new Color(0.0f, 0.7f, 1.0f, 1.0f); // Яркий неоновый голубой цвет

            RectTransform accentRect = accentLine.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 1);
            accentRect.anchorMax = new Vector2(1, 1);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0, 4);
            accentRect.anchoredPosition = Vector2.zero;

            // 4. Создаем Заголовок "ПАУЗА"
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(cardObj.transform, false);
            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "ПАУЗА";
            titleText.fontSize = 32;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;
            
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(300, 60);
            titleRect.anchoredPosition = new Vector2(0, 70);

            // 5. Создаем кнопку "ПРОДОЛЖИТЬ"
            CreateButton(cardObj.transform, "Btn_Resume", "ПРОДОЛЖИТЬ", new Vector2(0, -10), () => ResumeRace());
            
            // 6. Создаем кнопку "ВЫЙТИ ИЗ ГОНКИ"
            CreateButton(cardObj.transform, "Btn_Exit", "ВЫЙТИ ИЗ ГОНКИ", new Vector2(0, -75), () => ExitToMenuFromPause());

            pausePanel = overlayObj;
            pausePanel.SetActive(false);
        }

        private void CreateButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClickAction)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(280, 45);
            btnRect.anchoredPosition = pos;

            UnityEngine.UI.Image img = btnObj.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.15f, 0.16f, 0.2f, 1.0f); // Спокойный серый цвет кнопки

            UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.16f, 0.2f, 1.0f);
            colors.highlightedColor = new Color(0.0f, 0.5f, 1.0f, 1.0f); // Неоновый голубой при наведении
            colors.pressedColor = new Color(0.0f, 0.35f, 0.7f, 1.0f);
            colors.selectedColor = new Color(0.0f, 0.5f, 1.0f, 1.0f);
            btn.colors = colors;

            btn.onClick.AddListener(onClickAction);

            // Добавляем текст на кнопку
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 15;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.fontStyle = FontStyles.Bold;

            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
        }

        public void PauseRace()
        {
            if (isRaceFinished) return;
            isPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                
                // Выделяем первую кнопку для удобного управления с клавиатуры/геймпада
                UnityEngine.UI.Button firstBtn = pausePanel.GetComponentInChildren<UnityEngine.UI.Button>();
                if (firstBtn != null && UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstBtn.gameObject);
                }
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void ResumeRace()
        {
            isPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void ExitToMenuFromPause()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ExitToMenu();
        }
    }
}
