using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RacingUI
{
    public class NitroController : MonoBehaviour
    {
        [Header("Nitro Settings")]
        public float baseDuration = 1f;        // Базовая длительность (ур. 1) в секундах
        public float maxDuration = 5f;         // Максимальная длительность (ур. 5)
        public float baseForce = 7.5f;         // Базовая сила толчка вперед (уменьшена в 2 раза)
        
        [Header("Speed FOV Settings")]
        public float speedFovOffset = 20f;     // Дополнительный FOV на максимальной скорости (сделан более заметным)
        private Ezereal.EzerealCarController carController;
        
        [Header("Current State")]
        public float currentNitro = 100f;      // Баланс нитро в процентах (0..100)
        public bool isNitroActive = false;

        [Header("UI & Visuals")]
        public Slider nitroSlider;
        public float fovLerpSpeed = 5f;

        private Rigidbody rb;
        private Camera mainCam;
        private float baseFOV;
        private float targetFOVOffset = 12f;

        private int nitroLevel = 1;
        private float currentDuration;
        private float currentForce;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = GetComponentInChildren<Rigidbody>();

            carController = GetComponent<Ezereal.EzerealCarController>();
            if (carController == null) carController = GetComponentInChildren<Ezereal.EzerealCarController>();

            mainCam = Camera.main;
            if (mainCam != null)
            {
                baseFOV = mainCam.fieldOfView;
            }

            // Попытка автоматически найти слайдер на сцене по имени
            if (nitroSlider == null)
            {
                GameObject sliderGo = GameObject.Find("Nitro Slider");
                if (sliderGo != null)
                {
                    nitroSlider = sliderGo.GetComponent<Slider>();
                }
            }

            InitializeUpgrades();
            RefillNitro();
        }

        public void InitializeUpgrades()
        {
            if (DataManager.Instance != null)
            {
                int activeCarId = DataManager.Instance.GetActiveCarId();
                var upgrades = DataManager.Instance.GetCarUpgradeLevels(activeCarId);
                if (upgrades.ContainsKey("nitro"))
                {
                    nitroLevel = upgrades["nitro"];
                }
            }

            // Расчет длительности: от 4 сек (ур. 1) до 6 сек (ур. 5)
            float t = (float)(nitroLevel - 1) / 4f; // 0..1
            currentDuration = Mathf.Lerp(baseDuration, maxDuration, t);

            // Расчет силы ускорения: +12% к силе за каждый уровень прокачки
            currentForce = baseForce * (1f + (nitroLevel - 1) * 0.12f);

            Debug.Log($"[NitroController] Инициализировано Нитро Ур. {nitroLevel}: Длительность={currentDuration:F1} сек, Сила={currentForce:F1}");
        }

        private void Update()
        {
            // Считываем ввод (Клавиша F на клавиатуре или Y на геймпаде)
            bool inputPressed = false;

            if (Keyboard.current != null && Keyboard.current.fKey.isPressed)
            {
                inputPressed = true;
            }

            if (!inputPressed && Gamepad.current != null && Gamepad.current.buttonNorth.isPressed)
            {
                inputPressed = true;
            }

            // Проверяем возможность использования
            if (inputPressed && currentNitro > 0f && RaceManager.Instance != null && RaceManager.Instance.isRaceStarted && !RaceManager.Instance.isRaceFinished)
            {
                isNitroActive = true;
            }
            else
            {
                isNitroActive = false;
            }

            // Тратим нитро
            if (isNitroActive)
            {
                // Уменьшаем запас бака
                float drainRate = (100f / currentDuration) * Time.deltaTime;
                currentNitro = Mathf.Max(0f, currentNitro - drainRate);
            }

            // Обновляем слайдер на экране
            if (nitroSlider != null)
            {
                nitroSlider.value = currentNitro / 100f; // Слайдер работает в диапазоне 0..1
            }

            // Эффект отдаления камеры (Field of View zoom)
            UpdateCameraFOV();
        }

        private void FixedUpdate()
        {
            if (isNitroActive && rb != null)
            {
                // Применяем постоянное ускорение вперед (толкаем машину)
                rb.AddForce(transform.forward * currentForce, ForceMode.Acceleration);
            }
        }

        private void UpdateCameraFOV()
        {
            if (mainCam == null) return;

            float speedOffset = 0f;
            if (rb != null)
            {
                float currentSpeed = Vector3.Dot(transform.forward, rb.linearVelocity) * 3.6f;
                currentSpeed = Mathf.Max(0f, currentSpeed); // Игнорируем отрицательную скорость при езде назад
                
                float maxSpeed = 100f;
                if (carController != null)
                {
                    maxSpeed = Mathf.Max(10f, carController.maxForwardSpeed);
                }
                
                float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
                speedOffset = speedRatio * speedFovOffset;
            }

            float targetFOV = baseFOV + speedOffset + (isNitroActive ? targetFOVOffset : 0f);
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        }

        // Вызывается из RaceManager при завершении круга
        public void RefillNitro()
        {
            currentNitro = 100f;
            if (nitroSlider != null)
            {
                nitroSlider.value = 1f;
            }
            Debug.Log("[NitroController] Бак Нитро заправлен на 100%!");
        }
    }
}
