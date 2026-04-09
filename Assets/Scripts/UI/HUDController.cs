using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PhantomChaseVR
{
    /// <summary>
    /// Heads-up display shown at all times during gameplay.
    /// Displays: turn number, current phase, move timer, ticket counts,
    /// and a scrolling clue feed.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("Turn Info")]
        [SerializeField] private TextMeshProUGUI turnNumberText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Role")]
        [SerializeField] private TextMeshProUGUI roleText;

        [Header("Tickets")]
        [SerializeField] private TextMeshProUGUI busTicketText;
        [SerializeField] private TextMeshProUGUI taxiTicketText;
        [SerializeField] private TextMeshProUGUI trainTicketText;
        [SerializeField] private TextMeshProUGUI undergroundATicketText;
        [SerializeField] private TextMeshProUGUI undergroundBTicketText;

        [Header("Clue Feed")]
        [SerializeField] private TextMeshProUGUI clueFeedText;
        [SerializeField] private int maxClueLines = 5;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI currentNodeText;

        // ── Clue log ────────────────────────────────────────────────────
        private readonly List<string> clueLines = new List<string>();

        // ── References ──────────────────────────────────────────────────
        private TurnManager turnManager;
        private GameManager gameManager;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            gameManager = GameManager.Instance;
            turnManager = FindObjectOfType<TurnManager>();

            if (turnManager != null)
            {
                turnManager.OnPhaseChanged += HandlePhaseChanged;
                turnManager.OnTimerTick += HandleTimerTick;
            }

            if (gameManager != null)
            {
                gameManager.OnClueGenerated += HandleClue;
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnPhaseChanged -= HandlePhaseChanged;
                turnManager.OnTimerTick -= HandleTimerTick;
            }

            if (gameManager != null)
            {
                gameManager.OnClueGenerated -= HandleClue;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Sets the player role display.</summary>
        public void SetRole(PlayerRole role)
        {
            if (roleText != null)
            {
                roleText.text = role.ToString().ToUpper();
                roleText.color = role == PlayerRole.Phantom
                    ? new Color(0.659f, 0.333f, 0.969f)  // purple
                    : new Color(0.024f, 0.714f, 0.831f);  // cyan
            }
        }

        /// <summary>Updates the ticket count display.</summary>
        public void UpdateTickets(Dictionary<TransportType, int> tickets)
        {
            SetTicketText(busTicketText, TransportType.Bus, tickets);
            SetTicketText(taxiTicketText, TransportType.Taxi, tickets);
            SetTicketText(trainTicketText, TransportType.Train, tickets);
            SetTicketText(undergroundATicketText, TransportType.UndergroundA, tickets);
            SetTicketText(undergroundBTicketText, TransportType.UndergroundB, tickets);
        }

        /// <summary>Updates the current node display.</summary>
        public void SetCurrentNode(int nodeId, string stationName)
        {
            if (currentNodeText != null)
            {
                currentNodeText.text = $"Node {nodeId}: {stationName}";
            }
        }

        /// <summary>Adds a clue to the feed.</summary>
        public void AddClue(string clue)
        {
            clueLines.Add(clue);
            while (clueLines.Count > maxClueLines)
            {
                clueLines.RemoveAt(0);
            }
            RefreshClueFeed();
        }

        // ================================================================
        // Event handlers
        // ================================================================

        private void HandlePhaseChanged(TurnPhase phase, int turn)
        {
            if (turnNumberText != null)
            {
                turnNumberText.text = $"Turn {turn}";
            }

            if (phaseText != null)
            {
                phaseText.text = PhaseToDisplayString(phase);
                phaseText.color = phase == TurnPhase.Resolution
                    ? Color.yellow
                    : Color.white;
            }
        }

        private void HandleTimerTick(float secondsRemaining)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(secondsRemaining);
                timerText.text = $"{seconds}s";
                timerText.color = seconds <= 5 ? Color.red : Color.white;
            }
        }

        private void HandleClue(string clue)
        {
            AddClue(clue);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void SetTicketText(TextMeshProUGUI label, TransportType type,
                                    Dictionary<TransportType, int> tickets)
        {
            if (label == null) return;
            int count = tickets.ContainsKey(type) ? tickets[type] : 0;
            label.text = $"{type}: {count}";
        }

        private void RefreshClueFeed()
        {
            if (clueFeedText == null) return;
            clueFeedText.text = string.Join("\n", clueLines);
        }

        private static string PhaseToDisplayString(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.WaitingToStart: return "Waiting…";
                case TurnPhase.PhantomMove: return "Phantom's Move";
                case TurnPhase.HunterMove: return "Hunters' Move";
                case TurnPhase.Resolution: return "Resolving…";
                case TurnPhase.RoundEnd: return "Round Over";
                default: return phase.ToString();
            }
        }
    }
}
