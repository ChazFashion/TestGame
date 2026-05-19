using UnityEngine;
using Ezereal;

namespace RacingUI
{
    public class FinishTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Ищем контроллер во всей иерархии объекта, который вошел в триггер
            EzerealCarController car = other.transform.root.GetComponentInChildren<EzerealCarController>();
            
            if (car != null)
            {
                Debug.Log($"[FinishTrigger] Машина пересекла линию финиша: {car.gameObject.name}");
                if (RaceManager.Instance != null)
                {
                    RaceManager.Instance.OnCarCrossedFinish(car);
                }
            }
        }
    }
}
