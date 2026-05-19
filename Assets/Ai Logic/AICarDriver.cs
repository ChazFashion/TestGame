using UnityEngine;
using Ezereal;   // EzerealCarController
using RacingUI;  // DataManager
using AI;        // CatmullRomSpline

[RequireComponent(typeof(EzerealCarController))]
public class AICarDriver : MonoBehaviour
{
    [Header("Waypoints")]
    public WaypointContainer waypointContainer;
    
    [Header("AI Profile (Загружается из БД по имени)")]
    public string botName = "Бот Владимир";
    [Range(0f, 1f)] public float skill = 0.5f;       // Навык вождения (0 = новичок, 1 = профи)
    [Range(0f, 1f)] public float aggression = 0.5f;  // Агрессия (0 = осторожный, 1 = рискованный)

    [Header("Performance Settings")]
    public float maxSpeed = 90f;             // Максимальная скорость по прямой (км/ч)
    [Tooltip("Коэффициент снижения скорости в крутых поворотах (0.3 = снизить до 30%).")]
    public float cornerSlowdownFactor = 0.35f;

    [Header("Look-Ahead Settings")]
    [Tooltip("Дистанция упреждения взгляда бота на минимальной скорости.")]
    public float minLookAheadDistance = 6f;
    [Tooltip("Дистанция упреждения взгляда бота на максимальной скорости.")]
    public float maxLookAheadDistance = 25f;

    [Header("Unstuck System (Система Вызволения)")]
    [SerializeField] private bool isStuck = false;
    [SerializeField] private float stuckTimer = 0f;
    [SerializeField] private float unstuckTimer = 0f;
    [Tooltip("Время (сек) буксования на месте перед включением заднего хода.")]
    public float stuckThresholdTime = 0.8f; // Уменьшено с 1.8f для быстрой реакции
    [Tooltip("Длительность езды задом при сдаче назад.")]
    public float unstuckDuration = 1.5f;

    [Header("AI Physics Tweaks")]
    [Tooltip("Множитель сцепления шин для ИИ (1.0 = обычный зацеп, 1.6 = повышенный зацеп как на рельсах).")]
    public float aiGripBoost = 1.6f;
    [Tooltip("Сила прижимного эффекта к дороге (Downforce). Предотвращает переворачивание машины на высоких скоростях.")]
    public float downforce = 5.0f;
    [Tooltip("Вертикальное смещение центра масс (отрицательное значение прижимает центр тяжести ближе к земле).")]
    public float centerOfMassOffset = -0.5f;

    [Header("Debug Info")]
    [SerializeField] private float currentSpeedKmh = 0f;
    [SerializeField] private float targetSpeedKmh = 0f;
    [SerializeField] private float currentDistanceAlongSpline = 0f;
    [SerializeField] private bool hasLoadedFromDB = false;

    private EzerealCarController car;
    private Rigidbody rb;
    private CatmullRomSpline spline;

    // Первоначальные параметры сцепления колес
    private float originalFrontSidewaysStiffness = 1f;
    private float originalFrontForwardStiffness = 1f;
    private float originalRearSidewaysStiffness = 1f;
    private float originalRearForwardStiffness = 1f;

    // Точки для визуализации в Gizmos
    private Vector3 debugClosestPoint;
    private Vector3 debugTargetPoint;

    // Переменная для детектора застревания и сглаживания руля
    private float lastThrottle = 0f;
    private float lastSteerInput = 0f;

    void Awake()
    {
        car = GetComponent<EzerealCarController>();
        rb = GetComponent<Rigidbody>();

        // Обязательно переводим бота в режим автомата
        if (car != null)
        {
            car.transmissionMode = EzerealCarController.TransmissionModes.Automatic;
        }
    }

    void Start()
    {
        LoadProfileFromDatabase();
        InitializeSpline();

        if (car != null)
        {
            if (car.frontLeftWheelCollider != null)
            {
                originalFrontSidewaysStiffness = car.frontLeftWheelCollider.sidewaysFriction.stiffness;
                originalFrontForwardStiffness = car.frontLeftWheelCollider.forwardFriction.stiffness;
            }
            if (car.rearLeftWheelCollider != null)
            {
                originalRearSidewaysStiffness = car.rearLeftWheelCollider.sidewaysFriction.stiffness;
                originalRearForwardStiffness = car.rearLeftWheelCollider.forwardFriction.stiffness;
            }
        }

        // Физическая стабилизация: снижаем центр масс машины, чтобы высокий зацеп колес
        // не вызывал переворачивание кузова на бок в крутых поворотах.
        if (rb != null)
        {
            Vector3 com = rb.centerOfMass;
            com.y += centerOfMassOffset;
            rb.centerOfMass = com;
        }
    }

