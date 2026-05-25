using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace RacingUI
{
    public class RaceSettingsUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Выпадающий список для выбора соперника-бота")]
        [SerializeField] private TMP_Dropdown botDropdown;
        
        [Header("Lap Selection Options (Выберите один или оба варианта)")]
        [Tooltip("Текстовое поле ввода для прямого изменения количества кругов")]
        [SerializeField] private TMP_InputField lapsInputField;
        [Tooltip("Текстовый блок для отображения кругов (при использовании кнопок +/-)")]
        [SerializeField] private TMP_Text lapsValueText; 
        
        [Header("Default Settings")]
        [SerializeField] private int minLaps = 1;
        [SerializeField] private int maxLaps = 10;

        private int currentLaps = 3;
        private List<string> botNames = new List<string>();

        private void Start()
        {
            InitializeDropdown();
            InitializeLaps();
        }

        private void InitializeDropdown()
        {
            if (botDropdown == null) return;

            botDropdown.ClearOptions();

            if (DataManager.Instance != null)
            {
                botNames = DataManager.Instance.GetAllBotNames();
            }
            
            // Если база пустая или недоступна, добавим дефолтные имена ботов для стабильности
            if (botNames.Count == 0)
            {
                botNames.Add("Бот Владимир");
                botNames.Add("Бот Сергей");
                botNames.Add("Бот Алекс");
            }

            botDropdown.AddOptions(botNames);

            // Загружаем ранее сохраненное в DataManager имя оппонента
            string selectedBot = "Бот Сергей";
            if (DataManager.Instance != null)
            {
                selectedBot = DataManager.Instance.selectedOpponentBotName;
            }

            int index = botNames.IndexOf(selectedBot);
            if (index >= 0)
            {
                botDropdown.value = index;
            }
            else
            {
                botDropdown.value = 0;
            }

            // Добавляем слушатель изменения значения
            botDropdown.onValueChanged.AddListener(OnBotSelected);
        }

        private void InitializeLaps()
        {
            if (DataManager.Instance != null)
            {
                currentLaps = DataManager.Instance.selectedLapsCount;
            }

            UpdateLapsUI();

            if (lapsInputField != null)
            {
                lapsInputField.text = currentLaps.ToString();
                lapsInputField.onValueChanged.AddListener(OnLapsInputChanged);
            }
        }

        private void OnBotSelected(int index)
        {
            if (index < 0 || index >= botNames.Count) return;
            
            string chosenBot = botNames[index];
            if (DataManager.Instance != null)
            {
                DataManager.Instance.selectedOpponentBotName = chosenBot;
            }
            Debug.Log($"[RaceSettingsUI] Выбран соперник: {chosenBot}");
        }

        private void OnLapsInputChanged(string text)
        {
            if (int.TryParse(text, out int val))
            {
                SetLaps(val);
            }
        }

        // Вызывается из UI кнопкой "+"
        public void IncrementLaps()
        {
            SetLaps(currentLaps + 1);
        }

        // Вызывается из UI кнопкой "-"
        public void DecrementLaps()
        {
            SetLaps(currentLaps - 1);
        }

        private void SetLaps(int value)
        {
            currentLaps = Mathf.Clamp(value, minLaps, maxLaps);
            
            if (DataManager.Instance != null)
            {
                DataManager.Instance.selectedLapsCount = currentLaps;
            }

            UpdateLapsUI();
        }

        private void UpdateLapsUI()
        {
            if (lapsValueText != null)
            {
                lapsValueText.text = currentLaps.ToString();
            }
            
            if (lapsInputField != null && lapsInputField.text != currentLaps.ToString())
            {
                lapsInputField.text = currentLaps.ToString();
            }
        }
    }
}
