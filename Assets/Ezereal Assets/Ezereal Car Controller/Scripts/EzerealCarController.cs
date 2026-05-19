using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

namespace Ezereal
{
    public class EzerealCarController : MonoBehaviour // This is the main system resposible for car control.
    {
        [Header("Ezereal References")]
        [SerializeField] EzerealLightController ezerealLightController;
        [SerializeField] EzerealSoundController ezerealSoundController;
        [SerializeField] EzerealWheelFrictionController ezerealWheelFrictionController;

        [Header("References")]
        public Rigidbody vehicleRB;
        public WheelCollider frontLeftWheelCollider;
        public WheelCollider frontRightWheelCollider;
        public WheelCollider rearLeftWheelCollider;
        public WheelCollider rearRightWheelCollider;
        WheelCollider[] wheels;

        [SerializeField] Transform frontLeftWheelMesh;
        [SerializeField] Transform frontRightWheelMesh;
        [SerializeField] Transform rearLeftWheelMesh;
        [SerializeField] Transform rearRightWheelMesh;

        [SerializeField] Transform steeringWheel;

        [SerializeField] TMP_Text currentGearTMP_UI;
        [SerializeField] TMP_Text currentGearTMP_Dashboard;

        [SerializeField] TMP_Text currentSpeedTMP_UI;
        [SerializeField] TMP_Text currentSpeedTMP_Dashboard;
        [SerializeField] Slider accelerationSlider;

        [Header("Settings")]
        public bool isStarted = true;

        public float maxForwardSpeed = 100f; // 100f default
        public float maxReverseSpeed = 30f; // 30f default
        public float horsePower = 1000f; // 1000f default
        public float brakePower = 2000f; // 2000f default
        public float handbrakeForce = 3000f; // 3000f default
        public float maxSteerAngle = 30f; // 30f default
        public float steeringSpeed = 5f; // 5f default
        public float stopThreshold = 1f; // 1f default. At what speed car will make a full stop
        public float decelerationSpeed = 0.5f; // 0.5f default
        public float maxSteeringWheelRotation = 360f; // 360 for real steering wheel. 120 would be more suitable for racing.

        [Header("Drive Type")]
        public DriveTypes driveType = DriveTypes.RWD;

        [Header("Gearbox")]
        public AutomaticGears currentGear = AutomaticGears.Drive;

        public enum TransmissionModes { Automatic, Manual }
        [Header("Transmission Settings")]
        public TransmissionModes transmissionMode = TransmissionModes.Manual;
        public int currentManualGear = 0; // -1 = R, 0 = N, 1..5 = Forward gears

        [Header("Engine RPM Settings")]
        public float engineRPM = 1000f;
        public float maxRPM = 7000f;
        public float idleRPM = 1000f;
        public float redlineRPM = 6000f;
        public bool isLimiterActive = false;
        private float limiterTime = 0f;
        private bool isLimitingState = false;

        [Header("Debug Info")]
        public bool stationary = true;
        [SerializeField] float currentSpeed = 0f;
        [SerializeField] float currentAccelerationValue = 0f;
        [SerializeField] float currentBrakeValue = 0f;
        [SerializeField] float currentHandbrakeValue = 0f;
        [SerializeField] float currentSteerAngle = 0f;
        [SerializeField] float targetSteerAngle = 0f;
        [SerializeField] float FrontLeftWheelRPM = 0f;
        [SerializeField] float FrontRightWheelRPM = 0f;
        [SerializeField] float RearLeftWheelRPM = 0f;
        [SerializeField] float RearRightWheelRPM = 0f;

        [SerializeField] float speedFactor = 0f; // Leave at zero. Responsible for smooth acceleration and near-top-speed slowdown.

        #region Public input API (for player & AI)

        // 0..1
        public void SetAcceleration(float value)
        {
            currentAccelerationValue = Mathf.Clamp01(value);
        }

        // 0..1
        public void SetBrake(float value)
        {
            currentBrakeValue = Mathf.Clamp01(value);
        }

        // 0..1
        public void SetHandbrake(float value)
        {
            currentHandbrakeValue = Mathf.Clamp01(value);
        }