    private void LoadProfileFromDatabase()
    {
        DataManager dm = FindObjectOfType<DataManager>();
        if (dm != null)
        {
            var profile = dm.GetBotProfileByName(botName);
            if (profile != null)
            {
                skill = System.Convert.ToSingle(profile["skill"]);
                aggression = System.Convert.ToSingle(profile["aggression"]);
                hasLoadedFromDB = true;
                
                // Загружаем расширенные физические характеристики напрямую из БД
                if (profile.ContainsKey("max_speed")) 
                    maxSpeed = System.Convert.ToSingle(profile["max_speed"]);
                else
                    maxSpeed = Mathf.Lerp(75f, 110f, skill);

                if (profile.ContainsKey("grip_boost")) 
                    aiGripBoost = System.Convert.ToSingle(profile["grip_boost"]);

                if (profile.ContainsKey("downforce")) 
                    downforce = System.Convert.ToSingle(profile["downforce"]);

                if (profile.ContainsKey("handling_offset")) 
                    centerOfMassOffset = System.Convert.ToSingle(profile["handling_offset"]);

                Debug.Log($"[AICarDriver] Загружен профиль бота '{botName}' из БД: " +
                          $"Skill={skill:F2}, Aggression={aggression:F2}, MaxSpeed={maxSpeed:F1} км/ч, " +
                          $"GripBoost={aiGripBoost:F2}, Downforce={downforce:F2}, CenterOfMassOffset={centerOfMassOffset:F2}");
            }
        }
    }

    private void InitializeSpline()
    {
        if (waypointContainer != null && waypointContainer.waypoints != null && waypointContainer.waypoints.Length > 2)
        {
            spline = new CatmullRomSpline(waypointContainer.waypoints);
        }
        else if (waypointContainer != null)
        {
            // Если массив в WaypointContainer еще не заполнился в Awake,
            // попробуем собрать дочерние объекты напрямую
            int count = waypointContainer.transform.childCount;
            Transform[] wps = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                wps[i] = waypointContainer.transform.GetChild(i);
            }
            if (count > 2)
            {
                spline = new CatmullRomSpline(wps);
            }
        }

