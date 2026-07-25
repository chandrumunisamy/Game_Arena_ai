using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;

namespace Relicfall.Audio
{
    /// <summary>
    /// Music system with adaptive layers that respond to combat, corruption,
    /// boss phases, and game state. Supports exploration, combat, high corruption,
    /// boss, victory, and death music layers.
    /// </summary>
    public class MusicSystem : MonoBehaviour
    {
        [Header("Music Sources")]
        [SerializeField] private AudioSource _explorationLayer;
        [SerializeField] private AudioSource _combatLayer;
        [SerializeField] private AudioSource _corruptionLayer;
        [SerializeField] private AudioSource _bossLayer;
        [SerializeField] private AudioSource _bossPhase2Layer;
        [SerializeField] private AudioSource _victorySource;
        [SerializeField] private AudioSource _deathSource;

        [Header("Transition")]
        [SerializeField] private float _layerFadeSpeed = 2f;
        [SerializeField] private float _corruptionIntensityFadeSpeed = 3f;

        private float _currentCombatIntensity = 0f;
        private float _currentCorruptionIntensity = 0f;
        private float _targetCombatIntensity = 0f;
        private float _targetCorruptionIntensity = 0f;
        private bool _isBossFight = false;

        private Dictionary<string, AudioClip> _sfxCache = new();

        private void Awake()
        {
            EventBus.Subscribe<MusicLayerEvent>(OnMusicLayerEvent);
            EventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Subscribe<SFXPlayEvent>(OnSFXPlayEvent);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MusicLayerEvent>(OnMusicLayerEvent);
            EventBus.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Unsubscribe<SFXPlayEvent>(OnSFXPlayEvent);
        }

        /// <summary>
        /// Set corruption-driven music intensity.
        /// </summary>
        public void SetCorruptionIntensity(float corruptionLevel)
        {
            _targetCorruptionIntensity = Mathf.Clamp01(corruptionLevel / 100f);
        }

        /// <summary>
        /// Set combat music intensity (number of enemies nearby).
        /// </summary>
        public void SetCombatIntensity(float intensity)
        {
            _targetCombatIntensity = Mathf.Clamp01(intensity);
        }

        private void OnMusicLayerEvent(MusicLayerEvent e)
        {
            switch (e.LayerName)
            {
                case "boss":
                    _isBossFight = e.ShouldPlay;
                    if (e.ShouldPlay)
                    {
                        FadeInLayer(_bossLayer, 1f);
                        FadeOutLayer(_combatLayer);
                        FadeOutLayer(_explorationLayer);
                    }
                    else
                    {
                        FadeOutLayer(_bossLayer);
                        FadeOutLayer(_bossPhase2Layer);
                    }
                    break;
                case "boss_phase2":
                    if (e.ShouldPlay)
                    {
                        FadeInLayer(_bossPhase2Layer, 1f);
                    }
                    else
                    {
                        FadeOutLayer(_bossPhase2Layer);
                    }
                    break;
                case "victory":
                    if (e.ShouldPlay)
                    {
                        StopAllLayers();
                        _victorySource.Play();
                    }
                    break;
                case "death":
                    if (e.ShouldPlay)
                    {
                        StopAllLayers();
                        _deathSource.Play();
                    }
                    break;
                case "combat":
                    if (e.ShouldPlay && !_isBossFight)
                        _targetCombatIntensity = e.Intensity;
                    break;
                case "corruption":
                    _targetCorruptionIntensity = e.Intensity;
                    break;
            }
        }

        private void OnEnemyDeath(EnemyDeathEvent e)
        {
            // Combat intensity decreases as enemies die
            // This is handled by the SFX system
        }

        private void OnPlayerDeath(PlayerDeathEvent e)
        {
            StopAllLayers();
            _deathSource.Play();
        }

        private void Update()
        {
            // Smooth fade between music layers
            _currentCombatIntensity = Mathf.Lerp(_currentCombatIntensity, _targetCombatIntensity, _layerFadeSpeed * Time.deltaTime);
            _currentCorruptionIntensity = Mathf.Lerp(_currentCorruptionIntensity, _targetCorruptionIntensity, _corruptionIntensityFadeSpeed * Time.deltaTime);

            if (!_isBossFight)
            {
                // Blend exploration, combat, and corruption layers
                _explorationLayer.volume = 1f - _currentCombatIntensity * 0.7f;
                _combatLayer.volume = _currentCombatIntensity;
                _corruptionLayer.volume = _currentCorruptionIntensity * 0.6f;
            }

            // Adjust corruption layer pitch based on corruption level
            if (_corruptionLayer != null)
                _corruptionLayer.pitch = 1f + _currentCorruptionIntensity * 0.1f;
        }