        // -1..1
        public void SetSteer(float value)
        {
            value = Mathf.Clamp(value, -1f, 1f);
            targetSteerAngle = value * maxSteerAngle;
        }

        #endregion

        private void Awake()
        {
            wheels = new WheelCollider[]
            {
                frontLeftWheelCollider,
                frontRightWheelCollider,
                rearLeftWheelCollider,
                rearRightWheelCollider,
            };

            if (ezerealLightController == null)
            {
                Debug.LogWarning("EzerealLightController reference is missing. Ignore or attach one if you want to have light controls.");
            }

            if (ezerealSoundController == null)
            {
                Debug.LogWarning("EzerealSoundController reference is missing. Ignore or attach one if you want to have engine sounds.");
            }

            if (ezerealWheelFrictionController == null)
            {
                Debug.LogWarning("EzerealWheelFrictionController reference is missing. Ignore or attach one if you want to have friction controls.");
            }

            if (vehicleRB == null)
            {
                Debug.LogError("VehicleRB reference is missing for EzerealCarController!");
            }

            if (isStarted)
            {
                Debug.Log("Car is started.");

                if (ezerealLightController != null)
                {
                    ezerealLightController.MiscLightsOn();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOnEngineSound();
                }
            }

            // Инициализация отображения коробки передач при старте
            if (transmissionMode == TransmissionModes.Manual)
            {
                UpdateManualGearText();
            }
            else
            {
                UpdateGearText(currentGear);
            }
        }

        void OnStartCar()
        {
            isStarted = !isStarted;

            if (isStarted)
            {
                Debug.Log("Car started.");

                if (ezerealLightController != null)
                {
                    ezerealLightController.MiscLightsOn();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOnEngineSound();
                }
            }
            else
            {
                Debug.Log("Car turned off");

                if (ezerealLightController != null)
                {
                    ezerealLightController.AllLightsOff();
                }

                if (ezerealSoundController != null)
                {
                    ezerealSoundController.TurnOffEngineSound();
                }

                frontLeftWheelCollider.motorTorque = 0;
                frontRightWheelCollider.motorTorque = 0;
                rearLeftWheelCollider.motorTorque = 0;
                rearRightWheelCollider.motorTorque = 0;
            }
        }

        #region Input System callbacks (player)

        void OnAccelerate(InputValue accelerationValue)
        {
            SetAcceleration(accelerationValue.Get<float>());
            // Debug.Log("Acceleration: " + currentAccelerationValue.ToString());
        }

        void OnBrake(InputValue brakeValue)
        {
            SetBrake(brakeValue.Get<float>());
            // Debug.Log("Brake:" + currentBrakeValue.ToString());

            if (isStarted && ezerealLightController != null)
            {
                if (currentBrakeValue > 0)
                {
                    ezerealLightController.BrakeLightsOn();
                }
                else
                {
                    ezerealLightController.BrakeLightsOff();
                }
            }
        }

        void OnHandbrake(InputValue handbrakeValue)
        {
            SetHandbrake(handbrakeValue.Get<float>());

            if (isStarted)
            {
                if (currentHandbrakeValue > 0)
                {
                    if (ezerealWheelFrictionController != null)
                    {
                        ezerealWheelFrictionController.StartDrifting(currentHandbrakeValue);
                    }

                    if (ezerealLightController != null)
                    {
                        ezerealLightController.HandbrakeLightOn();
                    }
                }
                else
                {
                    if (ezerealWheelFrictionController != null)
                    {
                        ezerealWheelFrictionController.StopDrifting();
                    }

                    if (ezerealLightController != null)
                    {
                        ezerealLightController.HandbrakeLightOff();
                    }
                }
            }
        }

        void OnSteer(InputValue turnValue)
        {
            SetSteer(turnValue.Get<float>());
        }

        #endregion

        #region Core behaviour

