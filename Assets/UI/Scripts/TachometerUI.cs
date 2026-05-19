using UnityEngine;
using TMPro;
using Ezereal;

namespace RacingUI
{
    public class TachometerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Ссылка на контроллер машины. Если пустая, найдет игрока автоматически.")]
        [SerializeField] private EzerealCarController carController;
        
        [Tooltip("Стрелка тахометра (RectTransform), которую мы будем вращать.")]
        [SerializeField] private RectTransform needleTransform;
        
        [Tooltip("Цифровой индикатор оборотов (опционально).")]
        [SerializeField] private TMP_Text digitalRPMText;

        [Header("Rotation Settings")]
        [Tooltip("Угол поворота стрелки на 0 оборотах (в градусах).")]
        [SerializeField] private float minRPMAngle = 180f;
        
        [Tooltip("Угол поворота стрелки на максимальных оборотах (в градусах).")]
        [SerializeField] private float maxRPMAngle = -90f;

        [Header("Physical Animation settings")]
        [Tooltip("Насколько плавно движется стрелка (чем выше, тем быстрее и резче).")]
        [SerializeField] private float smoothSpeed = 15f;

        [Tooltip("Амплитуда тряски стрелки при отсечке (эффект вибрации).")]
        [SerializeField] private float limiterWobbleAmount = 3.5f;

        private float currentAngle;

        private void Start()
        {
            // Если ссылка не перетащена в инспекторе, попробуем найти на сцене
            if (carController == null)
            {
                carController = FindObjectOfType<EzerealCarController>();
            }

            if (needleTransform == null)
            {
                // Если повесили скрипт прямо на саму стрелку
                needleTransform = GetComponent<RectTransform>();
            }

            if (carController != null)
            {
                // Стартовый угол
                float targetAngle = GetAngleFromRPM(carController.engineRPM);
                currentAngle = targetAngle;
                needleTransform.localRotation = Quaternion.Euler(0, 0, currentAngle);
            }
        }

        private void Update()
        {
            if (carController == null)
            {
                // Постоянный поиск на случай, если машина спавнится динамически
                carController = FindObjectOfType<EzerealCarController>();
                if (carController == null) return;
            }

            if (needleTransform == null) return;

            // 1. Рассчитываем целевой угол на основе оборотов
            float targetAngle = GetAngleFromRPM(carController.engineRPM);

            // 2. Добавляем агрессивный эффект тряски (вибрации стрелки) на отсечке
            if (carController.isLimiterActive)
            {
                float wobble = Random.Range(-limiterWobbleAmount, limiterWobbleAmount);
                targetAngle += wobble;
            }

            // 3. Плавно интерполируем текущий угол к целевому (симуляция инерции стрелки)
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * smoothSpeed);

            // 4. Поворачиваем стрелку (вращение по оси Z)
            needleTransform.localRotation = Quaternion.Euler(0, 0, currentAngle);

            // 5. Обновляем текстовые данные (если подключены)
            if (digitalRPMText != null)
            {
                digitalRPMText.text = Mathf.RoundToInt(carController.engineRPM).ToString();
            }
        }

        private float GetAngleFromRPM(float rpm)
        {
            // Нормализуем обороты в диапазоне от idleRPM до maxRPM
            float t = Mathf.InverseLerp(0f, carController.maxRPM, rpm);
            
            // Линейно интерполируем угол между минимальным и максимальным значениями
            return Mathf.Lerp(minRPMAngle, maxRPMAngle, t);
        }
    }
}
