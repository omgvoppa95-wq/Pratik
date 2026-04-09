using System.Collections.Generic;
using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Tracks the transport tickets each player holds and enforces usage rules.
    ///
    /// Ticket types mirror <see cref="TransportType"/>.
    /// The Phantom has unlimited bus tickets and starts with extra underground
    /// tickets.  Hunters receive a fixed pool at game start.
    /// </summary>
    public class TicketSystem : MonoBehaviour
    {
        // ── Default ticket allotments ───────────────────────────────────
        [Header("Phantom Starting Tickets")]
        [SerializeField] private int phantomBusTickets = 999;
        [SerializeField] private int phantomTaxiTickets = 8;
        [SerializeField] private int phantomTrainTickets = 5;
        [SerializeField] private int phantomUndergroundATickets = 3;
        [SerializeField] private int phantomUndergroundBTickets = 2;

        [Header("Hunter Starting Tickets")]
        [SerializeField] private int hunterBusTickets = 999;
        [SerializeField] private int hunterTaxiTickets = 6;
        [SerializeField] private int hunterTrainTickets = 4;
        [SerializeField] private int hunterUndergroundATickets = 1;
        [SerializeField] private int hunterUndergroundBTickets = 1;

        // ── Per-player ticket pools ─────────────────────────────────────
        // Key = player ID (Photon ActorNumber or local index)
        private readonly Dictionary<int, Dictionary<TransportType, int>> playerTickets =
            new Dictionary<int, Dictionary<TransportType, int>>();

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Initialises the ticket pool for a player.  Call once when a
        /// player joins or when the round resets.
        /// </summary>
        public void InitialisePlayer(int playerId, bool isPhantom)
        {
            var pool = new Dictionary<TransportType, int>();

            if (isPhantom)
            {
                pool[TransportType.Bus] = phantomBusTickets;
                pool[TransportType.Taxi] = phantomTaxiTickets;
                pool[TransportType.Train] = phantomTrainTickets;
                pool[TransportType.UndergroundA] = phantomUndergroundATickets;
                pool[TransportType.UndergroundB] = phantomUndergroundBTickets;
            }
            else
            {
                pool[TransportType.Bus] = hunterBusTickets;
                pool[TransportType.Taxi] = hunterTaxiTickets;
                pool[TransportType.Train] = hunterTrainTickets;
                pool[TransportType.UndergroundA] = hunterUndergroundATickets;
                pool[TransportType.UndergroundB] = hunterUndergroundBTickets;
            }

            playerTickets[playerId] = pool;
        }

        /// <summary>Returns how many tickets the player has for a transport type.</summary>
        public int GetTicketCount(int playerId, TransportType type)
        {
            if (!playerTickets.ContainsKey(playerId)) return 0;
            return playerTickets[playerId].ContainsKey(type) ? playerTickets[playerId][type] : 0;
        }

        /// <summary>True if the player can afford to travel via <paramref name="type"/>.</summary>
        public bool CanUseTransport(int playerId, TransportType type)
        {
            return GetTicketCount(playerId, type) > 0;
        }

        /// <summary>
        /// Consumes one ticket of the given type.  Returns true if successful.
        /// </summary>
        public bool UseTicket(int playerId, TransportType type)
        {
            if (!CanUseTransport(playerId, type)) return false;
            playerTickets[playerId][type]--;
            return true;
        }

        /// <summary>
        /// Awards extra tickets (e.g. from a power-up or event card).
        /// </summary>
        public void AwardTickets(int playerId, TransportType type, int count)
        {
            if (!playerTickets.ContainsKey(playerId)) return;
            if (!playerTickets[playerId].ContainsKey(type))
            {
                playerTickets[playerId][type] = 0;
            }
            playerTickets[playerId][type] += count;
        }

        /// <summary>
        /// Returns all transport types for which the player owns at least
        /// one ticket, intersected with the types available from a node.
        /// </summary>
        public List<TransportType> GetAffordableTransports(int playerId,
            List<TransportType> availableTypes)
        {
            var result = new List<TransportType>();
            foreach (TransportType type in availableTypes)
            {
                if (CanUseTransport(playerId, type))
                {
                    result.Add(type);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a snapshot of all ticket counts for a player.
        /// Useful for UI display.
        /// </summary>
        public Dictionary<TransportType, int> GetAllTickets(int playerId)
        {
            if (!playerTickets.ContainsKey(playerId))
                return new Dictionary<TransportType, int>();
            return new Dictionary<TransportType, int>(playerTickets[playerId]);
        }

        /// <summary>
        /// Removes the player from the ticket tracking system.
        /// </summary>
        public void RemovePlayer(int playerId)
        {
            playerTickets.Remove(playerId);
        }
    }
}