        void Acceleration()
        {
            if (!isStarted) return;

            if (transmissionMode == TransmissionModes.Automatic)
            {
                if (currentGear == AutomaticGears.Drive)
                {
                    speedFactor = Mathf.InverseLerp(0, maxForwardSpeed, currentSpeed);
                    float torqueMultiplier = Mathf.Lerp(12f, 3f, speedFactor);
                    float currentMotorTorque = horsePower * torqueMultiplier * (1f - speedFactor);

                    ApplyTorqueToDriveWheels(currentMotorTorque * currentAccelerationValue);
                }
                else if (currentGear == AutomaticGears.Reverse)
                {
                    if (currentAccelerationValue > 0f && currentSpeed > -maxReverseSpeed)
                    {
                        float accel = 1f;
                        float reverseTorque = horsePower * 8f;
                        ApplyTorqueToDriveWheels(-accel * reverseTorque);
                    }
                    else
                    {
                        ClearWheelTorque();
                    }
                }
                else
                {
                    ClearWheelTorque();
                }
            }
            else // MANUAL TRANSMISSION MODE
            {
                if (currentManualGear == 0) // Neutral
                {
                    ClearWheelTorque();
                }
                else if (currentManualGear == -1) // Reverse
                {
                    if (currentAccelerationValue > 0f && currentSpeed > -maxReverseSpeed)
                    {
                        float accel = 1f;
                        float reverseTorque = horsePower * 8f;
                        ApplyTorqueToDriveWheels(-accel * reverseTorque);
                    }
                    else
                    {
                        ClearWheelTorque();
                    }
                }
                else // Forward gears (1 to 5)
                {
                    float gearMaxSpeed = GetManualGearMaxSpeed(currentManualGear);
                    float gearTorqueMultiplier = GetManualGearTorqueMultiplier(currentManualGear);

                    // Рассчитываем обороты/скорость для текущей передачи
                    float gearSpeedFactor = Mathf.InverseLerp(0, gearMaxSpeed, currentSpeed);

                    float currentMotorTorque = 0f;
                    // Если не превысили лимит оборотов передачи
                    if (currentSpeed < gearMaxSpeed)
                    {
                        currentMotorTorque = horsePower * gearTorqueMultiplier * (1f - gearSpeedFactor);
                    }

                    if (currentAccelerationValue > 0f)
                    {
                        ApplyTorqueToDriveWheels(currentMotorTorque * currentAccelerationValue);
                    }
                    else
                    {
                        ClearWheelTorque();
                    }
                }
            }

            UpdateAccelerationSlider();
        }

        private void ApplyTorqueToDriveWheels(float torque)
        {
            if (driveType == DriveTypes.RWD)
            {
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
            }
            else if (driveType == DriveTypes.FWD)
            {
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
            }
            else if (driveType == DriveTypes.AWD)
            {
                frontLeftWheelCollider.motorTorque = torque;
                frontRightWheelCollider.motorTorque = torque;
                rearLeftWheelCollider.motorTorque = torque;
                rearRightWheelCollider.motorTorque = torque;
            }
        }

        private void ClearWheelTorque()
        {
            frontLeftWheelCollider.motorTorque = 0;
            frontRightWheelCollider.motorTorque = 0;
            rearLeftWheelCollider.motorTorque = 0;
            rearRightWheelCollider.motorTorque = 0;
        }

        private float GetManualGearMaxSpeed(int gear)
        {
            switch (gear)
            {
                case 1: return maxForwardSpeed * 0.25f; // 25% от макс. скорости
                case 2: return maxForwardSpeed * 0.45f; // 45%
                case 3: return maxForwardSpeed * 0.68f; // 68%
                case 4: return maxForwardSpeed * 0.88f; // 88%
                case 5: return maxForwardSpeed;         // 100%
                default: return maxForwardSpeed;
            }
        }

        private float GetManualGearTorqueMultiplier(int gear)
        {
            switch (gear)
            {
                case 1: return 16f; // Пушечный разгон с места
                case 2: return 9.5f;
                case 3: return 6f;
                case 4: return 4f;
                case 5: return 2.5f; // Для поддержания максимальной скорости
                default: return 1f;
            }
        }

