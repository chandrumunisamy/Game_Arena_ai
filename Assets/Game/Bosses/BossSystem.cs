using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Combat;
using Relicfall.Corruption;
using Relicfall.Enemies;

namespace Relicfall.Bosses
{
    /// <summary>
    /// Boss definition ScriptableObject.
    /// Bosses have multiple phases, unique arena mechanics, and corruption-sensitive attacks.
    /// </summary>
    [CreateAssetMenu(fileName = "BossDef", menuName = "RELICFALL/Bosses/Boss Definition")]
    public class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string BossId;
        public string BossName;
        public string Title;
        public string Description;
        public RealmType Realm;
        public Sprite Portrait;
        public GameObject ModelPrefab;

        [Header("Stats")]
        public float BaseHealth = 500f;
        public float BaseDamage = 15f;
        public float BaseSpeed = 3f;
        public float StaggerThreshold = 100f;

        [Header("Phases")]
        public BossPhase[] Phases;

        [Header("Arena")]
        public GameObject ArenaPrefab;
        public ArenaMechanic[] ArenaMechanics;
        public float ArenaSize = 30f;

        [Header("Attacks")]
        public AttackDefinition[] NormalAttacks;
        public AttackDefinition[] Phase2Attacks;
        public AttackDefinition[] Phase3Attacks;
        public AttackDefinition SignatureAttack;
        public float SignatureAttackCooldown = 15f;

        [Header("Corruption Sensitivity")]
        public float CorruptionHealthMultiplier = 1.5f;
        public float CorruptionDamageMultiplier = 1.3f;
        public AttackDefinition[] CorruptionExclusiveAttacks;
        public float CorruptionPhaseThreshold = 50f;

        [Header("Audio")]
        public string BossMusicId;
        public string PhaseTransitionSoundId;
        public string DeathSoundId;
        public string VocalSoundId;

        [Header("Relic")]
        public RelicRarity GuaranteedRelicRarity = RelicRarity.Epic;
        public string[] GuaranteedRelicIds;

