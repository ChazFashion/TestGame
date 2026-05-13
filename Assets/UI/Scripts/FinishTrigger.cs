using UnityEngine;
using Ezereal;

namespace RacingUI
{
    public class FinishTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log("Что-то вошло в финиш: " + other.name);

            // Ищем контроллер во всей иерархии объекта, который вошел в триггер
            EzerealCarController car = other.transform.root.GetComponentInChildren<EzerealCarController>();
            
            if (car != null)
            {
                Debug.Log("Машина обнаружена: " + car.gameObject.name);
                if (car == RaceManager.Instance.playerCar)
                {
                    Debug.Log("ДА! Это игрок. Вызываем финиш.");
                    RaceManager.Instance.FinishRace(1);
                }
                else
                {
                    Debug.Log("Это не игрок. Это: " + car.gameObject.name + ". В RaceManager ждем: " + (RaceManager.Instance.playerCar != null ? RaceManager.Instance.playerCar.name : "null"));
                }
            }
            else
            {
                Debug.Log("Это не машина, на объекте " + other.name + " нет скрипта EzerealCarController");
            }
        }
    }
}
