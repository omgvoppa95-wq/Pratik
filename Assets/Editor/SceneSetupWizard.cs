#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhantomChaseVR.Editor
{
    /// <summary>
    /// One-click scene setup wizard that creates all manager GameObjects,
    /// configures the post-processing volume, and assigns prefab references.
    ///
    /// Usage: Unity menu → PhantomChase → Setup Scene
    /// </summary>
    public static class SceneSetupWizard
    {
        private const string PrefabRoot = "Assets/Prefabs";

        [MenuItem("PhantomChase/Setup Scene", false, 20)]
        public static void SetupScene()
        {
            CreateTransportNodeManagerObject();
            CreateNYCCityBuilderObject();
            CreateNPCCrowdManagerObject();
            CreatePostProcessingControllerObject();
            CreateGameManagerObject();
            ConfigureCamera();
            Debug.Log("[SceneSetupWizard] Scene setup complete. Assign any missing prefab references in the Inspector.");
        }

        [MenuItem("PhantomChase/Auto-Assign Prefab References", false, 21)]
        public static void AutoAssignPrefabs()
        {
            AssignTransportNodeManagerPrefabs();
            AssignNYCCityBuilderPrefabs();
            AssignNPCCrowdManagerPrefabs();
            Debug.Log("[SceneSetupWizard] Prefab references assigned.");
        }

        // ================================================================
        // Create manager GameObjects
        // ================================================================

        private static void CreateTransportNodeManagerObject()
        {
            if (Object.FindObjectOfType<TransportNodeManager>() != null)
            {
                Debug.Log("[SceneSetupWizard] TransportNodeManager already exists in scene.");
                return;
            }

            GameObject go = new GameObject("TransportNodeManager");
            go.AddComponent<TransportNodeManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create TransportNodeManager");
        }

        private static void CreateNYCCityBuilderObject()
        {
            if (Object.FindObjectOfType<NYCCityBuilder>() != null)
            {
                Debug.Log("[SceneSetupWizard] NYCCityBuilder already exists in scene.");
                return;
            }

            GameObject go = new GameObject("NYCCityBuilder");
            go.AddComponent<NYCCityBuilder>();
            Undo.RegisterCreatedObjectUndo(go, "Create NYCCityBuilder");
        }

        private static void CreateNPCCrowdManagerObject()
        {
            if (Object.FindObjectOfType<NPCCrowdManager>() != null)
            {
                Debug.Log("[SceneSetupWizard] NPCCrowdManager already exists in scene.");
                return;
            }

            GameObject go = new GameObject("NPCCrowdManager");
            go.AddComponent<NPCCrowdManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create NPCCrowdManager");
        }

        private static void CreatePostProcessingControllerObject()
        {
            if (Object.FindObjectOfType<PostProcessingController>() != null)
            {
                Debug.Log("[SceneSetupWizard] PostProcessingController already exists in scene.");
                return;
            }

            // Create Volume GameObject
            GameObject volumeGo = new GameObject("PostProcessVolume");
            Volume vol = volumeGo.AddComponent<Volume>();
            vol.isGlobal = true;

            // Create a new VolumeProfile asset
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string profilePath = "Assets/Settings/NightProfile.asset";
            EnsureSettingsFolder();
            AssetDatabase.CreateAsset(profile, profilePath);
            vol.profile = profile;

            Undo.RegisterCreatedObjectUndo(volumeGo, "Create PostProcessVolume");

            // Create PostProcessingController
            GameObject controllerGo = new GameObject("PostProcessingController");
            PostProcessingController controller = controllerGo.AddComponent<PostProcessingController>();

            // Wire the volume reference
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("postProcessVolume").objectReferenceValue = vol;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(controllerGo, "Create PostProcessingController");
        }

        private static void CreateGameManagerObject()
        {
            GameManager existing = Object.FindObjectOfType<GameManager>();
            if (existing != null)
            {
                Debug.Log("[SceneSetupWizard] GameManager already exists in scene.");
                return;
            }

            GameObject go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create GameManager");
        }

        // ================================================================
        // Camera configuration
        // ================================================================

        private static void ConfigureCamera()
        {
            Camera main = Camera.main;
            if (main == null) return;

            // Position camera overhead for a good view of the grid
            main.transform.position = new Vector3(0f, 120f, -30f);
            main.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            main.farClipPlane = 500f;
            main.backgroundColor = new Color(0.02f, 0.02f, 0.05f); // dark night sky
        }

        // ================================================================
        // Auto-assign prefab references
        // ================================================================

        private static void AssignTransportNodeManagerPrefabs()
        {
            TransportNodeManager mgr = Object.FindObjectOfType<TransportNodeManager>();
            if (mgr == null) return;

            SerializedObject so = new SerializedObject(mgr);

            TryAssignPrefab(so, "standardNodePrefab", PrefabRoot + "/Nodes/StandardNode.prefab");
            TryAssignPrefab(so, "undergroundNodePrefab", PrefabRoot + "/Nodes/UndergroundNode.prefab");
            TryAssignPrefab(so, "crowdZoneNodePrefab", PrefabRoot + "/Nodes/CrowdZoneNode.prefab");
            TryAssignPrefab(so, "busLinePrefab", PrefabRoot + "/Lines/BusLine.prefab");
            TryAssignPrefab(so, "taxiLinePrefab", PrefabRoot + "/Lines/TaxiLine.prefab");
            TryAssignPrefab(so, "trainLinePrefab", PrefabRoot + "/Lines/TrainLine.prefab");
            TryAssignPrefab(so, "undergroundLinePrefab", PrefabRoot + "/Lines/UndergroundLine.prefab");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }

        private static void AssignNYCCityBuilderPrefabs()
        {
            NYCCityBuilder builder = Object.FindObjectOfType<NYCCityBuilder>();
            if (builder == null) return;

            SerializedObject so = new SerializedObject(builder);

            // Tall buildings
            TryAssignPrefabArray(so, "tallBuildingPrefabs", new[]
            {
                PrefabRoot + "/Buildings/TallBuilding_A.prefab",
                PrefabRoot + "/Buildings/TallBuilding_B.prefab",
                PrefabRoot + "/Buildings/TallBuilding_C.prefab",
            });

            // Medium buildings
            TryAssignPrefabArray(so, "medBuildingPrefabs", new[]
            {
                PrefabRoot + "/Buildings/MedBuilding_A.prefab",
                PrefabRoot + "/Buildings/MedBuilding_B.prefab",
            });

            // Short buildings
            TryAssignPrefabArray(so, "shortBuildingPrefabs", new[]
            {
                PrefabRoot + "/Buildings/ShortBuilding_A.prefab",
                PrefabRoot + "/Buildings/ShortBuilding_B.prefab",
            });

            // Props
            TryAssignPrefab(so, "streetLampPrefab", PrefabRoot + "/Props/StreetLamp.prefab");
            TryAssignPrefab(so, "steamVentPrefab", PrefabRoot + "/Props/SteamVent.prefab");
            TryAssignPrefab(so, "fireHydrantPrefab", PrefabRoot + "/Props/FireHydrant.prefab");
            TryAssignPrefab(so, "yellowCabPrefab", PrefabRoot + "/Props/YellowCab.prefab");
            TryAssignPrefab(so, "trashCanPrefab", PrefabRoot + "/Props/TrashCan.prefab");
            TryAssignPrefab(so, "newsPaperBoxPrefab", PrefabRoot + "/Props/NewspaperBox.prefab");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(builder);
        }

        private static void AssignNPCCrowdManagerPrefabs()
        {
            NPCCrowdManager npcMgr = Object.FindObjectOfType<NPCCrowdManager>();
            if (npcMgr == null) return;

            SerializedObject so = new SerializedObject(npcMgr);

            string[] npcPaths = new string[5];
            for (int i = 0; i < 5; i++)
            {
                npcPaths[i] = PrefabRoot + $"/NPCs/NPC_Variant_{i}.prefab";
            }
            TryAssignPrefabArray(so, "npcVariantPrefabs", npcPaths);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(npcMgr);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void TryAssignPrefab(SerializedObject so, string propertyName, string assetPath)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneSetupWizard] Property '{propertyName}' not found.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                prop.objectReferenceValue = prefab;
            }
            else
            {
                Debug.LogWarning($"[SceneSetupWizard] Prefab not found at '{assetPath}'. Run 'Create All Prefabs' first.");
            }
        }

        private static void TryAssignPrefabArray(SerializedObject so, string propertyName, string[] assetPaths)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[SceneSetupWizard] Array property '{propertyName}' not found.");
                return;
            }

            prop.arraySize = assetPaths.Length;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
                if (prefab != null)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
                }
                else
                {
                    Debug.LogWarning($"[SceneSetupWizard] Prefab not found at '{assetPaths[i]}'.");
                }
            }
        }

        private static void EnsureSettingsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
        }
    }
}
#endif
