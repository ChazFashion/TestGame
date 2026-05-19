using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// Класс для построения и работы со сплайном Кэтмулла-Рома на основе массива точек.
    /// Использует Arc-Length Parameterization (параметризацию по длине дуги) для абсолютно
    /// равномерного движения по сплайну вне зависимости от расстояния между исходными вейпоинтами.
    /// </summary>
    public class CatmullRomSpline
    {
        private readonly List<Vector3> rawPoints = new List<Vector3>();
        private readonly List<Vector3> splinePoints = new List<Vector3>(); // Точки сплайна, распределенные с равным шагом
        private readonly List<float> pointDistances = new List<float>();    // Накопленная дистанция для каждой точки
        
        public float TotalLength { get; private set; }
        public int SplinePointsCount => splinePoints.Count;

        // Настройки построения
        private const int InterpolationStepsPerSegment = 20; // Сколько шагов расчета делать между исходными WP
        private const float TargetSpacing = 1.0f;           // Дистанция между точками сплайна в метрах для стабильной полилинии

        /// <summary>
        /// Инициализирует сплайн на основе массива точек.
        /// </summary>
        public CatmullRomSpline(Transform[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 3)
            {
                Debug.LogError("[CatmullRomSpline] Для построения сплайна необходимо минимум 3 вейпоинта!");
                return;
            }

            foreach (var wp in waypoints)
            {
                if (wp != null) rawPoints.Add(wp.position);
            }

            GenerateSpline();
        }

        private void GenerateSpline()
        {
            int n = rawPoints.Count;
            List<Vector3> densePoints = new List<Vector3>();

            // 1. Рассчитываем густую сетку точек сплайна
            for (int i = 0; i < n; i++)
            {
                // Для циклического сплайна берем 4 контрольные точки:
                Vector3 p0 = rawPoints[(i - 1 + n) % n];
                Vector3 p1 = rawPoints[i];
                Vector3 p2 = rawPoints[(i + 1) % n];
                Vector3 p3 = rawPoints[(i + 2) % n];

                for (int step = 0; step < InterpolationStepsPerSegment; step++)
                {
                    float t = (float)step / InterpolationStepsPerSegment;
                    Vector3 position = EvaluateCatmullRom(p0, p1, p2, p3, t);
                    densePoints.Add(position);
                }
            }

            // Добавляем самую первую точку в конец для замыкания петли
            densePoints.Add(densePoints[0]);

            // 2. Делаем Arc-Length Parameterization (перераспределяем точки с шагом в 1 метр)
            splinePoints.Clear();
            pointDistances.Clear();

            Vector3 currentPos = densePoints[0];
            splinePoints.Add(currentPos);
            pointDistances.Add(0f);

            float accumulatedDistance = 0f;
            float leftOverDistance = 0f;

            for (int i = 0; i < densePoints.Count - 1; i++)
            {
                Vector3 pA = densePoints[i];
                Vector3 pB = densePoints[i + 1];
                float segmentLength = Vector3.Distance(pA, pB);
                
                accumulatedDistance += segmentLength;

                float segmentProgress = 0f;
                while (leftOverDistance + segmentProgress < segmentLength)
                {
                    float distanceToEvaluate = TargetSpacing - leftOverDistance;
                    segmentProgress += distanceToEvaluate;
                    
                    float t = segmentProgress / segmentLength;
                    currentPos = Vector3.Lerp(pA, pB, t);

                    splinePoints.Add(currentPos);
                    pointDistances.Add(splinePoints.Count * TargetSpacing);
                    
                    leftOverDistance = 0f;
                }

                leftOverDistance = segmentLength - segmentProgress;
            }

            TotalLength = splinePoints.Count * TargetSpacing;
            Debug.Log($"[CatmullRomSpline] Успешно построен замкнутый сплайн. Точек: {splinePoints.Count}, Длина: {TotalLength:F1} м.");
        }

        /// <summary>
        /// Формула расчета точки Кэтмулла-Рома для параметра t [0..1]
        /// </summary>
        private Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float f0 = -0.5f * t3 + t2 - 0.5f * t;
            float f1 = 1.5f * t3 - 2.5f * t2 + 1.0f;
            float f2 = -1.5f * t3 + 2.0f * t2 + 0.5f * t;
            float f3 = 0.5f * t3 - 0.5f * t2;

            return p0 * f0 + p1 * f1 + p2 * f2 + p3 * f3;
        }

        /// <summary>
        /// Возвращает 3D-позицию на сплайне на определенном расстоянии (в метрах) от старта.
        /// </summary>
        public Vector3 GetPointAtDistance(float distance)
        {
            if (splinePoints.Count == 0) return Vector3.zero;

            // Зацикливаем дистанцию по длине сплайна
            distance = Mathf.Repeat(distance, TotalLength);

            // Так как точки распределены с шагом TargetSpacing (1м),
            // мы можем мгновенно найти индексы без бинарного поиска!
            float rawIndex = distance / TargetSpacing;
            int idx1 = Mathf.FloorToInt(rawIndex) % splinePoints.Count;
            int idx2 = (idx1 + 1) % splinePoints.Count;
            
            float t = rawIndex - Mathf.Floor(rawIndex);
            return Vector3.Lerp(splinePoints[idx1], splinePoints[idx2], t);
        }

        /// <summary>
        /// Находит индекс ближайшей точки сплайна к заданной позиции в пространстве
        /// и возвращает пройденное расстояние вдоль сплайна (в метрах).
        /// </summary>
        public float GetClosestDistance(Vector3 position, out Vector3 closestPoint)
        {
            closestPoint = Vector3.zero;
            if (splinePoints.Count == 0) return 0f;

            float minDistanceSq = float.MaxValue;
            int closestIndex = 0;

            // Находим грубо ближайшую точку
            for (int i = 0; i < splinePoints.Count; i++)
            {
                float distSq = (splinePoints[i] - position).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestIndex = i;
                }
            }

            // Уточняем позицию между найденной точкой и ее соседями
            int prevIdx = (closestIndex - 1 + splinePoints.Count) % splinePoints.Count;
            int nextIdx = (closestIndex + 1) % splinePoints.Count;

            Vector3 pPrev = splinePoints[prevIdx];
            Vector3 pCurr = splinePoints[closestIndex];
            Vector3 pNext = splinePoints[nextIdx];

            Vector3 projectOnPrev = ProjectPointOnSegment(position, pPrev, pCurr, out float tPrev);
            Vector3 projectOnNext = ProjectPointOnSegment(position, pCurr, pNext, out float tNext);

            float distPrevSq = (projectOnPrev - position).sqrMagnitude;
            float distNextSq = (projectOnNext - position).sqrMagnitude;

            float finalDistance;
            if (distPrevSq < distNextSq)
            {
                closestPoint = projectOnPrev;
                finalDistance = (closestIndex - 1 + tPrev) * TargetSpacing;
            }
            else
            {
                closestPoint = projectOnNext;
                finalDistance = (closestIndex + tNext) * TargetSpacing;
            }

            return Mathf.Repeat(finalDistance, TotalLength);
        }

        private Vector3 ProjectPointOnSegment(Vector3 point, Vector3 start, Vector3 end, out float t)
        {
            Vector3 ab = end - start;
            Vector3 ap = point - start;
            float abLenSq = ab.sqrMagnitude;
            
            if (abLenSq < 0.0001f)
            {
                t = 0f;
                return start;
            }

            t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abLenSq);
            return start + ab * t;
        }

        /// <summary>
        /// Отрисовывает сплайн в редакторе Unity в виде красивой линии.
        /// </summary>
        public void DrawGizmos(Color pathColor)
        {
            if (splinePoints.Count < 2) return;

            Gizmos.color = pathColor;
            for (int i = 0; i < splinePoints.Count; i++)
            {
                Vector3 p1 = splinePoints[i];
                Vector3 p2 = splinePoints[(i + 1) % splinePoints.Count];
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
