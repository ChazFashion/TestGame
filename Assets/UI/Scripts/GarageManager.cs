using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace RacingUI
{
    public class GarageManager : MonoBehaviour
    {
        public static GarageManager Instance;

        // Будет использовать DataManager.Instance.carPrefabs для сопоставления префабов.

        [Header("3 Car Slots Settings")]
        [Tooltip("Кнопки/Панели для отображения 3-х слотов машин")]
        [SerializeField] private GameObject[] carSlots;
        
        [Tooltip("Объекты рамки/подсветки выбранного слота")]
        [SerializeField] private GameObject[] slotHighlights;
        
        [Tooltip("Текстовые поля названий машин в слотах")]
        [SerializeField] private TMP_Text[] slotNameTexts;

        [Header("UI References")]
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text carNameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private Button actionButton; // Кнопка КУПИТЬ / ВЫБРАТЬ
        [SerializeField] private TMP_Text actionButtonText;
        [SerializeField] private CarStatsUI statsUI;

        [Header("Showroom Settings")]
        [SerializeField] private Transform podiumParent; // Объект Canvas/Garage/Podium
        [SerializeField] private float showcaseHeightOffset = 0.5f; // Сдвиг машины по высоте на подиуме

        [Header("Upgrade Sliders")]
        [SerializeField] private Slider engineSlider;
        [SerializeField] private Slider handlingSlider;
        [SerializeField] private Slider nitroSlider;

        [Header("Upgrade Buttons")]
        [SerializeField] private Button upgradeEngineButton;
        [SerializeField] private Button upgradeHandlingButton;
        [SerializeField] private Button upgradeNitroButton;

        [Header("Upgrade Cost Texts")]
        [SerializeField] private TMP_Text engineCostText;
        [SerializeField] private TMP_Text handlingCostText;
        [SerializeField] private TMP_Text nitroCostText;

        [Header("Max Upgrade Levels")]
        [SerializeField] private int maxUpgradeLevel = 5;
        [SerializeField] private int baseUpgradeCost = 150;

        private List<Dictionary<string, object>> carsInLibrary = new List<Dictionary<string, object>>();
        private int currentCarIndex = 0;
        private GameObject instantiatedModel;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            RefreshGarageData();
        }

        private bool rightStickReset = true;

        private void Update()
        {
            // 1. Управление правым джойстиком (Right Stick) на геймпаде
            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                Vector2 rightStick = UnityEngine.InputSystem.Gamepad.current.rightStick.ReadValue();

                // Порог срабатывания, чтобы перелистывать только при явном отклонении стика
                if (Mathf.Abs(rightStick.x) > 0.6f)
                {
                    if (rightStickReset)
                    {
                        if (rightStick.x > 0)
                        {
                            NextCar();
                        }
                        else
                        {
                            PrevCar();
                        }
                        rightStickReset = false; // Блокируем до возврата в центр
                    }
                }
                else
                {
                    rightStickReset = true; // Стик вернулся в центр
                }

                // 2. Кнопка 'A' (buttonSouth) для выбора / активации машины
                if (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    OnActionButtonClicked();
                }
            }
        }

        // Вызывается по клику на конкретный слот (мышкой)
        public void SelectCarSlot(int index)
        {
            if (carsInLibrary.Count == 0) return;
            if (index < 0 || index >= carsInLibrary.Count) return;
            currentCarIndex = index;
            
            if (DataManager.Instance != null)
            {
                int carId = System.Convert.ToInt32(carsInLibrary[currentCarIndex]["id"]);
                bool isUnlocked = System.Convert.ToBoolean(carsInLibrary[currentCarIndex]["is_unlocked"]);
                if (isUnlocked)
                {
                    DataManager.Instance.SetActiveCarId(carId);
                }
            }

            UpdateSelectedCarUI();
        }

        public void RefreshGarageData()
        {
            if (DataManager.Instance == null)
            {
                Debug.LogError("[GarageManager] DataManager.Instance не найден на сцене!");
                return;
            }

            // 1. Считываем данные из БД
            carsInLibrary = DataManager.Instance.GetAllCarsWithStatus();
            int activeCarId = DataManager.Instance.GetActiveCarId();

            // 2. Находим индекс активной машины в списке
            for (int i = 0; i < carsInLibrary.Count; i++)
            {
                if (System.Convert.ToInt32(carsInLibrary[i]["id"]) == activeCarId)
                {
                    currentCarIndex = i;
                    break;
                }
            }

            // 3. Обновляем экран
            UpdateSelectedCarUI();
            UpdateCoinsUI();
        }

        public void UpdateCoinsUI()
        {
            if (DataManager.Instance != null)
            {
                if (coinsText != null)
                {
                    coinsText.text = DataManager.Instance.GetCoins().ToString();
                }
                if (gemsText != null)
                {
                    gemsText.text = DataManager.Instance.GetGems().ToString();
                }
            }
        }

        public void NextCar()
        {
            if (carsInLibrary.Count == 0) return;
            currentCarIndex = (currentCarIndex + 1) % carsInLibrary.Count;
            
            if (DataManager.Instance != null)
            {
                int carId = System.Convert.ToInt32(carsInLibrary[currentCarIndex]["id"]);
                bool isUnlocked = System.Convert.ToBoolean(carsInLibrary[currentCarIndex]["is_unlocked"]);
                if (isUnlocked)
                {
                    DataManager.Instance.SetActiveCarId(carId);
                }
            }

            UpdateSelectedCarUI();
        }

        public void PrevCar()
        {
            if (carsInLibrary.Count == 0) return;
            currentCarIndex = (currentCarIndex - 1 + carsInLibrary.Count) % carsInLibrary.Count;
            
            if (DataManager.Instance != null)
            {
                int carId = System.Convert.ToInt32(carsInLibrary[currentCarIndex]["id"]);
                bool isUnlocked = System.Convert.ToBoolean(carsInLibrary[currentCarIndex]["is_unlocked"]);
                if (isUnlocked)
                {
                    DataManager.Instance.SetActiveCarId(carId);
                }
            }

            UpdateSelectedCarUI();
        }

        private void UpdateSelectedCarUI()
        {
            if (carsInLibrary.Count == 0) return;

            // Обновляем визуальное выделение и имена в слотах
            if (carSlots != null)
            {
                for (int i = 0; i < carSlots.Length; i++)
                {
                    if (carSlots[i] == null) continue;

                    if (i < carsInLibrary.Count)
                    {
                        carSlots[i].SetActive(true);
                        
                        if (slotNameTexts != null && i < slotNameTexts.Length && slotNameTexts[i] != null)
                        {
                            slotNameTexts[i].text = carsInLibrary[i]["name"].ToString().ToUpper();
                        }

                        if (slotHighlights != null && i < slotHighlights.Length && slotHighlights[i] != null)
                        {
                            slotHighlights[i].SetActive(i == currentCarIndex);
                        }
                    }
                    else
                    {
                        carSlots[i].SetActive(false);
                    }
                }
            }

            var car = carsInLibrary[currentCarIndex];
            int carId = System.Convert.ToInt32(car["id"]);
            string name = car["name"].ToString();
            string rarity = car["rarity"].ToString();
            int price = System.Convert.ToInt32(car["base_price"]);
            string prefabName = car["prefab_name"].ToString();
            bool isUnlocked = System.Convert.ToBoolean(car["is_unlocked"]);

            // Имя и редкость
            if (carNameText != null) carNameText.text = name.ToUpper();
            if (rarityText != null)
            {
                rarityText.text = rarity.ToUpper();
                // Раскраска редкости
                if (rarity.ToLower() == "legendary") rarityText.color = new Color(1f, 0.6f, 0f); // Оранжевый
                else if (rarity.ToLower() == "epic") rarityText.color = new Color(0.7f, 0.2f, 1f); // Фиолетовый
                else if (rarity.ToLower() == "rare") rarityText.color = new Color(0f, 0.6f, 1f); // Синий
                else rarityText.color = Color.gray;
            }

            // Спавним 3D модель на подиуме
            SpawnShowcaseModel(prefabName);

            // Передаем характеристики в CarStatsUI (мощность, скорость, масса) с учетом модели и прокачки
            if (statsUI != null)
            {
                var mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == prefabName);
                
                // Базовые параметры берем напрямую из базы данных
                float baseHp = car.ContainsKey("base_hp") ? System.Convert.ToSingle(car["base_hp"]) : 100f;
                float baseSpeed = car.ContainsKey("base_speed") ? System.Convert.ToSingle(car["base_speed"]) : 120f;
                float baseMass = car.ContainsKey("base_weight") ? System.Convert.ToSingle(car["base_weight"]) : 1200f;

                // Переопределяем параметры из инспектора DataManager, если они заданы пользователем
                if (mapping.baseHorsePower > 0f) baseHp = mapping.baseHorsePower;
                if (mapping.baseMaxSpeed > 0f) baseSpeed = mapping.baseMaxSpeed;
                if (mapping.baseMass > 0f) baseMass = mapping.baseMass;

                // Считаем влияние улучшений
                var levels = DataManager.Instance.GetCarUpgradeLevels(carId);
                int engine = levels["engine"];
                int handling = levels["handling"];

                float engineMultiplier = 1f + (engine - 1) * 0.15f;
                float finalHp = baseHp * engineMultiplier;
                float finalSpeed = baseSpeed * (1f + (engine - 1) * 0.08f);

                float massMultiplier = 1f - (handling - 1) * 0.05f;
                float finalMass = baseMass * massMultiplier;

                statsUI.UpdateStats(finalHp, finalSpeed, finalMass);
            }

            // Кнопка выбора/покупки
            int activeCarId = DataManager.Instance.GetActiveCarId();
            if (actionButton != null)
            {
                actionButton.interactable = true;

                if (isUnlocked)
                {
                    if (carId == activeCarId)
                    {
                        if (actionButtonText != null) actionButtonText.text = "ВЫБРАНО";
                        actionButton.interactable = false; // Уже выбрана
                    }
                    else
                    {
                        if (actionButtonText != null) actionButtonText.text = "ВЫБРАТЬ";
                    }
                }
                else
                {
                    if (actionButtonText != null) actionButtonText.text = $"КУПИТЬ: {price} 🪙";
                    int playerCoins = DataManager.Instance.GetCoins();
                    if (playerCoins < price)
                    {
                        // Делаем кнопку неактивной, если не хватает денег
                        if (actionButtonText != null) actionButtonText.text = $"НЕДОСТАТОЧНО МОНЕТ ({price})";
                        actionButton.interactable = false;
                    }
                }
            }

            // Обновляем панель прокачки
            UpdateUpgradePanel(carId, isUnlocked);
        }

        private void UpdateUpgradePanel(int carId, bool isUnlocked)
        {
            var levels = DataManager.Instance.GetCarUpgradeLevels(carId);
            int engine = levels["engine"];
            int handling = levels["handling"];
            int nitro = levels["nitro"];

            // Заполнение слайдеров
            if (engineSlider != null)
            {
                engineSlider.maxValue = maxUpgradeLevel;
                engineSlider.value = engine;
            }
            if (handlingSlider != null)
            {
                handlingSlider.maxValue = maxUpgradeLevel;
                handlingSlider.value = handling;
            }
            if (nitroSlider != null)
            {
                nitroSlider.maxValue = maxUpgradeLevel;
                nitroSlider.value = nitro;
            }

            // Проверка возможности улучшения
            ConfigureUpgradeButton(upgradeEngineButton, engineCostText, "engine", engine, isUnlocked);
            ConfigureUpgradeButton(upgradeHandlingButton, handlingCostText, "handling", handling, isUnlocked);
            ConfigureUpgradeButton(upgradeNitroButton, nitroCostText, "nitro", nitro, isUnlocked);
        }

        private void ConfigureUpgradeButton(Button btn, TMP_Text costText, string statName, int currentLevel, bool isUnlocked)
        {
            if (btn == null) return;

            // Обновляем текст с текущим уровнем
            TMP_Text buttonText = btn.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                string baseName = "Улучшить";
                if (statName == "engine") baseName = "Улучшить двигатель";
                else if (statName == "handling") baseName = "Улучшить управляемость";
                else if (statName == "nitro") baseName = "Улучшить нитро";

                buttonText.text = $"{baseName} (Ур. {currentLevel}/{maxUpgradeLevel})";
            }

            // Если машина заблокирована, нельзя улучшать
            if (!isUnlocked)
            {
                btn.interactable = false;
                if (costText != null) costText.text = "Блокировка";
                return;
            }

            // Если достигнут максимум
            if (currentLevel >= maxUpgradeLevel)
            {
                btn.interactable = false;
                if (costText != null) costText.text = "МАКС.";
                return;
            }

            int cost = CalculateUpgradeCost(currentLevel);
            if (costText != null) costText.text = $"{cost}";

            int playerCoins = DataManager.Instance.GetCoins();
            btn.interactable = playerCoins >= cost;
        }

        private int CalculateUpgradeCost(int currentLevel)
        {
            return baseUpgradeCost * currentLevel; // 1->2: 150, 2->3: 300, 3->4: 450, 4->5: 600
        }

        // Вызывается по клику на actionButton
        public void OnActionButtonClicked()
        {
            if (carsInLibrary.Count == 0) return;

            var car = carsInLibrary[currentCarIndex];
            int carId = System.Convert.ToInt32(car["id"]);
            int price = System.Convert.ToInt32(car["base_price"]);
            bool isUnlocked = System.Convert.ToBoolean(car["is_unlocked"]);

            if (isUnlocked)
            {
                // Просто выбираем машину
                DataManager.Instance.SetActiveCarId(carId);
                Debug.Log($"[GarageManager] Машина ID={carId} выбрана в качестве активной.");
                RefreshGarageData();
            }
            else
            {
                // Покупаем машину
                if (DataManager.Instance.BuyCar(carId, price))
                {
                    // Автоматически выбираем её после покупки
                    DataManager.Instance.SetActiveCarId(carId);
                    Debug.Log($"[GarageManager] Успешная покупка машины ID={carId}!");
                    RefreshGarageData();
                }
                else
                {
                    Debug.LogWarning("[GarageManager] Не удалось купить машину (возможно, не хватает монет).");
                }
            }
        }

        // Вызовы кнопок прокачки
        public void UpgradeEngine()
        {
            Debug.Log("[GarageManager] Нажата кнопка улучшения двигателя");
            TriggerUpgrade("engine");
        }

        public void UpgradeHandling()
        {
            Debug.Log("[GarageManager] Нажата кнопка улучшения управляемости");
            TriggerUpgrade("handling");
        }

        public void UpgradeNitro()
        {
            Debug.Log("[GarageManager] Нажата кнопка улучшения нитро");
            TriggerUpgrade("nitro");
        }

        private void TriggerUpgrade(string statName)
        {
            var car = carsInLibrary[currentCarIndex];
            int carId = System.Convert.ToInt32(car["id"]);
            bool isUnlocked = System.Convert.ToBoolean(car["is_unlocked"]);

            Debug.Log($"[GarageManager] Попытка улучшения {statName} для машины ID={carId}. Разблокирована ли: {isUnlocked}");

            if (!isUnlocked)
            {
                Debug.LogWarning("[GarageManager] Нельзя улучшать закрытую машину!");
                return;
            }

            var levels = DataManager.Instance.GetCarUpgradeLevels(carId);
            int currentLevel = levels[statName.ToLower()];
            int cost = CalculateUpgradeCost(currentLevel);
            int playerCoins = DataManager.Instance.GetCoins();

            Debug.Log($"[GarageManager] Текущий уровень: {currentLevel}, Стоимость улучшения: {cost}, Монет у игрока: {playerCoins}");

            if (DataManager.Instance.UpgradeCarStat(carId, statName, cost))
            {
                Debug.Log($"[GarageManager] Характеристика {statName} успешно улучшена!");
                RefreshGarageData();
                
                // Передаем обновленные параметры физики в CarStatsUI (если он активен)
                if (statsUI != null && instantiatedModel != null)
                {
                    var controller = instantiatedModel.GetComponent<Ezereal.EzerealCarController>();
                    if (controller == null) controller = instantiatedModel.GetComponentInChildren<Ezereal.EzerealCarController>();
                    // Обновляем параметры демонстрационной машины на лету
                    ApplyUpgradesToController(controller, carId);
                }
            }
            else
            {
                Debug.LogError($"[GarageManager] База данных вернула ошибку при улучшении {statName}!");
            }
        }

        // Применить параметры апгрейда к EzerealCarController
        public void ApplyUpgradesToController(Ezereal.EzerealCarController car, int carId)
        {
            if (car == null) return;

            var levels = DataManager.Instance.GetCarUpgradeLevels(carId);
            int engine = levels["engine"];
            int handling = levels["handling"];
            int nitro = levels["nitro"];

            // 1. Двигатель (HorsePower и Max Speed)
            // Базовые параметры увеличиваются на +15% за уровень
            float engineMultiplier = 1f + (engine - 1) * 0.15f;
            car.horsePower = car.horsePower * engineMultiplier;
            car.maxForwardSpeed = car.maxForwardSpeed * (1f + (engine - 1) * 0.08f);

            // 2. Управляемость (Масса и жесткость)
            // Снижаем массу на -5% за уровень (облегчение кузова)
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                float massMultiplier = 1f - (handling - 1) * 0.05f;
                rb.mass = rb.mass * massMultiplier;
            }

            // Повышаем сцепление колес
            // Коэффициент Friction в EzerealWheelFrictionController (если есть) увеличивается на +10%
            var frictionController = car.GetComponent<Ezereal.EzerealWheelFrictionController>();
            if (frictionController == null) frictionController = car.GetComponentInChildren<Ezereal.EzerealWheelFrictionController>();
            if (frictionController != null)
            {
                // Прокачка сцепления
                float gripMultiplier = 1f + (handling - 1) * 0.1f;
                // В зависимости от реализации ассета, можно применить этот множитель к параметрам скольжения
                Debug.Log($"[GarageManager] Применено улучшение сцепления: x{gripMultiplier}");
            }
            
            Debug.Log($"[GarageManager] Настроены параметры авто ID={carId}: Мощность={car.horsePower:F1} HP, Макс.Скорость={car.maxForwardSpeed:F1}");
        }

        private void SpawnShowcaseModel(string prefabName)
        {
            if (instantiatedModel != null)
            {
                Destroy(instantiatedModel);
            }

            if (DataManager.Instance == null) return;

            DataManager.CarPrefabMapping mapping = DataManager.Instance.carPrefabs.Find(m => m.prefabName == prefabName);
            if (mapping.menuShowcasePrefab != null)
            {
                // Находим исходный объект Player, чтобы скопировать координаты
                Transform placeholder = podiumParent.Find("Player");
                Vector3 pos = Vector3.zero;
                Quaternion rot = Quaternion.identity;
                Vector3 scale = Vector3.one;

                if (placeholder != null)
                {
                    pos = placeholder.localPosition;
                    rot = placeholder.localRotation;
                    scale = placeholder.localScale;
                    
                    placeholder.gameObject.SetActive(false); // Отключаем плейсхолдер
                }

                // Спавним демонстрационную модель
                instantiatedModel = Instantiate(mapping.menuShowcasePrefab, podiumParent);
                instantiatedModel.name = "ShowcaseCar_" + prefabName;
                instantiatedModel.transform.localPosition = pos + new Vector3(0f, showcaseHeightOffset, 0f);
                instantiatedModel.transform.localRotation = rot;
                instantiatedModel.transform.localScale = scale;

                // Отключаем физику на демонстрационной модели
                Rigidbody rb = instantiatedModel.GetComponent<Rigidbody>();
                if (rb == null) rb = instantiatedModel.GetComponentInChildren<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                Collider[] colliders = instantiatedModel.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = false;
                }

                // Удаляем аудиослушатели и камеры на демонстрационной модели, чтобы не было дубликатов в меню
                AudioListener[] listeners = instantiatedModel.GetComponentsInChildren<AudioListener>();
                foreach (var listener in listeners)
                {
                    Destroy(listener);
                }

                Camera[] cameras = instantiatedModel.GetComponentsInChildren<Camera>();
                foreach (var cam in cameras)
                {
                    cam.enabled = false;
                }

                // Отключаем управляющие скрипты, чтобы машина не пыталась ехать в меню
                MonoBehaviour[] scripts = instantiatedModel.GetComponentsInChildren<MonoBehaviour>();
                foreach (var script in scripts)
                {
                    if (script != this && !(script is ShowcaseRotator))
                    {
                        script.enabled = false;
                    }
                }

                // Если подключен CarStatsUI, передаем ему ссылку на эту машину для отрисовки графиков
                CarStatsUI statsUI = FindAnyObjectByType<CarStatsUI>();
                if (statsUI != null)
                {
                    var controller = instantiatedModel.GetComponent<Ezereal.EzerealCarController>();
                    if (controller == null) controller = instantiatedModel.GetComponentInChildren<Ezereal.EzerealCarController>();
                    
                    if (controller != null)
                    {
                        // Применяем улучшения к параметрам демонстрационной машины
                        int carId = System.Convert.ToInt32(carsInLibrary[currentCarIndex]["id"]);
                        ApplyUpgradesToController(controller, carId);
                        
                        statsUI.currentCar = controller;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[GarageManager] Не найдено сопоставление префаба подиума для: " + prefabName);
            }
        }
    }
}
