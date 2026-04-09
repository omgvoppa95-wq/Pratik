#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace PhantomChaseVR.Editor
{
    /// <summary>
    /// Editor utility that auto-generates every prefab required by
    /// TransportNodeManager, NYCCityBuilder, and NPCCrowdManager.
    ///
    /// Usage: Unity menu → PhantomChase → Create All Prefabs
    /// </summary>
    public static class PrefabFactory
    {
        private const string PrefabRoot = "Assets/Prefabs";
        private const string NodeFolder = PrefabRoot + "/Nodes";
        private const string LineFolder = PrefabRoot + "/Lines";
        private const string BuildingFolder = PrefabRoot + "/Buildings";
        private const string PropFolder = PrefabRoot + "/Props";
        private const string NPCFolder = PrefabRoot + "/NPCs";

        // ── Colours from the design doc ─────────────────────────────────
        private static readonly Color BusLineColor = new Color(0.894f, 0.325f, 0.541f, 1f);      // #E4538A
        private static readonly Color TaxiLineColor = new Color(0.961f, 0.620f, 0.043f, 1f);     // #F59E0B
        private static readonly Color TrainLineColor = new Color(0.961f, 0.620f, 0.043f, 1f);    // #F59E0B bright
        private static readonly Color UndergroundCyanColor = new Color(0.024f, 0.714f, 0.831f, 1f); // #06B6D4
        private static readonly Color UndergroundPurpleColor = new Color(0.659f, 0.333f, 0.969f, 1f); // #A855F7
        private static readonly Color DarkGrey = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color WarmOrange = new Color(1f, 0.55f, 0.1f, 1f);

        // ================================================================
        // Menu entries
        // ================================================================

        [MenuItem("PhantomChase/Create All Prefabs", false, 10)]
        public static void CreateAllPrefabs()
        {
            EnsureFolders();
            CreateNodePrefabs();
            CreateLinePrefabs();
            CreateBuildingPrefabs();
            CreateStreetPropPrefabs();
            CreateNPCPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PrefabFactory] All prefabs created successfully.");
        }

        [MenuItem("PhantomChase/Create Node Prefabs Only", false, 11)]
        public static void CreateNodePrefabsMenu() { EnsureFolders(); CreateNodePrefabs(); AssetDatabase.SaveAssets(); }

        [MenuItem("PhantomChase/Create Line Prefabs Only", false, 12)]
        public static void CreateLinePrefabsMenu() { EnsureFolders(); CreateLinePrefabs(); AssetDatabase.SaveAssets(); }

        // ================================================================
        // Folder setup
        // ================================================================

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabRoot, "Nodes");
            EnsureFolder(PrefabRoot, "Lines");
            EnsureFolder(PrefabRoot, "Buildings");
            EnsureFolder(PrefabRoot, "Props");
            EnsureFolder(PrefabRoot, "NPCs");
            EnsureFolder(PrefabRoot, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        // ================================================================
        // Node prefabs
        // ================================================================

        private static void CreateNodePrefabs()
        {
            CreateStandardNodePrefab();
            CreateUndergroundNodePrefab();
            CreateCrowdZoneNodePrefab();
            Debug.Log("[PrefabFactory] Node prefabs created.");
        }

        private static void CreateStandardNodePrefab()
        {
            GameObject root = new GameObject("StandardNode");

            // Disk (cylinder flattened to a disk)
            GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = "Disk";
            disk.transform.SetParent(root.transform);
            disk.transform.localPosition = Vector3.zero;
            disk.transform.localScale = new Vector3(3f, 0.05f, 3f); // radius 1.5, thin

            Material diskMat = CreateMaterial("StandardNodeDisk", DarkGrey);
            disk.GetComponent<Renderer>().sharedMaterial = diskMat;

            // Highlight ring (slightly larger cylinder, hidden by default)
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HighlightRing";
            ring.transform.SetParent(root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(3.4f, 0.02f, 3.4f);

            Material ringMat = CreateMaterial("HighlightRing", Color.white, transparent: true, alpha: 0.6f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            ring.SetActive(false);

            // Blocked overlay (red X — small red cube as placeholder)
            GameObject blocked = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blocked.name = "BlockedOverlay";
            blocked.transform.SetParent(root.transform);
            blocked.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            blocked.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            blocked.transform.localScale = new Vector3(2f, 2f, 1f);

            Material blockedMat = CreateMaterial("BlockedOverlay", Color.red, transparent: true, alpha: 0.7f);
            blocked.GetComponent<Renderer>().sharedMaterial = blockedMat;
            blocked.SetActive(false);

            // TMP label
            GameObject labelGo = new GameObject("NodeLabel");
            labelGo.transform.SetParent(root.transform);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = "0";
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // Add TransportNode component
            TransportNode tn = root.AddComponent<TransportNode>();
            // Wire serialized fields via SerializedObject
            SavePrefabAndWireNode(root, disk, ring, blocked, labelGo,
                                  null, NodeFolder + "/StandardNode.prefab");
        }

        private static void CreateUndergroundNodePrefab()
        {
            GameObject root = new GameObject("UndergroundNode");

            // Disk
            GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = "Disk";
            disk.transform.SetParent(root.transform);
            disk.transform.localPosition = Vector3.zero;
            disk.transform.localScale = new Vector3(4f, 0.05f, 4f); // radius 2.0

            Material diskMat = CreateMaterial("UndergroundNodeDisk", UndergroundCyanColor, emissive: true);
            disk.GetComponent<Renderer>().sharedMaterial = diskMat;

            // Highlight ring
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HighlightRing";
            ring.transform.SetParent(root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(4.4f, 0.02f, 4.4f);

            Material ringMat = CreateMaterial("HighlightRing_UG", Color.cyan, transparent: true, alpha: 0.6f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            ring.SetActive(false);

            // Blocked overlay
            GameObject blocked = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blocked.name = "BlockedOverlay";
            blocked.transform.SetParent(root.transform);
            blocked.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            blocked.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            blocked.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            Material blockedMat = CreateMaterial("BlockedOverlay_UG", Color.red, transparent: true, alpha: 0.7f);
            blocked.GetComponent<Renderer>().sharedMaterial = blockedMat;
            blocked.SetActive(false);

            // Underground glow effect (point light + particle placeholder)
            GameObject glow = new GameObject("UndergroundGlowEffect");
            glow.transform.SetParent(root.transform);
            glow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            Light glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = UndergroundCyanColor;
            glowLight.intensity = 2f;
            glowLight.range = 5f;
            glow.SetActive(false);

            // Staircase prop (simple cube placeholder)
            GameObject stairs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stairs.name = "StaircaseProp";
            stairs.transform.SetParent(root.transform);
            stairs.transform.localPosition = new Vector3(1.5f, 0.5f, 0f);
            stairs.transform.localScale = new Vector3(0.8f, 1f, 0.5f);

            Material stairMat = CreateMaterial("StairsMat", new Color(0.3f, 0.3f, 0.35f, 1f));
            stairs.GetComponent<Renderer>().sharedMaterial = stairMat;

            // TMP label
            GameObject labelGo = new GameObject("NodeLabel");
            labelGo.transform.SetParent(root.transform);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = "0";
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            SavePrefabAndWireNode(root, disk, ring, blocked, labelGo,
                                  glow, NodeFolder + "/UndergroundNode.prefab");
        }

        private static void CreateCrowdZoneNodePrefab()
        {
            GameObject root = new GameObject("CrowdZoneNode");

            // Disk
            GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = "Disk";
            disk.transform.SetParent(root.transform);
            disk.transform.localPosition = Vector3.zero;
            disk.transform.localScale = new Vector3(4f, 0.05f, 4f); // radius 2.0

            Material diskMat = CreateMaterial("CrowdZoneNodeDisk", WarmOrange, emissive: true);
            disk.GetComponent<Renderer>().sharedMaterial = diskMat;

            // Highlight ring
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HighlightRing";
            ring.transform.SetParent(root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(4.4f, 0.02f, 4.4f);

            Material ringMat = CreateMaterial("HighlightRing_CZ", Color.yellow, transparent: true, alpha: 0.6f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            ring.SetActive(false);

            // Blocked overlay
            GameObject blocked = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blocked.name = "BlockedOverlay";
            blocked.transform.SetParent(root.transform);
            blocked.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            blocked.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            blocked.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

            Material blockedMat = CreateMaterial("BlockedOverlay_CZ", Color.red, transparent: true, alpha: 0.7f);
            blocked.GetComponent<Renderer>().sharedMaterial = blockedMat;
            blocked.SetActive(false);

            // TMP label
            GameObject labelGo = new GameObject("NodeLabel");
            labelGo.transform.SetParent(root.transform);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = "0";
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            SavePrefabAndWireNode(root, disk, ring, blocked, labelGo,
                                  null, NodeFolder + "/CrowdZoneNode.prefab");
        }

        // ================================================================
        // Line prefabs
        // ================================================================

        private static void CreateLinePrefabs()
        {
            CreateLinePrefab("BusLine", BusLineColor, 0.3f, false, LineFolder + "/BusLine.prefab");
            CreateLinePrefab("TaxiLine", TaxiLineColor, 0.3f, true, LineFolder + "/TaxiLine.prefab");
            CreateLinePrefab("TrainLine", TrainLineColor, 0.5f, false, LineFolder + "/TrainLine.prefab");
            CreateLinePrefab("UndergroundLine", UndergroundCyanColor, 0.7f, false,
                             LineFolder + "/UndergroundLine.prefab", emissive: true);
            Debug.Log("[PrefabFactory] Line prefabs created.");
        }

        private static void CreateLinePrefab(string name, Color color, float width,
                                              bool dashed, string path, bool emissive = false)
        {
            GameObject go = new GameObject(name);
            LineRenderer lr = go.AddComponent<LineRenderer>();

            Material mat = CreateMaterial(name + "Mat", color, emissive: emissive);
            lr.sharedMaterial = mat;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;

            if (dashed)
            {
                // Dashed look via texture mode
                lr.textureMode = LineTextureMode.Tile;
                lr.textureScale = new Vector2(4f, 1f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ================================================================
        // Building prefabs
        // ================================================================

        private static void CreateBuildingPrefabs()
        {
            // Tall buildings (downtown, 15-28m) — 3 variants
            CreateBuildingPrefab("TallBuilding_A", new Color(0.25f, 0.25f, 0.28f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/TallBuilding_A.prefab");
            CreateBuildingPrefab("TallBuilding_B", new Color(0.3f, 0.28f, 0.25f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/TallBuilding_B.prefab");
            CreateBuildingPrefab("TallBuilding_C", new Color(0.2f, 0.22f, 0.25f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/TallBuilding_C.prefab");

            // Medium buildings (midtown, 8-15m)
            CreateBuildingPrefab("MedBuilding_A", new Color(0.35f, 0.33f, 0.3f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/MedBuilding_A.prefab");
            CreateBuildingPrefab("MedBuilding_B", new Color(0.4f, 0.35f, 0.32f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/MedBuilding_B.prefab");

            // Short buildings (outer, 4-8m)
            CreateBuildingPrefab("ShortBuilding_A", new Color(0.45f, 0.42f, 0.38f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/ShortBuilding_A.prefab");
            CreateBuildingPrefab("ShortBuilding_B", new Color(0.5f, 0.45f, 0.4f),
                                 new Vector3(1f, 1f, 1f), BuildingFolder + "/ShortBuilding_B.prefab");

            Debug.Log("[PrefabFactory] Building prefabs created.");
        }

        private static void CreateBuildingPrefab(string name, Color color, Vector3 scale, string path)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = scale;

            // Add window-like detail: emissive spots via second child cube
            Material mat = CreateMaterial(name + "Mat", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Window lights on the front face (warm yellow emissive quads)
            GameObject windowsParent = new GameObject("Windows");
            windowsParent.transform.SetParent(go.transform);
            windowsParent.transform.localPosition = Vector3.zero;

            Material windowMat = CreateMaterial(name + "WindowMat",
                new Color(1f, 0.85f, 0.4f, 1f), emissive: true);

            // Create a grid of small window quads on two faces
            for (int wx = 0; wx < 3; wx++)
            {
                for (int wy = 0; wy < 4; wy++)
                {
                    // Front face windows
                    GameObject win = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    win.name = $"Window_F_{wx}_{wy}";
                    win.transform.SetParent(windowsParent.transform);
                    win.transform.localPosition = new Vector3(
                        -0.3f + wx * 0.3f,
                        -0.3f + wy * 0.2f,
                        0.501f
                    );
                    win.transform.localScale = new Vector3(0.12f, 0.1f, 1f);
                    win.GetComponent<Renderer>().sharedMaterial = windowMat;

                    // Remove collider from window quads
                    Object.DestroyImmediate(win.GetComponent<Collider>());
                }
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // ================================================================
        // Street prop prefabs
        // ================================================================

        private static void CreateStreetPropPrefabs()
        {
            // Street lamp — tall thin cylinder + point light
            {
                GameObject root = new GameObject("StreetLamp");
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(root.transform);
                pole.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                pole.transform.localScale = new Vector3(0.1f, 2.5f, 0.1f);

                Material poleMat = CreateMaterial("LampPoleMat", new Color(0.15f, 0.15f, 0.15f));
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;

                GameObject bulb = new GameObject("LampLight");
                bulb.transform.SetParent(root.transform);
                bulb.transform.localPosition = new Vector3(0f, 5f, 0f);
                Light light = bulb.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.5f);
                light.intensity = 1.5f;
                light.range = 12f;

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/StreetLamp.prefab");
                Object.DestroyImmediate(root);
            }

            // Steam vent — small cylinder with upward particle
            {
                GameObject root = new GameObject("SteamVent");
                GameObject grate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                grate.name = "Grate";
                grate.transform.SetParent(root.transform);
                grate.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                grate.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);

                Material grateMat = CreateMaterial("SteamGrateMat", new Color(0.25f, 0.25f, 0.25f));
                grate.GetComponent<Renderer>().sharedMaterial = grateMat;

                // Particle system placeholder
                GameObject steam = new GameObject("SteamParticles");
                steam.transform.SetParent(root.transform);
                steam.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                ParticleSystem ps = steam.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = 2f;
                main.startSpeed = 1f;
                main.startSize = 0.3f;
                main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
                main.maxParticles = 30;
                var emission = ps.emission;
                emission.rateOverTime = 10f;

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/SteamVent.prefab");
                Object.DestroyImmediate(root);
            }

            // Fire hydrant
            {
                GameObject root = new GameObject("FireHydrant");
                // Body
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                body.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                Material hydrantMat = CreateMaterial("HydrantMat", new Color(0.8f, 0.15f, 0.1f));
                body.GetComponent<Renderer>().sharedMaterial = hydrantMat;

                // Cap
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Cap";
                cap.transform.SetParent(root.transform);
                cap.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                cap.transform.localScale = new Vector3(0.25f, 0.15f, 0.25f);
                cap.GetComponent<Renderer>().sharedMaterial = hydrantMat;

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/FireHydrant.prefab");
                Object.DestroyImmediate(root);
            }

            // Yellow cab
            {
                GameObject root = new GameObject("YellowCab");
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                body.transform.localScale = new Vector3(2f, 0.8f, 4.5f);

                Material cabMat = CreateMaterial("YellowCabMat", new Color(1f, 0.85f, 0f));
                body.GetComponent<Renderer>().sharedMaterial = cabMat;

                // Roof
                GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "Roof";
                roof.transform.SetParent(root.transform);
                roof.transform.localPosition = new Vector3(0f, 1.1f, -0.3f);
                roof.transform.localScale = new Vector3(1.8f, 0.5f, 2.5f);
                roof.GetComponent<Renderer>().sharedMaterial = cabMat;

                // Wheels (4 cylinders)
                Material wheelMat = CreateMaterial("WheelMat", new Color(0.1f, 0.1f, 0.1f));
                Vector3[] wheelPositions =
                {
                    new Vector3(-1f, 0.2f, 1.5f),
                    new Vector3(1f, 0.2f, 1.5f),
                    new Vector3(-1f, 0.2f, -1.5f),
                    new Vector3(1f, 0.2f, -1.5f)
                };
                for (int i = 0; i < 4; i++)
                {
                    GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = $"Wheel_{i}";
                    wheel.transform.SetParent(root.transform);
                    wheel.transform.localPosition = wheelPositions[i];
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheel.transform.localScale = new Vector3(0.4f, 0.15f, 0.4f);
                    wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/YellowCab.prefab");
                Object.DestroyImmediate(root);
            }

            // Trash can
            {
                GameObject root = new GameObject("TrashCan");
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                body.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);

                Material trashMat = CreateMaterial("TrashCanMat", new Color(0.3f, 0.35f, 0.3f));
                body.GetComponent<Renderer>().sharedMaterial = trashMat;

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/TrashCan.prefab");
                Object.DestroyImmediate(root);
            }

            // Newspaper box
            {
                GameObject root = new GameObject("NewspaperBox");
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                body.transform.localScale = new Vector3(0.5f, 0.9f, 0.4f);

                Material boxMat = CreateMaterial("NewsBoxMat", new Color(0.1f, 0.2f, 0.5f));
                body.GetComponent<Renderer>().sharedMaterial = boxMat;

                PrefabUtility.SaveAsPrefabAsset(root, PropFolder + "/NewspaperBox.prefab");
                Object.DestroyImmediate(root);
            }

            Debug.Log("[PrefabFactory] Street prop prefabs created.");
        }

        // ================================================================
        // NPC prefabs — 5 visual variants
        // ================================================================

        private static void CreateNPCPrefabs()
        {
            Color[] bodyColors =
            {
                new Color(0.2f, 0.2f, 0.3f),   // dark coat
                new Color(0.5f, 0.25f, 0.15f),  // brown jacket
                new Color(0.15f, 0.15f, 0.15f), // black
                new Color(0.35f, 0.35f, 0.4f),  // grey
                new Color(0.6f, 0.55f, 0.4f),   // tan
            };

            Color[] pantsColors =
            {
                new Color(0.15f, 0.15f, 0.2f),
                new Color(0.25f, 0.25f, 0.3f),
                new Color(0.1f, 0.1f, 0.12f),
                new Color(0.2f, 0.18f, 0.15f),
                new Color(0.3f, 0.28f, 0.25f),
            };

            for (int i = 0; i < 5; i++)
            {
                GameObject root = new GameObject($"NPC_Variant_{i}");

                // Body (torso)
                GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                torso.name = "Torso";
                torso.transform.SetParent(root.transform);
                torso.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                torso.transform.localScale = new Vector3(0.4f, 0.5f, 0.3f);

                Material torsoMat = CreateMaterial($"NPC{i}_TorsoMat", bodyColors[i]);
                torso.GetComponent<Renderer>().sharedMaterial = torsoMat;

                // Head
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root.transform);
                head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
                head.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

                Material headMat = CreateMaterial($"NPC{i}_HeadMat", new Color(0.85f, 0.7f, 0.55f));
                head.GetComponent<Renderer>().sharedMaterial = headMat;

                // Legs
                GameObject legs = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                legs.name = "Legs";
                legs.transform.SetParent(root.transform);
                legs.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                legs.transform.localScale = new Vector3(0.35f, 0.45f, 0.25f);

                Material legsMat = CreateMaterial($"NPC{i}_LegsMat", pantsColors[i]);
                legs.GetComponent<Renderer>().sharedMaterial = legsMat;

                PrefabUtility.SaveAsPrefabAsset(root, NPCFolder + $"/NPC_Variant_{i}.prefab");
                Object.DestroyImmediate(root);
            }

            Debug.Log("[PrefabFactory] NPC prefabs created.");
        }

        // ================================================================
        // Material helpers
        // ================================================================

        private static Material CreateMaterial(string name, Color color,
                                                bool emissive = false,
                                                bool transparent = false,
                                                float alpha = 1f)
        {
            string matPath = PrefabRoot + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            // Use URP Lit shader if available, otherwise Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = name;

            if (transparent)
            {
                color.a = alpha;
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            mat.color = color;

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // ================================================================
        // Prefab save + wire TransportNode serialized fields
        // ================================================================

        private static void SavePrefabAndWireNode(GameObject root, GameObject disk,
                                                    GameObject ring, GameObject blocked,
                                                    GameObject labelGo, GameObject glowEffect,
                                                    string path)
        {
            // Save prefab first
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

            // Wire serialized fields on the prefab's TransportNode
            TransportNode tn = prefab.GetComponent<TransportNode>();
            if (tn != null)
            {
                SerializedObject so = new SerializedObject(tn);

                // diskRenderer
                Transform diskT = prefab.transform.Find("Disk");
                if (diskT != null)
                {
                    so.FindProperty("diskRenderer").objectReferenceValue =
                        diskT.GetComponent<Renderer>();
                }

                // highlightRing
                Transform ringT = prefab.transform.Find("HighlightRing");
                if (ringT != null)
                {
                    so.FindProperty("highlightRing").objectReferenceValue = ringT.gameObject;
                }

                // blockedOverlay
                Transform blockedT = prefab.transform.Find("BlockedOverlay");
                if (blockedT != null)
                {
                    so.FindProperty("blockedOverlay").objectReferenceValue = blockedT.gameObject;
                }

                // nodeLabel
                Transform labelT = prefab.transform.Find("NodeLabel");
                if (labelT != null)
                {
                    so.FindProperty("nodeLabel").objectReferenceValue =
                        labelT.GetComponent<TextMeshPro>();
                }

                // undergroundGlowEffect
                Transform glowT = prefab.transform.Find("UndergroundGlowEffect");
                if (glowT != null)
                {
                    so.FindProperty("undergroundGlowEffect").objectReferenceValue = glowT.gameObject;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(root);
        }
    }
}
#endif
