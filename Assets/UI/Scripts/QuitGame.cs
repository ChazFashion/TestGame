using UnityEngine;

namespace RacingUI
{
    public class QuitGame : MonoBehaviour
    {
        /// <summary>
        /// Вызывается при нажатии кнопки "Выход".
        /// В редакторе останавливает Play Mode, в билде закрывает приложение.
        /// </summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            Debug.Log("[QuitGame] Выход из игры.");
        }
    }
}
