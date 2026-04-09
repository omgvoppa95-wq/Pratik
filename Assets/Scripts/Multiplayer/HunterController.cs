using System.Collections.Generic;
using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Controls a single Hunter player's movement, ticket usage, and
    /// clue tracking.  In a networked build one instance would exist
    /// per Hunter client; currently local-authority via <see cref="GameManager"/>.
    ///
    /// Attach to each Hunter's avatar GameObject.
    /// </summary>
    public class HunterController : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TransportNodeManager nodeManager;
        [SerializeField] private TicketSystem ticketSystem;

        // ── Player identity ─────────────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private int playerId;
        [SerializeField] private string displayName;

        // ── Movement state ──────────────────────────────────────────────
        private int currentNodeId;
        private TransportType? selectedTransport;
        private List<int> reachableNodes = new List<int>();

        // ── Movement animation ──────────────────────────────────────────
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 12f;
        private bool isMoving;
        private Vector3 moveTarget;

        // ── Clue log ────────────────────────────────────────────────────
        private readonly List<string> clueLog = new List<string>();
        private const int MaxClueLogSize = 50;

        // ================================================================
        // Properties
        // ================================================================

        public int CurrentNodeId => currentNodeId;
        public bool IsMoving => isMoving;
        public IReadOnlyList<string> ClueLog => clueLog;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            AutoDiscoverReferences();
            SubscribeToClues();
        }

        private void Update()
        {
            if (isMoving)
            {
                AnimateMovement();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromClues();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Initialises the Hunter on a starting node.
        /// </summary>
        public void Initialise(int startNodeId, int id, string name)
        {
            playerId = id;
            displayName = name;
            currentNodeId = startNodeId;
            SnapToNode(currentNodeId);
        }

        /// <summary>
        /// Selects a transport type for the upcoming move.
        /// Highlights all reachable destinations on the map.
        /// </summary>
        public void SelectTransport(TransportType type)
        {
            if (gameManager == null || nodeManager == null) return;

            if (ticketSystem != null && !ticketSystem.CanUseTransport(playerId, type))
            {
                Debug.Log($"[Hunter {displayName}] No {type} tickets remaining.");
                return;
            }

            selectedTransport = type;
            reachableNodes = nodeManager.GetNeighbours(currentNodeId, type, isPhantom: false);
            nodeManager.HighlightReachable(currentNodeId, type, isPhantom: false);
        }

        /// <summary>Clears the current transport selection and all highlights.</summary>
        public void CancelSelection()
        {
            selectedTransport = null;
            reachableNodes.Clear();
            if (nodeManager != null)
            {
                nodeManager.ClearHighlights();
            }
        }

        /// <summary>
        /// Attempts to move to <paramref name="targetNodeId"/>.
        /// Must have a transport selected first.
        /// Returns true if the move succeeds.
        /// </summary>
        public bool TryMoveTo(int targetNodeId)
        {
            if (!selectedTransport.HasValue)
            {
                Debug.Log($"[Hunter {displayName}] No transport selected.");
                return false;
            }

            if (!reachableNodes.Contains(targetNodeId))
            {
                Debug.Log($"[Hunter {displayName}] Node {targetNodeId} is not reachable via {selectedTransport.Value}.");
                return false;
            }

            bool success = gameManager.TryMovePlayer(playerId, targetNodeId, selectedTransport.Value);

            if (success)
            {
                currentNodeId = targetNodeId;
                StartMoveAnimation(targetNodeId);

                selectedTransport = null;
                reachableNodes.Clear();
                nodeManager.ClearHighlights();
            }

            return success;
        }

        /// <summary>
        /// Returns the transport types the Hunter can currently afford.
        /// </summary>
        public List<TransportType> GetAvailableTransports()
        {
            if (nodeManager == null) return new List<TransportType>();

            List<TransportType> mapTypes = nodeManager.GetAvailableTransports(currentNodeId, isPhantom: false);

            if (ticketSystem != null)
            {
                return ticketSystem.GetAffordableTransports(playerId, mapTypes);
            }

            return mapTypes;
        }

        /// <summary>
        /// Returns the Hunter's ticket counts for UI display.
        /// </summary>
        public Dictionary<TransportType, int> GetTicketCounts()
        {
            if (ticketSystem == null) return new Dictionary<TransportType, int>();
            return ticketSystem.GetAllTickets(playerId);
        }

        /// <summary>
        /// Checks whether the Hunter is on the same node as the Phantom.
        /// </summary>
        public bool IsOnPhantomNode()
        {
            if (gameManager == null) return false;
            PlayerState phantomState = gameManager.GetPlayerState(gameManager.PhantomPlayerId);
            return phantomState != null && phantomState.currentNodeId == currentNodeId;
        }

        /// <summary>
        /// Returns nodes adjacent to the Hunter that are also adjacent to
        /// the Phantom's last known position.  Useful for narrowing down search.
        /// </summary>
        public List<int> GetNearbyClueNodes(int phantomLastKnownNode)
        {
            var result = new List<int>();
            if (nodeManager == null) return result;

            // Get all neighbours of the Hunter
            foreach (TransportType t in System.Enum.GetValues(typeof(TransportType)))
            {
                List<int> myNeighbours = nodeManager.GetNeighbours(currentNodeId, t, isPhantom: false);
                foreach (int n in myNeighbours)
                {
                    if (nodeManager.AreAdjacent(n, phantomLastKnownNode) && !result.Contains(n))
                    {
                        result.Add(n);
                    }
                }
            }

            return result;
        }

        // ================================================================
        // Clue subscription
        // ================================================================

        private void SubscribeToClues()
        {
            if (gameManager != null)
            {
                gameManager.OnClueGenerated += HandleClue;
            }
        }

        private void UnsubscribeFromClues()
        {
            if (gameManager != null)
            {
                gameManager.OnClueGenerated -= HandleClue;
            }
        }

        private void HandleClue(string clue)
        {
            if (clueLog.Count >= MaxClueLogSize)
            {
                clueLog.RemoveAt(0);
            }
            clueLog.Add(clue);
        }

        // ================================================================
        // Movement animation
        // ================================================================

        private void StartMoveAnimation(int targetNodeId)
        {
            NodeData data = nodeManager.GetNodeData(targetNodeId);
            moveTarget = data.worldPosition;
            moveTarget.y = transform.position.y;
            isMoving = true;
        }

        private void AnimateMovement()
        {
            transform.position = Vector3.MoveTowards(
                transform.position, moveTarget, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, moveTarget) < 0.01f)
            {
                transform.position = moveTarget;
                isMoving = false;
            }
        }

        private void SnapToNode(int nodeId)
        {
            if (nodeManager == null) return;
            NodeData data = nodeManager.GetNodeData(nodeId);
            Vector3 pos = data.worldPosition;
            pos.y = transform.position.y;
            transform.position = pos;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void AutoDiscoverReferences()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (nodeManager == null) nodeManager = FindObjectOfType<TransportNodeManager>();
            if (ticketSystem == null) ticketSystem = FindObjectOfType<TicketSystem>();
        }
    }
}
