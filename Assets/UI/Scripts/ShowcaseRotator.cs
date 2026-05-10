using UnityEngine;

namespace RacingUI
{
    public class ShowcaseRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        public float rotationSpeed = 20f; // Скорость автоматического вращения
        public bool autoRotate = true;   // Включить/выключить авто-вращение

        [Header("Optional: Mouse Control")]
        public bool allowMouseDrag = true; // Разрешить вращение мышкой
        public float dragSpeed = 10f;      // Скорость вращения при перетаскивании

        void Update()
        {
            // 1. Автоматическое вращение
            if (autoRotate)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // 2. Вращение мышкой (ЛКМ)
            if (allowMouseDrag && Input.GetMouseButton(0))
            {
                float mouseX = Input.GetAxis("Mouse X");
                
                // Вращаем объект в зависимости от движения мыши
                transform.Rotate(Vector3.up, -mouseX * dragSpeed);
                
                // Временно отключаем авто-вращение, чтобы оно не мешало ручному
                autoRotate = false; 
            }
            
            // Если отпустили кнопку мыши — возвращаем авто-вращение через мгновение
            if (allowMouseDrag && Input.GetMouseButtonUp(0))
            {
                autoRotate = true;
            }
        }
    }
}
