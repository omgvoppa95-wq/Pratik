using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Describes the role each player takes in a round.
    /// </summary>
    public enum PlayerRole
    {
        Phantom,
        Hunter
    }

    /// <summary>
    /// Runtime data for a single connected player.
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public int playerId;
        public PlayerRole role;
        public int currentNodeId;
        public bool isConnected;
        public string displayName;
    }

    /// <summary>
    /// Central game-state authority.  Owns the player registry, orchestrates
    /// turns via <see cref="TurnManager"/>, enforces ticket consumption via
    /// <see cref="TicketSystem"/>, and bridges Domain A (map) with
    /// Domain B (multiplayer) and Domain C (VR UI).
    ///
    /// In a networked build this class would be a Photon MonoBehaviourPunCallbacks
    /// with RPCs.  The current implementation is single-player / local-authority
    /// so that it compiles without the Photon SDK installed.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── References (auto-discovered or assigned in Inspector) ───────
        [Header("Domain A References")]
        [SerializeField] private TransportNodeManager nodeManager;
        [SerializeField] private PostProcessingController postProcessing;
        [SerializeField] private NPCCrowdManager npcCrowdManager;

        [Header("Domain B References")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private TicketSystem ticketSystem;

        // ── Game configuration ──────────────────────────────────────────
        [Header("Game Config")]
        [SerializeField] private int phantomStartNode = 25; // Times Square
        [SerializeField] private int[] hunterStartNodes = { 1, 9, 54, 62 };

        // ── Events (consumed by Domain C / UI) ──────────────────────────
        /// <summary>Fired when any player moves.  Args: playerId, fromNode, toNode, transport.</summary>
        public event Action<int, int, int, TransportType> OnPlayerMoved;

        /// <summary>Fired when the Phantom is caught.  Arg: hunterId who caught them.</summary>
        public event Action<int> OnPhantomCaught;

        /// <summary>Fired when the round ends.  Arg: true if Phantom survived.</summary>
        public event Action<bool> OnRoundEnded;

        /// <summary>Fired when a clue is generated.  Arg: clue text.</summary>
        public event Action<string> OnClueGenerated;

        // ── Runtime state ───────────────────────────────────────────────
        private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
        private int phantomPlayerId = -1;
        private bool roundInProgress;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AutoDiscoverReferences();
        }

        private void Start()
        {
            if (turnManager != null)
            {
                turnManager.OnRevealTurn += HandleRevealTurn;
                turnManager.OnRoundComplete += HandleRoundComplete;
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnRevealTurn -= HandleRevealTurn;
                turnManager.OnRoundComplete -= HandleRoundComplete;
            }

            if (Instance == this) Instance = null;
        }

        // ================================================================
        // Public API — Player management
        // ================================================================

        /// <summary>
        /// Registers a player.  The first registered player becomes the Phantom;
        /// all others are Hunters.
        /// </summary>
        public void RegisterPlayer(int playerId, string displayName)
        {
            PlayerRole role = players.Count == 0 ? PlayerRole.Phantom : PlayerRole.Hunter;

            PlayerState state = new PlayerState
            {
                playerId = playerId,
                role = role,
                currentNodeId = -1,
                isConnected = true,
                displayName = displayName
            };

            players[playerId] = state;

            if (role == PlayerRole.Phantom)
            {
                phantomPlayerId = playerId;
            }

            ticketSystem.InitialisePlayer(playerId, role == PlayerRole.Phantom);
        }

        /// <summary>Removes a player who disconnected.</summary>
        public void UnregisterPlayer(int playerId)
        {
            players.Remove(playerId);
            ticketSystem.RemovePlayer(playerId);
        }

        /// <summary>Returns the state for a given player.</summary>
        public PlayerState GetPlayerState(int playerId)
        {
            return players.ContainsKey(playerId) ? players[playerId] : null;
        }

        /// <summary>Returns all registered players.</summary>
        public IEnumerable<PlayerState> GetAllPlayers()
        {
            return players.Values;
        }

        /// <summary>Returns the phantom player's ID.</summary>
        public int PhantomPlayerId => phantomPlayerId;

        // ================================================================
        // Public API — Round lifecycle
        // ================================================================

        /// <summary>
        /// Places all players on their starting nodes and begins the round.
        /// </summary>
        public void StartRound()
        {
            if (roundInProgress) return;
            roundInProgress = true;

            // Place Phantom
            if (players.ContainsKey(phantomPlayerId))
            {
                players[phantomPlayerId].currentNodeId = phantomStartNode;
            }

            // Place Hunters
            int hunterIdx = 0;
            foreach (var kv in players)
            {
                if (kv.Value.role == PlayerRole.Hunter)
                {
                    int startNode = hunterIdx < hunterStartNodes.Length
                        ? hunterStartNodes[hunterIdx]
                        : 1;
                    kv.Value.currentNodeId = startNode;
                    hunterIdx++;
                }
            }

            int hunterCount = 0;
            foreach (var kv in players)
            {
                if (kv.Value.role == PlayerRole.Hunter) hunterCount++;
            }

            turnManager.StartRound(hunterCount);
        }

        // ================================================================
        // Public API — Movement
        // ================================================================

        /// <summary>
        /// Attempts to move a player from their current node to
        /// <paramref name="targetNodeId"/> using the specified transport.
        /// Returns true if the move is valid and tickets are consumed.
        /// </summary>
        public bool TryMovePlayer(int playerId, int targetNodeId, TransportType transport)
        {
            if (!players.ContainsKey(playerId)) return false;

            PlayerState state = players[playerId];
            int fromNode = state.currentNodeId;
            bool isPhantom = state.role == PlayerRole.Phantom;

            // Validate edge exists
            List<int> neighbours = nodeManager.GetNeighbours(fromNode, transport, isPhantom);
            if (!neighbours.Contains(targetNodeId)) return false;

            // Validate ticket
            if (!ticketSystem.UseTicket(playerId, transport)) return false;

            // Execute move
            state.currentNodeId = targetNodeId;
            OnPlayerMoved?.Invoke(playerId, fromNode, targetNodeId, transport);

            // Check capture (hunter lands on Phantom's node)
            if (!isPhantom)
            {
                CheckCapture(playerId);
            }

            // Generate clue for hunters if Phantom moved
            if (isPhantom)
            {
                GenerateClue(transport, fromNode);
            }

            // Submit to turn manager
            if (isPhantom)
            {
                turnManager.SubmitPhantomMove();
            }
            else
            {
                turnManager.SubmitHunterMove();
            }

            return true;
        }

        /// <summary>
        /// Returns the set of valid destination node IDs for a player
        /// using a given transport type from their current position.
        /// Checks both adjacency and ticket availability.
        /// </summary>
        public List<int> GetValidMoves(int playerId, TransportType transport)
        {
            if (!players.ContainsKey(playerId)) return new List<int>();

            PlayerState state = players[playerId];
            bool isPhantom = state.role == PlayerRole.Phantom;

            if (!ticketSystem.CanUseTransport(playerId, transport))
                return new List<int>();

            return nodeManager.GetNeighbours(state.currentNodeId, transport, isPhantom);
        }

        /// <summary>
        /// Highlights all reachable nodes from a player's current position
        /// for a given transport type.
        /// </summary>
        public void ShowReachableNodes(int playerId, TransportType transport)
        {
            if (!players.ContainsKey(playerId)) return;
            PlayerState state = players[playerId];
            bool isPhantom = state.role == PlayerRole.Phantom;
            nodeManager.HighlightReachable(state.currentNodeId, transport, isPhantom);
        }

        /// <summary>Clears all highlighted nodes.</summary>
        public void ClearReachableHighlights()
        {
            nodeManager.ClearHighlights();
        }

        // ================================================================
        // Internal — capture detection
        // ================================================================

        private void CheckCapture(int hunterId)
        {
            if (phantomPlayerId < 0) return;
            if (!players.ContainsKey(phantomPlayerId)) return;

            PlayerState hunter = players[hunterId];
            PlayerState phantom = players[phantomPlayerId];

            if (hunter.currentNodeId == phantom.currentNodeId)
            {
                roundInProgress = false;
                OnPhantomCaught?.Invoke(hunterId);
                OnRoundEnded?.Invoke(false); // Phantom did NOT survive
                turnManager.ForceRoundEnd();
            }
        }

        // ================================================================
        // Internal — clue generation
        // ================================================================

        private void GenerateClue(TransportType transport, int fromNodeId)
        {
            // Clue tells hunters which transport the Phantom used and the zone
            int zone = nodeManager.GetZone(fromNodeId);
            string clue = $"The Phantom used {transport} from zone {zone}.";
            OnClueGenerated?.Invoke(clue);
        }

        // ================================================================
        // Internal — reveal handling
        // ================================================================

        private void HandleRevealTurn(int turn)
        {
            if (postProcessing != null)
            {
                postProcessing.TriggerRevealEffect();
            }

            // Broadcast the Phantom's current node to all hunters
            if (players.ContainsKey(phantomPlayerId))
            {
                int phantomNode = players[phantomPlayerId].currentNodeId;
                TransportNode tn = nodeManager.GetNodeObject(phantomNode);
                if (tn != null)
                {
                    tn.SetHighlight(true);
                }

                string stationName = nodeManager.GetStationName(phantomNode);
                OnClueGenerated?.Invoke($"REVEAL — The Phantom is at {stationName} (node {phantomNode})!");
            }
        }

        private void HandleRoundComplete()
        {
            roundInProgress = false;

            if (postProcessing != null)
            {
                postProcessing.TriggerFinalRevealEffect();
            }

            OnRoundEnded?.Invoke(true); // Phantom survived
        }

        // ================================================================
        // Internal — auto-discovery
        // ================================================================

        private void AutoDiscoverReferences()
        {
            if (nodeManager == null) nodeManager = FindObjectOfType<TransportNodeManager>();
            if (postProcessing == null) postProcessing = FindObjectOfType<PostProcessingController>();
            if (npcCrowdManager == null) npcCrowdManager = FindObjectOfType<NPCCrowdManager>();
            if (turnManager == null) turnManager = GetComponent<TurnManager>();
            if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
            if (ticketSystem == null) ticketSystem = GetComponent<TicketSystem>();
            if (ticketSystem == null) ticketSystem = FindObjectOfType<TicketSystem>();

            // Ensure TurnManager and TicketSystem exist (add to this GO if missing)
            if (turnManager == null) turnManager = gameObject.AddComponent<TurnManager>();
            if (ticketSystem == null) ticketSystem = gameObject.AddComponent<TicketSystem>();
        }
    }
}
