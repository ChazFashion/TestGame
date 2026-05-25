using UnityEngine;
using UnityEditor;
using Ezereal;
using RacingUI;
using System.Collections.Generic;

public class SovietCarPrefabCreator : EditorWindow
{
    [MenuItem("Tools/Build Soviet Car Prefabs")]
    public static void BuildPrefabs()
    {
        string truckPath = "Assets/Ezereal Assets/Ezereal Car Controller/Prefabs/More Prefabs/Without Cameras/Electric Truck - Ready To Drive.prefab";
        string newLadaPath = "Assets/Prefabs/Gameplay_vz08_green.prefab";
        string newVolgaPath = "Assets/Prefabs/Gameplay_gz24_red.prefab";

        string ladaVisualPath = "Assets/Low Poly Soviet Car Pack/Prefabs/vz08/vz08_ green.prefab";
        string volgaVisualPath = "Assets/Low Poly Soviet Car Pack/Prefabs/gz24/gz24_red.prefab";

        // 1. Create target folder
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 2. Build Lada Prefab
        BuildSingleCar(truckPath, newLadaPath, ladaVisualPath);

        // 3. Build Volga Prefab
        BuildSingleCar(truckPath, newVolgaPath, volgaVisualPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SovietCarPrefabCreator] Prefabs built successfully! Now updating DataManager in Scene_Menu...");

        // 4. Update DataManager in Scene_Menu
        UpdateDataManager(newLadaPath, newVolgaPath, ladaVisualPath, volgaVisualPath);
    }

