using UnityEngine;
using System.Collections.Generic;

namespace RacingUI
{
    public class WaypointArrow : MonoBehaviour
    {
        [Header("Settings")]
        public Transform arrowModel;      // For 3D arrow (can be null)
        public RectTransform uiArrow;     // For UI arrow on Canvas
        public float rotationSpeed = 10f; 
        public float lookDistance = 10f;  
        [Tooltip("Sprite rotation offset in degrees (e.g. 90, 180, -90) if it doesn't point UP by default.")]
        public float spriteRotationOffset = 0f;
        [Tooltip("Invert rotation direction if the arrow turns the wrong way.")]
        public bool invertRotation = false;

        [Header("GPS Navigation Mode")]
        [Tooltip("If enabled, the arrow will track AI waypoints ahead of the player to act as a GPS road guide.")]
        public bool useGpsMode = true;
        [Tooltip("How many waypoints ahead to look. Higher values make the rotation smoother.")]
        public int gpsLookAhead = 3;

        [Header("Space Reference")]
        [Tooltip("Use Main Camera orientation. If disabled, uses the player car's forward direction directly (recommended).")]
        public bool useCameraSpace = false;

        [Header("Target Player")]
        [Tooltip("Player Transform. If null, automatically searches via RaceManager or player tag.")]
        public Transform playerTransform; 

        [Header("Track Data")]
        [Tooltip("Container holding AI spline nodes. Required for GPS mode.")]
        public Transform waypointContainer; 
        private List<Transform> nodes = new List<Transform>();
        private int currentNodeIndex = 0;

        private Camera cachedCamera;
        private Transform lastTargetNode;
        private float logTimer = 0f;

        void Start()
        {
            if (waypointContainer != null)
            {
                foreach (Transform child in waypointContainer)
                {
                    nodes.Add(child);
                }
            }

            if (playerTransform == null)
            {
                FindPlayerTransform();
            }

            ResolveActualCarTransform();

            FindMainCamera();
        }

        void ResolveActualCarTransform()
        {
            if (playerTransform != null)
            {
                // If user dragged a static empty parent or sibling object,
                // automatically resolve it to the actual moving car object in its hierarchy.
                var car = playerTransform.GetComponent<Ezereal.EzerealCarController>();
                if (car == null) car = playerTransform.GetComponentInChildren<Ezereal.EzerealCarController>();
                if (car == null && playerTransform.parent != null)
                {
                    car = playerTransform.parent.GetComponentInChildren<Ezereal.EzerealCarController>();
                }
                if (car == null) car = playerTransform.GetComponentInParent<Ezereal.EzerealCarController>();

                if (car != null)
                {
                    // Find the Rigidbody, which is by definition the physically moving body of the car
                    Rigidbody rb = car.GetComponent<Rigidbody>();
                    if (rb == null) rb = car.GetComponentInChildren<Rigidbody>();
                    if (rb == null && car.transform.parent != null)
                    {
                        rb = car.transform.parent.GetComponentInChildren<Rigidbody>();
                    }
                    if (rb == null) rb = car.GetComponentInParent<Rigidbody>();

                    if (rb != null)
                    {
                        playerTransform = rb.transform;
                        Debug.Log("[WaypointArrow] Corrected Player Transform to moving Rigidbody: " + playerTransform.name);
                    }
                    else
                    {
                        playerTransform = car.transform;
                        Debug.Log("[WaypointArrow] Corrected Player Transform to active car object: " + playerTransform.name);
                    }
                }
            }
        }

        void FindMainCamera()
        {
            if (Camera.main != null)
            {
                cachedCamera = Camera.main;
            }
            else
            {
                cachedCamera = FindAnyObjectByType<Camera>();
                if (cachedCamera != null)
                {
                    Debug.LogWarning("[WaypointArrow] Camera.main not found! Using fallback: " + cachedCamera.name);
                }
            }
        }

        void FindPlayerTransform()
        {
            // 1. Try to find via RaceManager
            if (RaceManager.Instance != null && RaceManager.Instance.playerCar != null)
            {
                playerTransform = RaceManager.Instance.playerCar.transform;
                Debug.Log("[WaypointArrow] Player transform found via RaceManager: " + playerTransform.name);
                return;
            }

            // 2. Try to find via EzerealCarController component (non-AI)
            var cars = FindObjectsByType<Ezereal.EzerealCarController>(FindObjectsSortMode.None);
            foreach (var car in cars)
            {
                if (car.GetComponentInChildren<AICarDriver>() == null && car.GetComponentInParent<AICarDriver>() == null)
                {
                    playerTransform = car.transform;
                    Debug.Log("[WaypointArrow] Player transform found via EzerealCarController: " + playerTransform.name);
                    return;
                }
            }

            // 3. Fallback to Player tag
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                var car = playerObj.GetComponentInChildren<Ezereal.EzerealCarController>();
                if (car == null) car = playerObj.GetComponentInParent<Ezereal.EzerealCarController>();
                playerTransform = car != null ? car.transform : playerObj.transform;
                Debug.Log("[WaypointArrow] Player transform found via tag: " + playerTransform.name);
            }
        }

