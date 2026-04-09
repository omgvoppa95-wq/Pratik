using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PhantomChaseVR
{
    /// <summary>
    /// Controls the URP Post Processing Volume to achieve the 1990s NYC
    /// night-time look.  Exposes public methods called by GameManager RPCs
    /// for reveal effects and final reveal.
    /// </summary>
    public class PostProcessingController : MonoBehaviour
    {
        // ── Inspector reference ─────────────────────────────────────────
        [Header("Volume Reference")]
        [SerializeField] private Volume postProcessVolume;

        // ── Cached effect overrides ─────────────────────────────────────
        private Bloom bloom;
        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private FilmGrain filmGrain;
        private ColorAdjustments colorAdjustments;

        // ── Night base profile values ───────────────────────────────────
        private const float BaseBloomIntensity = 0.8f;
        private const float BaseBloomThreshold = 0.9f;
        private static readonly Color BaseBloomTint = new Color(1f, 0.549f, 0.259f, 1f); // #FF8C42

        private const float BaseFilmGrainIntensity = 0.35f;
        private const float BaseFilmGrainResponse = 0.8f;

        private const float BaseVignetteIntensity = 0.35f;
        private const float BaseVignetteSmoothness = 0.5f;

        private const float BaseChromaticAberrationIntensity = 0.15f;

        private const float BasePostExposure = -0.3f;
        private const float BaseContrast = 25f;
        private static readonly Color BaseColorFilter = new Color(0.545f, 0.682f, 0.831f, 1f); // #8BAED4

        // ── Reveal effect constants ─────────────────────────────────────
        private const float RevealBloomPeak = 3.0f;
        private const float RevealFlashDuration = 0.5f;
        private const float RevealLerpBackDuration = 2.5f;
        private static readonly Color RevealRedTint = new Color(1f, 0.15f, 0.1f, 1f);

        // ── Coroutine tracking ──────────────────────────────────────────
        private Coroutine activeRevealCoroutine;
        private Coroutine activeFinalRevealCoroutine;

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Start()
        {
            CacheVolumeOverrides();
            SetNightBaseProfile();
        }

        // ================================================================
        // Cache volume overrides from the assigned Volume
        // ================================================================

        private void CacheVolumeOverrides()
        {
            if (postProcessVolume == null)
            {
                Debug.LogWarning("[PostProcessingController] No Volume assigned.");
                return;
            }

            VolumeProfile profile = postProcessVolume.profile;
            if (profile == null)
            {
                Debug.LogWarning("[PostProcessingController] Volume has no profile.");
                return;
            }

            profile.TryGet(out bloom);
            profile.TryGet(out vignette);
            profile.TryGet(out chromaticAberration);
            profile.TryGet(out filmGrain);
            profile.TryGet(out colorAdjustments);
        }

        // ================================================================
        // Public API — called by GameManager RPCs
        // ================================================================

        /// <summary>
        /// Sets all post-processing effects to the default 1990s NYC night look.
        /// </summary>
        public void SetNightBaseProfile()
        {
            if (bloom != null)
            {
                bloom.active = true;
                bloom.intensity.Override(BaseBloomIntensity);
                bloom.threshold.Override(BaseBloomThreshold);
                bloom.tint.Override(BaseBloomTint);
            }

            if (filmGrain != null)
            {
                filmGrain.active = true;
                filmGrain.intensity.Override(BaseFilmGrainIntensity);
                filmGrain.response.Override(BaseFilmGrainResponse);
            }

            if (vignette != null)
            {
                vignette.active = true;
                vignette.intensity.Override(BaseVignetteIntensity);
                vignette.smoothness.Override(BaseVignetteSmoothness);
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.active = true;
                chromaticAberration.intensity.Override(BaseChromaticAberrationIntensity);
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.active = true;
                colorAdjustments.postExposure.Override(BasePostExposure);
                colorAdjustments.contrast.Override(BaseContrast);
                colorAdjustments.colorFilter.Override(BaseColorFilter);
            }
        }

        /// <summary>
        /// Phantom periodic reveal — brief red flash.
        /// Bloom intensity spikes to 3.0 + red colour filter for 0.5 s,
        /// then lerps back to base values over 2.5 s.
        /// </summary>
        public void TriggerRevealEffect()
        {
            if (activeRevealCoroutine != null)
            {
                StopCoroutine(activeRevealCoroutine);
            }
            activeRevealCoroutine = StartCoroutine(RevealEffectCoroutine());
        }

        /// <summary>
        /// Ghost silhouette flash at a specific world position.
        /// Similar to reveal but localised and shorter.
        /// </summary>
        public void TriggerVisionBleedEffect(Vector3 worldPos)
        {
            if (activeRevealCoroutine != null)
            {
                StopCoroutine(activeRevealCoroutine);
            }
            activeRevealCoroutine = StartCoroutine(VisionBleedCoroutine());
        }

        /// <summary>
        /// Final dramatic reveal — all lights up.
        /// Post-processing lerps to zero vignette, full exposure,
        /// warm white colour filter.
        /// </summary>
        public void TriggerFinalRevealEffect()
        {
            if (activeFinalRevealCoroutine != null)
            {
                StopCoroutine(activeFinalRevealCoroutine);
            }
            activeFinalRevealCoroutine = StartCoroutine(FinalRevealCoroutine());
        }

        // ================================================================
        // Coroutines
        // ================================================================

        private IEnumerator RevealEffectCoroutine()
        {
            // Spike phase: bloom + red tint
            if (bloom != null)
            {
                bloom.intensity.Override(RevealBloomPeak);
            }
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.Override(RevealRedTint);
            }

            yield return new WaitForSeconds(RevealFlashDuration);

            // Lerp back to base over 2.5 s
            float elapsed = 0f;
            while (elapsed < RevealLerpBackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / RevealLerpBackDuration);

                if (bloom != null)
                {
                    bloom.intensity.Override(Mathf.Lerp(RevealBloomPeak, BaseBloomIntensity, t));
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.colorFilter.Override(Color.Lerp(RevealRedTint, BaseColorFilter, t));
                }

                yield return null;
            }

            // Ensure exact base values
            SetNightBaseProfile();
            activeRevealCoroutine = null;
        }

        private IEnumerator VisionBleedCoroutine()
        {
            // Shorter, less intense version of the reveal
            float bleedBloom = 2.0f;
            Color bleedTint = new Color(0.6f, 0.1f, 0.8f, 1f); // purplish

            if (bloom != null)
            {
                bloom.intensity.Override(bleedBloom);
            }
            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.Override(0.6f);
            }
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.Override(bleedTint);
            }

            yield return new WaitForSeconds(0.3f);

            float elapsed = 0f;
            float lerpDuration = 1.5f;
            while (elapsed < lerpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lerpDuration);

                if (bloom != null)
                {
                    bloom.intensity.Override(Mathf.Lerp(bleedBloom, BaseBloomIntensity, t));
                }
                if (chromaticAberration != null)
                {
                    chromaticAberration.intensity.Override(
                        Mathf.Lerp(0.6f, BaseChromaticAberrationIntensity, t));
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.colorFilter.Override(Color.Lerp(bleedTint, BaseColorFilter, t));
                }

                yield return null;
            }

            SetNightBaseProfile();
            activeRevealCoroutine = null;
        }

        private IEnumerator FinalRevealCoroutine()
        {
            // Target: zero vignette, full exposure, warm white
            Color warmWhite = new Color(1f, 0.95f, 0.85f, 1f);
            float targetExposure = 1.5f;
            float targetContrast = 10f;
            float targetBloom = 1.5f;

            float elapsed = 0f;
            float duration = 4.0f;

            // Capture current values for lerp
            float startBloom = bloom != null ? bloom.intensity.value : BaseBloomIntensity;
            float startVignette = vignette != null ? vignette.intensity.value : BaseVignetteIntensity;
            float startExposure = colorAdjustments != null ? colorAdjustments.postExposure.value : BasePostExposure;
            float startContrast = colorAdjustments != null ? colorAdjustments.contrast.value : BaseContrast;
            Color startFilter = colorAdjustments != null ? colorAdjustments.colorFilter.value : BaseColorFilter;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                if (bloom != null)
                {
                    bloom.intensity.Override(Mathf.Lerp(startBloom, targetBloom, t));
                }
                if (vignette != null)
                {
                    vignette.intensity.Override(Mathf.Lerp(startVignette, 0f, t));
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.postExposure.Override(Mathf.Lerp(startExposure, targetExposure, t));
                    colorAdjustments.contrast.Override(Mathf.Lerp(startContrast, targetContrast, t));
                    colorAdjustments.colorFilter.Override(Color.Lerp(startFilter, warmWhite, t));
                }
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(Mathf.Lerp(BaseFilmGrainIntensity, 0f, t));
                }
                if (chromaticAberration != null)
                {
                    chromaticAberration.intensity.Override(
                        Mathf.Lerp(BaseChromaticAberrationIntensity, 0f, t));
                }

                yield return null;
            }

            activeFinalRevealCoroutine = null;
        }
    }
}
