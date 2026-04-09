using UnityEngine;
using TMPro;

namespace PhantomChaseVR
{
    /// <summary>
    /// Component attached to each of the 62 transport node GameObjects.
    /// Handles visual state: highlight ring, blocked indicator, underground glow,
    /// occupancy display, and floating node-number label.
    /// </summary>
    public class TransportNode : MonoBehaviour
    {
        // ── Set by TransportNodeManager on spawn ────────────────────────
        [HideInInspector] public int nodeId;
        [HideInInspector] public bool isUndergroundStation;
        [HideInInspector] public bool isCrowdBlendZone;
        [HideInInspector] public Vector3 worldPosition;

        // ── Inspector references ────────────────────────────────────────
        [Header("Visual References")]
        [SerializeField] private GameObject highlightRing;
        [SerializeField] private GameObject blockedOverlay;      // Red X sprite
        [SerializeField] private GameObject undergroundGlowEffect;
        [SerializeField] private TextMeshPro nodeLabel;

        [Header("Disk Renderer")]
        [SerializeField] private Renderer diskRenderer;

        // ── Colours ─────────────────────────────────────────────────────
        private static readonly Color StandardBaseColor = new Color(0.2f, 0.2f, 0.2f, 1f);      // dark grey
        private static readonly Color StandardHighlightColor = Color.white;
        private static readonly Color UndergroundCyanGlow = new Color(0.024f, 0.714f, 0.831f, 1f);  // #06B6D4
        private static readonly Color UndergroundPurpleGlow = new Color(0.659f, 0.333f, 0.969f, 1f); // #A855F7
        private static readonly Color CrowdZoneBaseColor = new Color(1f, 0.55f, 0.1f, 1f);       // warm orange
        private static readonly Color BlockedColor = Color.red;

        // ── Runtime state ───────────────────────────────────────────────
        private bool isHighlighted;
        private bool isBlocked;
        private int currentOccupants;
        private Material diskMaterial;
        private Color baseColor;

        // ── Sizes (radius) ──────────────────────────────────────────────
        private const float StandardRadius = 1.5f;
        private const float SpecialRadius = 2.0f;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            if (diskRenderer != null)
            {
                diskMaterial = diskRenderer.material; // instance material
            }

            // Ensure optional objects start hidden
            if (highlightRing != null) highlightRing.SetActive(false);
            if (blockedOverlay != null) blockedOverlay.SetActive(false);
            if (undergroundGlowEffect != null) undergroundGlowEffect.SetActive(false);
        }

        /// <summary>
        /// Called by TransportNodeManager right after instantiation to finalise visuals.
        /// </summary>
        public void Initialise()
        {
            // Determine base colour and scale
            float radius = StandardRadius;
            baseColor = StandardBaseColor;

            if (isUndergroundStation)
            {
                radius = SpecialRadius;
                baseColor = IsUndergroundA() ? UndergroundCyanGlow : UndergroundPurpleGlow;
            }
            else if (isCrowdBlendZone)
            {
                radius = SpecialRadius;
                baseColor = CrowdZoneBaseColor;
            }

            // Apply disk colour
            if (diskMaterial != null)
            {
                diskMaterial.color = baseColor;
            }

            // Scale disk to correct radius (assumes default prefab is unit-diameter)
            float diameter = radius * 2f;
            transform.localScale = new Vector3(diameter, transform.localScale.y, diameter);

            // Position label 0.5 units above disk, facing up
            if (nodeLabel != null)
            {
                nodeLabel.text = nodeId.ToString();
                nodeLabel.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                nodeLabel.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
            }

            // Auto-enable underground glow for underground stations
            if (isUndergroundStation && undergroundGlowEffect != null)
            {
                undergroundGlowEffect.SetActive(true);
            }
        }

        // ================================================================
        // Public API — called by TransportNodeManager / GameManager RPCs
        // ================================================================

        /// <summary>
        /// Called by TransportNodeManager.HighlightReachable() to show or hide
        /// the highlight ring on this node.
        /// </summary>
        public void SetHighlight(bool on)
        {
            isHighlighted = on;

            if (highlightRing != null)
            {
                highlightRing.SetActive(on);
            }

            // Tint the disk to the highlight colour while active
            if (diskMaterial != null)
            {
                diskMaterial.color = on ? GetHighlightColor() : baseColor;
            }
        }

        /// <summary>
        /// Called by GameManager RPC when a bridge collapse blocks this node.
        /// Shows or hides a red X overlay.
        /// </summary>
        public void ShowBlockedIndicator(bool blocked)
        {
            isBlocked = blocked;

            if (blockedOverlay != null)
            {
                blockedOverlay.SetActive(blocked);
            }

            if (diskMaterial != null && blocked)
            {
                diskMaterial.color = BlockedColor;
            }
            else if (diskMaterial != null && !blocked && !isHighlighted)
            {
                diskMaterial.color = baseColor;
            }
        }

        /// <summary>
        /// Called when a player occupies this node.
        /// Updates visual to reflect how many players are present.
        /// </summary>
        public void SetOccupied(int playerCount)
        {
            currentOccupants = playerCount;

            // Simple visual feedback: brighten disk proportionally
            if (diskMaterial != null && !isBlocked && !isHighlighted)
            {
                diskMaterial.color = playerCount > 0
                    ? Color.Lerp(baseColor, Color.white, 0.3f * Mathf.Min(playerCount, 3))
                    : baseColor;
            }
        }

        /// <summary>
        /// Show or hide the underground glow VFX.
        /// </summary>
        public void SetUndergroundGlow(bool on)
        {
            if (undergroundGlowEffect != null)
            {
                undergroundGlowEffect.SetActive(on);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Color GetHighlightColor()
        {
            if (isUndergroundStation)
            {
                // Pulsing handled by shader / animation; return bright version
                return Color.Lerp(baseColor, Color.white, 0.6f);
            }

            if (isCrowdBlendZone)
            {
                return Color.Lerp(CrowdZoneBaseColor, Color.white, 0.5f);
            }

            return StandardHighlightColor;
        }

        private bool IsUndergroundA()
        {
            foreach (int id in GameSettings.UNDERGROUND_A_NODES)
            {
                if (nodeId == id) return true;
            }
            return false;
        }
    }
}