        if (spline == null)
        {
            Debug.LogError($"[AICarDriver] Не удалось инициализировать сплайн для {gameObject.name}. Проверьте вейпоинты!");
        }
    }

    void FixedUpdate()
    {
        if (spline == null) return;

        // 1. Текущая скорость бота в км/ч + применение прижимной силы
        if (rb != null)
        {
            currentSpeedKmh = rb.linearVelocity.magnitude * 3.6f;

            // Физический прижим (Downforce): сила прижима растет пропорционально скорости автомобиля.
            // Это удерживает машину от опрокидывания и сохраняет максимальный контакт с дорогой.
            float speedMs = rb.linearVelocity.magnitude;
            Vector3 downForceVector = -transform.up * speedMs * rb.mass * (downforce * 0.05f);
            rb.AddForce(downForceVector, ForceMode.Force);
        }

        // 2. НАХОЖДЕНИЕ МАШИНЫ НА СПЛАЙНЕ (Проекция)
        // ВАЖНО: Делаем проекцию на сплайн в самом начале кадра!
        // Это гарантирует, что даже во время сдачи назад ИИ знает актуальное положение дороги и стен!
        currentDistanceAlongSpline = spline.GetClosestDistance(transform.position, out debugClosestPoint);

        // 3. ДЕТЕКТОР ЗАСТРЕВАНИЯ И ВЫЗВОЛЕНИЕ (Unstuck State Machine)
        if (isStuck)
        {
            unstuckTimer += Time.fixedDeltaTime;
            
            if (car != null)
            {
                car.transmissionMode = EzerealCarController.TransmissionModes.Automatic;
                car.currentGear = AutomaticGears.Reverse;

                var lightCtrl = car.GetComponentInChildren<EzerealLightController>();
                if (lightCtrl != null) lightCtrl.ReverseLightsOn();

                // Тормозим колеса в первые 0.15 сек после удара, чтобы погасить инерцию вращения вперед
                if (unstuckTimer < 0.15f)
                {
                    car.SetAcceleration(0f);
                    car.SetBrake(1.0f);
                }
                else
                {
                    car.SetAcceleration(1.0f);
                    car.SetBrake(0f);
                }

                // Рулим в противоположную сторону от траектории (актуально, т.к. debugClosestPoint обновляется!)
                Vector3 localClose = transform.InverseTransformPoint(debugClosestPoint);
                float escapeSteer = -Mathf.Sign(localClose.x);
                car.SetSteer(escapeSteer);

                // ФИЗИЧЕСКИЙ ХЕЛПЕР (Для выхода из мертвых зон):
                // Если машина сдает назад, но скорость все еще нулевая (застряла в коллайдере стены/стыке террейна),
                // мы прикладываем плавный дополнительный импульс силы назад, чтобы вытолкнуть физическое тело из зацепления.
                if (rb != null && unstuckTimer > 0.3f && currentSpeedKmh < 1.5f)
                {
                    rb.AddForce(-transform.forward * rb.mass * 4f, ForceMode.Force);
                }
            }

            if (unstuckTimer > 2.0f) // Увеличили длительность сдачи назад до 2.0 сек
            {
                isStuck = false;
                unstuckTimer = 0f;
                stuckTimer = 0f;

                if (car != null)
                {
                    car.currentGear = AutomaticGears.Drive;
                    var lightCtrl = car.GetComponentInChildren<EzerealLightController>();
                    if (lightCtrl != null) lightCtrl.ReverseLightsOff();
                }
                Debug.Log($"[AICarDriver] {botName} успешно выехал задом!");
            }

            return; // Прерываем обычное движение во время езды назад
        }

        // Проверяем застревание
        if (lastThrottle > 0.3f && currentSpeedKmh < 2.5f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > stuckThresholdTime)
            {
                isStuck = true;
                unstuckTimer = 0f;
                stuckTimer = 0f;
                Debug.Log($"[AICarDriver] {botName} уткнулся в препятствие! Сдаем задом.");
                return;
            }
        }
        else
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime);
        }

        // 4. МНОГОТОЧЕЧНЫЙ АНАЛИЗ КРИВИЗНЫ ВПЕРЕДИ (Curvature Lookup)
        // Чтобы исключить слепые зоны и тормозить заранее с высоких скоростей,
        // расстояние сканирования увеличивается динамически от 25 до 55 метров в зависимости от скорости.
        float speedPercent = Mathf.InverseLerp(0f, maxSpeed, currentSpeedKmh);
        float sensorDistance = Mathf.Lerp(25f, 55f, speedPercent);

        float maxCurvatureAhead = 0f;
        Vector3 roadDirHere = (spline.GetPointAtDistance(currentDistanceAlongSpline + 2.0f) - debugClosestPoint).normalized;

        for (float d = 3f; d <= sensorDistance; d += 3.5f)
        {
            Vector3 samplePos = spline.GetPointAtDistance(currentDistanceAlongSpline + d);
            Vector3 samplePosAhead = spline.GetPointAtDistance(currentDistanceAlongSpline + d + 2.0f);
            Vector3 sampleDir = (samplePosAhead - samplePos).normalized;

            float angle = Vector3.Angle(roadDirHere, sampleDir);
            if (angle > maxCurvatureAhead)
            {
                maxCurvatureAhead = angle;
            }
        }

        // 5. РАСЧЕТ ДИНАМИЧЕСКОГО УПРЕЖДЕНИЯ (Look-Ahead)
        float baseLookAhead = Mathf.Lerp(minLookAheadDistance, maxLookAheadDistance, speedPercent);

        // Если впереди крутой поворот, сжимаем взгляд, чтобы точнее идти по дуге.
        // Ограничили сжатие до 0.65f (вместо 0.45f), чтобы взгляд не "прыгал" слишком близко к капоту.
        float lookAheadDist = baseLookAhead;
        if (maxCurvatureAhead > 12f)
        {
            float curveFactor = Mathf.InverseLerp(12f, 60f, maxCurvatureAhead);
            lookAheadDist *= Mathf.Lerp(1.0f, 0.65f, curveFactor);
        }

        // Получаем целевую точку на сплайне впереди по трассе
        float targetDistance = currentDistanceAlongSpline + lookAheadDist;
        debugTargetPoint = spline.GetPointAtDistance(targetDistance);

        // 6. РУЛЕНИЕ (Плавное угловое следование)
        Vector3 localTargetPoint = transform.InverseTransformPoint(debugTargetPoint);
        float angleToTarget = Mathf.Atan2(localTargetPoint.x, localTargetPoint.z) * Mathf.Rad2Deg;
        
        // Нормализованный целевой угол руления в диапазоне [-1, 1]. Убрали множитель 1.4f, чтобы исключить избыточную поворачиваемость.
        float targetSteer = angleToTarget / (car != null ? car.maxSteerAngle : 30f);
        targetSteer = Mathf.Clamp(targetSteer, -1f, 1f);

        // СГЛАЖИВАНИЕ РУЛЯ (Steering Rate Limiter):
        // Ограничиваем скорость поворота колес (повысили до 4.5 в сек, чтобы быстрее возвращать руль прямо на выходе из поворота).
        // Это предотвращает запаздывание руления и уберегает от налета на стену после шпильки.
        float steerInput = Mathf.MoveTowards(lastSteerInput, targetSteer, Time.fixedDeltaTime * 4.5f);
        lastSteerInput = steerInput;

        // 7. РАСЧЕТ СКОРОСТИ И ТОРМОЖЕНИЯ (Процентное соотношение от Max Speed)
        float speedMultiplier = 1f;

        if (maxCurvatureAhead > 10f)
        {
            // Рассчитываем процент сброса скорости на основе изгиба сплайна.
            // t меняется от 0 (угол 10°) до 1 (угол 95°)
            float t = Mathf.InverseLerp(10f, 95f, maxCurvatureAhead);
            t = Mathf.Pow(t, 1.4f); // Нелинейность торможения

            // Целевой множитель скорости: от 0.85 (пологий поворот) до 0.20 (крутая шпилька)
            float targetPercent = Mathf.Lerp(0.85f, 0.20f, t);
            speedMultiplier = Mathf.Lerp(1f, targetPercent, t);
        }

        targetSpeedKmh = maxSpeed * speedMultiplier;

        // Корректируем скорость в зависимости от навыка (skill) бота
        float skillModifier = Mathf.Lerp(0.85f, 1.15f, skill);
        targetSpeedKmh *= skillModifier;

        // Лимитируем торможение агрессивных ботов
        if (aggression > 0.6f && maxCurvatureAhead > 15f)
        {
            targetSpeedKmh *= Mathf.Lerp(1f, 1.15f, aggression);
        }
        
        targetSpeedKmh = Mathf.Max(targetSpeedKmh, 15f); // Безопасный абсолютный минимум (км/ч)

        // 8. ПИ-КОНТРОЛЛЕР ГАЗА И ТОРМОЗА (с распределением сцепления ABS/ESC и трекшн-контролем)
        float speedError = targetSpeedKmh - currentSpeedKmh;
        float throttleInput = 0f;
        float brakeInput = 0f;

        if (speedError > 0f)
        {
            throttleInput = Mathf.Clamp01(speedError / 8f);

            // ТРЕКШН-КОНТРОЛЛЕР (Traction Control в поворотах):
            // Если колеса сильно повернуты (рулим > 0.15), принудительно урезаем газ.
            // Это решает проблему резкого ускорения бота внутри шпильки до того, как он закончил маневр.
            float steerFactor = Mathf.Abs(steerInput);
            if (steerFactor > 0.15f)
            {
                // На максимальном вывороте руля (1.0) газ урезается до 30%, на прямых - 100%
                throttleInput *= Mathf.Lerp(1.0f, 0.3f, (steerFactor - 0.15f) / 0.85f);
            }

            brakeInput = 0f;
        }
        else
        {
            throttleInput = 0f;
            brakeInput = Mathf.Clamp01(-speedError / 5f);

            // ИМИТАЦИЯ СИСТЕМЫ СТАБИЛИЗАЦИИ (ESC / ABS):
            // Если колеса сильно повернуты (рулим > 0.35), снижаем тормозное усилие прямо в повороте.
            // Это перераспределяет сцепление шин в пользу поперечного ведения (поворота) и спасает от разворота задней оси.
            float steerMagnitude = Mathf.Abs(steerInput);
            if (steerMagnitude > 0.35f)
            {
                float reductionT = Mathf.InverseLerp(0.35f, 0.9f, steerMagnitude);
                brakeInput *= Mathf.Lerp(1.0f, 0.2f, reductionT);
            }
        }

        lastThrottle = throttleInput;

        // ПОСТОЯННЫЙ ЗАЦЕП ШИН ДЛЯ ИИ (AI Tire Friction Boost):
        // Применяем постоянный повышенный зацеп (боковой и продольный), чтобы избежать резких скачков
        // сцепления при выходе из шпилек и исключить сильный занос при полном газе.
        if (car != null)
        {
            SetTireFriction(
                originalFrontSidewaysStiffness * aiGripBoost,
                originalFrontForwardStiffness * aiGripBoost,
                originalRearSidewaysStiffness * aiGripBoost,
                originalRearForwardStiffness * aiGripBoost
            );
        }

        // 9. ПЕРЕДАЧА УПРАВЛЕНИЯ В КОНТРОЛЛЕР
        if (car != null)
        {
            car.SetSteer(steerInput);
            car.SetAcceleration(throttleInput);
            car.SetBrake(brakeInput);
            car.SetHandbrake(0f);
        }
    }

    private void SetTireFriction(float frontSideways, float frontForward, float rearSideways, float rearForward)
    {
        if (car == null) return;
        
        if (car.frontLeftWheelCollider != null)
        {
            var sf = car.frontLeftWheelCollider.sidewaysFriction;
            sf.stiffness = frontSideways;
            car.frontLeftWheelCollider.sidewaysFriction = sf;

            var ff = car.frontLeftWheelCollider.forwardFriction;
            ff.stiffness = frontForward;
            car.frontLeftWheelCollider.forwardFriction = ff;
        }
        if (car.frontRightWheelCollider != null)
        {
            var sf = car.frontRightWheelCollider.sidewaysFriction;
            sf.stiffness = frontSideways;
            car.frontRightWheelCollider.sidewaysFriction = sf;

            var ff = car.frontRightWheelCollider.forwardFriction;
            ff.stiffness = frontForward;
            car.frontRightWheelCollider.forwardFriction = ff;
        }
        if (car.rearLeftWheelCollider != null)
        {
            var sf = car.rearLeftWheelCollider.sidewaysFriction;
            sf.stiffness = rearSideways;
            car.rearLeftWheelCollider.sidewaysFriction = sf;

            var ff = car.rearLeftWheelCollider.forwardFriction;
            ff.stiffness = rearForward;
            car.rearLeftWheelCollider.forwardFriction = ff;
        }
        if (car.rearRightWheelCollider != null)
        {
            var sf = car.rearRightWheelCollider.sidewaysFriction;
            sf.stiffness = rearSideways;
            car.rearRightWheelCollider.sidewaysFriction = sf;

            var ff = car.rearRightWheelCollider.forwardFriction;
            ff.stiffness = rearForward;
            car.rearRightWheelCollider.forwardFriction = ff;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spline == null)
        {
            // В Edit Mode собираем дочерние объекты напрямую, т.к. массив waypoints в инспекторе может быть не отсортирован
            if (waypointContainer != null)
            {
                int count = waypointContainer.transform.childCount;
                if (count > 2)
                {
                    Transform[] wps = new Transform[count];
                    for (int i = 0; i < count; i++)
                    {
                        wps[i] = waypointContainer.transform.GetChild(i);
                    }
                    CatmullRomSpline tempSpline = new CatmullRomSpline(wps);
                    tempSpline.DrawGizmos(new Color(0.1f, 0.8f, 0.2f, 0.4f));
                }
            }
            return;
        }

        // 1. Отрисовка траектории сплайна (светящаяся зеленая линия)
        spline.DrawGizmos(Color.green);

        // 2. Ближайшая проекция бота на сплайне (Красный шарик)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(debugClosestPoint, 0.6f);
        Gizmos.DrawLine(transform.position, debugClosestPoint);

        // 3. Точка взгляда бота на сплайне впереди (Синий шарик)
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(debugTargetPoint, 0.8f);
        Gizmos.DrawLine(transform.position, debugTargetPoint);
    }
}
