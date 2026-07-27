using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Core.Utils;
using Relicfall.Combat;
using Relicfall.Corruption;
using Relicfall.Player;

namespace Relicfall.Enemies
{
    /// <summary>
    /// Enemy definition ScriptableObject. Each enemy type has its own
    /// attacks, telegraphs, behaviour patterns, and corruption variants.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDef", menuName = "RELICFALL/Enemies/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string EnemyId;
        public string EnemyName;
        public string Description;
        public EnemyType Type;

        [Header("Visuals")]
        public GameObject ModelPrefab;
        public Sprite Icon;
        public Color CorruptionColor = new Color(0.85f, 0.2f, 0.3f);
        public float Scale = 1f;

        [Header("Stats")]
        public float BaseHealth = 50f;
        public float BaseDamage = 8f;
        public float BaseSpeed = 2f;
        public float StaggerThreshold = 30f;
        public float StaggerDuration = 2f;
        public float StaggerDamageMultiplier = 1f;
        public float DetectionRange = 8f;
        public float AttackRange = 1.5f;
        public float AttackCooldownMin = 0.8f;
        public float AttackCooldownMax = 2f;
        public float TelegraphDuration = 0.5f;
        public float AttackDuration = 0.4f;
        public float RecoveryDuration = 0.3f;
        public float TurnSpeed = 5f;

        [Header("Attacks")]
        public AttackDefinition[] Attacks;
        public AttackDefinition HeavyAttack;
        public AttackDefinition SpecialAttack;
        public float HeavyAttackChance = 0.2f;
        public float SpecialAttackChance = 0.1f;

        [Header("Behaviour")]
        public BehaviourPattern DefaultPattern = BehaviourPattern.Aggressive;
        public float PatrolRadius = 3f;
        public float MinAttackDistance = 1f;
        public float MaxAttackDistance = 2f;
        public bool CanBlock = false;
        public float BlockChance = 0f;
        public bool CanRetreat = false;
        public float RetreatDistance = 3f;

        [Header("Elite")]
        public float EliteHealthMultiplier = 2f;
        public float EliteDamageMultiplier = 1.5f;
        public float EliteSpeedMultiplier = 1.2f;
        public EliteModifier[] CompatibleEliteModifiers;

        [Header("Corruption")]
        public CorruptionVariantDefinition[] CorruptionVariants;
        public float CorruptionHealthMultiplier = 1.3f;
        public float CorruptionDamageMultiplier = 1.2f;
        public float CorruptionSpeedBonus = 0.5f;

