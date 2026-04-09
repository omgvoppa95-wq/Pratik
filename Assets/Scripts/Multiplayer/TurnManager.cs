using System;
using System.Collections;
using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Phase within a single game turn.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>Waiting for the round to start.</summary>
        WaitingToStart,
        /// <summary>Phantom chooses a transport and destination.</summary>
        PhantomMove,
        /// <summary>Hunters choose in order (or simultaneously).</summary>
        HunterMove,
        /// <summary>Post-move: reveal checks, clue generation, event cards.</summary>
        Resolution,
        /// <summary>Round over (all turns exhausted or Phantom caught).</summary>
        RoundEnd
    }

    /// <summary>
    /// Manages the turn-based flow of a single round.
    ///
    /// Turn sequence:
    ///   1.  PhantomMove  — Phantom picks transport + destination
    ///   2.  HunterMove   — each Hunter picks (order determined by seat)
    ///   3.  Resolution   — reveal timing, clue broadcast, event check
    ///   4.  Repeat from 1 until max turns or Phantom is caught.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // ── Configuration ───────────────────────────────────────────────
        [Header("Turn Settings")]
        [SerializeField] private int maxTurnsPerRound = 22;
        [SerializeField] private float moveTimeLimitSeconds = 30f;

        [Header("Reveal Timing")]
        [Tooltip("Turns on which the Phantom's position is revealed (1-indexed).")]
        [SerializeField] private int[] revealTurns = { 3, 8, 13, 18 };

        // ── Events ──────────────────────────────────────────────────────
        /// <summary>Fired when the turn phase changes.  Args: new phase, current turn number.</summary>
        public event Action<TurnPhase, int> OnPhaseChanged;

        /// <summary>Fired when the move timer ticks.  Arg: seconds remaining.</summary>
        public event Action<float> OnTimerTick;

        /// <summary>Fired when a reveal turn is reached.</summary>
        public event Action<int> OnRevealTurn;

        /// <summary>Fired when all turns are exhausted (Phantom wins by survival).</summary>
        public event Action OnRoundComplete;

        // ── Runtime state ───────────────────────────────────────────────
        private int currentTurn;
        private TurnPhase currentPhase = TurnPhase.WaitingToStart;
        private Coroutine timerCoroutine;
        private bool phantomMoveSubmitted;
        private int hunterMovesRemaining;
        private int totalHunters;

        // ================================================================
        // Properties
        // ================================================================

        public int CurrentTurn => currentTurn;
        public TurnPhase CurrentPhase => currentPhase;
        public float MoveTimeLimit => moveTimeLimitSeconds;

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Starts a new round.  Call after all players have joined and
        /// are placed on their starting nodes.
        /// </summary>
        public void StartRound(int hunterCount)
        {
            totalHunters = hunterCount;
            currentTurn = 0;
            AdvanceToNextTurn();
        }

        /// <summary>
        /// Called by PhantomController when the Phantom submits a move.
        /// </summary>
        public void SubmitPhantomMove()
        {
            if (currentPhase != TurnPhase.PhantomMove) return;
            phantomMoveSubmitted = true;
            StopTimer();
            SetPhase(TurnPhase.HunterMove);
            hunterMovesRemaining = totalHunters;
            StartTimer();
        }

        /// <summary>
        /// Called by HunterController when a Hunter submits their move.
        /// </summary>
        public void SubmitHunterMove()
        {
            if (currentPhase != TurnPhase.HunterMove) return;
            hunterMovesRemaining--;
            if (hunterMovesRemaining <= 0)
            {
                StopTimer();
                SetPhase(TurnPhase.Resolution);
                StartCoroutine(ResolvePhase());
            }
        }

        /// <summary>
        /// Forces the current phase to end (e.g. if the Phantom is caught
        /// mid-turn).
        /// </summary>
        public void ForceRoundEnd()
        {
            StopTimer();
            StopAllCoroutines();
            SetPhase(TurnPhase.RoundEnd);
        }

        /// <summary>Returns true if this turn is a reveal turn.</summary>
        public bool IsRevealTurn()
        {
            return IsRevealTurn(currentTurn);
        }

        /// <summary>Returns true if <paramref name="turn"/> is a reveal turn.</summary>
        public bool IsRevealTurn(int turn)
        {
            if (revealTurns == null) return false;
            return Array.IndexOf(revealTurns, turn) >= 0;
        }

        // ================================================================
        // Internal flow
        // ================================================================

        private void AdvanceToNextTurn()
        {
            currentTurn++;

            if (currentTurn > maxTurnsPerRound)
            {
                SetPhase(TurnPhase.RoundEnd);
                OnRoundComplete?.Invoke();
                return;
            }

            phantomMoveSubmitted = false;
            SetPhase(TurnPhase.PhantomMove);
            StartTimer();
        }

        private IEnumerator ResolvePhase()
        {
            // Check reveal
            if (IsRevealTurn())
            {
                OnRevealTurn?.Invoke(currentTurn);
                yield return new WaitForSeconds(3f); // Allow reveal animation to play
            }

            // Small pause between turns
            yield return new WaitForSeconds(0.5f);

            AdvanceToNextTurn();
        }

        // ================================================================
        // Timer
        // ================================================================

        private void StartTimer()
        {
            StopTimer();
            timerCoroutine = StartCoroutine(MoveTimerCoroutine());
        }

        private void StopTimer()
        {
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }
        }

        private IEnumerator MoveTimerCoroutine()
        {
            float remaining = moveTimeLimitSeconds;

            while (remaining > 0f)
            {
                OnTimerTick?.Invoke(remaining);
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            OnTimerTick?.Invoke(0f);

            // Time's up — auto-submit current phase
            if (currentPhase == TurnPhase.PhantomMove && !phantomMoveSubmitted)
            {
                // Phantom forfeits move (stays in place)
                SubmitPhantomMove();
            }
            else if (currentPhase == TurnPhase.HunterMove)
            {
                // All remaining hunters forfeit
                hunterMovesRemaining = 0;
                SetPhase(TurnPhase.Resolution);
                StartCoroutine(ResolvePhase());
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void SetPhase(TurnPhase phase)
        {
            currentPhase = phase;
            OnPhaseChanged?.Invoke(phase, currentTurn);
        }
    }
}