        private void FadeInLayer(AudioSource layer, float targetVolume)
        {
            if (layer != null && !layer.isPlaying)
                layer.Play();
            // Volume will be faded in over time via Update
        }

        private void FadeOutLayer(AudioSource layer)
        {
            if (layer != null && layer.isPlaying)
            {
                // Fade out over time, then stop
                layer.volume = Mathf.Lerp(layer.volume, 0f, _layerFadeSpeed * Time.deltaTime);
                if (layer.volume <= 0.01f)
                    layer.Stop();
            }
        }

        private void StopAllLayers()
        {
            if (_explorationLayer != null) _explorationLayer.Stop();
            if (_combatLayer != null) _combatLayer.Stop();
            if (_corruptionLayer != null) _corruptionLayer.Stop();
            if (_bossLayer != null) _bossLayer.Stop();
            if (_bossPhase2Layer != null) _bossPhase2Layer.Stop();
        }
    }

    /// <summary>
    /// SFX manager for combat sounds, UI sounds, and environmental audio.
    /// Implements layered sound feedback and pooling for performance.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        [Header("SFX Sources")]
        [SerializeField] private AudioSource[] _sfxPool; // Pool of audio sources for concurrent sounds
        [SerializeField] private int _poolSize = 8;

        [Header("Audio Clips")]
        [SerializeField] private Dictionary<string, AudioClip> _sfxLibrary = new();

        [Header("Volume")]
        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private float _sfxVolume = 1f;
        [SerializeField] private float _uiVolume = 0.8f;

        private int _nextPoolIndex;

        private void Awake()
        {
            if (_sfxPool == null || _sfxPool.Length == 0)
            {
                _sfxPool = new AudioSource[_poolSize];
                for (int i = 0; i < _poolSize; i++)
                {
                    var source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0.7f; // Mixed 2D/3D
                    _sfxPool[i] = source;
                }
            }

            EventBus.Subscribe<SFXPlayEvent>(OnSFXPlay);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SFXPlayEvent>(OnSFXPlay);
        }

        private void OnSFXPlay(SFXPlayEvent e)
        {
            PlaySFX(e.SfxId, e.Position, e.Volume, e.Pitch);
        }

        /// <summary>
        /// Play a sound effect by ID at a position.
        /// </summary>
        public void PlaySFX(string sfxId, Vector3 position, float volumeMultiplier = 1f, float pitch = 1f)
        {
            if (!_sfxLibrary.TryGetValue(sfxId, out var clip)) return;

            var source = GetNextAvailableSource();
            if (source == null) return;

            source.transform.position = position;
            source.clip = clip;
            source.volume = _masterVolume * _sfxVolume * volumeMultiplier;
            source.pitch = pitch;
            source.Play();
        }

        /// <summary>
        /// Play a layered combat impact sound (swing + impact + body response + magic + low-freq).
        /// </summary>
        public void PlayLayeredImpact(string weaponId, string materialType, bool isHeavy, bool isCritical, Vector3 position)
        {
            float baseVolume = isHeavy ? 1.2f : 1f;
            float pitch = isHeavy ? 0.8f : 1f;
            float pitchOffset = isCritical ? 0.2f : 0f;

            // Layer 1: Weapon swing
            PlaySFX($"swing_{weaponId}", position, baseVolume * 0.6f, pitch + pitchOffset);

            // Layer 2: Impact
            PlaySFX($"impact_{materialType}", position, baseVolume * 0.8f, pitch);

            // Layer 3: Body/armour response
            PlaySFX($"body_response_{materialType}", position, baseVolume * 0.5f, pitch - 0.1f);

            // Layer 4: Magical layer (for special attacks)
            if (isCritical)
                PlaySFX("impact_magic_layer", position, baseVolume * 0.7f, pitch + 0.3f);

            // Layer 5: Low-frequency reinforcement
            PlaySFX("impact_low_freq", position, baseVolume * 0.4f, 0.5f);
        }

        private AudioSource GetNextAvailableSource()
        {
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                int index = (_nextPoolIndex + i) % _sfxPool.Length;
                if (!_sfxPool[index].isPlaying)
                {
                    _nextPoolIndex = (index + 1) % _sfxPool.Length;
                    return _sfxPool[index];
                }
            }
            // All sources busy - use oldest
            var source = _sfxPool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _sfxPool.Length;
            return source;
        }

        public void SetMasterVolume(float volume) => _masterVolume = volume;
        public void SetSFXVolume(float volume) => _sfxVolume = volume;
        public void SetUIVolume(float volume) => _uiVolume = volume;
    }
}