        [Header("Intro")]
        public string IntroDialogue;
        public float IntroDuration = 3f;
    }

    [System.Serializable]
    public class BossPhase
    {
        public int PhaseNumber;
        public float HealthThreshold; // Percentage of health to trigger this phase (1.0 = start, 0.5 = 50% health)
        public string PhaseName;
        public float SpeedMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float AttackCooldownMultiplier = 0.8f;
        public AttackDefinition[] NewAttacks;
        public ArenaMechanic[] NewMechanics;
        public bool HasInvulnerabilityTransition = true;
        public float TransitionDuration = 2f;
        public string TransitionDialogue;
    }

    [System.Serializable]
    public class ArenaMechanic
    {
        public string MechanicName;
        public string Description;
        public MechanicType Type;
        public float TriggerDelay;
        public float Duration;
        public float Damage;
        public Vector3[] Positions;
        public bool CorruptionModified = false;
        public float CorruptionIntensityMultiplier = 1.5f;
    }

    public enum MechanicType
    {
        GroundFire,
        FallingDebris,
        ShockwaveRing,
        CorruptionZone,
        ArenaCollapse,
        WaterRise,
        RootTrap,
        SacredZone,
        TimeDistortion
    }

    /// <summary>
    /// Boss controller managing phases, special attacks, and arena mechanics.
    /// Extends enemy controller with phase transitions and unique boss behaviors.
    /// </summary>
    public class BossController : EnemyController
    {
        [Header("Boss Definition")]
        [SerializeField] private BossDefinition _bossDefinition;

        private int _currentPhase = 0;
        private float _phaseTransitionTimer;
        private bool _isInPhaseTransition;
        private float _signatureAttackCooldownTimer;
        private List<ArenaMechanic> _activeMechanics = new();
        private float _corruptionLevel;

        public BossDefinition BossDef => _bossDefinition;
        public int CurrentPhase => _currentPhase;
        public bool IsInPhaseTransition => _isInPhaseTransition;

        protected override void InitializeStats()
        {
            if (_bossDefinition == null) return;

            float health = _bossDefinition.BaseHealth;
            float damage = _bossDefinition.BaseDamage;

            // Apply corruption scaling
            health *= 1f + (_corruptionLevel / 100f) * _bossDefinition.CorruptionHealthMultiplier;
            damage *= 1f + (_corruptionLevel / 100f) * _bossDefinition.CorruptionDamageMultiplier;

            // Apply to components
            if (_healthComponent != null)
                _healthComponent.SetMaxHealth(health);
        }

        private void Update()
        {
            UpdateCurrentState();
            UpdateBossSpecific();
        }

        private void UpdateBossSpecific()
        {
            // Check phase transitions
            if (!_isInPhaseTransition && _bossDefinition?.Phases != null)
            {
                CheckPhaseTransition();
            }

            // Phase transition timer
            if (_isInPhaseTransition)
            {
                _phaseTransitionTimer -= Time.deltaTime;
                if (_phaseTransitionTimer <= 0f)
                {
                    _isInPhaseTransition = false;
                    SetState(EnemyState.Chase);
                }
            }

            // Signature attack cooldown
            _signatureAttackCooldownTimer -= Time.deltaTime;

            // Active arena mechanics
            UpdateArenaMechanics();

            // Corruption exclusive attacks
            if (_corruptionLevel >= _bossDefinition?.CorruptionPhaseThreshold && Random.value < 0.1f)
            {
                PerformCorruptionExclusiveAttack();
            }
        }

        private void CheckPhaseTransition()
        {
            if (_healthComponent == null) return;

            float healthPercent = _healthComponent.HealthPercent;

            for (int i = _currentPhase + 1; i < _bossDefinition.Phases.Length; i++)
            {
                var phase = _bossDefinition.Phases[i];
                if (healthPercent <= phase.HealthThreshold)
                {
                    StartPhaseTransition(i);
                    break;
                }
            }
        }

        private void StartPhaseTransition(int newPhase)
        {
            _currentPhase = newPhase;
            var phase = _bossDefinition.Phases[newPhase];
            _isInPhaseTransition = true;
            _phaseTransitionTimer = phase.TransitionDuration;

            // Invulnerability during transition
            SetIFrames(phase.TransitionDuration);

            // Phase transition feedback
            CombatFeedback.Instance.TriggerHitStop(ImpactType.Boss);
            CombatFeedback.Instance.TriggerCameraShake(0.6f, 8f);
            EventBus.Publish(new SFXPlayEvent { SfxId = _bossDefinition.PhaseTransitionSoundId, Position = transform.position });

            // Activate new arena mechanics
            if (phase.NewMechanics != null)
            {
                foreach (var mechanic in phase.NewMechanics)
                    ActivateArenaMechanic(mechanic);
            }

            // Music transition
            EventBus.Publish(new MusicLayerEvent { LayerName = "boss_phase2", ShouldPlay = true, Intensity = _currentPhase * 0.3f });

            Debug.Log($"Boss phase transition: {_currentPhase} ({phase.PhaseName})");
        }

        /// <summary>
        /// Perform the boss's signature attack.
        /// </summary>
        public void PerformSignatureAttack()
        {
            if (_signatureAttackCooldownTimer > 0f) return;
            if (_isInPhaseTransition) return;

            _signatureAttackCooldownTimer = _bossDefinition.SignatureAttackCooldown;

            SetState(EnemyState.SpecialAttack);

            // Telegraph signature attack (longer telegraph for readability)
            float telegraphDuration = 1.5f - (_currentPhase * 0.2f); // Phases speed up
            StartSignatureTelegraph(telegraphDuration);

            CombatFeedback.Instance.TriggerCameraShake(0.3f, 4f);
        }

        private void StartSignatureTelegraph(float duration)
        {
            // Visual telegraph for signature attack
            // Multiple expanding rings or a dramatic visual indicator
            EventBus.Publish(new SFXPlayEvent { SfxId = _bossDefinition.BossId + "_signature_telegraph", Position = transform.position, Volume = 1.5f });
        }

        private void PerformCorruptionExclusiveAttack()
        {
            if (_bossDefinition.CorruptionExclusiveAttacks == null || _bossDefinition.CorruptionExclusiveAttacks.Length == 0) return;

            var attack = _bossDefinition.CorruptionExclusiveAttacks[Random.Range(0, _bossDefinition.CorruptionExclusiveAttacks.Length)];
            SetState(EnemyState.SpecialAttack);
        }

        #region Arena Mechanics

        private void ActivateArenaMechanic(ArenaMechanic mechanic)
        {
            _activeMechanics.Add(mechanic);

            // Spawn mechanic VFX/hazard
            switch (mechanic.Type)
            {
                case MechanicType.GroundFire:
                    SpawnGroundFire(mechanic);
                    break;
                case MechanicType.FallingDebris:
                    SpawnFallingDebris(mechanic);
                    break;
                case MechanicType.ShockwaveRing:
                    SpawnShockwaveRing(mechanic);
                    break;
                case MechanicType.CorruptionZone:
                    SpawnCorruptionZone(mechanic);
                    break;
                case MechanicType.ArenaCollapse:
                    StartArenaCollapse(mechanic);
                    break;
                case MechanicType.WaterRise:
                    StartWaterRise(mechanic);
                    break;
                case MechanicType.RootTrap:
                    SpawnRootTrap(mechanic);
                    break;
                case MechanicType.SacredZone:
                    SpawnSacredZone(mechanic);
                    break;
                case MechanicType.TimeDistortion:
                    ActivateTimeDistortion(mechanic);
                    break;
            }
        }

        private void UpdateArenaMechanics()
        {
            // Update active mechanics
            for (int i = _activeMechanics.Count - 1; i >= 0; i--)
            {
                var mechanic = _activeMechanics[i];
                // Mechanic lifecycle management
            }
        }

        private void SpawnGroundFire(ArenaMechanic mechanic)
        {
            // Create fire hazard zones on arena floor
        }

        private void SpawnFallingDebris(ArenaMechanic mechanic)
        {
            // Create falling debris with ground telegraphs
        }

        private void SpawnShockwaveRing(ArenaMechanic mechanic)
        {
            // Create expanding shockwave ring from boss position
        }

        private void SpawnCorruptionZone(ArenaMechanic mechanic)
        {
            // Create corruption zones that damage and corrupt the player
        }

        private void StartArenaCollapse(ArenaMechanic mechanic)
        {
            // Start collapsing arena sections
            CombatFeedback.Instance.TriggerCameraShake(0.4f, 6f);
        }

        private void StartWaterRise(ArenaMechanic mechanic)
        {
            // For Drowned Dominion boss - rising water level
        }

        private void SpawnRootTrap(ArenaMechanic mechanic)
        {
            // For Verdant Maw boss - root traps that immobilize
        }

        private void SpawnSacredZone(ArenaMechanic mechanic)
        {
            // For Hollow Saint - sacred zones that become corrupted
        }

        private void ActivateTimeDistortion(ArenaMechanic mechanic)
        {
            // For Thirteenth Regent - time distortion zones
        }

        #endregion

        #region Boss Intros and Deaths

        /// <summary>
        /// Play boss intro sequence.
        /// </summary>
        public void PlayIntro()
        {
            // Freeze player briefly
            // Show boss name and title
            // Play intro dialogue
            // Start boss music

            EventBus.Publish(new MusicLayerEvent { LayerName = "boss", ShouldPlay = true, Intensity = 0.5f });
        }

        /// <summary>
        /// Handle boss death with unique rewards.
        /// </summary>
        public override void ReceiveDamage(float damage, Vector3 hitDirection, string source, int attackerId, bool isCritical = false)
        {
            if (_isInPhaseTransition) return; // Boss is invulnerable during phase transition

            base.ReceiveDamage(damage, hitDirection, source, attackerId, isCritical);
        }

        protected override void TriggerDeath()
        {
            // Boss death is more dramatic
            CombatFeedback.Instance.TriggerHitStop(ImpactType.Boss);
            CombatFeedback.Instance.TriggerCameraShake(1f, 10f);

            // Stop boss music, play victory
            EventBus.Publish(new MusicLayerEvent { LayerName = "boss", ShouldPlay = false });
            EventBus.Publish(new MusicLayerEvent { LayerName = "victory", ShouldPlay = true });

            // Publish boss defeat event
            EventBus.Publish(new BossDefeatedEvent
            {
                BossId = _bossDefinition.BossId,
                RunNumber = Core.GameManager.Instance?.CurrentRun?.RunDurationSeconds > 0 ? 1 : 0
            });

            // Clear all arena mechanics
            _activeMechanics.Clear();

            // Destroy with longer delay for dramatic death animation
            Destroy(gameObject, 3f);
        }

        #endregion

        /// <summary>
        /// Set corruption level affecting boss behavior.
        /// </summary>
        public void SetCorruption(float level)
        {
            _corruptionLevel = level;

            // Corruption modifies boss attacks and adds mechanics
            if (level >= _bossDefinition?.CorruptionPhaseThreshold)
            {
                // Add corruption-exclusive mechanics
            }
        }
    }

    // === Specific Boss Implementations ===

    /// <summary>
    /// The Oath-Breaker King - Realm 1 Boss
    /// Corrupted ruler with polearm combat, royal guard summons,
    /// arena breaking, and relic-counter attacks.
    /// </summary>
    public class OathBreakerKing : BossController
    {
        private bool _hasSummonedGuards;
        private bool _hasBrokenArena;
        private int _relicCounterAttacks;

        protected override void UpdateBossSpecific()
        {
            base.UpdateBossSpecific();

            // Summon royal guards periodically
            if (_currentPhase >= 1 && !_hasSummonedGuards)
            {
                SummonRoyalGuards();
                _hasSummonedGuards = true;
            }

            // Break arena sections at phase 2
            if (_currentPhase >= 2 && !_hasBrokenArena)
            {
                BreakArenaSection();
                _hasBrokenArena = true;
            }
        }

        private void SummonRoyalGuards()
        {
            // Spawn 2-3 sword/shield guards as support
            for (int i = 0; i < 3; i++)
            {
                // Spawn guard at flank positions
            }
        }

        private void BreakArenaSection()
        {
            // Collapse part of the arena, reducing available space
            ActivateArenaMechanic(new ArenaMechanic
            {
                MechanicName = "arena_collapse_phase2",
                Type = MechanicType.ArenaCollapse,
                Duration = 5f,
                Damage = 20f
            });
        }

        /// <summary>
        /// Use the player's stolen relics against them.
        /// Reads active relic tags and performs counter-attacks.
        /// </summary>
        public void RelicCounterAttack()
        {
            var relicManager = FindObjectOfType<RelicManager>();
            if (relicManager == null) return;

            var tags = relicManager.GetActiveTags();

            // Counter specific relic types
            if (tags.Contains("Dash"))
            {
                // Anti-dash: create dash-denial zone
            }
            if (tags.Contains("Parry"))
            {
                // Anti-parry: unparryable attack sequence
            }
            if (tags.Contains("Clone"))
            {
                // Clone-counter: attacks that target clones first
            }
        }
    }

    /// <summary>
    /// The Thirteenth Regent - Realm 2 Boss
    /// Time distortion boss with delayed attacks, attack echoes,
    /// accelerated hazards, and corruption-sensitive phase transitions.
    /// </summary>
    public class ThirteenthRegent : BossController
    {
        private List<GameObject> _attackEchoes = new();
        private float _timeDistortionRadius = 8f;

        protected override void UpdateBossSpecific()
        {
            base.UpdateBossSpecific();

            // Manage attack echoes
            UpdateAttackEchoes();

            // Time distortion zones
            if (_currentPhase >= 1)
            {
                MaintainTimeDistortion();
            }
        }

        /// <summary>
        /// Create a delayed attack echo that repeats the previous attack after a delay.
        /// </summary>
        public void CreateAttackEcho(Vector3 position, Vector3 direction, float damage, float delay = 2f)
        {
            // Spawn echo that repeats attack after delay
            var echoObj = new GameObject("AttackEcho");
            echoObj.transform.position = position;
            var echo = echoObj.AddComponent<AttackEcho>();
            echo.Initialize(position, direction, damage, delay);
            _attackEchoes.Add(echoObj);
        }

        private void UpdateAttackEchoes()
        {
            for (int i = _attackEchoes.Count - 1; i >= 0; i--)
            {
                if (_attackEchoes[i] == null)
                    _attackEchoes.RemoveAt(i);
            }
        }

        private void MaintainTimeDistortion()
        {
            // Active time distortion zone around the boss
            // Slows player movement and distorts telegraph timing
        }
    }

    /// <summary>
    /// Attack echo component for the Thirteenth Regent.
    /// Delays and repeats attacks after a time offset.
    /// </summary>
    public class AttackEcho : MonoBehaviour
    {
        private Vector3 _originPosition;
        private Vector3 _direction;
        private float _damage;
        private float _delay;
        private float _timer;
        private bool _hasExecuted;

        public void Initialize(Vector3 position, Vector3 direction, float damage, float delay)
        {
            _originPosition = position;
            _direction = direction;
            _damage = damage;
            _delay = delay;
            _timer = 0f;
            _hasExecuted = false;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            // Visual indicator (ghost attack preview)
            // Gradually make echo more visible as timer approaches delay

            if (!_hasExecuted && _timer >= _delay)
            {
                _hasExecuted = true;
                ExecuteEchoAttack();
                Destroy(gameObject, 0.5f);
            }
        }

        private void ExecuteEchoAttack()
        {
            // Perform the delayed attack at the echo position
            var hits = Physics.OverlapSphere(_originPosition + _direction * 2f, 1f, LayerMask.GetMask("PlayerHurtbox"));
            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<HealthComponent>();
                if (health != null)
                {
                    health.TakeDamage(_damage, _direction, "regent_echo", gameObject.GetInstanceID());
                }
            }
        }
    }

    /// <summary>
    /// The Hollow Saint - Realm 3 Boss
    /// Living statue that converts healing into hazards, creates sacred zones
    /// that become corrupted, and forces movement around the arena.
    /// </summary>
    public class HollowSaint : BossController
    {
        private List<GameObject> _sacredZones = new();
        private bool _hasAntiHealing;

        protected override void UpdateBossSpecific()
        {
            base.UpdateBossSpecific();

            // Manage sacred zones
            UpdateSacredZones();

            // Anti-healing mechanic
            if (_currentPhase >= 1 && !_hasAntiHealing)
            {
                _hasAntiHealing = true;
                // Register event to convert player healing into hazards
                EventBus.Subscribe<DamageEvent>(OnDamageDuringSacredZone);
            }
        }

        private void OnDamageDuringSacredZone(DamageEvent e)
        {
            // Check if damage is in a sacred zone
            // Convert healing attempts into damage or hazard spawns
        }

        /// <summary>
        /// Create a sacred zone that provides benefits but becomes corrupted.
        /// </summary>
        public void CreateSacredZone(Vector3 position, float radius, float duration)
        {
            // Sacred zone: initially provides healing/regeneration
            // Over time, becomes corrupted and damages the player
            var zoneObj = new GameObject("SacredZone");
            zoneObj.transform.position = position;
            var zone = zoneObj.AddComponent<SacredZone>();
            zone.Initialize(radius, duration);
            _sacredZones.Add(zoneObj);
        }

        private void UpdateSacredZones()
        {
            for (int i = _sacredZones.Count - 1; i >= 0; i--)
            {
                if (_sacredZones[i] == null)
                    _sacredZones.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Sacred zone component that transitions from beneficial to corrupted.
    /// </summary>
    public class SacredZone : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private float _timer;
        private float _corruptionStartPercent = 0.4f; // Zone starts corrupting at 40% duration

        public void Initialize(float radius, float duration)
        {
            _radius = radius;
            _duration = duration;
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            float progress = _timer / _duration;
            bool isCorrupted = progress >= _corruptionStartPercent;

            // Visual transition: golden -> crimson
            // Functional transition: healing zone -> damage zone

            if (isCorrupted)
            {
                // Damage player if inside zone
                var hits = Physics.OverlapSphere(transform.position, _radius, LayerMask.GetMask("PlayerHurtbox"));
                foreach (var hit in hits)
                {
                    var health = hit.GetComponentInParent<HealthComponent>();
                    if (health != null)
                    {
                        health.TakeDamage(5f * Time.deltaTime, Vector3.zero, "corrupted_sacred_zone", 0);
                    }
                }
            }
            else
            {
                // Heal player if inside zone
                var hits = Physics.OverlapSphere(transform.position, _radius, LayerMask.GetMask("PlayerHurtbox"));
                foreach (var hit in hits)
                {
                    var health = hit.GetComponentInParent<HealthComponent>();
                    if (health != null)
                    {
                        health.Heal(2f * Time.deltaTime);
                    }
                }
            }

            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Final boss - reacts to player's permanent progression,
    /// uses multiple relic categories, and has several patterns
    /// based on player choices.
    /// </summary>
    public class FinalBoss : BossController
    {
        private bool _hasAdaptedToPlayer;

        protected override void InitializeStats()
        {
            base.InitializeStats();

            // Adapt to player progression
            var progression = Core.GameManager.Instance?.Progression;
            if (progression != null)
            {
                // Boss health scales with total runs completed
                float runScaling = 1f + progression.RunsCompleted * 0.05f;
                _healthComponent.SetMaxHealth(_healthComponent.MaxHealth * runScaling);
            }
        }

        protected override void UpdateBossSpecific()
        {
            base.UpdateBossSpecific();

            if (!_hasAdaptedToPlayer)
            {
                AdaptToPlayerBuild();
                _hasAdaptedToPlayer = true;
            }
        }

        /// <summary>
        /// Adapt boss patterns to the player's current build and relic loadout.
        /// </summary>
        private void AdaptToPlayerBuild()
        {
            var relicManager = FindObjectOfType<RelicManager>();
            if (relicManager == null) return;

            var tags = relicManager.GetActiveTags();

            // Boss uses different attack patterns based on player's relic focus
            if (tags.Contains("Dash"))
            {
                // Anti-dash patterns: ground traps, timing attacks
            }
            if (tags.Contains("Parry"))
            {
                // Anti-parry patterns: multi-hit combos, feints
            }
            if (tags.Contains("Summon"))
            {
                // Anti-summon patterns: AoE attacks, minion clearing
            }

            // Boss can use relics from the player's discovered pool
        }
    }
}
