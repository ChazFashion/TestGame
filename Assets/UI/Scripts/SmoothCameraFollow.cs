using UnityEngine;

namespace RacingUI
{
    public class SmoothCameraFollow : MonoBehaviour
    {
        [Header("Target to Follow")]
        public Transform target;

        [Header("Positioning Settings")]
        [Tooltip("Дистанция сзади машины")]
        [SerializeField] private float distance = 5.5f;
        [Tooltip("Высота над машиной")]
        [SerializeField] private float height = 2.0f;

        [Header("Damping (Плавность)")]
        [SerializeField] private float rotationDamping = 3.0f;
        [SerializeField] private float heightDamping = 2.0f;

        private void LateUpdate()
        {
            if (target == null) return;

            // Вычисляем целевой угол поворота и высоту
            float wantedRotationAngle = target.eulerAngles.y;
            float wantedHeight = target.position.y + height;

            float currentRotationAngle = transform.eulerAngles.y;
            float currentHeight = transform.position.y;

            // Плавно сглаживаем угол поворота вокруг оси Y
            currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime);

            // Плавно сглаживаем высоту
            currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);

            // Превращаем угол в кватернион поворота
            Quaternion currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);

            // Позиционируем камеру сзади цели на расстоянии distance
            Vector3 targetPosition = target.position;
            targetPosition -= currentRotation * Vector3.forward * distance;

            // Устанавливаем высоту камеры
            targetPosition.y = currentHeight;

            // Применяем позицию камеры
            transform.position = targetPosition;

            // Камера всегда смотрит на цель (машину)
            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
