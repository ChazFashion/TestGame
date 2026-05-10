using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.SceneManagement;

namespace RacingUI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Screens (Экраны)")]
        public GameObject garageScreen;
        public GameObject raceSelectScreen;
        public GameObject showRoomScreen;
        public GameObject shopScreen;

        [Header("Auto Selection (Для геймпада)")]
        public GameObject firstRaceButton; // Сюда перетащи Btn_AcceptRace

        [Header("Tab Indicators (Активные полоски)")]
        public GameObject[] tabIndicators;

        [Header("Game HUD (Игровой интерфейс)")]
        public GameObject gameHUD;

        [Header("Menu Parts")]
        public GameObject topHeader;
        public PlayerInput menuInputSource; // Сюда перетащи Player Input именно с Канваса!

        private int currentTabIndex = 0;

        private void Start()
        {
            // Начинаем с первой вкладки
            SelectTab(0);
        }

        // Вызывается при клике на кнопку или через геймпад
        public void SelectTab(int index)
        {
            currentTabIndex = index;

            // 1. Управляем полосками под кнопками
            for (int i = 0; i < tabIndicators.Length; i++)
            {
                if (tabIndicators[i] != null)
                {
                    tabIndicators[i].SetActive(i == index);
                }
            }

            // 2. Переключаем экраны
            switch (index)
            {
                case 0: SwitchScreen(garageScreen, true); break;
                case 1: 
                    SwitchScreen(raceSelectScreen, true); 
                    // Задержка перед выделением кнопки, чтобы избежать ложных нажатий
                    StartCoroutine(SelectButtonDelayed(firstRaceButton));
                    break;
                case 2: SwitchScreen(showRoomScreen, true); break;
                case 3: SwitchScreen(shopScreen, true); break;
            }
        }

        private IEnumerator SelectButtonDelayed(GameObject button)
        {
            // Ждем один кадр, чтобы текущий инпут завершился
            yield return null; 
            
            if (button != null) 
            {
                EventSystem.current.SetSelectedGameObject(button);
            }
        }

        // --- ЛОГИКА ГЕЙМПАДА (LB / RB) ---
        public void OnTabNavigation(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            float direction = context.ReadValue<float>();

            if (direction > 0) // Вправо (RB)
            {
                int next = (currentTabIndex + 1) % 4;
                SelectTab(next);
            }
            else if (direction < 0) // Влево (LB)
            {
                int prev = (currentTabIndex - 1 + 4) % 4;
                SelectTab(prev);
            }
        }

        public void StartRace() 
        {
            // ЗАГРУЗКА СЦЕНЫ ГОНКИ
            SceneManager.LoadScene("Scene_Race_Track1");
        }

        private void SwitchScreen(GameObject newScreen, bool isMenu)
        {
            // 1. Управляем Хедером (верхняя панель)
            if (topHeader != null) topHeader.SetActive(isMenu);
            
            // 2. Управляем Вводом (переключение геймпада между UI и Машиной)
            if (menuInputSource != null) 
            {
                if (isMenu) 
                {
                    menuInputSource.enabled = true;
                    menuInputSource.ActivateInput();
                }
                else 
                {
                    menuInputSource.DeactivateInput();
                    menuInputSource.enabled = false; 
                }
            }

            // 3. Скрываем все экраны меню
            if (garageScreen != null) garageScreen.SetActive(false);
            if (raceSelectScreen != null) raceSelectScreen.SetActive(false);
            if (showRoomScreen != null) showRoomScreen.SetActive(false);
            if (shopScreen != null) shopScreen.SetActive(false);

            // 4. Управляем игровым HUD
            if (gameHUD != null) gameHUD.SetActive(!isMenu);

            // 5. Показываем нужный экран
            if (newScreen != null) newScreen.SetActive(true);
        }
    }
}
