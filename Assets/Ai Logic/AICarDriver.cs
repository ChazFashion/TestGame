using UnityEngine;
using Ezereal;   // EzerealCarController

[RequireComponent(typeof(EzerealCarController))]
public class AICarDriver : MonoBehaviour
{
    public WaypointContainer waypointContainer;
    public float waypointSwitchDistance = 5f;
    public float maxSpeed = 80f;          // км/ч для бота
    public float cornerSlowdownFactor = 0.5f; // насколько сбрасывать газ в повороте

    EzerealCarController car;
    Transform[] waypoints;
    int currentIndex = 0;

    bool inBrakeZone = false;

    void Awake()
    {
        car = GetComponent<EzerealCarController>();

        if (waypointContainer != null)
            waypoints = waypointContainer.waypoints;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];

        // 1) Переключение на следующий waypoint
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist < waypointSwitchDistance)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            target = waypoints[currentIndex];
        }

        // 2) Направление и угол до точки (в плоскости XZ)
        Vector3 localTarget = transform.InverseTransformPoint(target.position);
        float steer01 = Mathf.Clamp(localTarget.x / localTarget.magnitude, -1f, 1f);

        // 3) Газ / тормоз
        float desiredSpeed = maxSpeed;

        // Если сильно поворачиваем – сбрасываем допустимую скорость
        float steerAbs = Mathf.Abs(steer01);
        if (steerAbs > 0.2f)
            desiredSpeed *= Mathf.Lerp(1f, cornerSlowdownFactor, (steerAbs - 0.2f) / 0.8f);

        // Если в зоне торможения – ещё ниже
        if (inBrakeZone)
            desiredSpeed *= 0.4f;

        float currentSpeed = Mathf.Abs(car.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f);

        float throttle = 0f;
        float brake = 0f;

        if (currentSpeed < desiredSpeed - 2f)
        {
            throttle = 1f;
            brake = 0f;
        }
        else if (currentSpeed > desiredSpeed + 2f)
        {
            throttle = 0f;
            brake = 1f;
        }

        // 4) Отправляем ввод в твой контроллер
        car.SetSteer(steer01);
        car.SetAcceleration(throttle);
        car.SetBrake(brake);
        car.SetHandbrake(0f); // можно включать при сильном повороте, если хочешь дрифт
    }

    // Входим/выходим из зон торможения
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BreakingPoints"))
            inBrakeZone = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("BreakingPoints"))
            inBrakeZone = false;
    }
}
