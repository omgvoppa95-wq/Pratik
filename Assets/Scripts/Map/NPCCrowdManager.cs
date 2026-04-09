// ============================================================================
// NAVMESH SETUP INSTRUCTIONS
// ============================================================================
// SETUP: Window > AI > Navigation > Bake
// Agent radius: 0.3, Agent height: 1.8, Max slope: 10, Step height: 0.3
// Walkable areas: streets + sidewalks ONLY (not building interiors)
// Mark all building colliders as Navigation Static > Not Walkable
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PhantomChaseVR
{
    /// <summary>
    /// Manages 50 NPC pedestrians that walk the streets via NavMesh.
    /// NPCs give the city life and provide indirect clues to hunters when
    /// the Phantom causes them to panic.
    /// </summary>
    public class NPCCrowdManager : MonoBehaviour
    {
        // ── Inspector fields ────────────────────────────────────────────
        [Header("NPC Prefabs")]
        [SerializeField] private GameObject[] npcVariantPrefabs;  // 3-5 visual variants
        [SerializeField] private int totalNPCs = 50;
        [SerializeField] private float spawnRadius = 90f;
        [SerializeField] private float minSpawnRadius = 8f;

        [Header("Behaviour")]
        [SerializeField] private float normalWalkSpeed = 1.0f;
        [SerializeField] private float panicRunSpeed = 4.5f;
        [SerializeField] private float phantomReactRadius = 6f;
        [SerializeField] private float groupChance = 0.35f;

        [Header("Crowd Blend Zone Density")]
        [SerializeField] private int crowdBlendMultiplier = 3;

        [Header("Phantom Reference")]
        [Tooltip("Assign the Phantom player transform for panic detection.")]
        [SerializeField] private Transform phantomTransform;

        // ── Runtime ─────────────────────────────────────────────────────
        private readonly List<NPCAgent> allAgents = new List<NPCAgent>();
        private TransportNodeManager nodeManager;

        // Cached crowd blend zone world positions
        private readonly List<Vector3> crowdBlendPositions = new List<Vector3>();

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            nodeManager = FindObjectOfType<TransportNodeManager>();
            CacheCrowdBlendPositions();
            SpawnNPCs();
        }

        private void Update()
        {
            UpdatePanicState();
            ReassignIdleAgents();
        }

        // ================================================================
        // Crowd blend zone caching
        // ================================================================

        private void CacheCrowdBlendPositions()
        {
            if (nodeManager == null) return;

            foreach (int nodeId in GameSettings.CROWD_BLEND_NODES)
            {
                NodeData data = nodeManager.GetNodeData(nodeId);
                if (data.id != 0)
                {
                    crowdBlendPositions.Add(data.worldPosition);
                }
            }
        }

        // ================================================================
        // Spawning
        // ================================================================

        private void SpawnNPCs()
        {
            if (npcVariantPrefabs == null || npcVariantPrefabs.Length == 0)
            {
                Debug.LogWarning("[NPCCrowdManager] No NPC variant prefabs assigned.");
                return;
            }

            Transform npcParent = new GameObject("NPCs").transform;
            npcParent.SetParent(transform);

            int spawned = 0;

            // Phase 1: Spawn extra NPCs in crowd blend zones (3× density)
            foreach (Vector3 zonePos in crowdBlendPositions)
            {
                int extraCount = (crowdBlendMultiplier - 1) * 2; // extra NPCs per zone
                for (int i = 0; i < extraCount && spawned < totalNPCs * 2; i++)
                {
                    Vector3 spawnPos;
                    if (TryGetNavMeshPosition(zonePos, minSpawnRadius * 2f, out spawnPos))
                    {
                        SpawnSingleNPC(spawnPos, npcParent, ref spawned);
                    }
                }
            }

            // Phase 2: Spawn remaining NPCs distributed around the city
            int attempts = 0;
            while (spawned < totalNPCs && attempts < totalNPCs * 10)
            {
                attempts++;
                Vector3 randomCenter = GetWeightedSpawnCenter();
                Vector3 offset = Random.insideUnitSphere * spawnRadius;
                offset.y = 0f;
                Vector3 candidatePos = randomCenter + offset;

                if (Vector3.Distance(candidatePos, randomCenter) < minSpawnRadius) continue;

                Vector3 spawnPos;
                if (TryGetNavMeshPosition(candidatePos, 5f, out spawnPos))
                {
                    SpawnSingleNPC(spawnPos, npcParent, ref spawned);
                }
            }

            // Phase 3: Assign group pairs (35% chance)
            AssignGroupPairs();
        }

        private void SpawnSingleNPC(Vector3 position, Transform parent, ref int count)
        {
            GameObject prefab = npcVariantPrefabs[count % npcVariantPrefabs.Length];
            GameObject npcGo = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            npcGo.name = $"NPC_{count}";

            NavMeshAgent agent = npcGo.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = npcGo.AddComponent<NavMeshAgent>();
            }

            agent.speed = normalWalkSpeed;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking = true;

            NPCAgent npcAgent = new NPCAgent
            {
                gameObject = npcGo,
                agent = agent,
                isPanicking = false,
                partner = null
            };

            // Give an initial waypoint
            SetRandomWaypoint(npcAgent);

            allAgents.Add(npcAgent);
            count++;
        }

        // ================================================================
        // Group pairing
        // ================================================================

        private void AssignGroupPairs()
        {
            for (int i = 0; i < allAgents.Count - 1; i += 2)
            {
                if (Random.value < groupChance)
                {
                    allAgents[i].partner = allAgents[i + 1];
                    allAgents[i + 1].partner = allAgents[i];

                    // Partners share the same destination
                    if (allAgents[i].agent != null && allAgents[i].agent.hasPath)
                    {
                        allAgents[i + 1].agent.SetDestination(allAgents[i].agent.destination);
                    }
                }
            }
        }

        // ================================================================
        // Panic detection (runs every frame)
        // ================================================================

        private void UpdatePanicState()
        {
            if (phantomTransform == null) return;

            Vector3 phantomPos = phantomTransform.position;

            foreach (NPCAgent npc in allAgents)
            {
                if (npc.agent == null || npc.gameObject == null) continue;

                float dist = Vector3.Distance(npc.gameObject.transform.position, phantomPos);

                if (dist <= phantomReactRadius)
                {
                    if (!npc.isPanicking)
                    {
                        TriggerPanic(npc, phantomPos);
                    }
                }
                else if (npc.isPanicking)
                {
                    // Calm down once far enough away
                    CalmDown(npc);
                }
            }
        }

        private void TriggerPanic(NPCAgent npc, Vector3 phantomPos)
        {
            npc.isPanicking = true;
            npc.agent.speed = panicRunSpeed;

            // Run away from the phantom
            Vector3 fleeDir = (npc.gameObject.transform.position - phantomPos).normalized;
            Vector3 fleeTarget = npc.gameObject.transform.position + fleeDir * 20f;

            Vector3 navTarget;
            if (TryGetNavMeshPosition(fleeTarget, 10f, out navTarget))
            {
                npc.agent.SetDestination(navTarget);
            }

            // If this NPC has a partner, make partner panic too
            if (npc.partner != null && !npc.partner.isPanicking)
            {
                TriggerPanic(npc.partner, phantomPos);
            }
        }

        private void CalmDown(NPCAgent npc)
        {
            npc.isPanicking = false;
            npc.agent.speed = normalWalkSpeed;
            SetRandomWaypoint(npc);
        }

        // ================================================================
        // Idle waypoint reassignment
        // ================================================================

        private void ReassignIdleAgents()
        {
            foreach (NPCAgent npc in allAgents)
            {
                if (npc.agent == null || npc.gameObject == null) continue;
                if (npc.isPanicking) continue;

                // If agent has reached destination or has no path, assign new waypoint
                if (!npc.agent.pathPending && npc.agent.remainingDistance <= npc.agent.stoppingDistance)
                {
                    SetRandomWaypoint(npc);

                    // If paired, partner follows to same waypoint
                    if (npc.partner != null && npc.partner.agent != null)
                    {
                        npc.partner.agent.SetDestination(npc.agent.destination);
                    }
                }
            }
        }

        // ================================================================
        // Waypoint helpers
        // ================================================================

        private void SetRandomWaypoint(NPCAgent npc)
        {
            Vector3 center = npc.gameObject.transform.position;
            Vector3 offset = Random.insideUnitSphere * 40f;
            offset.y = 0f;

            Vector3 navTarget;
            if (TryGetNavMeshPosition(center + offset, 10f, out navTarget))
            {
                npc.agent.SetDestination(navTarget);
            }
        }

        /// <summary>
        /// Returns a weighted spawn centre: biased towards transport nodes
        /// and especially crowd blend zones for higher density.
        /// </summary>
        private Vector3 GetWeightedSpawnCenter()
        {
            // 40% chance to bias towards a crowd blend zone
            if (crowdBlendPositions.Count > 0 && Random.value < 0.4f)
            {
                return crowdBlendPositions[Random.Range(0, crowdBlendPositions.Count)];
            }

            // Otherwise random point within city bounds
            float x = Random.Range(-135f, -135f + 270f);
            float z = Random.Range(75f - 150f, 75f);
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// Attempts to find a valid NavMesh position near the given point.
        /// </summary>
        private static bool TryGetNavMeshPosition(Vector3 center, float maxRange, out Vector3 result)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(center, out hit, maxRange, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = center;
            return false;
        }

        // ================================================================
        // Internal NPC data structure
        // ================================================================

        private class NPCAgent
        {
            public GameObject gameObject;
            public NavMeshAgent agent;
            public bool isPanicking;
            public NPCAgent partner;
        }
    }
}
