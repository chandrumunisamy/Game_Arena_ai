using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Corruption;

namespace Relicfall.Relics
{
    /// <summary>
    /// Relic definition ScriptableObject. Each relic has:
    /// - A powerful benefit for the player
    /// - A corruption effect making the world more dangerous
    /// - Tags for synergy detection
    /// This is the CORE MECHANIC of RELICFALL.
    /// </summary>
    [CreateAssetMenu(fileName = "RelicDef", menuName = "RELICFALL/Relics/Relic Definition")]
    public class RelicDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string RelicId;
        public string RelicName;
        public string Description;
        public string FlavorText;
        public RelicRarity Rarity;
        public Sprite Icon;
        public GameObject PickupModelPrefab;

        [Header("Benefit")]
        public string BenefitDescription;
        public RelicEffectType BenefitType;
        public float BenefitValue;
        public float BenefitValue2; // Secondary value
        public string BenefitTag; // e.g., "dash_clone", "parry_slow"
        public string[] BenefitTags;
        public bool BenefitIsPercentage;
        public float BenefitDuration; // For timed effects
        public GameObject BenefitVFXPrefab;
        public string BenefitSoundId;

        [Header("Corruption")]
        public float CorruptionIncrease = 10f; // How much corruption this relic adds
        public string CorruptionEffectDescription;
        public CorruptionEffectType CorruptionType;
        public float CorruptionEffectValue;
        public float CorruptionEffectValue2;
        public string CorruptionTag;
        public GameObject CorruptionVFXPrefab;
        public string CorruptionSoundId;

        [Header("Synergy")]
        public string[] SynergyTags; // e.g., "Dash", "Clone", "Fire", "Bleed"
        public string[] IncompatibleTags;
        public string[] CompatibleWeaponFamilies;
        public SynergyEffect[] Synergies;

        [Header("Upgrade")]
        public string UpgradeCategory; // e.g., "Offensive", "Defensive", "Mobility"
        public int UpgradeTier = 1;
        public RelicDefinition[] UpgradeVariants; // Stronger versions

        [Header("Cursed")]
        public bool IsCursed = false;
        public string CurseCondition; // e.g., "low_health", "high_corruption"
        public float CurseTriggerThreshold;
        public float CurseBonusValue; // Extra benefit when curse condition met
        public float CursePenaltyValue; // Extra penalty when curse condition not met

        [Header("Visual")]
        public Color RelicGlowColor = new Color(0f, 0.9f, 1f, 1f);
        public Color CorruptionGlowColor = new Color(0.85f, 0.2f, 0.3f, 1f);

