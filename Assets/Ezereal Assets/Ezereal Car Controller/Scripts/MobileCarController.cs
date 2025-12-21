using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ezereal
{
    /// <summary>
    /// Мобильный контроллер для управления машиной через тач-экраны
    /// </summary>
    public class MobileCarController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EzerealCarController carController;
        
        [Header("Mobile Controls")]
        [SerializeField] private Joystick steeringJoystick; // Виртуальный джойстик для руля
        [SerializeField] private Button accelerateButton; // Кнопка газа
        [SerializeField] private Button brakeButton; // Кнопка тормоза
        [SerializeField] private Button handbrakeButton; // Кнопка ручного тормоза
        
        [Header("Settings")]
        [SerializeField] private float steeringSensitivity = 1f; // Чувствительность руля
        [SerializeField] private bool useAccelerometer = false; // Использовать акселерометр для руля
        
        // Внутренние переменные
        private float currentAcceleration = 0f;
        private float currentBrake = 0f;
        private float currentSteering = 0f;
        private float currentHandbrake = 0f;

        private void Awake()
        {
            if (carController == null)
            {
                carController = FindFirstObjectByType<EzerealCarController>();
            }
            
            SetupButtons();
        }

        private void SetupButtons()
        {
            // Настройка кнопки газа
            if (accelerateButton != null)
            {
                EventTrigger trigger = accelerateButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = accelerateButton.gameObject.AddComponent<EventTrigger>();
                }
                
                // Нажатие
                EventTrigger.Entry pointerDown = new EventTrigger.Entry();
                pointerDown.eventID = EventTriggerType.PointerDown;
                pointerDown.callback.AddListener((data) => { StartAccelerate(); });
                trigger.triggers.Add(pointerDown);
                
                // Отпускание
                EventTrigger.Entry pointerUp = new EventTrigger.Entry();
                pointerUp.eventID = EventTriggerType.PointerUp;
                pointerUp.callback.AddListener((data) => { StopAccelerate(); });
                trigger.triggers.Add(pointerUp);
            }
            
            // Настройка кнопки тормоза
            if (brakeButton != null)
            {
                EventTrigger trigger = brakeButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = brakeButton.gameObject.AddComponent<EventTrigger>();
                }
                
                EventTrigger.Entry pointerDown = new EventTrigger.Entry();
                pointerDown.eventID = EventTriggerType.PointerDown;
                pointerDown.callback.AddListener((data) => { StartBrake(); });
                trigger.triggers.Add(pointerDown);
                
                EventTrigger.Entry pointerUp = new EventTrigger.Entry();
                pointerUp.eventID = EventTriggerType.PointerUp;
                pointerUp.callback.AddListener((data) => { StopBrake(); });
                trigger.triggers.Add(pointerUp);
            }
            
            // Настройка кнопки ручного тормоза
            if (handbrakeButton != null)
            {
                EventTrigger trigger = handbrakeButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = handbrakeButton.gameObject.AddComponent<EventTrigger>();
                }
                
                EventTrigger.Entry pointerDown = new EventTrigger.Entry();
                pointerDown.eventID = EventTriggerType.PointerDown;
                pointerDown.callback.AddListener((data) => { StartHandbrake(); });
                trigger.triggers.Add(pointerDown);
                
                EventTrigger.Entry pointerUp = new EventTrigger.Entry();
                pointerUp.eventID = EventTriggerType.PointerUp;
                pointerUp.callback.AddListener((data) => { StopHandbrake(); });
                trigger.triggers.Add(pointerUp);
            }
        }

        private void Update()
        {
            // Обработка руля через джойстик
            if (steeringJoystick != null)
            {
                currentSteering = steeringJoystick.Horizontal * steeringSensitivity;
            }
            else if (useAccelerometer && SystemInfo.supportsAccelerometer)
            {
                // Альтернатива: использование акселерометра
                currentSteering = Input.acceleration.x * steeringSensitivity;
            }
            
            // Применение значений к контроллеру машины через рефлексию
            ApplyInputsToCar();
        }

        private void ApplyInputsToCar()
        {
            if (carController == null) return;
            
            // Используем публичные методы для установки значений
            carController.SetAcceleration(currentAcceleration);
            carController.SetBrake(currentBrake);
            carController.SetHandbrake(currentHandbrake);
            carController.SetSteering(currentSteering);
        }

        // Методы для кнопок
        public void StartAccelerate()
        {
            currentAcceleration = 1f;
        }

        public void StopAccelerate()
        {
            currentAcceleration = 0f;
        }

        public void StartBrake()
        {
            currentBrake = 1f;
        }

        public void StopBrake()
        {
            currentBrake = 0f;
        }

        public void StartHandbrake()
        {
            currentHandbrake = 1f;
        }

        public void StopHandbrake()
        {
            currentHandbrake = 0f;
        }
    }
}

