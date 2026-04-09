using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PhantomChaseVR
{
    /// <summary>
    /// Renders an overhead minimap showing the 62 transport nodes and
    /// player positions.  Uses a RenderTexture from a dedicated orthographic
    /// camera, or falls back to a UI-based dot representation.
    ///
    /// Modes:
    ///   1. Camera-based: a second orthographic camera renders the scene
    ///      top-down into a RenderTexture displayed on a RawImage.
    ///   2. UI-dot-based: procedurally places coloured dots on a panel
    ///      based on node world positions (no second camera needed).
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        // ── Mode ────────────────────────────────────────────────────────
        [Header("Mode")]
        [SerializeField] private bool useCameraMode = true;

        // ── Camera mode references ──────────────────────────────────────
        [Header("Camera Mode")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RawImage minimapRawImage;
        [SerializeField] private int textureSize = 512;

        // ── UI-dot mode references ──────────────────────────────────────
        [Header("UI-Dot Mode")]
        [SerializeField] private RectTransform dotContainer;
        [SerializeField] private GameObject nodeDotPrefab;
        [SerializeField] private GameObject playerDotPrefab;

        [Header("Map Bounds (world-space)")]
        [SerializeField] private float mapMinX = -150f;
        [SerializeField] private float mapMaxX = 150f;
        [SerializeField] private float mapMinZ = -100f;
        [SerializeField] private float mapMaxZ = 100f;

        // ── Player marker colours ───────────────────────────────────────
        private static readonly Color PhantomColor = new Color(0.659f, 0.333f, 0.969f); // purple
        private static readonly Color HunterColor = new Color(0.024f, 0.714f, 0.831f);  // cyan
        private static readonly Color NodeDotColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        // ── Runtime ─────────────────────────────────────────────────────
        private TransportNodeManager nodeManager;
        private GameManager gameManager;
        private readonly Dictionary<int, GameObject> nodeDots = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> playerDots = new Dictionary<int, GameObject>();
        private RenderTexture minimapRT;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            nodeManager = FindObjectOfType<TransportNodeManager>();
            gameManager = GameManager.Instance;

            if (useCameraMode)
            {
                SetupCameraMode();
            }
            else
            {
                SetupDotMode();
            }
        }

        private void LateUpdate()
        {
            if (!useCameraMode)
            {
                UpdatePlayerDots();
            }
        }

        private void OnDestroy()
        {
            if (minimapRT != null)
            {
                minimapRT.Release();
            }
        }

        // ================================================================
        // Camera mode setup
        // ================================================================

        private void SetupCameraMode()
        {
            if (minimapCamera == null) return;

            minimapRT = new RenderTexture(textureSize, textureSize, 16);
            minimapCamera.targetTexture = minimapRT;
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = 100f;
            minimapCamera.transform.position = new Vector3(0f, 200f, -15f);
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            minimapCamera.cullingMask = ~0; // render everything

            if (minimapRawImage != null)
            {
                minimapRawImage.texture = minimapRT;
            }
        }

        // ================================================================
        // UI-dot mode setup
        // ================================================================

        private void SetupDotMode()
        {
            if (nodeManager == null || dotContainer == null) return;

            // Create a dot for each node
            for (int id = 1; id <= GameSettings.TOTAL_NODES; id++)
            {
                NodeData data = nodeManager.GetNodeData(id);
                if (data.id == 0) continue;

                Vector2 uiPos = WorldToMinimap(data.worldPosition);
                GameObject dot = CreateDot(nodeDotPrefab, dotContainer, uiPos, NodeDotColor, $"NodeDot_{id}");
                nodeDots[id] = dot;
            }
        }

        // ================================================================
        // Player dot updates (UI-dot mode only)
        // ================================================================

        private void UpdatePlayerDots()
        {
            if (gameManager == null || dotContainer == null) return;

            // Remove stale player dots
            var staleIds = new List<int>();
            foreach (var kv in playerDots)
            {
                PlayerState state = gameManager.GetPlayerState(kv.Key);
                if (state == null || !state.isConnected)
                {
                    staleIds.Add(kv.Key);
                }
            }
            foreach (int id in staleIds)
            {
                if (playerDots[id] != null) Destroy(playerDots[id]);
                playerDots.Remove(id);
            }

            // Update / create player dots
            foreach (PlayerState player in gameManager.GetAllPlayers())
            {
                if (!player.isConnected) continue;

                NodeData nodeData = nodeManager.GetNodeData(player.currentNodeId);
                if (nodeData.id == 0) continue;

                Vector2 uiPos = WorldToMinimap(nodeData.worldPosition);
                Color color = player.role == PlayerRole.Phantom ? PhantomColor : HunterColor;

                if (playerDots.ContainsKey(player.playerId) && playerDots[player.playerId] != null)
                {
                    // Update position
                    RectTransform rt = playerDots[player.playerId].GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = uiPos;
                }
                else
                {
                    // Create new dot
                    GameObject dot = CreateDot(playerDotPrefab, dotContainer, uiPos, color,
                                               $"PlayerDot_{player.playerId}");
                    // Make player dots slightly larger
                    RectTransform rt = dot.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(8f, 8f);
                    playerDots[player.playerId] = dot;
                }
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Vector2 WorldToMinimap(Vector3 worldPos)
        {
            if (dotContainer == null) return Vector2.zero;

            Rect rect = dotContainer.rect;
            float nx = Mathf.InverseLerp(mapMinX, mapMaxX, worldPos.x);
            float nz = Mathf.InverseLerp(mapMinZ, mapMaxZ, worldPos.z);

            return new Vector2(
                rect.xMin + nx * rect.width,
                rect.yMin + nz * rect.height
            );
        }

        private GameObject CreateDot(GameObject prefab, RectTransform parent,
                                      Vector2 anchoredPos, Color color, string name)
        {
            GameObject dot;
            if (prefab != null)
            {
                dot = Instantiate(prefab, parent);
            }
            else
            {
                // Fallback: create a simple UI image
                dot = new GameObject(name);
                dot.transform.SetParent(parent, false);
                Image img = dot.AddComponent<Image>();
                img.color = color;
                RectTransform rt = dot.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(4f, 4f);
            }

            dot.name = name;
            RectTransform dotRT = dot.GetComponent<RectTransform>();
            if (dotRT != null)
            {
                dotRT.anchoredPosition = anchoredPos;
            }

            // Set colour if using Image
            Image image = dot.GetComponent<Image>();
            if (image != null) image.color = color;

            return dot;
        }
    }
}