        void UpdateManualGearText()
        {
            string gearText = "N";
            if (currentManualGear == -1) gearText = "R";
            else if (currentManualGear == 0) gearText = "N";
            else gearText = currentManualGear.ToString();

            UpdateGearText(gearText);
        }

        void Braking()
        {
            if (currentBrakeValue > 0f)
            {
                frontLeftWheelCollider.brakeTorque = currentBrakeValue * brakePower;
                frontRightWheelCollider.brakeTorque = currentBrakeValue * brakePower;
            }
            else
            {
                frontLeftWheelCollider.brakeTorque = 0;
                frontRightWheelCollider.brakeTorque = 0;
            }
        }

        void Handbraking()
        {
            if (currentHandbrakeValue > 0f)
            {
                rearLeftWheelCollider.motorTorque = 0;
                rearRightWheelCollider.motorTorque = 0;
                rearLeftWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;
                rearRightWheelCollider.brakeTorque = currentHandbrakeValue * handbrakeForce;
            }
            else
            {
                rearLeftWheelCollider.brakeTorque = 0;
                rearRightWheelCollider.brakeTorque = 0;
            }
        }

        void Steering()
        {
            float adjustedspeedFactor = Mathf.InverseLerp(20, maxForwardSpeed, currentSpeed); //minimum speed affecting steerAngle is 20
            float adjustedTurnAngle = targetSteerAngle * (1 - adjustedspeedFactor); //based on current speed.
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, adjustedTurnAngle, Time.deltaTime * steeringSpeed);

            frontLeftWheelCollider.steerAngle = currentSteerAngle;
            frontRightWheelCollider.steerAngle = currentSteerAngle;

            UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
            UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
            UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
            UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
        }

