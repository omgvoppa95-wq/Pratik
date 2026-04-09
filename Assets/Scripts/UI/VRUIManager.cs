using UnityEngine;

namespace PhantomChaseVR
{
    /// <summary>
    /// Root UI manager that owns all UI panels and handles panel switching.
    /// Designed for VR world-space canvases that follow the player's view.
    ///
    /// Panels:
    ///   • HUD (always visible during gameplay)
    ///   • Transport Selection (shown during move phase)
    ///   • Player Info (toggle)
    ///   • Minimap (toggle)
    ///   • Pause Menu (toggle)
    /// </summary>
    public class VRUIManager : MonoBehaviour
    {
        // ── Panel references (assign in Inspector) ──────────────────────
        [Header("UI Panels")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject transportSelectionPanel;
        [SerializeField] private GameObject playerInfoPanel;
        [SerializeField] private GameObject minimapPanel;
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("Canvas Settings")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private float canvasDistance = 2.5f;
        [SerializeField] private float canvasScale = 0.002f;

        [Header("References")]
        [SerializeField] private Transform vrCameraTransform;

        // ── Panel components ────────────────────────────────────────────
        private HUDController hudController;
        private TransportSelectionUI transportUI;
        private MinimapController minimapController;
        private PlayerInfoPanel playerInfo;

        // ── State ───────────────────────────────────────────────────────
        private bool isPaused;
        private bool isMinimapVisible;
        private bool isPlayerInfoVisible;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            CacheComponents();
            SetupCanvas();
        }

        private void Start()
        {
            // Start with HUD visible, everything else hidden
            ShowHUD();
            HideTransportSelection();
            HidePlayerInfo();
            HideMinimap();
            HidePauseMenu();

            SubscribeToEvents();
        }

        private void LateUpdate()
        {
            FollowCamera();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Shows the transport selection panel (called at start of move phase).</summary>
        public void ShowTransportSelection()
        {
            SetPanelActive(transportSelectionPanel, true);
        }

        /// <summary>Hides the transport selection panel.</summary>
        public void HideTransportSelection()
        {
            SetPanelActive(transportSelectionPanel, false);
        }

        /// <summary>Toggles the minimap visibility.</summary>
        public void ToggleMinimap()
        {
            isMinimapVisible = !isMinimapVisible;
            SetPanelActive(minimapPanel, isMinimapVisible);
        }

        /// <summary>Toggles the player info panel.</summary>
        public void TogglePlayerInfo()
        {
            isPlayerInfoVisible = !isPlayerInfoVisible;
            SetPanelActive(playerInfoPanel, isPlayerInfoVisible);
        }

        /// <summary>Toggles the pause menu.</summary>
        public void TogglePauseMenu()
        {
            isPaused = !isPaused;
            SetPanelActive(pauseMenuPanel, isPaused);

            // Optionally pause game time
            Time.timeScale = isPaused ? 0f : 1f;
        }

        /// <summary>Returns references to child panel controllers for external wiring.</summary>
        public HUDController GetHUDController() => hudController;
        public TransportSelectionUI GetTransportUI() => transportUI;
        public MinimapController GetMinimapController() => minimapController;
        public PlayerInfoPanel GetPlayerInfo() => playerInfo;

        // ================================================================
        // Internal
        // ================================================================

        private void ShowHUD()
        {
            SetPanelActive(hudPanel, true);
        }

        private void HidePlayerInfo()
        {
            isPlayerInfoVisible = false;
            SetPanelActive(playerInfoPanel, false);
        }

        private void HideMinimap()
        {
            isMinimapVisible = false;
            SetPanelActive(minimapPanel, false);
        }

        private void HidePauseMenu()
        {
            isPaused = false;
            SetPanelActive(pauseMenuPanel, false);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        private void CacheComponents()
        {
            if (hudPanel != null) hudController = hudPanel.GetComponent<HUDController>();
            if (transportSelectionPanel != null) transportUI = transportSelectionPanel.GetComponent<TransportSelectionUI>();
            if (minimapPanel != null) minimapController = minimapPanel.GetComponent<MinimapController>();
            if (playerInfoPanel != null) playerInfo = playerInfoPanel.GetComponent<PlayerInfoPanel>();
        }

        private void SetupCanvas()
        {
            if (worldCanvas == null) worldCanvas = GetComponent<Canvas>();
            if (worldCanvas != null)
            {
                worldCanvas.renderMode = RenderMode.WorldSpace;
                worldCanvas.transform.localScale = Vector3.one * canvasScale;
            }
        }

        private void FollowCamera()
        {
            if (vrCameraTransform == null)
            {
                // Fallback: try main camera
                Camera cam = Camera.main;
                if (cam != null) vrCameraTransform = cam.transform;
                else return;
            }

            // Position canvas in front of the camera
            Vector3 forward = vrCameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = vrCameraTransform.position + forward * canvasDistance;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // ================================================================
        // Event wiring
        // ================================================================

        private void SubscribeToEvents()
        {
            if (GameManager.Instance == null) return;

            TurnManager tm = FindObjectOfType<TurnManager>();
            if (tm != null)
            {
                tm.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            TurnManager tm = FindObjectOfType<TurnManager>();
            if (tm != null)
            {
                tm.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(TurnPhase phase, int turn)
        {
            switch (phase)
            {
                case TurnPhase.PhantomMove:
                case TurnPhase.HunterMove:
                    ShowTransportSelection();
                    break;
                case TurnPhase.Resolution:
                case TurnPhase.RoundEnd:
                    HideTransportSelection();
                    break;
            }
        }
    }
}
