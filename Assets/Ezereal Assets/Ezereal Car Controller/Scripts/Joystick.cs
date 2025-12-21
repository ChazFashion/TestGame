using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Ezereal
{
    /// <summary>
    /// Простой виртуальный джойстик для мобильного управления
    /// </summary>
    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;
        [SerializeField] private float deadZone = 0f;
        [SerializeField] private bool snapX = false;
        [SerializeField] private bool snapY = false;
        
        [Header("Components")]
        [SerializeField] public RectTransform background = null;
        [SerializeField] public RectTransform handle = null;
        
        private Canvas canvas;
        private Camera cam;
        private RectTransform baseRect = null;
        
        public float Horizontal { get { return input.x; } }
        public float Vertical { get { return input.y; } }
        public Vector2 Direction { get { return new Vector2(Horizontal, Vertical); } }
        
        private Vector2 input = Vector2.zero;

        protected virtual void Start()
        {
            if (background == null)
            {
                background = GetComponent<RectTransform>();
            }
            
            baseRect = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            
            if (canvas != null)
            {
                cam = canvas.worldCamera;
                
                // Убеждаемся, что есть EventSystem для работы с мышью в редакторе
                if (EventSystem.current == null)
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
            
            Vector2 center = new Vector2(0.5f, 0.5f);
            if (background != null)
            {
                background.pivot = center;
            }
            if (handle != null)
            {
                handle.anchorMin = center;
                handle.anchorMax = center;
                handle.pivot = center;
                handle.anchoredPosition = Vector2.zero;
            }
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || canvas == null) return;
            
            cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                cam = canvas.worldCamera;

            Vector2 position = RectTransformUtility.WorldToScreenPoint(cam, background.position);
            Vector2 radius = background.sizeDelta / 2;
            
            // Исправляем расчет для правильной работы с разными разрешениями
            float scaleFactor = canvas.scaleFactor;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                scaleFactor = 1f;
            }
            
            input = (eventData.position - position) / (radius * scaleFactor);
            FormatInput();
            HandleInput(input.magnitude, input.normalized, radius, cam);
            
            if (handle != null)
            {
                handle.anchoredPosition = input * radius * handleRange;
            }
        }

        protected virtual void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
        {
            if (magnitude > deadZone)
            {
                if (magnitude > 1)
                    input = normalised;
            }
            else
                input = Vector2.zero;
        }

        private void FormatInput()
        {
            if (snapX)
                input.x = input.x != 0 ? Mathf.Sign(input.x) : 0;
            if (snapY)
                input.y = input.y != 0 ? Mathf.Sign(input.y) : 0;
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            input = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        protected Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
        {
            Vector2 localPoint = Vector2.zero;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, screenPosition, cam, out localPoint))
            {
                Vector2 pivotOffset = baseRect.pivot * baseRect.sizeDelta;
                return localPoint - (background.anchorMax * baseRect.sizeDelta) + pivotOffset;
            }
            return Vector2.zero;
        }
    }
}

