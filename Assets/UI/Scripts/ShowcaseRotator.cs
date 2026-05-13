using UnityEngine;
using UnityEngine.InputSystem; // Добавляем новую систему ввода

namespace RacingUI
{
    public class ShowcaseRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        public float rotationSpeed = 20f;
        public bool autoRotate = true;

        [Header("Optional: Mouse Control")]
        public bool allowMouseDrag = true;
        public float dragSpeed = 0.5f; // Немного уменьшим для новой системы

        void Update()
        {
            // 1. Автоматическое вращение
            if (autoRotate)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // 2. Вращение мышкой (используем новую систему ввода)
            if (allowMouseDrag && Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed)
                {
                    // Получаем движение мыши из новой системы
                    float mouseX = Mouse.current.delta.x.ReadValue();
                    
                    if (Mathf.Abs(mouseX) > 0.01f)
                    {
                        transform.Rotate(Vector3.up, -mouseX * dragSpeed);
                        autoRotate = false; 
                    }
                }
                
                // Если кнопку отпустили (была нажата в прошлом кадре, а теперь нет)
                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    autoRotate = true;
                }
            }
        }
    }
}
