using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;

namespace Relicfall.Corruption
{
    /// <summary>
    /// Corruption tracker that manages corruption level during a run.
    /// Corruption affects enemies, hazards, rewards, visuals, and music.
    /// This is the DEFINING MECHANIC of RELICFALL.
    /// 
    /// Thresholds:
    /// 0-24%: Stable realm
    /// 25-49%: Distorted realm  
    /// 50-74%: Dangerous realm
    /// 75-99%: Critical realm
    /// 100%: Realm collapse
    /// </summary>
    public class CorruptionTracker
    {
        public float CurrentLevel { get; private set; }
        public int CurrentThresholdIndex { get; private set; }
        public bool IsCollapsed { get; private set; }
        public float CollapseTimeRemaining { get; private set; }

        private const float THRESHOLD_1 = 25f;
        private const float THRESHOLD_2 = 50f;
        private const float THRESHOLD_3 = 75f;
        private const float THRESHOLD_4 = 100f;
        private const float COLLAPSE_DURATION = 60f;
        private const float PASSIVE_CORRUPTION_RATE = 0.5f; // per minute
        private const float ROOM_CORRUPTION_BASE = 3f;

        private float _passiveTimer;
        private float _cap = 100f;
        private List<CorruptionModifier> _activeModifiers = new();

        public event System.Action<float, float> OnCorruptionChanged;
        public event System.Action<int> OnThresholdCrossed;
        public event System.Action OnRealmCollapse;

        public CorruptionTracker()
        {
            CurrentLevel = 0f;
            CurrentThresholdIndex = 0;
            IsCollapsed = false;
            CollapseTimeRemaining = COLLAPSE_DURATION;
        }

        /// <summary>
        /// Increase corruption by a specific amount.
        /// </summary>
        public void Increase(float amount)
        {
            float previous = CurrentLevel;
            CurrentLevel = Mathf.Min(_cap, CurrentLevel + amount);
            CheckThresholdCrossing(previous, CurrentLevel);
            OnCorruptionChanged?.Invoke(previous, CurrentLevel);
        }

        /// <summary>
        /// Reduce corruption by a specific amount.
        /// </summary>
        public void Reduce(float amount)
        {
            float previous = CurrentLevel;
            CurrentLevel = Mathf.Max(0f, CurrentLevel - amount);
            CheckThresholdCrossing(previous, CurrentLevel);
            OnCorruptionChanged?.Invoke(previous, CurrentLevel);
        }

        /// <summary>
        /// Reset corruption for a new run.
        /// </summary>
        public void Reset()
        {
            CurrentLevel = 0f;
            CurrentThresholdIndex = 0;
            IsCollapsed = false;
            CollapseTimeRemaining = COLLAPSE_DURATION;
            _passiveTimer = 0f;
            _activeModifiers.Clear();
        }

        /// <summary>
        /// Tick corruption over time (passive corruption growth).
        /// Called each frame during a run.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Passive corruption: increases slowly over time
            _passiveTimer += deltaTime;
            float passiveIncrease = PASSIVE_CORRUPTION_RATE / 60f * deltaTime; // per minute rate
            Increase(passiveIncrease);

            // Check for realm collapse
            if (IsCollapsed)
            {
                CollapseTimeRemaining -= deltaTime;
                if (CollapseTimeRemaining <= 0f)
                {
                    // Realm collapse complete - force run end
                    EventBus.Publish(new PlayerDeathEvent
                    {
                        DeathPosition = Vector3.zero,
                        DeathCause = "realm_collapse",
                        RunDurationSeconds = 0
                    });
                }
            }

            // Apply active modifiers
            foreach (var mod in _activeModifiers)
            {
                if (mod.IsActive)
                    mod.Tick(deltaTime);
            }
        }

        /// <summary>
        /// Add corruption for completing a room.
        /// </summary>
        public void AddRoomCorruption(float bonus = 0f)
        {
            Increase(ROOM_CORRUPTION_BASE + bonus);
        }

        /// <summary>
        /// Set corruption cap (for stabilizer relic).
        /// </summary>
        public void SetCap(float cap)
        {
            _cap = Mathf.Clamp(cap, 50f, 100f);
            if (CurrentLevel > _cap)
            {
                CurrentLevel = _cap;
                OnCorruptionChanged?.Invoke(CurrentLevel, CurrentLevel);
            }
        }