    private static void BuildSingleCar(string truckPath, string targetPath, string visualPrefabPath)
    {
        AssetDatabase.DeleteAsset(targetPath);
        if (!AssetDatabase.CopyAsset(truckPath, targetPath))
        {
            Debug.LogError($"[SovietCarPrefabCreator] Failed to copy truck prefab to {targetPath}!");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(targetPath);
        if (root == null)
        {
            Debug.LogError($"[SovietCarPrefabCreator] Failed to load prefab contents from {targetPath}!");
            return;
        }

        try
        {
            EzerealCarController car = root.GetComponentInChildren<EzerealCarController>();
            if (car == null)
            {
                Debug.LogError($"[SovietCarPrefabCreator] EzerealCarController not found in {targetPath}!");
                return;
            }

            // Configure stiffer suspension for better stability and handling
            ConfigureStiffSuspension(car);

            // Find physics root (the Rigidbody of the vehicle)
            Rigidbody rb = root.GetComponentInChildren<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError($"[SovietCarPrefabCreator] Rigidbody not found in {targetPath}!");
                return;
            }
            Transform physicsRoot = rb.transform;

            // Reparent critical components to physics root so they move with physics
            Transform wheelColliders = FindChildRecursive(root.transform, "Wheel Colliders");
            if (wheelColliders != null)
            {
                wheelColliders.SetParent(physicsRoot, true);
            }

            Transform controllerTransform = car.transform;
            if (controllerTransform != root.transform)
            {
                controllerTransform.SetParent(physicsRoot, true);
            }

            // Hide original meshes folder recursively
            Transform meshesFolder = FindChildRecursive(root.transform, "Meshes");
            if (meshesFolder != null)
            {
                meshesFolder.gameObject.SetActive(false);
            }

            // Hide original dashboard canvas if it exists, but keep HUD Overlay Canvas active
            Canvas[] originalCanvases = root.GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in originalCanvases)
            {
                if (canvas.gameObject.name != "Overlay Canvas")
                {
                    canvas.gameObject.SetActive(false);
                }
            }

            // Instantiate visual prefab
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            if (visualPrefab == null)
            {
                Debug.LogError($"[SovietCarPrefabCreator] Visual prefab not found at {visualPrefabPath}!");
                return;
            }

            GameObject visualInstance = Instantiate(visualPrefab, physicsRoot);
            visualInstance.name = visualPrefab.name;
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            // Remove physics from visual model
            Rigidbody visualRb = visualInstance.GetComponent<Rigidbody>();
            if (visualRb == null) visualRb = visualInstance.GetComponentInChildren<Rigidbody>();
            if (visualRb != null) DestroyImmediate(visualRb, true);

            Collider[] visualColliders = visualInstance.GetComponentsInChildren<Collider>(true);
            foreach (var col in visualColliders)
            {
                DestroyImmediate(col, true);
            }

            // Set static to false
            visualInstance.isStatic = false;
            foreach (Transform t in visualInstance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = false;
            }

            // Find wheels
            Transform flWheel = null;
            Transform frWheel = null;
            Transform rlWheel = null;
            Transform rrWheel = null;

            foreach (Transform child in visualInstance.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLower();
                if (name.Contains("wheel") || name.Contains("whl") || name.Contains("koleso"))
                {
                    if (name.Contains("f_l") || (name.Contains("front") && name.Contains("left")) || name.Contains("_fl"))
                    {
                        flWheel = child;
                    }
                    else if (name.Contains("f_r") || (name.Contains("front") && name.Contains("right")) || name.Contains("_fr"))
                    {
                        frWheel = child;
                    }
                    else if (name.Contains("r_l") || (name.Contains("rear") && name.Contains("left")) || name.Contains("back_l") || name.Contains("_rl") || name.Contains("_bl") || name.Contains("b_l"))
                    {
                        rlWheel = child;
                    }
                    else if (name.Contains("r_r") || (name.Contains("rear") && name.Contains("right")) || name.Contains("back_r") || name.Contains("_rr") || name.Contains("_br") || name.Contains("b_r"))
                    {
                        rrWheel = child;
                    }
                }
            }

            var controllerType = typeof(EzerealCarController);

            // Align wheel colliders and assign meshes
            if (flWheel != null && car.frontLeftWheelCollider != null)
            {
                controllerType.GetField("frontLeftWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, flWheel);
                car.frontLeftWheelCollider.transform.localPosition = GetTargetLocalPosition(root.transform, flWheel, car.frontLeftWheelCollider.transform.parent);
            }
            if (frWheel != null && car.frontRightWheelCollider != null)
            {
                controllerType.GetField("frontRightWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, frWheel);
                car.frontRightWheelCollider.transform.localPosition = GetTargetLocalPosition(root.transform, frWheel, car.frontRightWheelCollider.transform.parent);
            }
            if (rlWheel != null && car.rearLeftWheelCollider != null)
            {
                controllerType.GetField("rearLeftWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, rlWheel);
                car.rearLeftWheelCollider.transform.localPosition = GetTargetLocalPosition(root.transform, rlWheel, car.rearLeftWheelCollider.transform.parent);
            }
            if (rrWheel != null && car.rearRightWheelCollider != null)
            {
                controllerType.GetField("rearRightWheelMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(car, rrWheel);
                car.rearRightWheelCollider.transform.localPosition = GetTargetLocalPosition(root.transform, rrWheel, car.rearRightWheelCollider.transform.parent);
            }

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            Debug.Log($"[SovietCarPrefabCreator] Successfully built gameplay prefab: {targetPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SovietCarPrefabCreator] Exception occurred: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Vector3 GetTargetLocalPosition(Transform root, Transform targetVisual, Transform colliderParent)
    {
        return colliderParent.InverseTransformPoint(targetVisual.position);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void UpdateDataManager(string newLadaPath, string newVolgaPath, string ladaVisualPath, string volgaVisualPath)
    {
        string menuScenePath = "Assets/Scenes/Scene_Menu.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(menuScenePath);
        
        DataManager dm = FindAnyObjectByType<DataManager>();
        if (dm == null)
        {
            Debug.LogError("[SovietCarPrefabCreator] DataManager not found in Scene_Menu! Make sure to open the scene first.");
            return;
        }

        GameObject gameplayLada = AssetDatabase.LoadAssetAtPath<GameObject>(newLadaPath);
        GameObject gameplayVolga = AssetDatabase.LoadAssetAtPath<GameObject>(newVolgaPath);

        GameObject menuLada = AssetDatabase.LoadAssetAtPath<GameObject>(ladaVisualPath);
        GameObject menuVolga = AssetDatabase.LoadAssetAtPath<GameObject>(volgaVisualPath);

        for (int i = 0; i < dm.carPrefabs.Count; i++)
        {
            var mapping = dm.carPrefabs[i];
            if (mapping.prefabName == "vz08_green")
            {
                mapping.gameplayPrefab = gameplayLada;
                mapping.menuShowcasePrefab = menuLada;
                dm.carPrefabs[i] = mapping;
                Debug.Log("[SovietCarPrefabCreator] Assigned gameplay Lada prefab in DataManager.");
            }
            else if (mapping.prefabName == "gz24_red")
            {
                mapping.gameplayPrefab = gameplayVolga;
                mapping.menuShowcasePrefab = menuVolga;
                dm.carPrefabs[i] = mapping;
                Debug.Log("[SovietCarPrefabCreator] Assigned gameplay Volga prefab in DataManager.");
            }
        }

        EditorUtility.SetDirty(dm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[SovietCarPrefabCreator] DataManager saved successfully!");
    }

    private static void ConfigureStiffSuspension(EzerealCarController car)
    {
        WheelCollider[] colliders = car.GetComponentsInChildren<WheelCollider>(true);
        if (colliders.Length == 0)
        {
            colliders = car.transform.root.GetComponentsInChildren<WheelCollider>(true);
        }

        foreach (var col in colliders)
        {
            if (col != null)
            {
                JointSpring spring = col.suspensionSpring;
                spring.spring = 75000f; // Stiffer springs (was 35000)
                spring.damper = 6000f;  // Stiffer dampers (was 4500)
                col.suspensionSpring = spring;
                col.suspensionDistance = 0.22f; // Sportier, lower height (was 0.3)
            }
        }
        Debug.Log($"[SovietCarPrefabCreator] Configured stiffer suspension for {colliders.Length} wheel colliders of {car.gameObject.name}");
    }
}
