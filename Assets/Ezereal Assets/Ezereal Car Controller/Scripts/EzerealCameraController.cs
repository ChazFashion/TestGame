using UnityEngine;
using UnityEngine.InputSystem;

namespace Ezereal
{
    public class EzerealCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] CameraViews currentCameraView = CameraViews.cockpit;
        [SerializeField] private GameObject[] cameras; // Assume cameras are in order: cockpit, close, far, locked, wheel
        
        [Header("Mobile Camera Settings")]
        [SerializeField] private bool isMobileMode = false;
        [SerializeField] private Camera mobileCamera;
        [SerializeField] private Transform carTransform;
        [SerializeField] private float mobileCameraHeight = 15f; // Высота камеры над машиной
        [SerializeField] private float mobileCameraDistance = 20f; // Расстояние от машины
        [SerializeField] private float mobileCameraAngle = 45f; // Угол наклона камеры (в градусах)
        [SerializeField] private float cameraFollowSpeed = 5f; // Скорость следования камеры
        [SerializeField] private Vector3 cameraOffset = Vector3.zero; // Дополнительное смещение камеры

        private void Awake()
        {
            // Автоматическое определение мобильной платформы
            #if UNITY_ANDROID || UNITY_IOS || UNITY_IPHONE
            isMobileMode = true;
            #endif
            
            if (isMobileMode)
            {
                SetupMobileCamera();
            }
            else
            {
                SetCameraView(CameraViews.close);
            }
        }

        private void SetupMobileCamera()
        {
            // Если мобильная камера не назначена, создаем её
            if (mobileCamera == null)
            {
                GameObject mobileCamObj = new GameObject("MobileCamera");
                mobileCamera = mobileCamObj.AddComponent<Camera>();
                mobileCamObj.transform.SetParent(transform);
            }
            
            // Находим машину, если не назначена
            if (carTransform == null)
            {
                EzerealCarController car = FindFirstObjectByType<EzerealCarController>();
                if (car != null)
                {
                    carTransform = car.transform;
                }
            }
            
            // Отключаем все другие камеры
            if (cameras != null)
            {
                foreach (GameObject cam in cameras)
                {
                    if (cam != null)
                    {
                        cam.SetActive(false);
                    }
                }
            }
            
            // Настраиваем мобильную камеру
            mobileCamera.enabled = true;
            mobileCamera.fieldOfView = 60f; // Оптимальный FOV для мобильных устройств
        }

        private void LateUpdate()
        {
            if (isMobileMode && mobileCamera != null && carTransform != null)
            {
                UpdateMobileCamera();
            }
        }

        private void UpdateMobileCamera()
        {
            // Вычисляем позицию камеры
            Vector3 carPosition = carTransform.position;
            Vector3 carForward = carTransform.forward;
            
            // Вычисляем направление камеры (назад и вверх от машины)
            float angleRad = mobileCameraAngle * Mathf.Deg2Rad;
            Vector3 cameraDirection = -carForward * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad);
            cameraDirection.Normalize();
            
            // Позиция камеры
            Vector3 targetPosition = carPosition + cameraDirection * mobileCameraDistance + Vector3.up * mobileCameraHeight + cameraOffset;
            
            // Плавное перемещение камеры
            mobileCamera.transform.position = Vector3.Lerp(
                mobileCamera.transform.position, 
                targetPosition, 
                Time.deltaTime * cameraFollowSpeed
            );
            
            // Камера смотрит на машину
            Vector3 lookAtPosition = carPosition + Vector3.up * 2f; // Смотрим немного выше центра машины
            mobileCamera.transform.LookAt(lookAtPosition);
        }

        void OnSwitchCamera()
        {
            if (isMobileMode) return; // На мобильных устройствах не переключаем камеру
            
            currentCameraView = (CameraViews)(((int)currentCameraView + 1) % cameras.Length);
            SetCameraView(currentCameraView);
        }

        private void SetCameraView(CameraViews view)
        {
            if (cameras == null) return;
            
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    cameras[i].SetActive(i == (int)view);
                }
            }
        }
        
        // Публичные методы для настройки мобильной камеры
        public void SetMobileMode(bool enabled)
        {
            isMobileMode = enabled;
            if (enabled)
            {
                SetupMobileCamera();
            }
            else
            {
                if (mobileCamera != null)
                {
                    mobileCamera.enabled = false;
                }
                SetCameraView(CameraViews.close);
            }
        }
        
        public void SetMobileCameraHeight(float height)
        {
            mobileCameraHeight = height;
        }
        
        public void SetMobileCameraDistance(float distance)
        {
            mobileCameraDistance = distance;
        }
        
        public void SetMobileCameraAngle(float angle)
        {
            mobileCameraAngle = angle;
        }
    }
}