        int GetClosestNodeIndex(Vector3 position)
        {
            if (nodes.Count == 0) return 0;
            
            int closest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float dist = Vector3.Distance(position, nodes[i].position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = i;
                }
            }
            return closest;
        }

        void Update()
        {
            // If player transform is still not found, try searching
            if (playerTransform == null)
            {
                FindPlayerTransform();
                if (playerTransform == null) return; 
            }

            Transform targetNode = null;

            if (useGpsMode && nodes.Count > 0)
            {
                // GPS Mode: track AI waypoints ahead of the player position
                int closestIndex = GetClosestNodeIndex(playerTransform.position);
                int targetIndex = (closestIndex + gpsLookAhead) % nodes.Count;
                targetNode = nodes[targetIndex];
            }
            else
            {
                // Checkpoint Mode: track player's current checkpoint from RaceManager
                if (RaceManager.Instance != null)
                {
                    targetNode = RaceManager.Instance.GetPlayerTargetCheckpoint();
                }

                // Fallback for free ride (sequentially track nodes list)
                if (targetNode == null)
                {
                    if (nodes.Count == 0) return;
                    targetNode = nodes[currentNodeIndex];

                    // Distance switch for free ride mode
                    Vector3 flatPos = new Vector3(playerTransform.position.x, 0, playerTransform.position.z);
                    Vector3 flatTarget = new Vector3(targetNode.position.x, 0, targetNode.position.z);

                    if (Vector3.Distance(flatPos, flatTarget) < lookDistance)
                    {
                        Debug.Log("--- NODE PASSED (Free Ride): " + nodes[currentNodeIndex].name + " ---");
                        currentNodeIndex = (currentNodeIndex + 1) % nodes.Count;
                    }
                }
            }

            // Hide the UI arrow if there is no active target node (e.g. race finished)
            if (targetNode == null)
            {
                if (uiArrow != null) uiArrow.gameObject.SetActive(false);
                return;
            }
            else
            {
                if (uiArrow != null) uiArrow.gameObject.SetActive(true);
            }

            // Log target node updates for debugging
            if (targetNode != lastTargetNode)
            {
                Debug.Log($"[WaypointArrow] Target changed! New target: {targetNode.name}");
                lastTargetNode = targetNode;
            }

            // Find camera if missing and useCameraSpace is enabled
            if (useCameraSpace && cachedCamera == null)
            {
                FindMainCamera();
            }

            // Compute direction to target
            Vector3 direction = targetNode.position - playerTransform.position;

            // --- 3D MODEL ARROW LOGIC ---
            if (arrowModel != null)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                arrowModel.rotation = Quaternion.Slerp(arrowModel.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // --- UI CANVAS ARROW LOGIC ---
            if (uiArrow != null)
            {
                // Select coordinate space (Camera or Player Car)
                Transform refTransform = (useCameraSpace && cachedCamera != null) ? cachedCamera.transform : playerTransform;

                // Project directions to horizontal plane (XZ)
                Vector3 flatDir = new Vector3(direction.x, 0, direction.z).normalized;
                Vector3 refForward = new Vector3(refTransform.forward.x, 0, refTransform.forward.z).normalized;
                Vector3 refRight = new Vector3(refTransform.right.x, 0, refTransform.right.z).normalized;

                // Dot product project direction onto horizontal forward/right axes
                float forwardDist = Vector3.Dot(flatDir, refForward);
                float rightDist = Vector3.Dot(flatDir, refRight);

                // Compute angle [-180, 180]
                float angle = Mathf.Atan2(rightDist, forwardDist) * Mathf.Rad2Deg;

                // Rotate the UI Arrow
                float targetZRotation = (invertRotation ? angle : -angle) + spriteRotationOffset;
                uiArrow.localRotation = Quaternion.Slerp(
                    uiArrow.localRotation, 
                    Quaternion.Euler(0, 0, targetZRotation), 
                    Time.deltaTime * rotationSpeed
                );

                // Diagnostic log
                logTimer += Time.deltaTime;
                if (logTimer >= 1f)
                {
                    logTimer = 0f;
                    Transform spaceRef = (useCameraSpace && cachedCamera != null) ? cachedCamera.transform : playerTransform;
                    Debug.Log($"[ArrowDiag] Игрок: {playerTransform.name} (поз={playerTransform.position}), Цель: {targetNode.name} (поз={targetNode.position}), Ориентир: {spaceRef.name} (поз={spaceRef.position}, rot={spaceRef.rotation.eulerAngles}), Угол Z={targetZRotation}");
                }
            }
        }

        // Draw debug lines in Scene window
        private void OnDrawGizmos()
        {
            if (waypointContainer == null) return;

            Gizmos.color = Color.yellow;
            Transform lastPoint = null;
            foreach (Transform child in waypointContainer)
            {
                if (lastPoint != null) Gizmos.DrawLine(lastPoint.position, child.position);
                Gizmos.DrawWireSphere(child.position, 0.5f);
                lastPoint = child;
            }

            if (Application.isPlaying && playerTransform != null)
            {
                Transform targetNode = null;
                if (useGpsMode && nodes.Count > 0)
                {
                    int closestIndex = GetClosestNodeIndex(playerTransform.position);
                    int targetIndex = (closestIndex + gpsLookAhead) % nodes.Count;
                    targetNode = nodes[targetIndex];
                }
                else
                {
                    if (RaceManager.Instance != null) targetNode = RaceManager.Instance.GetPlayerTargetCheckpoint();
                    if (targetNode == null && nodes.Count > 0) targetNode = nodes[currentNodeIndex];
                }

                if (targetNode != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(playerTransform.position, targetNode.position);
                }
            }
        }
    }
}