        /// <summary>
        /// Get corruption multiplier for enemy stats.
        /// </summary>
        public float GetEnemyHealthMultiplier()
        {
            float mult = 1f;
            if (CurrentLevel >= THRESHOLD_1) mult += 0.2f;
            if (CurrentLevel >= THRESHOLD_2) mult += 0.3f;
            if (CurrentLevel >= THRESHOLD_3) mult += 0.4f;
            if (CurrentLevel >= THRESHOLD_4) mult += 0.5f;
            return mult;
        }

        /// <summary>
        /// Get corruption multiplier for enemy damage.
        /// </summary>
        public float GetEnemyDamageMultiplier()
        {
            float mult = 1f;
            if (CurrentLevel >= THRESHOLD_1) mult += 0.15f;
            if (CurrentLevel >= THRESHOLD_2) mult += 0.25f;
            if (CurrentLevel >= THRESHOLD_3) mult += 0.35f;
            if (CurrentLevel >= THRESHOLD_4) mult += 0.5f;
            return mult;
        }

        /// <summary>
        /// Get corruption modifier for reward quality.
        /// Higher corruption = better rewards.
        /// </summary>
        public float GetRewardQualityMultiplier()
        {
            float mult = 1f;
            if (CurrentLevel >= THRESHOLD_2) mult += 0.25f;
            if (CurrentLevel >= THRESHOLD_3) mult += 0.5f;
            if (CurrentLevel >= THRESHOLD_4) mult += 1f;
            return mult;
        }

        /// <summary>
        /// Get corruption modifier for elite spawn probability.
        /// </summary>
        public float GetEliteProbabilityModifier()
        {
            float prob = 0f;
            if (CurrentLevel >= THRESHOLD_1) prob += 0.1f;
            if (CurrentLevel >= THRESHOLD_2) prob += 0.15f;
            if (CurrentLevel >= THRESHOLD_3) prob += 0.2f;
            if (CurrentLevel >= THRESHOLD_4) prob += 0.3f;
            return prob;
        }

        /// <summary>
        /// Get healing effectiveness modifier.
        /// High corruption reduces healing.
        /// </summary>
        public float GetHealingModifier()
        {
            float mod = 1f;
            if (CurrentLevel >= THRESHOLD_3) mod -= 0.2f;
            if (CurrentLevel >= THRESHOLD_4) mod -= 0.3f;
            return Mathf.Max(0.3f, mod);
        }

        /// <summary>
        /// Get corruption modifier for hazard frequency.
        /// </summary>
        public float GetHazardFrequencyMultiplier()
        {
            float mult = 1f;
            if (CurrentLevel >= THRESHOLD_1) mult += 0.3f;
            if (CurrentLevel >= THRESHOLD_2) mult += 0.5f;
            if (CurrentLevel >= THRESHOLD_3) mult += 1f;
            if (CurrentLevel >= THRESHOLD_4) mult += 2f;
            return mult;
        }

        /// <summary>
        /// Should additional enemy modifiers be applied?
        /// </summary>
        public bool ShouldApplyEnemyModifiers() => CurrentLevel >= THRESHOLD_1;

        /// <summary>
        /// Should mutated enemies appear?
        /// </summary>
        public bool ShouldSpawnMutatedEnemies() => CurrentLevel >= THRESHOLD_2;

        /// <summary>
        /// Should elite invasions occur?
        /// </summary>
        public bool ShouldEliteInvade() => CurrentLevel >= THRESHOLD_3;

        /// <summary>
        /// Is realm collapse active?
        /// </summary>
        public bool IsRealmCollapsed() => CurrentLevel >= THRESHOLD_4;

        /// <summary>
        /// Get the current corruption tier name for UI display.
        /// </summary>
        public string GetCorruptionTierName()
        {
            if (CurrentLevel < THRESHOLD_1) return "Stable";
            if (CurrentLevel < THRESHOLD_2) return "Distorted";
            if (CurrentLevel < THRESHOLD_3) return "Dangerous";
            if (CurrentLevel < THRESHOLD_4) return "Critical";
            return "Collapsed";
        }

