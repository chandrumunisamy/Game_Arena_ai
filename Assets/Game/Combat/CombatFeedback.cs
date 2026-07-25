using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Relicfall.Combat
{
    /// <summary>
    /// Manages combat feedback including hit stop, camera shake, damage flashes,
    /// and impact particles. Centralized system for all combat visceral feedback.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        private static CombatFeedback _instance;
        public static CombatFeedback Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CombatFeedback");
                    _instance = go.AddComponent<CombatFeedback>();
                }
                return _instance;
            }
        }

        [Header("Hit Stop")]
        [SerializeField] private float _defaultHitStopDuration = 0.05f;
        [SerializeField] private float _heavyHitStopDuration = 0.12f;
        [SerializeField] private float _parryHitStopDuration = 0.15f;
        [SerializeField] private float _bossHitStopDuration = 0.2f;

        [Header("Camera Shake")]
        [SerializeField] private AnimationCurve _shakeCurve = new AnimationCurve(
            new Keyframe(0, 0),
            new Keyframe(0.1f, 1),
            new Keyframe(0.3f, 0.5f),
            new Keyframe(1, 0)
        );

        [Header("Damage Flash")]
        [SerializeField] private Color _damageFlashColor = new Color(1f, 0.2f, 0.1f, 1f);
        [SerializeField] private float _damageFlashDuration = 0.1f;
        [SerializeField] private Material _damageFlashMaterialTemplate;

        [Header("Impact Particles")]
        [SerializeField] private GameObject _lightImpactPrefab;
        [SerializeField] private GameObject _heavyImpactPrefab;
        [SerializeField] private GameObject _criticalImpactPrefab;
        [SerializeField] private GameObject _parryImpactPrefab;

        [Header("Sound")]
        [SerializeField] private float _defaultImpactPitch = 1f;
        [SerializeField] private float _heavyImpactPitch = 0.8f;

        private List<(Coroutine coroutine, float originalTimeScale)> _activeHitStops = new();
        private Vector3 _originalCameraPosition;
        private Camera _mainCamera;

        private void Awake()
        {
            if (_instance != null && _instance != this)
                Destroy(gameObject);
            _instance = this;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera != null)
                _originalCameraPosition = _mainCamera.transform.position;
        }

        #region Hit Stop

        /// <summary>
        /// Trigger hit stop (brief time freeze for impact emphasis).
        /// Multiple hit stops can stack briefly.
        /// </summary>
        public void TriggerHitStop(float duration)
        {
            StartCoroutine(HitStopCoroutine(duration));
        }

        /// <summary>
        /// Trigger hit stop with preset duration based on impact type.
        /// </summary>
        public void TriggerHitStop(ImpactType type)
        {
            float duration = type switch
            {
                ImpactType.Light => _defaultHitStopDuration,
                ImpactType.Heavy => _heavyHitStopDuration,
                ImpactType.Parry => _parryHitStopDuration,
                ImpactType.Boss => _bossHitStopDuration,
                ImpactType.Critical => _heavyHitStopDuration,
                _ => _defaultHitStopDuration
            };
            StartCoroutine(HitStopCoroutine(duration));
        }

        private IEnumerator HitStopCoroutine(float duration)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // Use unscaled time for the hit stop duration itself
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = originalTimeScale;
        }

        #endregion

        #region Camera Shake

        /// <summary>
        /// Trigger camera shake with intensity and frequency.
        /// </summary>
        public void TriggerCameraShake(float intensity, float frequency = 5f, float duration = 0.3f)
        {
            intensity *= CameraShake.GlobalIntensity;
            if (intensity <= 0.01f) return;

            StartCoroutine(CameraShakeCoroutine(intensity, frequency, duration));
        }

        private IEnumerator CameraShakeCoroutine(float intensity, float frequency, float duration)
        {
            if (_mainCamera == null) yield break;

            float elapsed = 0f;
            Vector3 startPos = _mainCamera.transform.position;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curveValue = _shakeCurve.Evaluate(t);
                float currentIntensity = intensity * curveValue;

                float offsetX = Mathf.Sin(elapsed * frequency) * currentIntensity;
                float offsetY = Mathf.Cos(elapsed * frequency * 1.3f) * currentIntensity * 0.5f;

                _mainCamera.transform.position = startPos + new Vector3(offsetX, offsetY, 0);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _mainCamera.transform.position = startPos;
        }

        #endregion

        #region Damage Flash

        /// <summary>
        /// Trigger a damage flash on a target renderer.
        /// </summary>
        public void TriggerDamageFlash(Renderer renderer, float duration = 0.1f)
        {
            if (renderer == null || _damageFlashMaterialTemplate == null) return;
            StartCoroutine(DamageFlashCoroutine(renderer, duration));
        }

        private IEnumerator DamageFlashCoroutine(Renderer renderer, float duration)
        {
            Material[] originalMaterials = renderer.sharedMaterials;
            Material flashMat = new Material(_damageFlashMaterialTemplate);
            flashMat.color = _damageFlashColor;

            Material[] flashMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < flashMaterials.Length; i++)
                flashMaterials[i] = flashMat;

            renderer.materials = flashMaterials;

            yield return new WaitForSecondsRealtime(duration);

            renderer.materials = originalMaterials;
            Destroy(flashMat);
        }

        #endregion

        #region Impact Particles

        /// <summary>
        /// Spawn impact particles at a hit position.
        /// </summary>
        public void SpawnImpactEffect(ImpactType type, Vector3 position, Vector3 direction)
        {
            GameObject prefab = type switch
            {
                ImpactType.Light => _lightImpactPrefab,
                ImpactType.Heavy => _heavyImpactPrefab,
                ImpactType.Critical => _criticalImpactPrefab,
                ImpactType.Parry => _parryImpactPrefab,
                _ => _lightImpactPrefab
            };

            if (prefab == null) return;

            var impact = Instantiate(prefab, position, Quaternion.LookRotation(direction));
            Destroy(impact, 1f);
        }

        #endregion

        #region Sound

        /// <summary>
        /// Play an impact sound with layered audio.
        /// </summary>
        public void PlayImpactSound(ImpactType type, Vector3 position, MaterialType material = MaterialType.Body)
        {
            // Sound is handled by the audio system through events
            var sfxId = $"impact_{type.ToString().ToLower()}_{material.ToString().ToLower()}";
            Relicfall.Core.Events.EventBus.Publish(new Relicfall.Core.Events.SFXPlayEvent
            {
                SfxId = sfxId,
                Position = position,
                Volume = type == ImpactType.Heavy ? 1.2f : 1f,
                Pitch = type == ImpactType.Heavy ? _heavyImpactPitch : _defaultImpactPitch
            });
        }

        #endregion
    }

    /// <summary>
    /// Impact types for feedback classification.
    /// </summary>
    public enum ImpactType
    {
        Light,
        Heavy,
        Critical,
        Parry,
        Boss,
        Execution
    }

    /// <summary>
    /// Material types for impact sound variation.
    /// </summary>
    public enum MaterialType
    {
        Body,
        Armor,
        Stone,
        Metal,
        Flesh,
        Magic
    }

    /// <summary>
    /// Camera shake global settings with player-adjustable intensity.
    /// </summary>
    public static class CameraShake
    {
        public static float GlobalIntensity { get; set; } = 1f;

        /// <summary>
        /// Set global intensity from settings (0 to 2 range).
        /// </summary>
        public static void SetGlobalIntensity(float intensity)
        {
            GlobalIntensity = Mathf.Clamp(intensity, 0f, 2f);
        }
    }
}
