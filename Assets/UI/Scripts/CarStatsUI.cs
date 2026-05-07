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
        public StatField weightStat;
        public StatField speedStat;

        private void Update()
        {
            if (currentCar == null) return;

            // Sync stats from the EzerealCarController
            UpdateStat(hpStat, currentCar.horsePower);
            UpdateStat(speedStat, currentCar.maxForwardSpeed);
            
            // Weight isn't directly in the script but in the Rigidbody
            if (currentCar.vehicleRB != null)
            {
                UpdateStat(weightStat, currentCar.vehicleRB.mass);
            }
        }

        private void UpdateStat(StatField field, float currentValue)
        {
            if (field.valueText != null)
            {
                field.valueText.text = currentValue.ToString("F0");
            }

            if (field.barSlider != null)
            {
                // Smoothly lerp the slider value for that "premium" feel
                field.barSlider.value = Mathf.Lerp(field.barSlider.value, currentValue / field.maxValue, Time.deltaTime * 5f);
            }
        }
    }
}
