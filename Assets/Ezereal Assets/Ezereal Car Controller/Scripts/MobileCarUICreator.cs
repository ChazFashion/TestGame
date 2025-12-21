using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Ezereal
{
    /// <summary>
    /// Утилита для автоматического создания UI элементов мобильного управления
    /// </summary>
    public class MobileCarUICreator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool createOnStart = true;
        [SerializeField] private bool enableInEditor = true; // Включить мобильное управление в редакторе для тестирования
        [SerializeField] private Canvas targetCanvas;
        
        [Header("UI Prefabs (Optional)")]
        [SerializeField] private Sprite joystickBackgroundSprite;
        [SerializeField] private Sprite joystickHandleSprite;
        [SerializeField] private Sprite buttonSprite;
        
        [Header("Colors")]
        [SerializeField] private Color accelerateButtonColor = new Color(0.2f, 0.8f, 0.2f, 0.7f); // Зеленый
        [SerializeField] private Color brakeButtonColor = new Color(0.8f, 0.2f, 0.2f, 0.7f); // Красный
        [SerializeField] private Color handbrakeButtonColor = new Color(0.8f, 0.6f, 0.2f, 0.7f); // Оранжевый
        
        private MobileCarController mobileController;
        private Joystick steeringJoystick;
        private Button accelerateButton;
        private Button brakeButton;
        private Button handbrakeButton;

        private void Start()
        {
            #if UNITY_EDITOR
            if (createOnStart && enableInEditor)
            {
                CreateMobileUI();
            }
            #else
            if (createOnStart)
            {
                CreateMobileUI();
            }
            #endif
        }

        [ContextMenu("Create Mobile UI")]
        public void CreateMobileUI()
        {
            // Находим или создаем Canvas
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
                if (targetCanvas == null)
                {
                    GameObject canvasObj = new GameObject("MobileUICanvas");
                    targetCanvas = canvasObj.AddComponent<Canvas>();
                    targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    
                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    
                    canvasObj.AddComponent<GraphicRaycaster>();
                    
                    // Убеждаемся, что есть EventSystem для работы с мышью
                    if (FindFirstObjectByType<EventSystem>() == null)
                    {
                        GameObject eventSystemObj = new GameObject("EventSystem");
                        eventSystemObj.AddComponent<EventSystem>();
                        
                        // Используем правильный Input Module в зависимости от настроек проекта
                        #if ENABLE_INPUT_SYSTEM
                        // Новый Input System
                        eventSystemObj.AddComponent<InputSystemUIInputModule>();
                        #else
                        // Старый Input System
                        eventSystemObj.AddComponent<StandaloneInputModule>();
                        #endif
                    }
                }
            }
            
            // Находим MobileCarController
            mobileController = FindFirstObjectByType<MobileCarController>();
            if (mobileController == null)
            {
                GameObject controllerObj = new GameObject("MobileCarController");
                mobileController = controllerObj.AddComponent<MobileCarController>();
            }
            
            // Создаем джойстик для руля (левая сторона экрана)
            CreateSteeringJoystick();
            
            // Создаем кнопки управления (правая сторона экрана)
            CreateControlButtons();
            
            // Привязываем элементы к контроллеру
            LinkUIElementsToController();
        }

        private void CreateSteeringJoystick()
        {
            // Создаем контейнер для джойстика
            GameObject joystickContainer = new GameObject("SteeringJoystick");
            joystickContainer.transform.SetParent(targetCanvas.transform, false);
            
            RectTransform containerRect = joystickContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(0, 0);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(250, 250); // Увеличил размер для лучшей видимости
            containerRect.anchoredPosition = new Vector2(150, 150);
            
            // Фон джойстика - делаем более заметным для тестирования
            GameObject background = new GameObject("Background");
            background.transform.SetParent(joystickContainer.transform, false);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Более заметный цвет
            if (joystickBackgroundSprite != null)
            {
                bgImage.sprite = joystickBackgroundSprite;
            }
            else
            {
                // Создаем круглый спрайт для фона
                Texture2D bgTexture = new Texture2D(128, 128);
                for (int x = 0; x < 128; x++)
                {
                    for (int y = 0; y < 128; y++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64));
                        if (dist < 64)
                        {
                            bgTexture.SetPixel(x, y, Color.white);
                        }
                        else
                        {
                            bgTexture.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                bgTexture.Apply();
                bgImage.sprite = Sprite.Create(bgTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            }
            
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            // Ручка джойстика - делаем более заметной
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(joystickContainer.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(1, 1, 1, 0.8f); // Более яркая
            if (joystickHandleSprite != null)
            {
                handleImage.sprite = joystickHandleSprite;
            }
            else
            {
                // Создаем круглый спрайт для ручки
                Texture2D handleTexture = new Texture2D(64, 64);
                for (int x = 0; x < 64; x++)
                {
                    for (int y = 0; y < 64; y++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                        if (dist < 32)
                        {
                            handleTexture.SetPixel(x, y, Color.white);
                        }
                        else
                        {
                            handleTexture.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                handleTexture.Apply();
                handleImage.sprite = Sprite.Create(handleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            }
            
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(100, 100); // Увеличил размер ручки
            handleRect.anchoredPosition = Vector2.zero;
            
            // Добавляем компонент Joystick
            steeringJoystick = joystickContainer.AddComponent<Joystick>();
            steeringJoystick.background = bgRect;
            steeringJoystick.handle = handleRect;
            
            // Добавляем GraphicRaycaster для работы с мышью
            if (joystickContainer.GetComponent<GraphicRaycaster>() == null)
            {
                joystickContainer.AddComponent<GraphicRaycaster>();
            }
        }

        private void CreateControlButtons()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float buttonSize = 120f;
            float spacing = 20f;
            
            // Кнопка газа (вверху справа)
            accelerateButton = CreateButton("AccelerateButton", 
                new Vector2(screenWidth - buttonSize - spacing, screenHeight - buttonSize - spacing - 200),
                buttonSize, accelerateButtonColor, "ГАЗ");
            
            // Кнопка тормоза (посередине справа)
            brakeButton = CreateButton("BrakeButton",
                new Vector2(screenWidth - buttonSize - spacing, screenHeight - buttonSize * 2 - spacing * 2 - 200),
                buttonSize, brakeButtonColor, "ТОРМОЗ");
            
            // Кнопка ручного тормоза (внизу справа)
            handbrakeButton = CreateButton("HandbrakeButton",
                new Vector2(screenWidth - buttonSize - spacing, spacing + 100),
                buttonSize, handbrakeButtonColor, "РУЧНОЙ");
        }

        private Button CreateButton(string name, Vector2 position, float size, Color color, string text)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(targetCanvas.transform, false);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            
            Image image = buttonObj.AddComponent<Image>();
            image.color = color;
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
            }
            else
            {
                // Создаем простой круглый спрайт
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                image.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            }
            
            Button button = buttonObj.AddComponent<Button>();
            
            // Добавляем текст
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 24;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            return button;
        }

        private void LinkUIElementsToController()
        {
            if (mobileController == null) return;
            
            // Используем публичные свойства или методы для установки значений
            var controllerType = typeof(MobileCarController);
            
            // Устанавливаем джойстик
            var joystickField = controllerType.GetField("steeringJoystick", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (joystickField != null) joystickField.SetValue(mobileController, steeringJoystick);
            
            // Устанавливаем кнопки
            var accelField = controllerType.GetField("accelerateButton", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (accelField != null) accelField.SetValue(mobileController, accelerateButton);
            
            var brakeField = controllerType.GetField("brakeButton", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (brakeField != null) brakeField.SetValue(mobileController, brakeButton);
            
            var handbrakeField = controllerType.GetField("handbrakeButton", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (handbrakeField != null) handbrakeField.SetValue(mobileController, handbrakeButton);
        }
    }
}

