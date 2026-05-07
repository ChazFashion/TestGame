using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RacingUI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject garageScreen;
        public GameObject leaderboardScreen;
        public GameObject raceSelectionScreen;
        public GameObject shopScreen;

        [Header("Tab Indicators (Твои полоски)")]
        [Tooltip("Перетащи сюда свои объекты ActiveLine по порядку")]
        public GameObject[] tabIndicators;

        [Header("Game HUD")]
        public GameObject gameHUD;

        private void Start()
        {
            // Начинаем с гаража (индекс 0)
            SelectTab(0);
        }

        // Этот метод мы привяжем к кнопкам в инспекторе
        public void SelectTab(int index)
        {
            // 1. Включаем/выключаем полоски
            for (int i = 0; i < tabIndicators.Length; i++)
            {
                if (tabIndicators[i] != null)
                {
                    // Полоска активна только если её индекс совпадает с нажатой кнопкой
                    tabIndicators[i].SetActive(i == index);
                }
            }

            // 2. Переключаем сами экраны
            switch (index)
            {
                case 0: ShowGarage(); break;
                case 1: ShowRaceSelection(); break;
                case 2: ShowShop(); break;
                case 3: ShowLeaderboard(); break;
            }
        }

        public void ShowGarage() => SwitchScreen(garageScreen, true);
        public void ShowLeaderboard() => SwitchScreen(leaderboardScreen, true);
        public void ShowRaceSelection() => SwitchScreen(raceSelectionScreen, true);
        public void ShowShop() => SwitchScreen(shopScreen, true);
        public void StartRace() => SwitchScreen(null, false);

        private void SwitchScreen(GameObject newScreen, bool isMenu)
        {
            if (garageScreen != null) garageScreen.SetActive(false);
            if (leaderboardScreen != null) leaderboardScreen.SetActive(false);
            if (raceSelectionScreen != null) raceSelectionScreen.SetActive(false);
            if (shopScreen != null) shopScreen.SetActive(false);

            if (gameHUD != null) gameHUD.SetActive(!isMenu);
            if (newScreen != null) newScreen.SetActive(true);
        }
    }
}
