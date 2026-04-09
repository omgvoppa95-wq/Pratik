using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PhantomChaseVR
{
    /// <summary>
    /// Displays information about the local player and all other connected
    /// players: role, current node (for hunters — Phantom's node is hidden),
    /// connection status, and nearby-player indicators.
    /// </summary>
    public class PlayerInfoPanel : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("Local Player")]
        [SerializeField] private TextMeshProUGUI localPlayerNameText;
        [SerializeField] private TextMeshProUGUI localPlayerRoleText;
        [SerializeField] private TextMeshProUGUI localPlayerNodeText;

        [Header("Other Players List")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerListItemPrefab;

        [Header("Nearby Indicator")]
        [SerializeField] private TextMeshProUGUI nearbyPlayersText;

        // ── State ───────────────────────────────────────────────────────
        private int localPlayerId;
        private GameManager gameManager;
        private TransportNodeManager nodeManager;
        private readonly List<GameObject> spawnedListItems = new List<GameObject>();

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            gameManager = GameManager.Instance;
            nodeManager = FindObjectOfType<TransportNodeManager>();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Sets which player this panel represents.</summary>
        public void SetLocalPlayer(int playerId)
        {
            localPlayerId = playerId;
        }

        /// <summary>
        /// Refreshes all displayed information.
        /// Call each time the game state changes (after a move, new turn, etc.).
        /// </summary>
        public void Refresh()
        {
            if (gameManager == null) return;

            RefreshLocalPlayer();
            RefreshPlayerList();
            RefreshNearbyPlayers();
        }

        // ================================================================
        // Local player display
        // ================================================================

        private void RefreshLocalPlayer()
        {
            PlayerState local = gameManager.GetPlayerState(localPlayerId);
            if (local == null) return;

            if (localPlayerNameText != null)
            {
                localPlayerNameText.text = local.displayName;
            }

            if (localPlayerRoleText != null)
            {
                localPlayerRoleText.text = local.role.ToString();
                localPlayerRoleText.color = local.role == PlayerRole.Phantom
                    ? new Color(0.659f, 0.333f, 0.969f)
                    : new Color(0.024f, 0.714f, 0.831f);
            }

            if (localPlayerNodeText != null && nodeManager != null)
            {
                string name = nodeManager.GetStationName(local.currentNodeId);
                localPlayerNodeText.text = $"At: {name} (node {local.currentNodeId})";
            }
        }

        // ================================================================
        // Other players list
        // ================================================================

        private void RefreshPlayerList()
        {
            // Clear old items
            foreach (GameObject go in spawnedListItems)
            {
                if (go != null) Destroy(go);
            }
            spawnedListItems.Clear();

            if (playerListContainer == null) return;

            PlayerState localState = gameManager.GetPlayerState(localPlayerId);
            bool localIsHunter = localState != null && localState.role == PlayerRole.Hunter;

            foreach (PlayerState player in gameManager.GetAllPlayers())
            {
                if (player.playerId == localPlayerId) continue;

                string nodeInfo;
                if (player.role == PlayerRole.Phantom && localIsHunter)
                {
                    // Don't reveal the Phantom's position to hunters
                    nodeInfo = "???";
                }
                else if (nodeManager != null)
                {
                    nodeInfo = nodeManager.GetStationName(player.currentNodeId);
                }
                else
                {
                    nodeInfo = $"Node {player.currentNodeId}";
                }

                SpawnPlayerListItem(player, nodeInfo);
            }
        }

        private void SpawnPlayerListItem(PlayerState player, string nodeInfo)
        {
            if (playerListItemPrefab == null || playerListContainer == null) return;

            GameObject item = Instantiate(playerListItemPrefab, playerListContainer);
            item.name = $"Player_{player.playerId}";
            spawnedListItems.Add(item);

            TextMeshProUGUI label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string status = player.isConnected ? "" : " [DC]";
                label.text = $"{player.displayName} ({player.role}) — {nodeInfo}{status}";
                label.color = player.role == PlayerRole.Phantom
                    ? new Color(0.659f, 0.333f, 0.969f)
                    : new Color(0.024f, 0.714f, 0.831f);
            }
        }

        // ================================================================
        // Nearby players indicator
        // ================================================================

        private void RefreshNearbyPlayers()
        {
            if (nearbyPlayersText == null || nodeManager == null) return;

            PlayerState local = gameManager.GetPlayerState(localPlayerId);
            if (local == null) return;

            var nearbyNames = new List<string>();

            foreach (PlayerState player in gameManager.GetAllPlayers())
            {
                if (player.playerId == localPlayerId) continue;
                if (!player.isConnected) continue;

                // "Nearby" = same node or adjacent node
                if (player.currentNodeId == local.currentNodeId ||
                    nodeManager.AreAdjacent(player.currentNodeId, local.currentNodeId))
                {
                    nearbyNames.Add(player.displayName);
                }
            }

            if (nearbyNames.Count == 0)
            {
                nearbyPlayersText.text = "No players nearby";
                nearbyPlayersText.color = Color.grey;
            }
            else
            {
                nearbyPlayersText.text = $"Nearby: {string.Join(", ", nearbyNames)}";
                nearbyPlayersText.color = Color.yellow;
            }
        }
    }
}
