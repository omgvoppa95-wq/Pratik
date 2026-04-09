using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Procedurally generates the 1990s NYC night-time city geometry around the
    /// 62-node transport grid.  Fills in streets, sidewalks, buildings of varying
    /// height, and street props (lamps, steam vents, fire hydrants, cabs, etc.).
    ///
    /// Assign all prefab arrays and props in the Inspector.
    /// </summary>
    public class NYCCityBuilder : MonoBehaviour
    {
        // ── Building prefabs ────────────────────────────────────────────
        [Header("Building Prefabs")]
        [SerializeField] private GameObject[] tallBuildingPrefabs;   // 15-28 m (downtown core, rows 0-2)
        [SerializeField] private GameObject[] medBuildingPrefabs;    // 8-15 m  (midtown, rows 2-4)
        [SerializeField] private GameObject[] shortBuildingPrefabs;  // 4-8 m   (outer, row 5)

        // ── Street props ────────────────────────────────────────────────
        [Header("Street Props")]
        [SerializeField] private GameObject streetLampPrefab;
        [SerializeField] private GameObject steamVentPrefab;
        [SerializeField] private GameObject fireHydrantPrefab;
        [SerializeField] private GameObject yellowCabPrefab;
        [SerializeField] private GameObject trashCanPrefab;
        [SerializeField] private GameObject newsPaperBoxPrefab;

        // ── City bounds ─────────────────────────────────────────────────
        [Header("City Bounds")]
        [SerializeField] private float cityWidth = 300f;
        [SerializeField] private float cityDepth = 180f;
        [SerializeField] private int buildingRows = 6;
        [SerializeField] private int buildingCols = 10;

        // ── Street geometry ─────────────────────────────────────────────
        [Header("Street Geometry")]
        [SerializeField] private float avenueWidth = 8f;
        [SerializeField] private float streetWidth = 5f;
        [SerializeField] private float sidewalkWidth = 2f;

        // ── Prop spacing ────────────────────────────────────────────────
        [Header("Prop Spacing")]
        [SerializeField] private float lampInterval = 15f;
        [SerializeField] private float steamVentChance = 0.025f;  // per metre (~1 per 40 m)

        // ── Grid reference (matches TransportNodeManager) ───────────────
        private const float CellWidth = 30f;
        private const float CellDepth = 30f;
        private static readonly Vector3 GridOrigin = new Vector3(-135f, 0f, 75f);

        // Random seed for reproducibility
        private System.Random rng;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            rng = new System.Random(42);
            GenerateCity();
        }

        // ================================================================
        // Top-level generation
        // ================================================================

        private void GenerateCity()
        {
            GenerateStreetSurfaces();
            GenerateBuildings();
            PlaceStreetLamps();
            PlaceRandomStreetProps();
            PlaceYellowCabs();
        }

        // ================================================================
        // Street surfaces (avenues = vertical / E-W, streets = horizontal / N-S)
        // ================================================================

        private void GenerateStreetSurfaces()
        {
            // Streets run between node rows (horizontal)
            // Avenues run between node columns (vertical)
            // We create simple stretched quads for each street segment.

            float startX = GridOrigin.x - sidewalkWidth;
            float startZ = GridOrigin.z + sidewalkWidth;
            float endX = startX + cityWidth;
            float endZ = startZ - cityDepth;

            Transform streetsParent = new GameObject("Streets").transform;
            streetsParent.SetParent(transform);

            // Horizontal streets (between each row)
            for (int row = 0; row <= buildingRows; row++)
            {
                float z = GridOrigin.z - row * CellDepth;
                float width = row % 2 == 0 ? avenueWidth : streetWidth;

                GameObject street = GameObject.CreatePrimitive(PrimitiveType.Cube);
                street.name = $"Street_Row_{row}";
                street.transform.SetParent(streetsParent);
                street.transform.position = new Vector3(
                    startX + cityWidth * 0.5f,
                    -0.05f,
                    z
                );
                street.transform.localScale = new Vector3(cityWidth, 0.1f, width + sidewalkWidth * 2f);
                ApplyStreetMaterial(street);
            }

            // Vertical avenues (between each column)
            for (int col = 0; col <= buildingCols; col++)
            {
                float x = GridOrigin.x + col * CellWidth;
                float width = col % 3 == 0 ? avenueWidth : streetWidth;

                GameObject avenue = GameObject.CreatePrimitive(PrimitiveType.Cube);
                avenue.name = $"Avenue_Col_{col}";
                avenue.transform.SetParent(streetsParent);
                avenue.transform.position = new Vector3(
                    x,
                    -0.05f,
                    startZ - cityDepth * 0.5f
                );
                avenue.transform.localScale = new Vector3(width + sidewalkWidth * 2f, 0.1f, cityDepth);
                ApplyStreetMaterial(avenue);
            }
        }

        // ================================================================
        // Building generation
        // ================================================================

        private void GenerateBuildings()
        {
            Transform buildingsParent = new GameObject("Buildings").transform;
            buildingsParent.SetParent(transform);

            for (int row = 0; row < buildingRows; row++)
            {
                for (int col = 0; col < buildingCols; col++)
                {
                    // Each city block sits between two streets and two avenues
                    float blockX = GridOrigin.x + col * CellWidth + avenueWidth * 0.5f + sidewalkWidth;
                    float blockZ = GridOrigin.z - row * CellDepth - streetWidth * 0.5f - sidewalkWidth;

                    float blockWidth = CellWidth - avenueWidth - sidewalkWidth * 2f;
                    float blockDepth = CellDepth - streetWidth - sidewalkWidth * 2f;

                    if (blockWidth <= 0f || blockDepth <= 0f) continue;

                    // Determine building type by row
                    GameObject prefab = PickBuildingPrefab(row);

                    float height = GetBuildingHeight(row);

                    // Place 1-2 buildings per block
                    int buildingsPerBlock = rng.NextDouble() < 0.4 ? 2 : 1;
                    for (int b = 0; b < buildingsPerBlock; b++)
                    {
                        float offsetX = (float)(rng.NextDouble() * blockWidth * 0.3f);
                        float offsetZ = (float)(rng.NextDouble() * blockDepth * 0.3f);

                        Vector3 pos = new Vector3(
                            blockX + offsetX + (b * blockWidth * 0.5f),
                            height * 0.5f,
                            blockZ - offsetZ
                        );

                        if (prefab != null)
                        {
                            GameObject building = Instantiate(prefab, pos, Quaternion.identity, buildingsParent);
                            building.name = $"Building_R{row}_C{col}_{b}";

                            // Scale the building to fill part of the block
                            float bw = blockWidth / buildingsPerBlock * 0.85f;
                            float bd = blockDepth * 0.85f;
                            building.transform.localScale = new Vector3(bw, height, bd);
                        }
                        else
                        {
                            // Fallback: use a primitive cube
                            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            building.name = $"Building_R{row}_C{col}_{b}";
                            building.transform.SetParent(buildingsParent);
                            building.transform.position = pos;

                            float bw = blockWidth / buildingsPerBlock * 0.85f;
                            float bd = blockDepth * 0.85f;
                            building.transform.localScale = new Vector3(bw, height, bd);
                            ApplyBuildingMaterial(building, row);
                        }
                    }
                }
            }
        }

        private GameObject PickBuildingPrefab(int row)
        {
            if (row <= 1 && tallBuildingPrefabs != null && tallBuildingPrefabs.Length > 0)
                return tallBuildingPrefabs[rng.Next(tallBuildingPrefabs.Length)];
            if (row <= 3 && medBuildingPrefabs != null && medBuildingPrefabs.Length > 0)
                return medBuildingPrefabs[rng.Next(medBuildingPrefabs.Length)];
            if (shortBuildingPrefabs != null && shortBuildingPrefabs.Length > 0)
                return shortBuildingPrefabs[rng.Next(shortBuildingPrefabs.Length)];
            return null;
        }

        private float GetBuildingHeight(int row)
        {
            if (row <= 1) return 15f + (float)(rng.NextDouble() * 13f);  // 15-28 m
            if (row <= 3) return 8f + (float)(rng.NextDouble() * 7f);    // 8-15 m
            return 4f + (float)(rng.NextDouble() * 4f);                   // 4-8 m
        }

        // ================================================================
        // Street lamps — every 15 units along streets
        // ================================================================

        private void PlaceStreetLamps()
        {
            if (streetLampPrefab == null) return;

            Transform lampsParent = new GameObject("StreetLamps").transform;
            lampsParent.SetParent(transform);

            // Along horizontal streets
            for (int row = 0; row <= buildingRows; row++)
            {
                float z = GridOrigin.z - row * CellDepth;
                for (float x = GridOrigin.x; x < GridOrigin.x + cityWidth; x += lampInterval)
                {
                    // Place on both sidewalks
                    PlaceProp(streetLampPrefab, new Vector3(x, 0f, z + sidewalkWidth), lampsParent);
                    PlaceProp(streetLampPrefab, new Vector3(x, 0f, z - sidewalkWidth), lampsParent);
                }
            }

            // Along vertical avenues
            for (int col = 0; col <= buildingCols; col++)
            {
                float x = GridOrigin.x + col * CellWidth;
                for (float z = GridOrigin.z; z > GridOrigin.z - cityDepth; z -= lampInterval)
                {
                    PlaceProp(streetLampPrefab, new Vector3(x + sidewalkWidth, 0f, z), lampsParent);
                    PlaceProp(streetLampPrefab, new Vector3(x - sidewalkWidth, 0f, z), lampsParent);
                }
            }
        }

        // ================================================================
        // Random street props (steam vents, hydrants, trash, newspaper boxes)
        // ================================================================

        private void PlaceRandomStreetProps()
        {
            Transform propsParent = new GameObject("StreetProps").transform;
            propsParent.SetParent(transform);

            for (int row = 0; row <= buildingRows; row++)
            {
                float z = GridOrigin.z - row * CellDepth;
                for (float x = GridOrigin.x; x < GridOrigin.x + cityWidth; x += 1f)
                {
                    // Steam vents — ~1 per 40 m
                    if (rng.NextDouble() < steamVentChance && steamVentPrefab != null)
                    {
                        float side = rng.NextDouble() < 0.5 ? sidewalkWidth : -sidewalkWidth;
                        PlaceProp(steamVentPrefab, new Vector3(x, 0f, z + side), propsParent);
                    }

                    // Fire hydrant — sparse
                    if (rng.NextDouble() < 0.005f && fireHydrantPrefab != null)
                    {
                        PlaceProp(fireHydrantPrefab, new Vector3(x, 0f, z + sidewalkWidth), propsParent);
                    }

                    // Trash can
                    if (rng.NextDouble() < 0.008f && trashCanPrefab != null)
                    {
                        PlaceProp(trashCanPrefab, new Vector3(x, 0f, z - sidewalkWidth), propsParent);
                    }

                    // 1990s newspaper box
                    if (rng.NextDouble() < 0.004f && newsPaperBoxPrefab != null)
                    {
                        PlaceProp(newsPaperBoxPrefab, new Vector3(x, 0f, z + sidewalkWidth), propsParent);
                    }
                }
            }
        }

        // ================================================================
        // Yellow cabs — parked near taxi column nodes
        // ================================================================

        private void PlaceYellowCabs()
        {
            if (yellowCabPrefab == null) return;

            Transform cabsParent = new GameObject("YellowCabs").transform;
            cabsParent.SetParent(transform);

            // Taxi edges exist in rows 1-4; place cabs near those nodes
            for (int rowIdx = 1; rowIdx <= 4; rowIdx++)
            {
                int startId, count;
                switch (rowIdx)
                {
                    case 1: startId = 10; count = 11; break;
                    case 2: startId = 21; count = 11; break;
                    case 3: startId = 32; count = 11; break;
                    case 4: startId = 43; count = 11; break;
                    default: continue;
                }

                for (int i = 0; i < count; i += 2)
                {
                    int col = i; // startCol is 0 for rows 1-4
                    Vector3 nodePos = TransportNodeManager.GridToWorld(col, rowIdx);
                    // Park cab slightly offset from node
                    Vector3 cabPos = nodePos + new Vector3(3f, 0f, -2f);
                    float yRotation = rng.NextDouble() < 0.5 ? 0f : 180f;
                    GameObject cab = Instantiate(yellowCabPrefab, cabPos, Quaternion.Euler(0f, yRotation, 0f), cabsParent);
                    cab.name = $"YellowCab_R{rowIdx}_C{col}";
                }
            }
        }

        // ================================================================
        // Utility
        // ================================================================

        private GameObject PlaceProp(GameObject prefab, Vector3 position, Transform parent)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, position, Quaternion.identity, parent);
            return go;
        }

        private static void ApplyStreetMaterial(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                r.material.color = new Color(0.15f, 0.15f, 0.18f, 1f); // dark asphalt
            }
        }

        private static void ApplyBuildingMaterial(GameObject go, int row)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null || r.material == null) return;

            // Slightly varied building colours for visual diversity
            float grey = 0.2f + row * 0.05f;
            r.material.color = new Color(grey, grey, grey + 0.02f, 1f);
        }
    }
}