        /// <summary>
        /// Get the visual distortion intensity for rendering.
        /// </summary>
        public float GetVisualDistortionIntensity()
        {
            return Mathf.Clamp01(CurrentLevel / 100f);
        }

        /// <summary>
        /// Get music intensity for adaptive music system.
        /// </summary>
        public float GetMusicIntensity()
        {
            return Mathf.Clamp01(CurrentLevel / 75f);
        }

        /// <summary>
        /// Get lighting modification parameters.
        /// </summary>
        public CorruptionLightingData GetLightingData()
        {
            var data = new CorruptionLightingData();

            // Color shift toward crimson/magenta with increasing corruption
            float t = CurrentLevel / 100f;
            data.AmbientColor = Color.Lerp(
                new Color(0.2f, 0.2f, 0.25f), // Neutral
                new Color(0.3f, 0.1f, 0.15f), // Corrupted crimson
                t
            );

            data.FogDensity = Mathf.Lerp(0.01f, 0.05f, t);
            data.FogColor = Color.Lerp(Color.gray, new Color(0.4f, 0.1f, 0.15f), t);
            data.EmissiveIntensity = Mathf.Lerp(0f, 1f, t);
            data.VFXIntensity = t;
            data.FloatingDebrisCount = (int)Mathf.Lerp(0, 20, t);
            data.CrackIntensity = Mathf.Lerp(0f, 1f, t);

            return data;
        }

        /// <summary>
        /// Add a corruption modifier (from a relic's corruption effect).
        /// </summary>
        public void AddModifier(CorruptionModifier modifier)
        {
            _activeModifiers.Add(modifier);
        }

        /// <summary>
        /// Remove a corruption modifier (when relic is sacrificed).
        /// </summary>
        public void RemoveModifier(string modifierId)
        {
            _activeModifiers.RemoveAll(m => m.ModifierId == modifierId);
        }

        private void CheckThresholdCrossing(float previous, float current)
        {
            int prevThreshold = GetThresholdIndex(previous);
            int newThreshold = GetThresholdIndex(current);

            if (newThreshold > prevThreshold)
            {
                CurrentThresholdIndex = newThreshold;
                OnThresholdCrossed?.Invoke(newThreshold);
                EventBus.Publish(new CorruptionThresholdEvent
                {
                    CorruptionLevel = current,
                    ThresholdIndex = newThreshold
                });

                // Check for realm collapse
                if (current >= THRESHOLD_4)
                {
                    IsCollapsed = true;
                    OnRealmCollapse?.Invoke();
                    EventBus.Publish(new RealmCollapseEvent { TimeRemaining = COLLAPSE_DURATION });
                }
            }
            else if (newThreshold < prevThreshold)
            {
                CurrentThresholdIndex = newThreshold;
                // Downgrading threshold (rare, only if corruption is reduced significantly)
                if (IsCollapsed && current < THRESHOLD_4)
                {
                    IsCollapsed = false;
                    CollapseTimeRemaining = COLLAPSE_DURATION;
                }
            }
        }

        private int GetThresholdIndex(float level)
        {
            if (level < THRESHOLD_1) return 0;
            if (level < THRESHOLD_2) return 1;
            if (level < THRESHOLD_3) return 2;
            if (level < THRESHOLD_4) return 3;
            return 4;
        }
    }

    /// <summary>
    /// Corruption modifier from relic corruption effects.
    /// These modify enemy behavior, hazards, or other world properties.
    /// </summary>
    public class CorruptionModifier
    {
        public string ModifierId;
        public string SourceRelicId;
        public string ModifierName;
        public string Description;
        public CorruptionEffectType Type;
        public float Value;
        public float Value2;
        public bool IsActive = true;
        public float Duration; // -1 = permanent for the run

        public void Tick(float deltaTime)
        {
            if (Duration > 0f)
            {
                Duration -= deltaTime;
                if (Duration <= 0f)
                    IsActive = false;
            }
        }
    }

    /// <summary>
    /// Lighting and visual data for corruption-driven rendering changes.
    /// </summary>
    public struct CorruptionLightingData
    {
        public Color AmbientColor;
        public float FogDensity;
        public Color FogColor;
        public float EmissiveIntensity;
        public float VFXIntensity;
        public int FloatingDebrisCount;
        public float CrackIntensity;
    }
}