        [Header("Audio")]
        public string SpawnSoundId;
        public string AttackSoundId;
        public string HeavyAttackSoundId;
        public string DeathSoundId;
        public string VocalSoundId;
    }

    public enum EnemyType
    {
        SwordGuard,
        ShieldGuard,
        SpearGuard,
        Archer,
        CorruptedMage,
        HeavyKnight,
        Assassin,
        Summoner,
        LivingStatue,
        CorruptionBeast
    }

    public enum BehaviourPattern
    {
        Aggressive,
        Defensive,
        Cautious,
        Flanking,
        Support,
        Stationary
    }

    /// <summary>
    /// Corruption variant definition for enemy mutations.
    /// </summary>
    [System.Serializable]
    public class CorruptionVariantDefinition
    {
        public float MinCorruptionLevel;
        public string VariantName;
        public float HealthModifier;
        public float DamageModifier;
        public float SpeedModifier;
        public string[] AddedAttackIds;
        public bool HasVisualMutation;
        public Color MutationColor;
    }

    /// <summary>
    /// Elite modifier definitions that change enemy behaviour.
    /// Not just stat increases; modifiers alter actual combat behavior.
    /// </summary>
    public enum EliteModifier
    {
        Mirrored,    // Creates mirror attacks
        Frenzied,    // Faster attacks, no retreat
        Armoured,    // Blocks more, stagger resistance
        Vampiric,    // Heals on hit
        Explosive,   // Explodes on death
        Teleporting, // Teleports during combat
        TimeShifted, // Delayed attack echoes
        CorruptionLinked, // Gets stronger with corruption
        Summoning,   // Summons minor enemies
        Shielded     // Has rechargeable shield
    }

    /// <summary>
    /// Enemy state machine states for AI behaviour.
    /// </summary>
    public enum EnemyState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Telegraph,
        Attacking,
        Recovery,
        Retreat,
        Block,
        Staggered,
        HitReaction,
        Death,
        Spawn,
        EliteEntrance,
        SpecialAttack
    }

    /// <summary>
    /// Central enemy controller that manages AI state machine,
    /// combat actions, and coordination with other enemies.
    /// Implements readable telegraphs, attack recovery, and stagger behaviour.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private EnemyDefinition _definition;

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] protected HealthComponent _healthComponent;
        [SerializeField] private StaggerComponent _staggerComponent;
        [SerializeField] private HitboxManager _hitboxManager;
        [SerializeField] private EnemyMarker _marker;
        [SerializeField] private SkinnedMeshRenderer _mainRenderer;

        [Header("Runtime")]
        [SerializeField] private Transform _target; // Player
        [SerializeField] private float _corruptionLevel; // Current corruption affecting this enemy

        // State machine
        private EnemyState _currentState = EnemyState.Idle;
        private float _stateTimer;
        private float _stateStartTime;
        private bool _isElite;
        private EliteModifier _eliteModifier;
        private bool _isCorruptedVariant;

        // Combat AI
        private float _attackCooldownTimer;
        private float _currentAttackCooldown;
        private AttackDefinition _queuedAttack;
        private Vector3 _telegraphDirection;
        private float _telegraphDuration;
        private bool _isTelegraphing;
        private Vector3 _spawnPosition;
        private float _patrolTimer;
        private Vector3 _patrolTarget;
        private bool _hasSeenPlayer;
        private float _alertTimer;
        private float _chaseTimer;

        // Elite modifier effects
        private bool _hasMirroredClone;
        private GameObject _mirrorClone;
        private float _vampiricHealAmount;
        private bool _hasShield;
        private float _shieldHealth;
        private float _shieldRechargeTimer;
        private float _summonCooldown;
        private float _teleportCooldown;

        // Corruption scaling
        private float _scaledHealth;
        private float _scaledDamage;
        private float _scaledSpeed;
        private float _scaledStaggerThreshold;

        // Group coordination
        private EnemyGroupCoordinator _groupCoordinator;
        private int _groupRole; // 0 = primary, 1 = support, 2 = flanker

        // Hit reactions
        private Vector3 _hitDirection;
        private float _knockbackForce;

        public EnemyState CurrentState => _currentState;
        public EnemyDefinition Definition => _definition;
        public bool IsElite => _isElite;
        public bool IsAlive => _healthComponent != null && _healthComponent.IsAlive;
        public float HealthPercent => _healthComponent?.HealthPercent ?? 0f;

        private void Awake()
        {
            _marker = GetComponent<EnemyMarker>();
            if (_marker == null)
                _marker = gameObject.AddComponent<EnemyMarker>();
            _marker.EnemyType = _definition?.EnemyId ?? "unknown";
        }

        private void Start()
        {
            FindPlayer();
            InitializeStats();
            InitializeState(EnemyState.Spawn);
        }

        /// <summary>Updates shared enemy AI. Bosses extend this through UpdateBossSpecific.</summary>
        protected virtual void Update()
        {
            UpdateCurrentState();
            UpdateBossSpecific();
        }

        protected virtual void UpdateBossSpecific() { }

        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _target = player.transform;
        }

        protected virtual void InitializeStats()
        {
            if (_definition == null) return;

            // Base stats
            _scaledHealth = _definition.BaseHealth;
            _scaledDamage = _definition.BaseDamage;
            _scaledSpeed = _definition.BaseSpeed;
            _scaledStaggerThreshold = _definition.StaggerThreshold;

            // Apply corruption scaling
            float corruptionScale = 1f + (_corruptionLevel / 100f) * 0.5f;
            _scaledHealth *= corruptionScale;
            _scaledDamage *= corruptionScale;
            _scaledSpeed += _definition.CorruptionSpeedBonus * (_corruptionLevel / 100f);
            _scaledStaggerThreshold *= corruptionScale;

            // Apply elite scaling
            if (_isElite)
            {
                _scaledHealth *= _definition.EliteHealthMultiplier;
                _scaledDamage *= _definition.EliteDamageMultiplier;
                _scaledSpeed *= _definition.EliteSpeedMultiplier;
            }

            // Apply to components
            if (_healthComponent != null)
                _healthComponent.SetMaxHealth(_scaledHealth);

            if (_staggerComponent != null)
            {
                // Set stagger threshold based on scaling
            }

            _spawnPosition = transform.position;
        }

        /// <summary>
        /// Configure this enemy as an elite with a specific modifier.
        /// </summary>
        public void ConfigureElite(EliteModifier modifier)
        {
            _isElite = true;
            _eliteModifier = modifier;

            switch (modifier)
            {
                case EliteModifier.Mirrored:
                    _hasMirroredClone = true;
                    break;
                case EliteModifier.Frenzied:
                    _scaledSpeed *= 1.3f;
                    _currentAttackCooldown *= 0.5f;
                    break;
                case EliteModifier.Armoured:
                    _scaledStaggerThreshold *= 2f;
                    _definition.CanBlock = true;
                    _definition.BlockChance = 0.5f;
                    break;
                case EliteModifier.Vampiric:
                    _vampiricHealAmount = _scaledDamage * 0.3f;
                    break;
                case EliteModifier.Shielded:
                    _hasShield = true;
                    _shieldHealth = _scaledHealth * 0.3f;
                    break;
                case EliteModifier.Summoning:
                    _summonCooldown = 8f;
                    break;
                case EliteModifier.Teleporting:
                    _teleportCooldown = 5f;
                    break;
            }

            InitializeStats();
            SetState(EnemyState.EliteEntrance);
        }

        /// <summary>
        /// Set corruption level affecting this enemy.
        /// </summary>
        public virtual void SetCorruption(float level)
        {
            _corruptionLevel = level;

            // Check for corruption variants
            if (_definition?.CorruptionVariants != null)
            {
                foreach (var variant in _definition.CorruptionVariants)
                {
                    if (level >= variant.MinCorruptionLevel && variant.HasVisualMutation)
                    {
                        ApplyCorruptionVariant(variant);
                    }
                }
            }
        }

        private void ApplyCorruptionVariant(CorruptionVariantDefinition variant)
        {
            _isCorruptedVariant = true;
            _scaledHealth *= variant.HealthModifier;
            _scaledDamage *= variant.DamageModifier;
            _scaledSpeed *= variant.SpeedModifier;

            // Visual mutation
            if (_mainRenderer != null && variant.HasVisualMutation)
            {
                // Apply corruption color to material
            }
        }

        #region State Machine

        private void InitializeState(EnemyState state)
        {
            SetState(state);
        }

        protected void SetState(EnemyState newState)
        {
            if (_currentState == newState) return;
            if (_currentState == EnemyState.Death) return;

            ExitState(_currentState);
            _currentState = newState;
            _stateStartTime = Time.time;
            _stateTimer = 0f;
            EnterState(newState);
        }

        private void EnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    SetAnimation("Idle");
                    break;
                case EnemyState.Patrol:
                    SetAnimation("Walk");
                    ChoosePatrolTarget();
                    break;
                case EnemyState.Alert:
                    SetAnimation("Alert");
                    _alertTimer = 0.5f;
                    break;
                case EnemyState.Chase:
                    SetAnimation("Run");
                    break;
                case EnemyState.Telegraph:
                    SetAnimation("Telegraph");
                    StartTelegraph();
                    break;
                case EnemyState.Attacking:
                    SetAnimation("Attack");
                    StartAttack();
                    break;
                case EnemyState.Recovery:
                    SetAnimation("Recovery");
                    _stateTimer = _definition?.RecoveryDuration ?? 0.3f;
                    break;
                case EnemyState.Retreat:
                    SetAnimation("Retreat");
                    break;
                case EnemyState.Block:
                    SetAnimation("Block");
                    break;
                case EnemyState.Staggered:
                    SetAnimation("Stagger");
                    _stateTimer = _definition?.StaggerDuration ?? 2f;
                    CombatFeedback.Instance.TriggerHitStop(ImpactType.Heavy);
                    EventBus.Publish(new EnemyStaggerEvent
                    {
                        EnemyInstanceId = gameObject.GetInstanceID(),
                        StaggerDuration = _definition?.StaggerDuration ?? 2f
                    });
                    break;
                case EnemyState.HitReaction:
                    SetAnimation("HitReaction");
                    ApplyKnockback();
                    break;
                case EnemyState.Death:
                    SetAnimation("Death");
                    TriggerDeath();
                    break;
                case EnemyState.Spawn:
                    SetAnimation("Spawn");
                    _stateTimer = 1f;
                    break;
                case EnemyState.EliteEntrance:
                    SetAnimation("EliteEntrance");
                    _stateTimer = 1.5f;
                    // Elite entrance effect
                    CombatFeedback.Instance.TriggerCameraShake(0.2f, 4f);
                    break;
                case EnemyState.SpecialAttack:
                    SetAnimation("SpecialAttack");
                    break;
            }
        }

        private void ExitState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Telegraph:
                    EndTelegraph();
                    break;
                case EnemyState.Attacking:
                    EndAttack();
                    break;
                case EnemyState.Block:
                    // End blocking
                    break;
            }
        }

        protected void UpdateCurrentState()
        {
            float dt = Time.deltaTime;
            _stateTimer += dt;

            switch (_currentState)
            {
                case EnemyState.Idle:
                    UpdateIdle(dt);
                    break;
                case EnemyState.Patrol:
                    UpdatePatrol(dt);
                    break;
                case EnemyState.Alert:
                    UpdateAlert(dt);
                    break;
                case EnemyState.Chase:
                    UpdateChase(dt);
                    break;
                case EnemyState.Telegraph:
                    UpdateTelegraph(dt);
                    break;
                case EnemyState.Attacking:
                    UpdateAttack(dt);
                    break;
                case EnemyState.Recovery:
                    UpdateRecovery(dt);
                    break;
                case EnemyState.Retreat:
                    UpdateRetreat(dt);
                    break;
                case EnemyState.Block:
                    UpdateBlock(dt);
                    break;
                case EnemyState.Staggered:
                    UpdateStaggered(dt);
                    break;
                case EnemyState.HitReaction:
                    UpdateHitReaction(dt);
                    break;
                case EnemyState.Spawn:
                    UpdateSpawn(dt);
                    break;
                case EnemyState.EliteEntrance:
                    UpdateEliteEntrance(dt);
                    break;
                case EnemyState.SpecialAttack:
                    UpdateSpecialAttack(dt);
                    break;
            }

            // Elite modifier effects (applied regardless of state)
            UpdateEliteEffects(dt);

            // Stagger decay
            if (_staggerComponent != null)
                _staggerComponent.DecayStagger(dt);

            // Attack cooldown
            _attackCooldownTimer -= dt;
        }

        #endregion

        #region Idle / Patrol

        private void UpdateIdle(float dt)
        {
            if (CheckPlayerInRange())
            {
                SetState(EnemyState.Alert);
                return;
            }

            // Transition to patrol after idle time
            if (_stateTimer > 3f)
                SetState(EnemyState.Patrol);
        }

        private void UpdatePatrol(float dt)
        {
            if (CheckPlayerInRange())
            {
                SetState(EnemyState.Alert);
                return;
            }

            // Move toward patrol target
            Vector3 dir = (_patrolTarget - transform.position).normalized;
            dir.y = 0f;
            transform.position += dir * _scaledSpeed * 0.5f * dt;

            // Rotate toward movement direction
            if (dir.sqrMagnitude > 0.01f)
                RotateToward(dir, _definition?.TurnSpeed ?? 5f, dt);

            // Check if reached patrol target
            if (Vector3.Distance(transform.position, _patrolTarget) < 0.5f)
            {
                ChoosePatrolTarget();
            }
        }

        private void ChoosePatrolTarget()
        {
            float radius = _definition?.PatrolRadius ?? 3f;
            Vector2 randomOffset = Random.insideUnitCircle * radius;
            _patrolTarget = _spawnPosition + new Vector3(randomOffset.x, 0, randomOffset.y);
        }

        #endregion

        #region Alert / Chase

        private void UpdateAlert(float dt)
        {
            _alertTimer -= dt;
            if (_alertTimer <= 0f)
            {
                _hasSeenPlayer = true;
                SetState(EnemyState.Chase);
            }
        }

        private void UpdateChase(float dt)
        {
            if (_target == null)
            {
                SetState(EnemyState.Idle);
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _target.position);

            // Check if in attack range
            if (distToPlayer <= (_definition?.AttackRange ?? 1.5f) && _attackCooldownTimer <= 0f)
            {
                // Start telegraphing attack
                ChooseAttack(distToPlayer);
                SetState(EnemyState.Telegraph);
                return;
            }

            // Check if too far - give up chasing
            if (distToPlayer > (_definition?.DetectionRange ?? 8f) * 1.5f)
            {
                _hasSeenPlayer = false;
                SetState(EnemyState.Patrol);
                return;
            }

            // Move toward player
            Vector3 dir = (_target.position - transform.position).normalized;
            dir.y = 0f;
            transform.position += dir * _scaledSpeed * dt;

            // Rotate toward player
            RotateToward(dir, _definition?.TurnSpeed ?? 5f, dt);

            // Check if should retreat (for defensive enemies)
            if (_definition?.CanRetreat == true && distToPlayer < (_definition?.MinAttackDistance ?? 1f))
            {
                SetState(EnemyState.Retreat);
                return;
            }

            // Check if should block (for shield enemies)
            if (_definition?.CanBlock == true && Random.value < (_definition?.BlockChance ?? 0f))
            {
                SetState(EnemyState.Block);
                return;
            }
        }

        #endregion

        #region Telegraph / Attack

        private void StartTelegraph()
        {
            _isTelegraphing = true;
            _telegraphDirection = (_target?.position ?? transform.forward * 3f) - transform.position;
            _telegraphDirection.y = 0f;
            _telegraphDirection.Normalize();

            _telegraphDuration = _definition?.TelegraphDuration ?? 0.5f;

            // Rotate toward target (gradual, not instant)
            RotateToward(_telegraphDirection, (_definition?.TurnSpeed ?? 5f) * 0.5f, Time.deltaTime);

            // Create ground telegraph visual
            SpawnGroundTelegraph(_telegraphDirection, _telegraphDuration);

            // Play telegraph sound
            EventBus.Publish(new SFXPlayEvent
            {
                SfxId = _definition?.EnemyId + "_telegraph",
                Position = transform.position,
                Volume = 0.7f,
                Pitch = 1.2f
            });
        }

        private void UpdateTelegraph(float dt)
        {
            _stateTimer += dt;

            // Gradually rotate toward target during telegraph
            if (_target != null)
            {
                Vector3 dir = (_target.position - transform.position).normalized;
                dir.y = 0f;
                RotateToward(dir, (_definition?.TurnSpeed ?? 5f) * 0.3f, dt);
            }

            // Check if telegraph is complete
            if (_stateTimer >= _telegraphDuration)
            {
                SetState(EnemyState.Attacking);
            }
        }

        private void EndTelegraph()
        {
            _isTelegraphing = false;
            RemoveGroundTelegraph();
        }

        private void StartAttack()
        {
            // Choose and activate the queued attack
            if (_queuedAttack != null && _hitboxManager != null)
            {
                _hitboxManager.EnableHitbox(
                    _queuedAttack.HitboxName,
                    _queuedAttack.ActiveStartPercent,
                    _queuedAttack.ActiveEndPercent
                );
            }

            // Rotate toward telegraph direction (final snap)
            if (_telegraphDirection.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(_telegraphDirection.x, _telegraphDirection.z) * Mathf.Rad2Deg;
                // Only snap partially - enemies don't perfectly track
                float currentAngle = transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
                transform.Rotate(0, delta * 0.8f, 0); // 80% tracking, not 100%
            }

            // Play attack sound
            EventBus.Publish(new SFXPlayEvent
            {
                SfxId = _definition?.AttackSoundId ?? "enemy_attack",
                Position = transform.position
            });

            // Set attack cooldown
            _currentAttackCooldown = Random.Range(
                _definition?.AttackCooldownMin ?? 0.8f,
                _definition?.AttackCooldownMax ?? 2f
            );
            _attackCooldownTimer = _currentAttackCooldown;

            // Lunge forward during attack
            float lunge = _queuedAttack?.LungeDistance ?? 0.3f;
            transform.position += _telegraphDirection * lunge;
        }

        private void UpdateAttack(float dt)
        {
            float attackDuration = _definition?.AttackDuration ?? 0.4f;

            if (_stateTimer >= attackDuration)
            {
                SetState(EnemyState.Recovery);
            }
        }

        private void EndAttack()
        {
            _hitboxManager?.DisableAllHitboxes();
        }

        private void ChooseAttack(float distToPlayer)
        {
            // Choose attack based on distance, type, and chance
            if (_definition?.Attacks != null && _definition.Attacks.Length > 0)
            {
                // Heavy attack chance
                if (Random.value < (_definition?.HeavyAttackChance ?? 0.2f) && _definition.HeavyAttack != null)
                {
                    _queuedAttack = _definition.HeavyAttack;
                }
                // Special attack chance
                else if (Random.value < (_definition?.SpecialAttackChance ?? 0.1f) && _definition.SpecialAttack != null)
                {
                    _queuedAttack = _definition.SpecialAttack;
                }
                // Regular attack
                else
                {
                    _queuedAttack = _definition.Attacks[Random.Range(0, _definition.Attacks.Length)];
                }
            }
            else
            {
                // Fallback: create default attack
                _queuedAttack = CreateDefaultAttack();
            }

            _telegraphDuration = _queuedAttack?.ActiveStartPercent > 0 
                ? _telegraphDuration * (1f + _queuedAttack.ActiveStartPercent) 
                : _telegraphDuration;
        }

        private AttackDefinition CreateDefaultAttack()
        {
            // Create a minimal default attack definition
            var def = AttackDefinition.CreateInstance<AttackDefinition>();
            def.AttackId = "default_enemy_attack";
            def.BaseDamage = _scaledDamage;
            def.HitboxName = "weapon";
            def.HitboxSize = new Vector3(1f, 0.8f, 1.5f);
            def.HitboxCenter = new Vector3(0.5f, 1f, 0.5f);
            def.ActiveStartPercent = 0.3f;
            def.ActiveEndPercent = 0.7f;
            def.KnockbackForce = 1f;
            def.HasKnockback = true;
            def.IsParryable = true;
            return def;
        }

        #endregion

        #region Recovery / Retreat / Block

        private void UpdateRecovery(float dt)
        {
            if (_stateTimer >= (_definition?.RecoveryDuration ?? 0.3f))
            {
                // Return to chase or idle
                if (CheckPlayerInRange())
                    SetState(EnemyState.Chase);
                else
                    SetState(EnemyState.Idle);
            }
        }

        private void UpdateRetreat(float dt)
        {
            if (_target == null) { SetState(EnemyState.Idle); return; }

            Vector3 retreatDir = (transform.position - _target.position).normalized;
            retreatDir.y = 0f;
            transform.position += retreatDir * _scaledSpeed * 0.8f * dt;

            float dist = Vector3.Distance(transform.position, _target.position);
            float retreatDist = _definition?.RetreatDistance ?? 3f;
            if (dist >= retreatDist + (_definition?.AttackRange ?? 1.5f))
            {
                SetState(EnemyState.Chase);
            }
        }

        private void UpdateBlock(float dt)
        {
            // Check if player attacks during block - would deflect
            if (_stateTimer >= 1f)
                SetState(EnemyState.Chase);
        }

        #endregion

        #region Staggered / Hit Reaction

        private void UpdateStaggered(float dt)
        {
            if (_stateTimer >= (_definition?.StaggerDuration ?? 2f))
            {
                SetState(EnemyState.Recovery);
            }
        }

        private void UpdateHitReaction(float dt)
        {
            if (_stateTimer >= 0.25f)
            {
                if (CheckPlayerInRange())
                    SetState(EnemyState.Chase);
                else
                    SetState(EnemyState.Idle);
            }
        }

        private void ApplyKnockback()
        {
            Vector3 knockback = GameMath.CalculateKnockback(_hitDirection, _knockbackForce);
            transform.position += knockback;
        }

        #endregion

        #region Spawn / Elite Entrance

        private void UpdateSpawn(float dt)
        {
            if (_stateTimer >= 1f)
                SetState(EnemyState.Idle);
        }

        private void UpdateEliteEntrance(float dt)
        {
            if (_stateTimer >= 1.5f)
                SetState(EnemyState.Chase);
        }

        #endregion

        #region Special Attack

        private void UpdateSpecialAttack(float dt)
        {
            if (_stateTimer >= 1f)
                SetState(EnemyState.Recovery);
        }

        #endregion

        #region Elite Effects

        private void UpdateEliteEffects(float dt)
        {
            if (!_isElite) return;

            switch (_eliteModifier)
            {
                case EliteModifier.Vampiric:
                    // Heal amount per hit is handled in OnDamageDealt
                    break;
                case EliteModifier.Shielded:
                    UpdateShield(dt);
                    break;
                case EliteModifier.Summoning:
                    UpdateSummoning(dt);
                    break;
                case EliteModifier.Teleporting:
                    UpdateTeleporting(dt);
                    break;
                case EliteModifier.Mirrored:
                    UpdateMirrored(dt);
                    break;
            }
        }

        private void UpdateShield(float dt)
        {
            if (_shieldHealth < _scaledHealth * 0.3f)
            {
                _shieldRechargeTimer -= dt;
                if (_shieldRechargeTimer <= 0f)
                {
                    _shieldHealth = Mathf.Min(_scaledHealth * 0.3f, _shieldHealth + _scaledHealth * 0.05f);
                    _shieldRechargeTimer = 5f;
                }
            }
        }

        private void UpdateSummoning(float dt)
        {
            _summonCooldown -= dt;
            if (_summonCooldown <= 0f && _currentState == EnemyState.Chase)
            {
                // Summon a minor enemy near the elite
                SpawnMinion();
                _summonCooldown = 8f;
            }
        }

        private void UpdateTeleporting(float dt)
        {
            _teleportCooldown -= dt;
            if (_teleportCooldown <= 0f && _target != null)
            {
                // Teleport to a flanking position
                Vector3 flankPos = _target.position + Random.onUnitSphere * 3f;
                flankPos.y = transform.position.y;
                transform.position = flankPos;
                _teleportCooldown = 5f;

                // Teleport VFX
                EventBus.Publish(new SFXPlayEvent { SfxId = "teleport", Position = transform.position });
            }
        }

        private void UpdateMirrored(float dt)
        {
            // Mirror clone attacks in tandem with main enemy
            if (_hasMirroredClone && _mirrorClone == null)
            {
                // Create mirror clone
                _mirrorClone = Instantiate(gameObject, transform.position + transform.right * 2f, transform.rotation);
                // Make mirror clone weaker
                var mirrorHealth = _mirrorClone.GetComponent<HealthComponent>();
                if (mirrorHealth != null)
                    mirrorHealth.SetMaxHealth(_scaledHealth * 0.3f);
            }
        }

        private void SpawnMinion()
        {
            // Spawn a weak minion near the elite
            var minionPrefab = Resources.Load<GameObject>("Enemies/Minion");
            if (minionPrefab != null)
            {
                var minion = Instantiate(minionPrefab, transform.position + Random.insideUnitSphere * 2f, Quaternion.identity);
                // Minion stats are much weaker
                var minionHealth = minion.GetComponent<HealthComponent>();
                if (minionHealth != null)
                    minionHealth.SetMaxHealth(_scaledHealth * 0.2f);
            }
        }

        #endregion

        #region Utility

        private bool CheckPlayerInRange()
        {
            if (_target == null) return false;
            float dist = Vector3.Distance(transform.position, _target.position);
            return dist <= (_definition?.DetectionRange ?? 8f);
        }

        private void RotateToward(Vector3 direction, float speed, float dt)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float delta = Mathf.DeltaAngle(currentAngle, targetAngle);

            // Enemies do NOT rotate instantly - this is intentional for readability
            float maxRotate = speed * dt;
            float rotateAmount = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), maxRotate);
            transform.Rotate(0, rotateAmount, 0);
        }

        private void SpawnGroundTelegraph(Vector3 direction, float duration)
        {
            // Create a visual telegraph on the ground
            var telegraphPrefab = Resources.Load<GameObject>("VFX/GroundTelegraph");
            if (telegraphPrefab != null)
            {
                var telegraph = Instantiate(telegraphPrefab, transform.position, Quaternion.LookRotation(direction));
                var telegraphComponent = telegraph.AddComponent<GroundTelegraph>();
                telegraphComponent.Initialize(duration, direction, _definition?.AttackRange ?? 1.5f);
                Destroy(telegraph, duration + 0.2f);
            }
        }

        private void RemoveGroundTelegraph()
        {
            // Ground telegraph is destroyed automatically after duration
        }

        private void SetAnimation(string animName)
        {
            if (_animator != null)
                _animator.Play(animName);
        }

        #endregion

        #region Damage Reception

        /// <summary>
        /// Handle incoming damage with hit reactions, stagger, and parry checks.
        /// </summary>
        public virtual void ReceiveDamage(float damage, Vector3 hitDirection, string source, int attackerId, bool isCritical = false)
        {
            if (!IsAlive) return;

            // Check for player parry opportunity
            var player = _target?.GetComponent<PlayerController>();
            if (player != null && player.IsInParryWindow && _queuedAttack?.IsParryable == true)
            {
                // Player parried this attack
                if (player.TryParry(gameObject.GetInstanceID()))
                {
                    // Parry successful - enter staggered state immediately
                    SetState(EnemyState.Staggered);
                    return;
                }
            }

            // Shield check for shielded elites
            if (_hasShield && _shieldHealth > 0f)
            {
                float shieldAbsorb = Mathf.Min(_shieldHealth, damage);
                _shieldHealth -= shieldAbsorb;
                damage -= shieldAbsorb;

                if (damage <= 0f) return;

                // Shield break feedback
                CombatFeedback.Instance.TriggerCameraShake(0.3f, 5f);
                EventBus.Publish(new SFXPlayEvent { SfxId = "shield_break", Position = transform.position });
            }

            // Apply damage
            _healthComponent.TakeDamage(damage, hitDirection, source, attackerId, isCritical);

            // Combat feedback
            ImpactType impactType = isCritical ? ImpactType.Critical : ImpactType.Light;
            CombatFeedback.Instance.TriggerHitStop(impactType);
            CombatFeedback.Instance.TriggerCameraShake(damage * 0.005f, 3f);
            CombatFeedback.Instance.SpawnImpactEffect(impactType, transform.position, hitDirection);
            CombatFeedback.Instance.TriggerDamageFlash(_mainRenderer);

            // Stagger buildup
            if (_staggerComponent != null)
                _staggerComponent.AddStaggerDamage(damage * (_definition?.StaggerDamageMultiplier ?? 1f));

            // Hit direction for knockback
            _hitDirection = hitDirection;
            _knockbackForce = damage * 0.1f;

            // Vampiric elite heals when dealing damage (not when receiving)
            // This is handled elsewhere

            // State transition based on damage
            if (_healthComponent.IsAlive)
            {
                if (_staggerComponent != null && _staggerComponent.IsStaggered)
                {
                    SetState(EnemyState.Staggered);
                }
                else
                {
                    SetState(EnemyState.HitReaction);
                }
            }
        }

        #endregion

        #region Death

        protected virtual void TriggerDeath()
        {
            _marker.IsAlive = false;

            // Death feedback
            CombatFeedback.Instance.TriggerHitStop(ImpactType.Heavy);
            CombatFeedback.Instance.TriggerCameraShake(0.3f, 5f);

            // Publish death event
            EventBus.Publish(new EnemyDeathEvent
            {
                EnemyInstanceId = gameObject.GetInstanceID(),
                EnemyType = _definition?.EnemyId ?? "unknown",
                IsElite = _isElite,
                IsExecution = false,
                DeathPosition = transform.position
            });

            // Explosive elite modifier
            if (_isElite && _eliteModifier == EliteModifier.Explosive)
            {
                SpawnExplosion();
            }

            // Destroy mirror clone
            if (_mirrorClone != null)
                Destroy(_mirrorClone);

            // Delayed destroy (for death animation)
            Destroy(gameObject, 2f);
        }

        private void SpawnExplosion()
        {
            // Create explosion VFX and damage nearby
            float explosionRadius = 4f;
            float explosionDamage = _scaledDamage * 2f;

            var hits = Physics.OverlapSphere(transform.position, explosionRadius, LayerMask.GetMask("PlayerHurtbox"));
            foreach (var hit in hits)
            {
                var playerHealth = hit.GetComponentInParent<HealthComponent>();
                if (playerHealth != null)
                {
                    Vector3 dir = (hit.transform.position - transform.position).normalized;
                    playerHealth.TakeDamage(explosionDamage, dir, "explosion", gameObject.GetInstanceID());
                }
            }

            CombatFeedback.Instance.TriggerCameraShake(0.8f, 10f);
            EventBus.Publish(new SFXPlayEvent { SfxId = "elite_explosion", Position = transform.position, Volume = 2f });

            // Spawn explosion VFX
            var explosionPrefab = Resources.Load<GameObject>("VFX/EliteExplosion");
            if (explosionPrefab != null)
                Destroy(Instantiate(explosionPrefab, transform.position, Quaternion.identity), 2f);
        }

        #endregion

        private void Update()
        {
            UpdateCurrentState();
        }
    }

    /// <summary>
    /// Ground telegraph component for visual attack telegraphs.
    /// Shows expanding rings or directional indicators before attacks.
    /// </summary>
    public class GroundTelegraph : MonoBehaviour
    {
        private float _duration;
        private Vector3 _direction;
        private float _range;
        private float _timer;

        public void Initialize(float duration, Vector3 direction, float range)
        {
            _duration = duration;
            _direction = direction;
            _range = range;
            _timer = 0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float progress = _timer / _duration;

            // Expand telegraph visual over time
            // In full implementation, this would animate a mesh/projector
            transform.localScale = Vector3.Lerp(
                new Vector3(0.1f, 0.1f, 0.1f),
                new Vector3(_range, 0.01f, _range),
                progress
            );

            // Fade from warning color to attack color
            // Yellow -> Red over telegraph duration
        }
    }

    /// <summary>
    /// Coordinates groups of enemies in combat encounters.
    /// Prevents all enemies attacking simultaneously and creates
    /// coordinated attack patterns.
    /// </summary>
    public class EnemyGroupCoordinator : MonoBehaviour
    {
        [SerializeField] private float _globalAttackCooldown = 0.5f;
        [SerializeField] private int _maxSimultaneousAttacks = 2;
        [SerializeField] private float _flankingAngle = 45f;

        private List<EnemyController> _groupMembers = new();
        private float _globalCooldownTimer;
        private int _currentAttackingCount;

        public void RegisterEnemy(EnemyController enemy)
        {
            _groupMembers.Add(enemy);
        }

        public void UnregisterEnemy(EnemyController enemy)
        {
            _groupMembers.Remove(enemy);
        }

        /// <summary>
        /// Check if an enemy can start attacking (group coordination).
        /// Prevents too many enemies attacking at once.
        /// </summary>
        public bool CanAttack(EnemyController enemy)
        {
            if (_currentAttackingCount >= _maxSimultaneousAttacks) return false;
            if (_globalCooldownTimer > 0f) return false;
            return true;
        }

        /// <summary>
        /// Notify coordinator that an enemy has started attacking.
        /// </summary>
        public void NotifyAttackStart(EnemyController enemy)
        {
            _currentAttackingCount++;
            _globalCooldownTimer = _globalAttackCooldown;
        }

        /// <summary>
        /// Notify coordinator that an enemy has finished attacking.
        /// </summary>
        public void NotifyAttackEnd(EnemyController enemy)
        {
            _currentAttackingCount = Mathf.Max(0, _currentAttackingCount - 1);
        }

        /// <summary>
        /// Get flanking position for an enemy relative to the player.
        /// </summary>
        public Vector3 GetFlankingPosition(EnemyController enemy, Transform player)
        {
            // Calculate position that flanks from a different angle than existing enemies
            float baseAngle = 0f;
            foreach (var member in _groupMembers)
            {
                if (member != enemy)
                {
                    float angle = Mathf.Atan2(
                        (member.transform.position - player.position).x,
                        (member.transform.position - player.position).z
                    ) * Mathf.Rad2Deg;
                    baseAngle += angle;
                }
            }

            // Flank from opposite angle
            float flankAngle = baseAngle + _flankingAngle + Random.Range(-15f, 15f);
            float distance = 3f;
            Vector3 pos = player.position + new Vector3(
                Mathf.Cos(flankAngle * Mathf.Deg2Rad) * distance,
                0,
                Mathf.Sin(flankAngle * Mathf.Deg2Rad) * distance
            );

            return pos;
        }

        private void Update()
        {
            _globalCooldownTimer -= Time.deltaTime;
        }
    }
}