        [Header("Balance")]
        public float Weight = 1f; // Probability weight for random selection
        public bool RequiresDiscovery = true; // Must be discovered before appearing in pool
        public string RequiredRelicId; // Requires another relic first
        public int MinCorruptionLevel; // Only appears at this corruption level
        public int MaxRunsCompleted; // Only appears after this many runs completed
    }

    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Cursed
    }

    public enum RelicEffectType
    {
        DamageIncrease,
        DamageModifier,
        NewAttack,
        NewAbility,
        CriticalModifier,
        ParryModifier,
        DashModifier,
        MovementModifier,
        DefenseModifier,
        HealthModifier,
        HealingModifier,
        StaggerModifier,
        ExecutionModifier,
        ProjectileModifier,
        SummonEffect,
        StatusEffect,
        AreaEffect,
        CloneEffect,
        TimeEffect,
        EconomyModifier,
        RewardModifier,
        CorruptionModifier,
        ExtractionModifier,
        CustomBehavior
    }

    public enum CorruptionEffectType
    {
        EnemyModifier,
        EnemySpawnModifier,
        HazardModifier,
        ArenaModifier,
        HealingModifier,
        EliteModifier,
        BossModifier,
        EnvironmentalVFX,
        MusicModifier,
        RewardModifier,
        CorruptionModifier,
        CustomBehavior
    }

    [System.Serializable]
    public class SynergyEffect
    {
        public string RequiredTag1;
        public string RequiredTag2;
        public string SynergyName;
        public string SynergyDescription;
        public float BonusValue;
        public bool IsNewBehavior;
        public string NewBehaviorDescription;
    }

    /// <summary>
    /// Runtime relic instance that tracks active effects and synergy state.
    /// Mutable runtime class - not a ScriptableObject.
    /// </summary>
    public class ActiveRelic
    {
        public string RelicId;
        public RelicDefinition Definition;
        public bool IsActive = true;
        public bool IsCorruptionActive = true;
        public float CorruptionAdded;
        public List<string> ActiveSynergies = new();
        public float StackCount = 1f;
        public float RemainingDuration; // For timed effects
    }

    /// <summary>
    /// Relic runtime manager that handles active relics, synergy detection,
    /// corruption effects, and relic activation/deactivation.
    /// </summary>
    public class RelicManager : MonoBehaviour
    {
        [SerializeField] private int _maxRelicSlots = 12;
        [SerializeField] private float _relicPickupRange = 2f;

        private List<ActiveRelic> _activeRelics = new();
        private Dictionary<string, ActiveRelic> _relicLookup = new();
        private Dictionary<string, int> _tagCounts = new(); // Count of relics with each tag
        private HashSet<string> _activeTags = new();
        private HashSet<string> _discoveredRelics = new();

        public List<ActiveRelic> ActiveRelics => _activeRelics;
        public int RelicCount => _activeRelics.Count;
        public int MaxSlots => _maxRelicSlots;
        public bool HasSlotsAvailable => _activeRelics.Count < _maxRelicSlots;

        // Player reference for applying effects
        private Player.PlayerController _player;
        private CorruptionTracker _corruption;

        private void Awake()
        {
            _corruption = new CorruptionTracker();
        }

        private void Start()
        {
            _player = GetComponent<Player.PlayerController>();
            EventBus.Subscribe<RelicCollectedEvent>(OnRelicCollected);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<RelicCollectedEvent>(OnRelicCollected);
        }

        /// <summary>
        /// React to a relic being collected elsewhere in the game (pickups, rewards).
        /// The relic itself is applied through CollectRelic; this only tracks discovery.
        /// </summary>
        private void OnRelicCollected(RelicCollectedEvent e)
        {
            if (!string.IsNullOrEmpty(e.RelicId))
                _discoveredRelics.Add(e.RelicId);
        }

        /// <summary>
        /// Collect a relic and apply its effects.
        /// </summary>
        public bool CollectRelic(RelicDefinition relicDef)
        {
            if (!HasSlotsAvailable) return false;

            var activeRelic = new ActiveRelic
            {
                RelicId = relicDef.RelicId,
                Definition = relicDef,
                CorruptionAdded = relicDef.CorruptionIncrease,
                IsActive = true,
                IsCorruptionActive = true
            };

            _activeRelics.Add(activeRelic);
            _relicLookup[relicDef.RelicId] = activeRelic;
            _discoveredRelics.Add(relicDef.RelicId);

            // Update tag counts
            foreach (var tag in relicDef.SynergyTags)
            {
                _tagCounts[tag] = _tagCounts.GetValueOrDefault(tag) + 1;
                _activeTags.Add(tag);
            }

            // Apply benefit effect
            ApplyBenefit(activeRelic);

            // Apply corruption effect
            _corruption.Increase(relicDef.CorruptionIncrease);

            // Check for synergies
            CheckSynergies(activeRelic);

            // Publish event
            EventBus.Publish(new RelicCollectedEvent
            {
                RelicId = relicDef.RelicId,
                Rarity = relicDef.Rarity.ToString(),
                CorruptionIncrease = relicDef.CorruptionIncrease
            });

            return true;
        }

        /// <summary>
        /// Remove a relic (for sacrifice during extraction).
        /// </summary>
        public void RemoveRelic(string relicId)
        {
            if (!_relicLookup.TryGetValue(relicId, out var relic)) return;

            _activeRelics.Remove(relic);
            _relicLookup.Remove(relicId);

            // Remove tag counts
            foreach (var tag in relic.Definition.SynergyTags)
            {
                _tagCounts[tag] = Mathf.Max(0, _tagCounts.GetValueOrDefault(tag) - 1);
                if (_tagCounts[tag] == 0)
                    _activeTags.Remove(tag);
            }

            // Remove benefit effect
            RemoveBenefit(relic);

            // Remove corruption effect
            // Corruption increase stays (you already paid the price)
            // But corruption EFFECTS from this relic are removed

            // Re-check synergies
            RefreshSynergies();
        }

        /// <summary>
        /// Apply a relic's benefit effect to the player.
        /// </summary>
        private void ApplyBenefit(ActiveRelic relic)
        {
            var def = relic.Definition;
            if (def == null) return;

            switch (def.BenefitType)
            {
                case RelicEffectType.DamageIncrease:
                    // Modify player damage
                    // In full implementation, modify damage calculation
                    break;
                case RelicEffectType.DamageModifier:
                    // Change damage behavior (e.g., attacks leave bleed)
                    break;
                case RelicEffectType.CriticalModifier:
                    // Modify critical hit behavior
                    break;
                case RelicEffectType.ParryModifier:
                    // Extend parry window, add parry effects
                    break;
                case RelicEffectType.DashModifier:
                    // Modify dash behavior
                    break;
                case RelicEffectType.CloneEffect:
                    // Dash creates attacking clone
                    if (def.BenefitTag == "dash_clone")
                    {
                        // Register dash clone callback
                    }
                    break;
                case RelicEffectType.TimeEffect:
                    // Parry slows enemies
                    if (def.BenefitTag == "parry_slow")
                    {
                        // Register parry slow callback
                    }
                    break;
                case RelicEffectType.DefenseModifier:
                    // Increase defense
                    break;
                case RelicEffectType.HealthModifier:
                    // Increase max health
                    break;
                case RelicEffectType.HealingModifier:
                    // Modify healing
                    break;
                case RelicEffectType.SummonEffect:
                    // Summon a helper
                    break;
                case RelicEffectType.EconomyModifier:
                    // Modify rewards
                    break;
                case RelicEffectType.RewardModifier:
                    // Extra reward choices
                    break;
                case RelicEffectType.ExecutionModifier:
                    // Executions increase damage
                    break;
                case RelicEffectType.CorruptionModifier:
                    // Modify corruption effects
                    break;
                case RelicEffectType.CustomBehavior:
                    // Custom scripted behavior
                    break;
            }
        }

        /// <summary>
        /// Remove a relic's benefit effect.
        /// </summary>
        private void RemoveBenefit(ActiveRelic relic)
        {
            // Reverse the benefit modifications
            // In full implementation, track and reverse all modifications
        }

        /// <summary>
        /// Check for new synergies when a relic is added.
        /// </summary>
        private void CheckSynergies(ActiveRelic newRelic)
        {
            foreach (var synergy in newRelic.Definition.Synergies)
            {
                // Check if we have relics with both required tags
                bool hasTag1 = _activeTags.Contains(synergy.RequiredTag1);
                bool hasTag2 = _activeTags.Contains(synergy.RequiredTag2);

                if (hasTag1 && hasTag2)
                {
                    // Synergy activated!
                    newRelic.ActiveSynergies.Add(synergy.SynergyName);

                    EventBus.Publish(new RelicSynergyEvent
                    {
                        RelicId1 = newRelic.RelicId,
                        RelicId2 = FindRelicWithTag(synergy.RequiredTag2)?.RelicId ?? "",
                        SynergyTag = synergy.SynergyName
                    });
                }
            }
        }

        /// <summary>
        /// Refresh all synergies (after relic removal).
        /// </summary>
        private void RefreshSynergies()
        {
            // Clear all synergy state
            foreach (var relic in _activeRelics)
                relic.ActiveSynergies.Clear();

            // Re-check all synergies
            foreach (var relic in _activeRelics)
                CheckSynergies(relic);
        }

        /// <summary>
        /// Find an active relic with a specific tag.
        /// </summary>
        private ActiveRelic FindRelicWithTag(string tag)
        {
            foreach (var relic in _activeRelics)
            {
                if (relic.Definition?.SynergyTags != null)
                {
                    foreach (var t in relic.Definition.SynergyTags)
                    {
                        if (t == tag) return relic;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Check if player has a relic with a specific tag.
        /// </summary>
        public bool HasRelicWithTag(string tag) => _activeTags.Contains(tag);

        /// <summary>
        /// Get the count of relics with a specific tag.
        /// </summary>
        public int GetRelicCountWithTag(string tag) => _tagCounts.GetValueOrDefault(tag);

        /// <summary>
        /// Get all active synergy tags.
        /// </summary>
        public HashSet<string> GetActiveTags() => _activeTags;

        /// <summary>
        /// Calculate damage multiplier from all active relic benefits.
        /// </summary>
        public float GetDamageMultiplier()
        {
            float multiplier = 1f;
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitType == RelicEffectType.DamageIncrease)
                {
                    if (relic.Definition.BenefitIsPercentage)
                        multiplier += relic.Definition.BenefitValue / 100f;
                    else
                        multiplier += relic.Definition.BenefitValue;
                }
            }
            return multiplier;
        }

        /// <summary>
        /// Calculate critical hit modifier from relics.
        /// </summary>
        public float GetCriticalChanceBonus()
        {
            float bonus = 0f;
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitType == RelicEffectType.CriticalModifier)
                    bonus += relic.Definition.BenefitValue;
            }
            return bonus;
        }

        /// <summary>
        /// Check if dash should create a clone.
        /// </summary>
        public bool ShouldDashCreateClone()
        {
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitTag == "dash_clone")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if parry should slow enemies.
        /// </summary>
        public bool ShouldParrySlowEnemies()
        {
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitTag == "parry_slow")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Should critical hits cause explosions?
        /// </summary>
        public bool ShouldCritExplode()
        {
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitTag == "crit_explode")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Should executions increase damage permanently?
        /// </summary>
        public bool ShouldExecutionIncreaseDamage()
        {
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitTag == "execution_damage")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get healing effectiveness modifier (can be reduced by corruption relics).
        /// </summary>
        public float GetHealingModifier()
        {
            float modifier = 1f;
            foreach (var relic in _activeRelics)
            {
                if (relic.IsActive && relic.Definition.BenefitType == RelicEffectType.HealingModifier)
                    modifier *= relic.Definition.BenefitValue;
                if (relic.IsCorruptionActive && relic.Definition.CorruptionType == CorruptionEffectType.HealingModifier)
                    modifier *= relic.Definition.CorruptionEffectValue;
            }
            return modifier;
        }

        /// <summary>
        /// Get the corruption corruption effects for current corruption level.
        /// </summary>
        public void ApplyCorruptionEffects(float corruptionLevel)
        {
            foreach (var relic in _activeRelics)
            {
                relic.IsCorruptionActive = true;
                // Corruption effects are applied through the corruption system
            }
        }

        /// <summary>
        /// Reset all relics for a new run.
        /// </summary>
        public void ResetForNewRun()
        {
            _activeRelics.Clear();
            _relicLookup.Clear();
            _tagCounts.Clear();
            _activeTags.Clear();
        }

        /// <summary>
        /// Mark a relic as discovered (persists across runs).
        /// </summary>
        public void DiscoverRelic(string relicId)
        {
            _discoveredRelics.Add(relicId);
        }

        /// <summary>
        /// Check if a relic has been discovered.
        /// </summary>
        public bool IsRelicDiscovered(string relicId) => _discoveredRelics.Contains(relicId);

        /// <summary>
        /// Get discovered relic IDs for saving.
        /// </summary>
        public List<string> GetDiscoveredRelicIds() => new List<string>(_discoveredRelics);
    }

    /// <summary>
    /// Relic data generator. Contains all 50+ relic definitions as data.
    /// In production, these would be ScriptableObject assets; this class
    /// generates them programmatically for rapid prototyping.
    /// </summary>
    public static class RelicDataGenerator
    {
        private static List<RelicDefinition> _allRelics;

        public static List<RelicDefinition> GetAllRelics()
        {
            if (_allRelics == null)
                GenerateAllRelics();
            return _allRelics;
        }

        private static void GenerateAllRelics()
        {
            _allRelics = new List<RelicDefinition>();

            // === OFFENSIVE RELICS (10) ===

            AddRelic("mirror_fang", "Mirror Fang", RelicRarity.Rare,
                "Dashing creates a temporary attacking clone that mimics your last attack.",
                "Certain enemies also create weaker clones when they dash.",
                RelicEffectType.CloneEffect, "dash_clone", 1f,
                CorruptionEffectType.EnemyModifier, "enemy_clone", 1f,
                new[] { "Dash", "Clone" }, "Offensive");

            AddRelic("clockbreaker", "Clockbreaker", RelicRarity.Epic,
                "Successful parries briefly slow all nearby enemies.",
                "Environmental hazards periodically accelerate, becoming more dangerous.",
                RelicEffectType.ParryModifier, "parry_slow", 0.5f,
                CorruptionEffectType.HazardModifier, "hazard_accelerate", 1.5f,
                new[] { "Parry", "Time" }, "Offensive");

            AddRelic("blood_crown", "Blood Crown", RelicRarity.Rare,
                "Critical hits cause area explosions, damaging nearby enemies.",
                "Wounded enemies gain attack speed, becoming more aggressive.",
                RelicEffectType.CriticalModifier, "crit_explode", 3f,
                CorruptionEffectType.EnemyModifier, "enemy_frenzy_wounded", 1.3f,
                new[] { "Critical", "Area" }, "Offensive");

            AddRelic("devouring_gauntlet", "Devouring Gauntlet", RelicRarity.Epic,
                "Each execution permanently increases your damage for the current run.",
                "Healing effectiveness decreases after each execution.",
                RelicEffectType.ExecutionModifier, "execution_damage", 0.1f,
                CorruptionEffectType.HealingModifier, "healing_decrease_per_exec", 0.05f,
                new[] { "Execution", "Damage" }, "Offensive");

            AddRelic("emberfang", "Emberfang", RelicRarity.Uncommon,
                "Light attacks apply stacking fire damage over time.",
                "Enemies occasionally emit flame bursts when hit.",
                RelicEffectType.StatusEffect, "fire_dot", 3f,
                CorruptionEffectType.EnemyModifier, "enemy_fire_burst", 0.15f,
                new[] { "Fire", "StatusEffect" }, "Offensive");

            AddRelic("shard_edge", "Shard Edge", RelicRarity.Common,
                "Heavy attacks shatter on impact, sending fragments that damage nearby enemies.",
                "Fragmented stone hazards appear more frequently in arenas.",
                RelicEffectType.DamageModifier, "heavy_shatter", 0.6f,
                CorruptionEffectType.HazardModifier, "stone_fragments", 2f,
                new[] { "Heavy", "Projectile" }, "Offensive");

            AddRelic("frostjaw", "Frostjaw", RelicRarity.Uncommon,
                "Dash attacks freeze enemies briefly, preventing movement.",
                "Ice patches appear on arena floors, slowing your movement.",
                RelicEffectType.DashModifier, "dash_freeze", 1f,
                CorruptionEffectType.HazardModifier, "ice_patches", 3f,
                new[] { "Dash", "Ice" }, "Offensive");

            AddRelic("stormveil", "Stormveil", RelicRarity.Rare,
                "After dodging three attacks in quick succession, unleash a lightning burst.",
                "Lightning strikes periodically target random areas in the arena.",
                RelicEffectType.NewAbility, "dodge_lightning", 1f,
                CorruptionEffectType.HazardModifier, "arena_lightning", 1f,
                new[] { "Dash", "Projectile", "Electric" }, "Offensive");

            AddRelic("ravage_claw", "Ravage Claw", RelicRarity.Uncommon,
                "Light combo finishers cause enemies to bleed, losing health over time.",
                "Bleeding enemies spread their affliction to nearby allies.",
                RelicEffectType.StatusEffect, "bleed_combo", 5f,
                CorruptionEffectType.EnemyModifier, "bleed_spread", 1f,
                new[] { "Bleed", "StatusEffect" }, "Offensive");

            AddRelic("wrath_sigil", "Wrath Sigil", RelicRarity.Common,
                "Damage increases as corruption rises, scaling up to 50% bonus at high corruption.",
                "Enemy damage also scales with corruption more aggressively.",
                RelicEffectType.CorruptionModifier, "damage_from_corruption", 0.5f,
                CorruptionEffectType.EnemyModifier, "enemy_damage_corruption", 0.7f,
                new[] { "Corruption", "Damage" }, "Offensive");

            // === DEFENSIVE RELICS (8) ===

            AddRelic("ironveil_mirror", "Ironveil Mirror", RelicRarity.Uncommon,
                "Successful parries reflect 30% of blocked damage back to attackers.",
                "Parried enemies gain temporary armor, reducing subsequent damage.",
                RelicEffectType.ParryModifier, "parry_reflect", 0.3f,
                CorruptionEffectType.EnemyModifier, "parry_armor", 0.3f,
                new[] { "Parry", "Reflect" }, "Defensive");

            AddRelic("stoneheart", "Stoneheart", RelicRarity.Common,
                "Taking damage reduces subsequent damage by 10% for 3 seconds.",
                "Enemies also gain temporary damage resistance after being hit.",
                RelicEffectType.DefenseModifier, "damage_reduction_on_hit", 0.1f,
                CorruptionEffectType.EnemyModifier, "enemy_damage_resist", 0.1f,
                new[] { "Defense", "Damage" }, "Defensive");

            AddRelic("ghostveil", "Ghostveil", RelicRarity.Rare,
                "Dash grants 1 extra second of invulnerability frames after the dash ends.",
                "Enemies gain brief invulnerability when entering new attack phases.",
                RelicEffectType.DashModifier, "extended_iframes", 1f,
                CorruptionEffectType.EnemyModifier, "enemy_phase_iframes", 0.5f,
                new[] { "Dash", "IFrames" }, "Defensive");

            AddRelic("bone_aegis", "Bone Aegis", RelicRarity.Uncommon,
                "After stagger, gain a shield that absorbs the next hit completely.",
                "Staggered enemies immediately recover and gain a brief speed boost.",
                RelicEffectType.DefenseModifier, "shield_after_stagger", 1f,
                CorruptionEffectType.EnemyModifier, "stagger_speed_boost", 1.5f,
                new[] { "Stagger", "Defense" }, "Defensive");

            AddRelic("hollow_coin", "Hollow Coin", RelicRarity.Rare,
                "Reward rooms offer an additional choice when selecting upgrades.",
                "One reward choice may be cursed, offering a powerful benefit with extra corruption.",
                RelicEffectType.RewardModifier, "extra_reward_choice", 1f,
                CorruptionEffectType.RewardModifier, "cursed_reward_option", 1f,
                new[] { "Economy", "Reward" }, "Economy");

            AddRelic("veil_of_ashes", "Veil of Ashes", RelicRarity.Common,
                "When health drops below 30%, movement speed increases by 25%.",
                "When your health drops below 30%, enemies also move 15% faster.",
                RelicEffectType.MovementModifier, "speed_when_low_hp", 0.25f,
                CorruptionEffectType.EnemyModifier, "enemy_speed_when_player_low", 0.15f,
                new[] { "Mobility", "LowHealth" }, "Defensive");

            AddRelic("thorned_mirror", "Thorned Mirror", RelicRarity.Uncommon,
                "Enemies that hit you take 15% of their own damage as thorns damage.",
                "Elite enemies become immune to thorns effects.",
                RelicEffectType.DefenseModifier, "thorns", 0.15f,
                CorruptionEffectType.EliteModifier, "elite_thorns_immune", 1f,
                new[] { "Defense", "Thorns" }, "Defensive");

            AddRelic("spectral_aegis", "Spectral Aegis", RelicRarity.Epic,
                "Every 10 seconds, gain a spectral shield that blocks one hit.",
                "Shield recharge slows as corruption increases.",
                RelicEffectType.NewAbility, "spectral_shield", 10f,
                CorruptionEffectType.CustomBehavior, "shield_recharge_slow", 0.5f,
                new[] { "Defense", "Shield" }, "Defensive");

            // === MOBILITY RELICS (6) ===

            AddRelic("windrunner_mark", "Windrunner Mark", RelicRarity.Uncommon,
                "Dash distance increases by 40% and dash can cross small gaps.",
                "Enemies gain longer dash distance and can cross gaps.",
                RelicEffectType.DashModifier, "dash_distance_increase", 0.4f,
                CorruptionEffectType.EnemyModifier, "enemy_dash_increase", 0.3f,
                new[] { "Dash", "Mobility" }, "Mobility");

            AddRelic("shadowstep", "Shadowstep", RelicRarity.Rare,
                "After dash, briefly become invisible to enemies for 1.5 seconds.",
                "Assassin enemies can also disappear briefly.",
                RelicEffectType.DashModifier, "dash_invisible", 1.5f,
                CorruptionEffectType.EnemyModifier, "assassin_invisible", 1f,
                new[] { "Dash", "Stealth" }, "Mobility");

            AddRelic("leaping_crown", "Leaping Crown", RelicRarity.Common,
                "Jump height and distance increased, allowing traversal of elevated platforms.",
                "Some enemies gain leap attacks that reach elevated positions.",
                RelicEffectType.MovementModifier, "jump_boost", 1.3f,
                CorruptionEffectType.EnemyModifier, "enemy_leap_attack", 1f,
                new[] { "Mobility", "Jump" }, "Mobility");

            AddRelic("currentblade", "Currentblade", RelicRarity.Uncommon,
                "Moving continuously for 3 seconds increases attack speed by 20%.",
                "Enemies that stand still gain increasing defense.",
                RelicEffectType.MovementModifier, "speed_attack_boost", 0.2f,
                CorruptionEffectType.EnemyModifier, "enemy_standing_defense", 0.15f,
                new[] { "Mobility", "Speed" }, "Mobility");

            AddRelic("phase_walker", "Phase Walker", RelicRarity.Epic,
                "Dash can pass through enemies, damaging those you phase through.",
                "Some enemies gain phasing ability, passing through walls.",
                RelicEffectType.DashModifier, "dash_phase_through", 0.5f,
                CorruptionEffectType.EnemyModifier, "enemy_phase", 1f,
                new[] { "Dash", "Phase" }, "Mobility");

            AddRelic("rift_anchor", "Rift Anchor", RelicRarity.Legendary,
                "Mark a location. Dash toward the mark from anywhere in the room.",
                "Enemies create unstable rift marks that teleport projectiles unpredictably.",
                RelicEffectType.NewAbility, "rift_mark_recall", 1f,
                CorruptionEffectType.HazardModifier, "enemy_rift_marks", 1f,
                new[] { "Dash", "Teleport" }, "Mobility");

            // === PARRY RELICS (4) ===

            AddRelic("perfect_strike", "Perfect Strike", RelicRarity.Uncommon,
                "Parry window is extended by 0.1 seconds.",
                "Enemy telegraph durations are shortened by 0.1 seconds.",
                RelicEffectType.ParryModifier, "parry_window_extend", 0.1f,
                CorruptionEffectType.EnemyModifier, "telegraph_shorten", 0.1f,
                new[] { "Parry", "Time" }, "Parry");

            AddRelic("counterpulse", "Counterpulse", RelicRarity.Rare,
                "Perfect parries create a shockwave that staggers nearby enemies.",
                "Staggered enemies recover faster from stagger.",
                RelicEffectType.ParryModifier, "parry_shockwave", 3f,
                CorruptionEffectType.EnemyModifier, "stagger_recovery_fast", 0.5f,
                new[] { "Parry", "Shockwave" }, "Parry");

            AddRelic("echo_parry", "Echo Parry", RelicRarity.Epic,
                "Parrying creates a delayed attack echo that hits the parried enemy again.",
                "Enemies create delayed attack echoes after their attacks.",
                RelicEffectType.ParryModifier, "parry_echo_attack", 1f,
                CorruptionEffectType.EnemyModifier, "enemy_attack_echo", 0.7f,
                new[] { "Parry", "Time", "Echo" }, "Parry");

            AddRelic("nullward", "Nullward", RelicRarity.Common,
                "Parrying removes one corruption point.",
                "Failed parries add two corruption points.",
                RelicEffectType.ParryModifier, "parry_reduce_corruption", 1f,
                CorruptionEffectType.CorruptionModifier, "parry_fail_corruption", 2f,
                new[] { "Parry", "Corruption" }, "Parry");

            // === CRITICAL RELICS (4) ===

            AddRelic("fateblade", "Fateblade", RelicRarity.Uncommon,
                "Critical hit chance increases by 15%.",
                "Elite enemies gain 20% critical hit chance.",
                RelicEffectType.CriticalModifier, "crit_chance_increase", 0.15f,
                CorruptionEffectType.EliteModifier, "elite_crit_chance", 0.2f,
                new[] { "Critical" }, "Critical");

            AddRelic("decap_mark", "Decap Mark", RelicRarity.Rare,
                "Critical hits on staggered enemies deal triple damage.",
                "Staggered enemies become immune to critical damage.",
                RelicEffectType.CriticalModifier, "crit_on_staggered_3x", 3f,
                CorruptionEffectType.EnemyModifier, "staggered_crit_immune", 1f,
                new[] { "Critical", "Stagger" }, "Critical");

            AddRelic("cascade_mark", "Cascade Mark", RelicRarity.Rare,
                "Critical hits trigger a second guaranteed critical on the next attack.",
                "After an enemy critical cascade, your defense drops briefly.",
                RelicEffectType.CriticalModifier, "crit_cascade", 1f,
                CorruptionEffectType.EnemyModifier, "player_defense_drop", 0.2f,
                new[] { "Critical", "Combo" }, "Critical");

            AddRelic("void_edge", "Void Edge", RelicRarity.Epic,
                "Every 5th attack is guaranteed to be critical regardless of stats.",
                "Every 5th enemy attack has guaranteed critical properties.",
                RelicEffectType.CriticalModifier, "guaranteed_crit_5th", 5f,
                CorruptionEffectType.EnemyModifier, "enemy_guaranteed_crit_5th", 5f,
                new[] { "Critical", "Pattern" }, "Critical");

            // === SUMMON RELICS (3) ===

            AddRelic("spirit_blade", "Spirit Blade", RelicRarity.Rare,
                "Summon a spectral ally that attacks enemies near you.",
                "Summoner enemies create additional spectral minions.",
                RelicEffectType.SummonEffect, "spectral_ally", 1f,
                CorruptionEffectType.EnemyModifier, "summoner_extra_minions", 1f,
                new[] { "Summon", "Spirit" }, "Summon");

            AddRelic("bone_legion", "Bone Legion", RelicRarity.Epic,
                "After killing 5 enemies, summon a bone soldier that fights for 15 seconds.",
                "Enemies killed by bone soldiers spawn hostile bone fragments.",
                RelicEffectType.SummonEffect, "bone_soldier", 5f,
                CorruptionEffectType.EnemyModifier, "bone_fragments_hostile", 1f,
                new[] { "Summon", "Execution" }, "Summon");

            AddRelic("runic_golem", "Runic Golem", RelicRarity.Legendary,
                "Summon a golem that blocks enemy projectiles and attacks periodically.",
                "Living statue enemies become more aggressive and gain projectile attacks.",
                RelicEffectType.SummonEffect, "runic_golem", 1f,
                CorruptionEffectType.EnemyModifier, "statue_aggressive_projectile", 1f,
                new[] { "Summon", "Defense" }, "Summon");

            // === STATUS EFFECT RELICS (4) ===

            AddRelic("poisonfang", "Poisonfang", RelicRarity.Common,
                "Heavy attacks apply poison that deals damage over 5 seconds.",
                "Poison pools appear on arena floors.",
                RelicEffectType.StatusEffect, "poison_heavy", 5f,
                CorruptionEffectType.HazardModifier, "poison_pools", 3f,
                new[] { "Poison", "StatusEffect" }, "StatusEffect");

            AddRelic("curse_mark", "Curse Mark", RelicRarity.Uncommon,
                "Mark enemies on hit. Marked enemies take 20% more damage from all sources.",
                "Marked enemies explode on death, dealing damage to the player if nearby.",
                RelicEffectType.StatusEffect, "mark_damage_increase", 0.2f,
                CorruptionEffectType.EnemyModifier, "marked_death_explosion", 3f,
                new[] { "Mark", "StatusEffect" }, "StatusEffect");

            AddRelic("chains_of_ruin", "Chains of Ruin", RelicRarity.Rare,
                "Enemies hit by dash attacks are chained together, sharing damage.",
                "Chained enemies also share healing effects.",
                RelicEffectType.DashModifier, "dash_chain_damage", 1f,
                CorruptionEffectType.EnemyModifier, "enemy_chain_heal", 1f,
                new[] { "Dash", "Chain" }, "StatusEffect");

            AddRelic("void_contagion", "Void Contagion", RelicRarity.Epic,
                "Killing an enemy spreads void damage to nearby enemies.",
                "Void contagion can spread back to you from infected enemies.",
                RelicEffectType.ExecutionModifier, "void_contagion_kill", 3f,
                CorruptionEffectType.EnemyModifier, "void_contagion_to_player", 1f,
                new[] { "Execution", "Contagion" }, "StatusEffect");

            // === ECONOMY RELICS (4) ===

            AddRelic("greed_crown", "Greed Crown", RelicRarity.Common,
                "Reward quality increases by 20% for each relic held.",
                "Corruption increases faster with each relic held.",
                RelicEffectType.EconomyModifier, "relic_reward_quality", 0.2f,
                CorruptionEffectType.CustomBehavior, "corruption_relic_accel", 0.1f,
                new[] { "Economy", "Reward" }, "Economy");

            AddRelic("sacrificial_dagger", "Sacrificial Dagger", RelicRarity.Uncommon,
                "Sacrifice 10% current health to double the next reward's quality.",
                "Health sacrifice also increases nearby enemy damage temporarily.",
                RelicEffectType.EconomyModifier, "sacrifice_health_reward", 2f,
                CorruptionEffectType.EnemyModifier, "sacrifice_enemy_damage", 1.3f,
                new[] { "Economy", "Sacrifice" }, "Economy");

            AddRelic("thieves_eye", "Thieves Eye", RelicRarity.Rare,
                "See additional route preview information at branch points.",
                "Route previews may show deceptive or false information.",
                RelicEffectType.EconomyModifier, "route_preview_boost", 1f,
                CorruptionEffectType.CustomBehavior, "deceptive_route_info", 0.3f,
                new[] { "Economy", "Information" }, "Economy");

            AddRelic("bankers_sigil", "Banker's Sigil", RelicRarity.Uncommon,
                "10% of unbanked resources are automatically banked during the run.",
                "Elite enemies drop less reward currency.",
                RelicEffectType.EconomyModifier, "auto_bank_percent", 0.1f,
                CorruptionEffectType.EliteModifier, "elite_less_currency", 0.7f,
                new[] { "Economy", "Extraction" }, "Economy");

            // === CORRUPTION MANIPULATION RELICS (4) ===

            AddRelic("purifier_flame", "Purifier Flame", RelicRarity.Rare,
                "Healing reduces corruption by 2 points each time.",
                "Corruption reduction is halved when corruption is above 50%.",
                RelicEffectType.CorruptionModifier, "heal_reduce_corruption", 2f,
                CorruptionEffectType.CustomBehavior, "corruption_reduction_halved", 0.5f,
                new[] { "Corruption", "Healing" }, "Corruption");

            AddRelic("accursed_bounty", "Accursed Bounty", RelicRarity.Uncommon,
                "At corruption above 75%, rewards are doubled.",
                "At corruption above 75%, enemies are 30% stronger.",
                RelicEffectType.CorruptionModifier, "high_corruption_double_reward", 2f,
                CorruptionEffectType.EnemyModifier, "high_corruption_stronger", 1.3f,
                new[] { "Corruption", "Economy" }, "Corruption");

            AddRelic("stabilizer_anchor", "Stabilizer Anchor", RelicRarity.Epic,
                "Corruption cannot exceed 80% unless you choose to push it.",
                "The realm collapse timer is shortened by 30 seconds.",
                RelicEffectType.CorruptionModifier, "corruption_cap_80", 80f,
                CorruptionEffectType.CustomBehavior, "collapse_timer_shorter", 30f,
                new[] { "Corruption", "Stability" }, "Corruption");

            AddRelic("collapse_harvester", "Collapse Harvester", RelicRarity.Legendary,
                "During realm collapse, all rewards are tripled.",
                "Realm collapse is twice as dangerous.",
                RelicEffectType.CorruptionModifier, "collapse_reward_triple", 3f,
                CorruptionEffectType.CustomBehavior, "collapse_danger_double", 2f,
                new[] { "Corruption", "Collapse" }, "Corruption");

            // === HIGH-RISK CURSED RELICS (5) ===

            AddRelic("cursed_mirror", "Cursed Mirror", RelicRarity.Cursed,
                "All damage is doubled, but you take the same damage on every hit.",
                "No additional corruption effect - the benefit itself is the curse.",
                RelicEffectType.DamageModifier, "double_damage_double_taken", 2f,
                CorruptionEffectType.CustomBehavior, "none", 0f,
                new[] { "Cursed", "Damage" }, "Cursed",
                isCursed: true);

            AddRelic("blood Pact", "Blood Pact", RelicRarity.Cursed,
                "Gain 50% damage but lose 40% max health.",
                "Healing effectiveness is reduced by 30%.",
                RelicEffectType.DamageModifier, "blood_pact_damage", 0.5f,
                CorruptionEffectType.HealingModifier, "blood_pact_healing", 0.7f,
                new[] { "Cursed", "Health", "Damage" }, "Cursed",
                isCursed: true);

            AddRelic("chaos_engine", "Chaos Engine", RelicRarity.Cursed,
                "Random powerful effects activate every 15 seconds.",
                "Random dangerous effects also occur every 15 seconds.",
                RelicEffectType.NewAbility, "chaos_random_benefit", 15f,
                CorruptionEffectType.CustomBehavior, "chaos_random_penalty", 15f,
                new[] { "Cursed", "Random" }, "Cursed",
                isCursed: true);

            AddRelic("fractured_soul", "Fractured Soul", RelicRarity.Cursed,
                "Gain an extra relic slot but start each room at 50% health.",
                "Corruption effects are 50% stronger.",
                RelicEffectType.CorruptionModifier, "extra_slot_half_health", 1f,
                CorruptionEffectType.CustomBehavior, "corruption_effects_stronger", 1.5f,
                new[] { "Cursed", "Slots", "Corruption" }, "Cursed",
                isCursed: true);

            AddRelic("oath_of_void", "Oath of Void", RelicRarity.Cursed,
                "You cannot heal from any source, but damage is tripled.",
                "No additional corruption - the inability to heal IS the corruption effect.",
                RelicEffectType.DamageModifier, "no_heal_triple_damage", 3f,
                CorruptionEffectType.HealingModifier, "zero_healing", 0f,
                new[] { "Cursed", "Healing", "Damage" }, "Cursed",
                isCursed: true);

            // === EXTRACTION RELICS (2) ===

            AddRelic("escape_artist", "Escape Artist", RelicRarity.Uncommon,
                "Extraction points appear in every room after the first elite encounter.",
                "Extraction rooms contain an additional enemy ambush.",
                RelicEffectType.ExtractionModifier, "extraction_every_room", 1f,
                CorruptionEffectType.CustomBehavior, "extraction_ambush", 1f,
                new[] { "Extraction", "Mobility" }, "Extraction");

            AddRelic("deep_diver", "Deep Diver", RelicRarity.Rare,
                "Continuing past extraction multiplies rewards by an additional 50% each time.",
                "Each skipped extraction adds 15 corruption.",
                RelicEffectType.ExtractionModifier, "skip_extraction_multiplier", 0.5f,
                CorruptionEffectType.CustomBehavior, "skip_extraction_corruption", 15f,
                new[] { "Extraction", "Corruption" }, "Extraction");
        }

        private static void AddRelic(
            string id, string name, RelicRarity rarity,
            string benefitDesc, string corruptionDesc,
            RelicEffectType benefitType, string benefitTag, float benefitValue,
            CorruptionEffectType corruptionType, string corruptionTag, float corruptionValue,
            string[] synergyTags, string upgradeCategory,
            bool isCursed = false)
        {
            var relic = RelicDefinition.CreateInstance<RelicDefinition>();
            relic.RelicId = id;
            relic.RelicName = name;
            relic.Rarity = rarity;
            relic.Description = benefitDesc;
            relic.BenefitDescription = benefitDesc;
            relic.CorruptionEffectDescription = corruptionDesc;
            relic.BenefitType = benefitType;
            relic.BenefitTag = benefitTag;
            relic.BenefitValue = benefitValue;
            relic.CorruptionType = corruptionType;
            relic.CorruptionTag = corruptionTag;
            relic.CorruptionEffectValue = corruptionValue;
            relic.CorruptionIncrease = rarity == RelicRarity.Cursed ? 5f : 
                                       rarity == RelicRarity.Common ? 8f :
                                       rarity == RelicRarity.Uncommon ? 10f :
                                       rarity == RelicRarity.Rare ? 12f :
                                       rarity == RelicRarity.Epic ? 15f : 20f;
            relic.SynergyTags = synergyTags;
            relic.UpgradeCategory = upgradeCategory;
            relic.IsCursed = isCursed;

            relic.Weight = rarity == RelicRarity.Common ? 3f :
                          rarity == RelicRarity.Uncommon ? 2f :
                          rarity == RelicRarity.Rare ? 1.5f :
                          rarity == RelicRarity.Epic ? 0.8f :
                          rarity == RelicRarity.Legendary ? 0.3f : 0.5f;

            // Set glow colors
            relic.RelicGlowColor = new Color(0f, 0.9f, 1f, 1f); // Cyan
            relic.CorruptionGlowColor = new Color(0.85f, 0.2f, 0.3f, 1f); // Crimson

            if (isCursed)
            {
                relic.CorruptionGlowColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple for cursed
            }

            _allRelics.Add(relic);
        }

        /// <summary>
        /// Select a random relic from the pool, weighted by rarity and conditions.
        /// </summary>
        public static RelicDefinition SelectRandomRelic(float corruptionLevel, int runsCompleted, List<string> discoveredRelics, List<string> excludeIds = null)
        {
            var eligible = new List<RelicDefinition>();
            float totalWeight = 0f;

            foreach (var relic in GetAllRelics())
            {
                if (excludeIds != null && excludeIds.Contains(relic.RelicId)) continue;
                if (relic.RequiresDiscovery && !discoveredRelics.Contains(relic.RelicId)) continue;
                if (relic.MinCorruptionLevel > corruptionLevel) continue;
                if (relic.MaxRunsCompleted > 0 && runsCompleted < relic.MaxRunsCompleted) continue;

                eligible.Add(relic);
                totalWeight += relic.Weight;
            }

            if (eligible.Count == 0) return null;

            // Weighted random selection
            float randomValue = Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var relic in eligible)
            {
                cumulative += relic.Weight;
                if (randomValue <= cumulative)
                    return relic;
            }

            return eligible[eligible.Count - 1];
        }
    }
}