        void Slowdown()
        {
            if (vehicleRB == null) return;

            if (currentAccelerationValue == 0 && currentBrakeValue == 0 && currentHandbrakeValue == 0)
            {
#if UNITY_6000_0_OR_NEWER
                vehicleRB.linearVelocity = Vector3.Lerp(vehicleRB.linearVelocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#else
                vehicleRB.velocity = Vector3.Lerp(vehicleRB.velocity, Vector3.zero, Time.deltaTime * decelerationSpeed);
#endif
            }
        }

        #endregion

        #region Gearbox

        void OnDownShift()
        {
            if (transmissionMode == TransmissionModes.Manual)
            {
                if (currentManualGear > -1)
                {
                    currentManualGear--;
                    UpdateManualGearText();

                    if (currentManualGear == -1 && isStarted && ezerealLightController != null)
                    {
                        ezerealLightController.ReverseLightsOn();
                    }
                }
            }
            else // Automatic
            {
                switch (currentGear)
                {
                    case AutomaticGears.Reverse:
                        // already at lowest
                        break;

                    case AutomaticGears.Neutral:
                        currentGear--;
                        UpdateGearText("R");
                        if (isStarted && ezerealLightController != null)
                        {
                            ezerealLightController.ReverseLightsOn();
                        }
                        break;

                    case AutomaticGears.Drive:
                        currentGear--;
                        UpdateGearText("N");
                        break;
                }
            }
        }

        void OnUpShift()
        {
            if (transmissionMode == TransmissionModes.Manual)
            {
                if (currentManualGear < 5)
                {
                    if (currentManualGear == -1 && isStarted && ezerealLightController != null)
                    {
                        ezerealLightController.ReverseLightsOff();
                    }

                    currentManualGear++;
                    UpdateManualGearText();
                }
            }
            else // Automatic
            {
                switch (currentGear)
                {
                    case AutomaticGears.Reverse:
                        currentGear++;
                        UpdateGearText("N");

                        if (isStarted && ezerealLightController != null)
                        {
                            ezerealLightController.ReverseLightsOff();
                        }
                        break;

                    case AutomaticGears.Neutral:
                        currentGear++;
                        UpdateGearText("D");
                        break;

                    case AutomaticGears.Drive:
                        // already at highest
                        break;
                }
            }
        }

        #endregion

        private void FixedUpdate()
        {
            Acceleration();
            Braking();
            Handbraking();
            Steering();
            Slowdown();
            RotateSteeringWheel();

            if (Mathf.Abs(frontLeftWheelCollider.rpm) < stopThreshold &&
                Mathf.Abs(frontRightWheelCollider.rpm) < stopThreshold &&
                Mathf.Abs(rearLeftWheelCollider.rpm) < stopThreshold &&
                Mathf.Abs(rearRightWheelCollider.rpm) < stopThreshold)
            {
                stationary = true;
            }
            else
            {
                stationary = false;
            }

            if (vehicleRB != null)
            {
#if UNITY_6000_0_OR_NEWER
                currentSpeed = Vector3.Dot(vehicleRB.gameObject.transform.forward, vehicleRB.linearVelocity);
                currentSpeed *= 3.6f;
                UpdateSpeedText(currentSpeed);
#else
                currentSpeed = Vector3.Dot(vehicleRB.gameObject.transform.forward, vehicleRB.velocity);
                currentSpeed *= 3.6f;
                UpdateSpeedText(currentSpeed);
#endif
            }

            FrontLeftWheelRPM = frontLeftWheelCollider.rpm;
            FrontRightWheelRPM = frontRightWheelCollider.rpm;
            RearLeftWheelRPM = rearLeftWheelCollider.rpm;
            RearRightWheelRPM = rearRightWheelCollider.rpm;

            CalculateEngineRPM();
        }

        private float GetDriveWheelRPM()
        {
            float rpmSum = 0f;
            int count = 0;

            if (driveType == DriveTypes.RWD || driveType == DriveTypes.AWD)
            {
                if (rearLeftWheelCollider != null) { rpmSum += Mathf.Abs(rearLeftWheelCollider.rpm); count++; }
                if (rearRightWheelCollider != null) { rpmSum += Mathf.Abs(rearRightWheelCollider.rpm); count++; }
            }
            
            if (driveType == DriveTypes.FWD || driveType == DriveTypes.AWD)
            {
                if (frontLeftWheelCollider != null) { rpmSum += Mathf.Abs(frontLeftWheelCollider.rpm); count++; }
                if (frontRightWheelCollider != null) { rpmSum += Mathf.Abs(frontRightWheelCollider.rpm); count++; }
            }

            return count > 0 ? (rpmSum / count) : 0f;
        }

        private float GetManualGearRatio(int gear)
        {
            switch (gear)
            {
                case 1: return 22f;
                case 2: return 12f;
                case 3: return 8f;
                case 4: return 5.5f;
                case 5: return 3.8f;
                case -1: return 15f; // Задняя передача
                default: return 0f;  // Нейтралка
            }
        }

        void CalculateEngineRPM()
        {
            if (!isStarted)
            {
                engineRPM = Mathf.MoveTowards(engineRPM, 0f, Time.deltaTime * 3000f);
                isLimiterActive = false;
                return;
            }

            if (transmissionMode == TransmissionModes.Manual)
            {
                if (currentManualGear == 0) // Neutral
                {
                    if (currentAccelerationValue > 0)
                    {
                        engineRPM = Mathf.MoveTowards(engineRPM, maxRPM, Time.deltaTime * 9000f);
                    }
                    else
                    {
                        engineRPM = Mathf.MoveTowards(engineRPM, idleRPM, Time.deltaTime * 5000f);
                    }
                }
                else // Gears 1..5 and R
                {
                    float wheelRPM = GetDriveWheelRPM();
                    float ratio = GetManualGearRatio(currentManualGear);
                    
                    // Физический расчет оборотов двигателя на основе вращения колес!
                    float targetRPM = idleRPM + (wheelRPM * ratio);
                    
                    // Сглаживаем, чтобы стрелка не дергалась слишком дико
                    engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.deltaTime * 12f);
                }
            }
            else // Automatic Mode
            {
                float wheelRPM = GetDriveWheelRPM();
                float speedKmh = Mathf.Abs(currentSpeed);
                float ratio = 6.0f; 
                
                if (currentGear == AutomaticGears.Drive)
                {
                    // Имитируем переключения автомата
                    if (speedKmh < maxForwardSpeed * 0.25f) ratio = 22f;
                    else if (speedKmh < maxForwardSpeed * 0.5f) ratio = 12f;
                    else if (speedKmh < maxForwardSpeed * 0.75f) ratio = 8f;
                    else ratio = 5.5f;
                }
                else if (currentGear == AutomaticGears.Reverse)
                {
                    ratio = 15f;
                }
                else
                {
                    ratio = 0f;
                }

                if (ratio == 0f)
                {
                    engineRPM = Mathf.MoveTowards(engineRPM, idleRPM, Time.deltaTime * 4000f);
                }
                else
                {
                    float targetRPM = idleRPM + (wheelRPM * ratio);
                    engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.deltaTime * 8f);
                }
            }

