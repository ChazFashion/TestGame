using UnityEngine;

namespace RacingUI
{
    public class RaceWaypoint : MonoBehaviour
    {
        [Tooltip("Порядковый номер вейпоинта (0, 1, 2 и т.д.)")]
        public int waypointIndex;

        private void OnTriggerEnter(Collider other)
        {
            // Ищем контроллер машины во всей иерархии вошедшего объекта
            var car = other.transform.root.GetComponentInChildren<Ezereal.EzerealCarController>();
            if (car != null)
            {
                if (RaceManager.Instance != null)
                {
                    RaceManager.Instance.OnCarPassedWaypoint(car, waypointIndex);
                }
            }
        }
    }
}
