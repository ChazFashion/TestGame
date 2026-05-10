using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RacingUI
{
    public class CarStatsUI : MonoBehaviour
    {
        [System.Serializable]
        public struct StatField
        {
            public string label;
            public TMP_Text valueText;
            public Slider barSlider;
            public float maxValue;
        }

        [Header("Car Data")]
        public Ezereal.EzerealCarController currentCar;

        [Header("UI Fields")]
        public StatField hpStat;
        public StatField speedStat;
        public StatField weightStat;

        [Header("Settings")]
        public float animationSpeed = 5f;

        private void OnEnable()
        {
            // Обнуляем полоски при каждом открытии гаража для эффекта анимации
            if (hpStat.barSlider != null) hpStat.barSlider.value = 0;
            if (speedStat.barSlider != null) speedStat.barSlider.value = 0;
            if (weightStat.barSlider != null) weightStat.barSlider.value = 0;
        }

        private void Update()
        {
            if (currentCar == null) return;

            // Плавно подтягиваем значения из скрипта машины к слайдерам
            AnimateStat(hpStat, currentCar.horsePower);
            AnimateStat(speedStat, currentCar.maxForwardSpeed);
            
            // Вес берем из Rigidbody машины
            if (currentCar.vehicleRB != null)
            {
                AnimateStat(weightStat, currentCar.vehicleRB.mass);
            }
        }

        private void AnimateStat(StatField field, float targetValue)
        {
            if (field.barSlider != null)
            {
                // Защита от нулевого MaxValue
                if (field.maxValue <= 0) field.maxValue = 100f; 
                
                field.barSlider.maxValue = field.maxValue;
                field.barSlider.value = Mathf.Lerp(field.barSlider.value, targetValue, Time.deltaTime * animationSpeed);
            }

            if (field.valueText != null)
            {
                // Показываем целое число
                field.valueText.text = Mathf.RoundToInt(targetValue).ToString();
            }
        }
    }
}