            // Ограничиваем обороты сверху
            if (engineRPM > maxRPM) engineRPM = maxRPM;

            // Обработка отсечки (Limiter)
            if (engineRPM >= maxRPM - 50f && currentAccelerationValue > 0f)
            {
                limiterTime += Time.deltaTime;
                if (limiterTime > 0.07f) // частота пульсации ~14 Гц
                {
                    isLimitingState = !isLimitingState;
                    limiterTime = 0f;
                }

                if (isLimitingState)
                {
                    engineRPM = maxRPM - 700f; // обороты резко падают на отсечке
                    isLimiterActive = true;
                }
                else
                {
                    isLimiterActive = false;
                }
            }
            else
            {
                limiterTime = 0f;
                isLimitingState = false;
                isLimiterActive = false;
            }
        }

        private void UpdateWheel(WheelCollider col, Transform mesh)
        {
            col.GetWorldPose(out Vector3 position, out Quaternion rotation);
            mesh.SetPositionAndRotation(position, rotation);
        }

        void RotateSteeringWheel()
        {
            float currentXAngle = steeringWheel.transform.localEulerAngles.x; // Maximum steer angle in degrees

            float normalizedSteerAngle = Mathf.Clamp(frontLeftWheelCollider.steerAngle, -maxSteerAngle, maxSteerAngle);
            float rotation = Mathf.Lerp(maxSteeringWheelRotation, -maxSteeringWheelRotation, (normalizedSteerAngle + maxSteerAngle) / (2 * maxSteerAngle));

            steeringWheel.localRotation = Quaternion.Euler(currentXAngle, 0, rotation);
        }

        void UpdateGearText(string gear)
        {
            if (currentGearTMP_UI != null) currentGearTMP_UI.text = gear;
            if (currentGearTMP_Dashboard != null) currentGearTMP_Dashboard.text = gear;
        }

        void UpdateGearText(AutomaticGears gear)
        {
            string gearText = gear == AutomaticGears.Reverse ? "R" : (gear == AutomaticGears.Neutral ? "N" : "D");
            if (currentGearTMP_UI != null) currentGearTMP_UI.text = gearText;
            if (currentGearTMP_Dashboard != null) currentGearTMP_Dashboard.text = gearText;
        }

        void UpdateSpeedText(float speed)
        {
            string speedText = Mathf.RoundToInt(Mathf.Abs(speed)).ToString();
            if (currentSpeedTMP_UI != null) currentSpeedTMP_UI.text = speedText;
            if (currentSpeedTMP_Dashboard != null) currentSpeedTMP_Dashboard.text = speedText;
        }

        void UpdateAccelerationSlider()
        {
            if (accelerationSlider != null)
            {
                bool isDriving = false;
                if (transmissionMode == TransmissionModes.Automatic)
                {
                    isDriving = (currentGear == AutomaticGears.Drive || currentGear == AutomaticGears.Reverse);
                }
                else
                {
                    isDriving = (currentManualGear != 0); // Любая передача кроме нейтралки
                }

                if (isDriving)
                {
                    accelerationSlider.value = Mathf.Lerp(accelerationSlider.value, currentAccelerationValue, Time.deltaTime * 15f);
                }
                else
                {
                    accelerationSlider.value = 0;
                }
            }
        }

        public bool InAir()
        {
            foreach (WheelCollider wheel in wheels)
            {
                if (wheel.GetGroundHit(out _))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
