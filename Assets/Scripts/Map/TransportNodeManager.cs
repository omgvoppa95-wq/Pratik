using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhantomChaseVR
{
    // ====================================================================
    // Transport types available in the game
    // ====================================================================
    public enum TransportType
    {
        Bus,
        Taxi,
        Train,
        UndergroundA,
        UndergroundB
    }

    // ====================================================================
    // Lightweight data struct returned by GetNodeData()
    // ====================================================================
    [System.Serializable]
    public struct NodeData
    {
        public int id;
        public int row;
        public int col;
        public int zone;
        public Vector3 worldPosition;
        public bool isUndergroundStation;
        public bool isCrowdBlendZone;
        public string stationName;
    }

    // ====================================================================
    // TransportNodeManager — owns the 62-node NYC transport graph
    // ====================================================================
    /// <summary>
    /// Spawns and manages all 62 transport nodes and the edges (bus, taxi,
    /// train, underground) that connect them.  This is the single source of
    /// truth for the game board.
    ///
    /// Assign the 7 prefabs in the Unity Inspector:
    ///   standardNodePrefab, undergroundNodePrefab, crowdZoneNodePrefab,
    ///   busLinePrefab, taxiLinePrefab, trainLinePrefab, undergroundLinePrefab
    ///
    /// Each line prefab must have a LineRenderer component pre-configured
    /// with the correct colour (see route line colours in the design doc).
    /// </summary>
    public class TransportNodeManager : MonoBehaviour
    {
        // ── Node prefabs (assign in Inspector) ──────────────────────────
        [Header("Node Prefabs")]
        [SerializeField] private GameObject standardNodePrefab;
        [SerializeField] private GameObject undergroundNodePrefab;
        [SerializeField] private GameObject crowdZoneNodePrefab;

        // ── Line prefabs (assign in Inspector) ──────────────────────────
        [Header("Line Prefabs — each needs a LineRenderer component")]
        [SerializeField] private GameObject busLinePrefab;
        [SerializeField] private GameObject taxiLinePrefab;
        [SerializeField] private GameObject trainLinePrefab;
        [SerializeField] private GameObject undergroundLinePrefab;

        // ── Grid settings ───────────────────────────────────────────────
        private const float CellWidth = 30f;
        private const float CellDepth = 30f;
        private static readonly Vector3 GridOrigin = new Vector3(-135f, 0f, 75f);

        // ── Runtime data ────────────────────────────────────────────────
        private readonly Dictionary<int, NodeData> nodeDataMap = new Dictionary<int, NodeData>();
        private readonly Dictionary<int, TransportNode> nodeObjectMap = new Dictionary<int, TransportNode>();
        private readonly Dictionary<int, Dictionary<TransportType, List<int>>> adjacency =
            new Dictionary<int, Dictionary<TransportType, List<int>>>();
        private readonly Dictionary<int, int> nodeZoneMap = new Dictionary<int, int>();

        private readonly List<GameObject> activeLineObjects = new List<GameObject>();

        // Row layout: (startId, endId, startCol, nodeCount)
        private static readonly (int startId, int count, int startCol)[] RowLayout =
        {
            (1,  9,  0),   // Row 0: nodes  1– 9, cols 0–8
            (10, 11, 0),   // Row 1: nodes 10–20, cols 0–10
            (21, 11, 0),   // Row 2: nodes 21–31, cols 0–10
            (32, 11, 0),   // Row 3: nodes 32–42, cols 0–10
            (43, 11, 0),   // Row 4: nodes 43–53, cols 0–10
            (54, 9,  1),   // Row 5: nodes 54–62, cols 1–9
        };

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            BuildNodeData();
            BuildAdjacency();
            SpawnNodes();
        }

        // ================================================================
        // Grid helpers
        // ================================================================

        /// <summary>Converts a grid column/row to a world-space position.</summary>
        public static Vector3 GridToWorld(int col, int row)
        {
            return GridOrigin + new Vector3(col * CellWidth, 0f, -row * CellDepth);
        }

        // ================================================================
        // Node data construction
        // ================================================================

        private void BuildNodeData()
        {
            for (int rowIdx = 0; rowIdx < RowLayout.Length; rowIdx++)
            {
                var (startId, count, startCol) = RowLayout[rowIdx];
                for (int i = 0; i < count; i++)
                {
                    int id = startId + i;
                    int col = startCol + i;
                    Vector3 pos = GridToWorld(col, rowIdx);

                    bool isUnderground = IsUndergroundStation(id);
                    bool isCrowd = IsCrowdBlendZone(id);

                    int zone = ComputeZone(col, rowIdx);

                    string name = GenerateStationName(id, rowIdx, col);

                    NodeData data = new NodeData
                    {
                        id = id,
                        row = rowIdx,
                        col = col,
                        zone = zone,
                        worldPosition = pos,
                        isUndergroundStation = isUnderground,
                        isCrowdBlendZone = isCrowd,
                        stationName = name
                    };

                    nodeDataMap[id] = data;
                    nodeZoneMap[id] = zone;
                }
            }
        }

        // ================================================================
        // Adjacency / edges
        // ================================================================

        private void BuildAdjacency()
        {
            // Initialise empty adjacency lists for every node and transport type
            foreach (int id in nodeDataMap.Keys)
            {
                adjacency[id] = new Dictionary<TransportType, List<int>>();
                foreach (TransportType t in System.Enum.GetValues(typeof(TransportType)))
                {
                    adjacency[id][t] = new List<int>();
                }
            }

            // ── Bus edges (horizontal neighbours within same row) ───────
            for (int rowIdx = 0; rowIdx < RowLayout.Length; rowIdx++)
            {
                var (startId, count, _) = RowLayout[rowIdx];
                for (int i = 0; i < count - 1; i++)
                {
                    int a = startId + i;
                    int b = startId + i + 1;
                    AddBidirectional(a, b, TransportType.Bus);
                }
            }

            // ── Bus edges (vertical neighbours between rows sharing same column) ──
            // Build a column→(id) lookup
            var columnNodes = new Dictionary<int, List<(int id, int row)>>();
            foreach (var kv in nodeDataMap)
            {
                int col = kv.Value.col;
                if (!columnNodes.ContainsKey(col))
                    columnNodes[col] = new List<(int, int)>();
                columnNodes[col].Add((kv.Key, kv.Value.row));
            }

            foreach (var kv in columnNodes)
            {
                var sorted = kv.Value.OrderBy(n => n.row).ToList();
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    // Connect nodes in adjacent rows
                    if (sorted[i + 1].row - sorted[i].row == 1)
                    {
                        AddBidirectional(sorted[i].id, sorted[i + 1].id, TransportType.Bus);
                    }
                }
            }

            // ── Taxi edges (long-range horizontal: every 2nd node in rows 1-4) ──
            for (int rowIdx = 1; rowIdx <= 4; rowIdx++)
            {
                var (startId, count, _) = RowLayout[rowIdx];
                for (int i = 0; i < count - 2; i += 2)
                {
                    int a = startId + i;
                    int b = startId + i + 2;
                    AddBidirectional(a, b, TransportType.Taxi);
                }
            }

            // ── Train edges (long diagonal / express connections) ────────
            // Train runs an express route across the grid:
            // 1→11→22→33→44→54  and  9→19→29→39→49→62
            int[] trainLineA = { 1, 11, 22, 33, 44, 54 };
            int[] trainLineB = { 9, 19, 29, 39, 49, 62 };
            for (int i = 0; i < trainLineA.Length - 1; i++)
                AddBidirectional(trainLineA[i], trainLineA[i + 1], TransportType.Train);
            for (int i = 0; i < trainLineB.Length - 1; i++)
                AddBidirectional(trainLineB[i], trainLineB[i + 1], TransportType.Train);

            // Cross-town train: 22→25→29 (mid-grid)
            AddBidirectional(22, 25, TransportType.Train);
            AddBidirectional(25, 29, TransportType.Train);

            // ── Underground A (cyan): stations 11, 44, 55 ───────────────
            AddBidirectional(11, 44, TransportType.UndergroundA);
            AddBidirectional(44, 55, TransportType.UndergroundA);

            // ── Underground B (purple): stations 17, 50 ─────────────────
            AddBidirectional(17, 50, TransportType.UndergroundB);
        }

        private void AddBidirectional(int a, int b, TransportType type)
        {
            if (!adjacency.ContainsKey(a) || !adjacency.ContainsKey(b)) return;
            if (!adjacency[a][type].Contains(b)) adjacency[a][type].Add(b);
            if (!adjacency[b][type].Contains(a)) adjacency[b][type].Add(a);
        }

        // ================================================================
        // Node spawning
        // ================================================================

        private void SpawnNodes()
        {
            foreach (var kv in nodeDataMap)
            {
                NodeData data = kv.Value;
                GameObject prefab = ChoosePrefab(data);
                if (prefab == null)
                {
                    Debug.LogWarning($"[TransportNodeManager] No prefab assigned for node {data.id}. Using empty GO.");
                    prefab = new GameObject($"Node_{data.id}_Fallback");
                }

                GameObject go = Instantiate(prefab, data.worldPosition, Quaternion.identity, transform);
                go.name = $"Node_{data.id}";

                TransportNode tn = go.GetComponent<TransportNode>();
                if (tn == null) tn = go.AddComponent<TransportNode>();

                tn.nodeId = data.id;
                tn.isUndergroundStation = data.isUndergroundStation;
                tn.isCrowdBlendZone = data.isCrowdBlendZone;
                tn.worldPosition = data.worldPosition;
                tn.Initialise();

                nodeObjectMap[data.id] = tn;
            }
        }

        private GameObject ChoosePrefab(NodeData data)
        {
            if (data.isUndergroundStation && undergroundNodePrefab != null)
                return undergroundNodePrefab;
            if (data.isCrowdBlendZone && crowdZoneNodePrefab != null)
                return crowdZoneNodePrefab;
            return standardNodePrefab;
        }

        // ================================================================
        // Public API — used by Domain B (Multiplayer) and Domain C (VR UI)
        // ================================================================

        /// <summary>
        /// Returns the IDs of all nodes reachable from <paramref name="nodeId"/>
        /// via the given transport type.  The Phantom can use all types;
        /// hunters cannot use UndergroundA / B unless they hold a special ticket.
        /// </summary>
        public List<int> GetNeighbours(int nodeId, TransportType type, bool isPhantom)
        {
            if (!adjacency.ContainsKey(nodeId)) return new List<int>();

            // Phantom has unrestricted access
            if (isPhantom) return new List<int>(adjacency[nodeId][type]);

            // Hunters can't use underground without special ticket (handled by caller)
            return new List<int>(adjacency[nodeId][type]);
        }

        /// <summary>
        /// Returns every transport type that has at least one edge from this node.
        /// </summary>
        public List<TransportType> GetAvailableTransports(int nodeId, bool isPhantom)
        {
            var result = new List<TransportType>();
            if (!adjacency.ContainsKey(nodeId)) return result;

            foreach (var kv in adjacency[nodeId])
            {
                if (kv.Value.Count > 0) result.Add(kv.Key);
            }
            return result;
        }

        /// <summary>Returns the lightweight NodeData struct for a given ID.</summary>
        public NodeData GetNodeData(int nodeId)
        {
            return nodeDataMap.ContainsKey(nodeId) ? nodeDataMap[nodeId] : default;
        }

        /// <summary>Returns the scene TransportNode component for a given ID.</summary>
        public TransportNode GetNodeObject(int id)
        {
            return nodeObjectMap.ContainsKey(id) ? nodeObjectMap[id] : null;
        }

        /// <summary>True if the node is an underground A or B station.</summary>
        public bool IsUndergroundStation(int nodeId)
        {
            return System.Array.IndexOf(GameSettings.UNDERGROUND_A_NODES, nodeId) >= 0
                || System.Array.IndexOf(GameSettings.UNDERGROUND_B_NODES, nodeId) >= 0;
        }

        /// <summary>True if the node is one of the crowd blend zones.</summary>
        public bool IsCrowdBlendZone(int nodeId)
        {
            return System.Array.IndexOf(GameSettings.CROWD_BLEND_NODES, nodeId) >= 0;
        }

        /// <summary>Returns a generated station name for the given node.</summary>
        public string GetStationName(int nodeId)
        {
            return nodeDataMap.ContainsKey(nodeId) ? nodeDataMap[nodeId].stationName : $"Node {nodeId}";
        }

        /// <summary>Returns the zone (1–8) for the given node, used for Taxi GPS pings.</summary>
        public int GetZone(int nodeId)
        {
            return nodeZoneMap.ContainsKey(nodeId) ? nodeZoneMap[nodeId] : 0;
        }

        /// <summary>Returns all node IDs that belong to the specified zone.</summary>
        public List<int> GetNodesInZone(int zone)
        {
            return nodeZoneMap.Where(kv => kv.Value == zone).Select(kv => kv.Key).ToList();
        }

        /// <summary>True if nodes a and b share at least one edge of any transport type.</summary>
        public bool AreAdjacent(int a, int b)
        {
            if (!adjacency.ContainsKey(a)) return false;
            foreach (var kv in adjacency[a])
            {
                if (kv.Value.Contains(b)) return true;
            }
            return false;
        }

        /// <summary>
        /// Highlights all nodes reachable from <paramref name="fromNode"/> via
        /// the given transport type.
        /// </summary>
        public void HighlightReachable(int fromNode, TransportType type, bool isPhantom)
        {
            ClearHighlights();
            List<int> reachable = GetNeighbours(fromNode, type, isPhantom);
            foreach (int id in reachable)
            {
                TransportNode tn = GetNodeObject(id);
                if (tn != null) tn.SetHighlight(true);
            }

            // Also draw line renderers
            DrawRouteLines(fromNode, reachable, type);
        }

        /// <summary>Clears all node highlights and removes route lines.</summary>
        public void ClearHighlights()
        {
            foreach (var kv in nodeObjectMap)
            {
                kv.Value.SetHighlight(false);
            }

            foreach (GameObject lineGo in activeLineObjects)
            {
                if (lineGo != null) Destroy(lineGo);
            }
            activeLineObjects.Clear();
        }

        // ================================================================
        // Route line rendering
        // ================================================================

        private void DrawRouteLines(int fromNode, List<int> toNodes, TransportType type)
        {
            GameObject linePrefab = GetLinePrefab(type);
            if (linePrefab == null) return;

            Vector3 fromPos = GetWorldPos(fromNode);

            foreach (int toId in toNodes)
            {
                Vector3 toPos = GetWorldPos(toId);
                GameObject lineGo = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, transform);
                LineRenderer lr = lineGo.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.positionCount = 2;
                    lr.SetPosition(0, fromPos + Vector3.up * 0.3f);
                    lr.SetPosition(1, toPos + Vector3.up * 0.3f);
                }
                activeLineObjects.Add(lineGo);
            }
        }

        private GameObject GetLinePrefab(TransportType type)
        {
            switch (type)
            {
                case TransportType.Bus: return busLinePrefab;
                case TransportType.Taxi: return taxiLinePrefab;
                case TransportType.Train: return trainLinePrefab;
                case TransportType.UndergroundA:
                case TransportType.UndergroundB:
                    return undergroundLinePrefab;
                default: return null;
            }
        }

        private Vector3 GetWorldPos(int nodeId)
        {
            return nodeDataMap.ContainsKey(nodeId) ? nodeDataMap[nodeId].worldPosition : Vector3.zero;
        }

        // ================================================================
        // Zone & name helpers
        // ================================================================

        /// <summary>
        /// Computes a zone (1–8) based on column and row.
        /// The map is divided into an approximate 4×2 zone grid.
        /// </summary>
        private static int ComputeZone(int col, int row)
        {
            int zoneCol = Mathf.Clamp(col / 3, 0, 3);  // 0-3
            int zoneRow = row < 3 ? 0 : 1;               // 0 or 1
            return zoneRow * 4 + zoneCol + 1;             // 1-8
        }

        /// <summary>Generates a thematic NYC station name for a node.</summary>
        private static string GenerateStationName(int id, int row, int col)
        {
            // Special nodes
            if (id == GameSettings.TIMES_SQUARE_NODE) return "Times Square";

            foreach (int uaId in GameSettings.UNDERGROUND_A_NODES)
                if (id == uaId) return $"Underground A - Station {id}";
            foreach (int ubId in GameSettings.UNDERGROUND_B_NODES)
                if (id == ubId) return $"Underground B - Station {id}";

            // Grid-based names
            string[] avenues = { "1st Ave", "2nd Ave", "3rd Ave", "Lexington", "Park Ave",
                                 "Madison", "5th Ave", "6th Ave", "7th Ave", "Broadway", "8th Ave" };
            string[] streets = { "42nd St", "34th St", "23rd St", "14th St", "Houston St", "Canal St" };

            string ave = col < avenues.Length ? avenues[col] : $"Ave {col}";
            string st = row < streets.Length ? streets[row] : $"{row}th St";

            return $"{ave} & {st}";
        }
    }
}
