using System.Collections.Generic;
using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Controls the Phantom player's movement, ticket usage, and special
    /// abilities.  In a networked build this would be owned by the Phantom
    /// client; currently it works via local authority through
    /// <see cref="GameManager"/>.
    ///
    /// Attach to the Phantom's avatar GameObject.
    /// </summary>
    public class PhantomController : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TransportNodeManager nodeManager;
        [SerializeField] private TicketSystem ticketSystem;

        // ── Player identity ─────────────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private int playerId;

        // ── Movement state ──────────────────────────────────────────────
        private int currentNodeId;
        private TransportType? selectedTransport;
        private List<int> reachableNodes = new List<int>();

        // ── Movement animation ──────────────────────────────────────────
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 15f;
        private bool isMoving;
        private Vector3 moveTarget;

        // ================================================================
        // Properties
        // ================================================================

        public int CurrentNodeId => currentNodeId;
        public bool IsMoving => isMoving;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            AutoDiscoverReferences();
        }

        private void Update()
        {
            if (isMoving)
            {
                AnimateMovement();
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Initialises the Phantom on a starting node.
        /// </summary>
        public void Initialise(int startNodeId, int id)
        {
            playerId = id;
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

            // Check ticket availability
            if (ticketSystem != null && !ticketSystem.CanUseTransport(playerId, type))
            {
                Debug.Log($"[Phantom] No {type} tickets remaining.");
                return;
            }

            selectedTransport = type;
            reachableNodes = nodeManager.GetNeighbours(currentNodeId, type, isPhantom: true);
            nodeManager.HighlightReachable(currentNodeId, type, isPhantom: true);
        }

        /// <summary>
        /// Clears the current transport selection and all highlights.
        /// </summary>
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
                Debug.Log("[Phantom] No transport selected.");
                return false;
            }

            if (!reachableNodes.Contains(targetNodeId))
            {
                Debug.Log($"[Phantom] Node {targetNodeId} is not reachable via {selectedTransport.Value}.");
                return false;
            }

            bool success = gameManager.TryMovePlayer(playerId, targetNodeId, selectedTransport.Value);

            if (success)
            {
                int previousNode = currentNodeId;
                currentNodeId = targetNodeId;
                StartMoveAnimation(targetNodeId);

                // Clear selection
                selectedTransport = null;
                reachableNodes.Clear();
                nodeManager.ClearHighlights();
            }

            return success;
        }

        /// <summary>
        /// Returns the transport types the Phantom can currently afford.
        /// </summary>
        public List<TransportType> GetAvailableTransports()
        {
            if (nodeManager == null) return new List<TransportType>();

            List<TransportType> mapTypes = nodeManager.GetAvailableTransports(currentNodeId, isPhantom: true);

            if (ticketSystem != null)
            {
                return ticketSystem.GetAffordableTransports(playerId, mapTypes);
            }

            return mapTypes;
        }

        /// <summary>
        /// Returns the Phantom's ticket counts for UI display.
        /// </summary>
        public Dictionary<TransportType, int> GetTicketCounts()
        {
            if (ticketSystem == null) return new Dictionary<TransportType, int>();
            return ticketSystem.GetAllTickets(playerId);
        }

        // ================================================================
        // Movement animation
        // ================================================================

        private void StartMoveAnimation(int targetNodeId)
        {
            NodeData data = nodeManager.GetNodeData(targetNodeId);
            moveTarget = data.worldPosition;
            moveTarget.y = transform.position.y; // maintain avatar height
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
