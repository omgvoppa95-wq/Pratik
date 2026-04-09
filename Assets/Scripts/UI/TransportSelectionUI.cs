using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PhantomChaseVR
{
    /// <summary>
    /// Displays a list of available transport types the player can select,
    /// then shows reachable destination nodes for the chosen type.
    ///
    /// Works for both Phantom and Hunter players — the controller that
    /// calls <see cref="Show"/> determines which types are available.
    ///
    /// Layout: vertical list of transport buttons on the left, destination
    /// node buttons on the right.
    /// </summary>
    public class TransportSelectionUI : MonoBehaviour
    {
        // ── Inspector references ────────────────────────────────────────
        [Header("Transport Buttons")]
        [SerializeField] private Transform transportButtonContainer;
        [SerializeField] private GameObject transportButtonPrefab;

        [Header("Destination Buttons")]
        [SerializeField] private Transform destinationButtonContainer;
        [SerializeField] private GameObject destinationButtonPrefab;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI selectedTransportLabel;
        [SerializeField] private TextMeshProUGUI instructionLabel;

        // ── Colour mapping per transport type ───────────────────────────
        private static readonly Dictionary<TransportType, Color> TransportColors =
            new Dictionary<TransportType, Color>
        {
            { TransportType.Bus,           new Color(0.894f, 0.325f, 0.541f) },  // #E4538A
            { TransportType.Taxi,          new Color(0.961f, 0.620f, 0.043f) },  // #F59E0B
            { TransportType.Train,         new Color(0.961f, 0.620f, 0.043f) },  // bright yellow
            { TransportType.UndergroundA,  new Color(0.024f, 0.714f, 0.831f) },  // #06B6D4
            { TransportType.UndergroundB,  new Color(0.659f, 0.333f, 0.969f) },  // #A855F7
        };

        // ── Callbacks ───────────────────────────────────────────────────
        private System.Action<TransportType> onTransportSelected;
        private System.Action<int> onDestinationSelected;

        // ── State ───────────────────────────────────────────────────────
        private TransportType? currentTransport;
        private readonly List<GameObject> spawnedTransportButtons = new List<GameObject>();
        private readonly List<GameObject> spawnedDestinationButtons = new List<GameObject>();

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Shows the transport selection UI with the given available types
        /// and ticket counts.
        /// </summary>
        public void Show(List<TransportType> availableTypes,
                         Dictionary<TransportType, int> ticketCounts,
                         System.Action<TransportType> transportCallback,
                         System.Action<int> destinationCallback)
        {
            onTransportSelected = transportCallback;
            onDestinationSelected = destinationCallback;
            currentTransport = null;

            ClearSpawned(spawnedTransportButtons);
            ClearSpawned(spawnedDestinationButtons);

            if (instructionLabel != null)
            {
                instructionLabel.text = "Select a transport type";
            }
            if (selectedTransportLabel != null)
            {
                selectedTransportLabel.text = "";
            }

            // Spawn a button for each available transport
            foreach (TransportType type in availableTypes)
            {
                int count = ticketCounts.ContainsKey(type) ? ticketCounts[type] : 0;
                SpawnTransportButton(type, count);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Shows destination nodes after a transport type has been selected.
        /// </summary>
        public void ShowDestinations(List<int> reachableNodeIds, TransportNodeManager nodeManager)
        {
            ClearSpawned(spawnedDestinationButtons);

            if (instructionLabel != null)
            {
                instructionLabel.text = "Select a destination";
            }

            foreach (int nodeId in reachableNodeIds)
            {
                string name = nodeManager != null ? nodeManager.GetStationName(nodeId) : $"Node {nodeId}";
                SpawnDestinationButton(nodeId, name);
            }
        }

        /// <summary>Hides the UI and clears all spawned buttons.</summary>
        public void Hide()
        {
            ClearSpawned(spawnedTransportButtons);
            ClearSpawned(spawnedDestinationButtons);
            currentTransport = null;
            gameObject.SetActive(false);
        }

        // ================================================================
        // Internal — button spawning
        // ================================================================

        private void SpawnTransportButton(TransportType type, int ticketCount)
        {
            if (transportButtonPrefab == null || transportButtonContainer == null) return;

            GameObject btnGo = Instantiate(transportButtonPrefab, transportButtonContainer);
            btnGo.name = $"Btn_{type}";
            spawnedTransportButtons.Add(btnGo);

            // Label
            TextMeshProUGUI label = btnGo.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = $"{type}  ×{ticketCount}";
            }

            // Colour
            Image img = btnGo.GetComponent<Image>();
            if (img != null && TransportColors.ContainsKey(type))
            {
                img.color = TransportColors[type];
            }

            // Click handler
            Button btn = btnGo.GetComponent<Button>();
            if (btn != null)
            {
                TransportType captured = type;
                btn.onClick.AddListener(() => OnTransportButtonClicked(captured));
            }
        }

        private void SpawnDestinationButton(int nodeId, string stationName)
        {
            if (destinationButtonPrefab == null || destinationButtonContainer == null) return;

            GameObject btnGo = Instantiate(destinationButtonPrefab, destinationButtonContainer);
            btnGo.name = $"Btn_Node_{nodeId}";
            spawnedDestinationButtons.Add(btnGo);

            TextMeshProUGUI label = btnGo.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = $"{nodeId} — {stationName}";
            }

            Button btn = btnGo.GetComponent<Button>();
            if (btn != null)
            {
                int captured = nodeId;
                btn.onClick.AddListener(() => OnDestinationButtonClicked(captured));
            }
        }

        // ================================================================
        // Button callbacks
        // ================================================================

        private void OnTransportButtonClicked(TransportType type)
        {
            currentTransport = type;

            if (selectedTransportLabel != null)
            {
                selectedTransportLabel.text = type.ToString();
                if (TransportColors.ContainsKey(type))
                {
                    selectedTransportLabel.color = TransportColors[type];
                }
            }

            onTransportSelected?.Invoke(type);
        }

        private void OnDestinationButtonClicked(int nodeId)
        {
            onDestinationSelected?.Invoke(nodeId);
        }

        // ================================================================
        // Cleanup
        // ================================================================

        private void ClearSpawned(List<GameObject> list)
        {
            foreach (GameObject go in list)
            {
                if (go != null) Destroy(go);
            }
            list.Clear();
        }
    }
}
