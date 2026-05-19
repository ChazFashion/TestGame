using UnityEngine;
using System.Collections.Generic;

namespace RacingUI
{
    public class WaypointArrow : MonoBehaviour
    {
        [Header("Settings")]
        public Transform arrowModel;      // Для 3D стрелки (можно оставить пустым)
        public RectTransform uiArrow;     // Для стрелки на экране (Канвас)
        public float rotationSpeed = 10f; 
        public float lookDistance = 10f;  

        [Header("Track Data")]
        public Transform waypointContainer; 
        private List<Transform> nodes = new List<Transform>();
        private int currentNodeIndex = 0;

        void Start()
        {
            if (waypointContainer != null)
            {
                foreach (Transform child in waypointContainer)
                {
                    nodes.Add(child);
                }
            }
        }

        void Update()
        {
            if (nodes.Count == 0) return;

            // Синхронизируем цель стрелки с текущим чекпоинтом игрока из RaceManager
            if (RaceManager.Instance != null)
            {
                int nextIndex = RaceManager.Instance.GetPlayerCheckpointIndex();
                if (nextIndex < nodes.Count)
                {
                    currentNodeIndex = nextIndex;
                }
            }

            Transform targetNode = nodes[currentNodeIndex];
            Vector3 direction = targetNode.position - transform.position;

            // --- ЛОГИКА ДЛЯ 3D СТРЕЛКИ ---
            if (arrowModel != null)
            {
                // Стрелка смотрит прямо на цель в 3D
                Quaternion targetRot = Quaternion.LookRotation(direction);
                arrowModel.rotation = Quaternion.Slerp(arrowModel.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // --- ЛОГИКА ДЛЯ UI СТРЕЛКИ (Канвас) ---
            if (uiArrow != null)
            {
                Vector3 localDir = transform.InverseTransformDirection(direction);
                float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                uiArrow.localRotation = Quaternion.Slerp(uiArrow.localRotation, Quaternion.Euler(0, 0, -angle), Time.deltaTime * rotationSpeed);
            }

            // Если гонка не запущена, переключаем чекпоинты просто по дистанции (для свободной езды)
            if (RaceManager.Instance == null)
            {
                Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
                Vector3 flatTarget = new Vector3(targetNode.position.x, 0, targetNode.position.z);

                if (Vector3.Distance(flatPos, flatTarget) < lookDistance)
                {
                    Debug.Log("--- ПРОЙДЕНО (Свободная езда): " + nodes[currentNodeIndex].name + " ---");
                    currentNodeIndex = (currentNodeIndex + 1) % nodes.Count;
                }
            }
        }

        // РИСУЕМ ЛИНИИ В ОКНЕ SCENE ДЛЯ ПРОВЕРКИ
        private void OnDrawGizmos()
        {
            if (waypointContainer == null) return;

            Gizmos.color = Color.yellow;
            Transform lastPoint = null;
            foreach (Transform child in waypointContainer)
            {
                if (lastPoint != null) Gizmos.DrawLine(lastPoint.position, child.position);
                Gizmos.DrawWireSphere(child.position, 1f);
                lastPoint = child;
            }

            // Рисуем линию к текущей цели красным цветом
            if (Application.isPlaying && nodes.Count > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, nodes[currentNodeIndex].position);
            }
        }
    }
}
