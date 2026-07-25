using UnityEngine;
using Relicfall.Core.Events;
using Relicfall.Core.Utils;
using Relicfall.Combat;

namespace Relicfall.Player
{
    /// <summary>
    /// Player state machine states for combat and movement.
    /// Each state has clear entry/exit conditions and animation bindings.
    /// </summary>
    public enum PlayerState
    {
        Idle,
        Moving,
        LightAttack1,
        LightAttack2,
        LightAttack3,
        HeavyAttack,
        ChargedHeavyAttack,
        ChargingHeavy,
        Dash,
        Parry,
        ParrySuccess,
        HitReaction,
        Knockback,
        Execution,
        AbilityCast,
        Ultimate,
        Dead,
        Interacting
    }

    /// <summary>
    /// Central player controller that manages movement, combat state machine,
    /// and coordinates input with gameplay actions.
    /// Implements responsive combat with buffering, cancel windows, and state transitions.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintMultiplier = 1.3f;
        [SerializeField] private float _acceleration = 15f;
        [SerializeField] private float _deceleration = 20f;
        [SerializeField] private float _turnSpeed = 12f;

        [Header("Dash")]
        [SerializeField] private float _dashDistance = 4f;
        [SerializeField] private float _dashDuration = 0.2f;
        [SerializeField] private float _dashCooldown = 0.8f;
        [SerializeField] private float _dashIFramesDuration = 0.3f;

        [Header("Parry")]
        [SerializeField] private float _parryWindowDuration = 0.3f;
        [SerializeField] private float _parryCooldown = 0.5f;
        [SerializeField] private float _parrySuccessDuration = 0.8f;
        [SerializeField] private float _parryCounterWindow = 1.5f;
        [SerializeField] private float _parryStaggerDuration = 2f;

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;

        [Header("Attack Parameters")]
        [SerializeField] private AttackDefinition _lightAttack1Def;
        [SerializeField] private AttackDefinition _lightAttack2Def;
        [SerializeField] private AttackDefinition _lightAttack3Def;
        [SerializeField] private AttackDefinition _heavyAttackDef;
        [SerializeField] private AttackDefinition _chargedHeavyDef;

        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private HitboxManager _hitboxManager;
        [SerializeField] private WeaponHandler _weaponHandler;

        // State machine
        private PlayerState _currentState = PlayerState.Idle;
        private PlayerState _previousState;
        private float _stateTimer;
        private float _stateStartTime;
        private bool _canCancelCurrentState;
        private bool _isInIFrames;
        private float _iFramesTimer;
        private bool _isInParryWindow;
        private float _parryWindowTimer;

        // Combat tracking
        private int _comboStep;
        private float _comboResetTimer;
        private float _comboResetWindow = 0.8f;
        private bool _heavyChargeHeld;
        private float _heavyChargeTimer;
        private float _heavyChargeDuration = 0.6f;

        // Cooldowns
        private CooldownTimer _dashCooldownTimer;
        private CooldownTimer _parryCooldownTimer;
        private CooldownTimer _ultimateCooldownTimer;
        private CooldownTimer _abilityCooldownTimer;

        // Movement
        private Vector3 _moveVelocity;
        private Vector3 _dashDirection;
        private Vector3 _dashStartPos;
        private Vector3 _dashEndPos;

        // Damage flash
        private float _damageFlashTimer;
        private SkinnedMeshRenderer[] _renderers;
        private Material[] _originalMaterials;
        private Material _damageFlashMaterial;

        // Velocity for smooth damp
        private float _currentSpeed;

        public PlayerState CurrentState => _currentState;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float HealthPercent => _currentHealth / _maxHealth;
        public bool IsAlive => _currentState != PlayerState.Dead;
        public bool IsInIFrames => _isInIFrames;
        public bool IsInParryWindow => _isInParryWindow;
        public int ComboStep => _comboStep;
        public Vector3 MoveVelocity => _moveVelocity;
        public WeaponHandler Weapon => _weaponHandler;

        public System.Action<PlayerState> OnStateChanged;
        public System.Action<float, float> OnHealthChanged;

        private void Awake()
        {
            _dashCooldownTimer = new CooldownTimer(_dashCooldown);
            _parryCooldownTimer = new CooldownTimer(_parryCooldown);
            _ultimateCooldownTimer = new CooldownTimer(30f);
            _abilityCooldownTimer = new CooldownTimer(8f);

            _currentHealth = _maxHealth;

            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        private void Start()
        {
            if (_inputHandler == null)
                _inputHandler = GetComponent<PlayerInputHandler>();
            if (_hitboxManager == null)
                _hitboxManager = GetComponent<HitboxManager>();
            if (_weaponHandler == null)
                _weaponHandler = GetComponent<WeaponHandler>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            UpdateStateTimer(dt);
            UpdateCooldowns(dt);
            UpdateIFrames(dt);
            UpdateComboReset(dt);
            UpdateDamageFlash(dt);

            // Process buffered inputs
            ProcessInputBuffer();

            // State-specific updates
            UpdateCurrentState(dt);
        }

        #region State Machine

        private void SetState(PlayerState newState)
        {
            if (_currentState == newState) return;
            if (_currentState == PlayerState.Dead) return;

            ExitState(_currentState);
            _previousState = _currentState;
            _currentState = newState;
            _stateStartTime = Time.time;
            _stateTimer = 0f;
            EnterState(newState);

            OnStateChanged?.Invoke(newState);
        }

        private void EnterState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Idle:
                    _canCancelCurrentState = true;
                    SetAnimation("Idle");
                    break;
                case PlayerState.Moving:
                    _canCancelCurrentState = true;
                    SetAnimation("Run");
                    break;
                case PlayerState.LightAttack1:
                    _canCancelCurrentState = false;
                    _comboStep = 1;
                    _comboResetTimer = _comboResetWindow;
                    SetAnimation("LightAttack1");
                    PerformAttack(_lightAttack1Def);
                    EventBus.Publish(new PlayerAttackEvent { AttackType = "light", ComboStep = 1, WeaponId = _weaponHandler?.CurrentWeaponId ?? "" });
                    break;
                case PlayerState.LightAttack2:
                    _canCancelCurrentState = false;
                    _comboStep = 2;
                    _comboResetTimer = _comboResetWindow;
                    SetAnimation("LightAttack2");
                    PerformAttack(_lightAttack2Def);
                    EventBus.Publish(new PlayerAttackEvent { AttackType = "light", ComboStep = 2, WeaponId = _weaponHandler?.CurrentWeaponId ?? "" });
                    break;
                case PlayerState.LightAttack3:
                    _canCancelCurrentState = false;
                    _comboStep = 3;
                    _comboResetTimer = _comboResetWindow;
                    SetAnimation("LightAttack3");
                    PerformAttack(_lightAttack3Def);
                    EventBus.Publish(new PlayerAttackEvent { AttackType = "light", ComboStep = 3, WeaponId = _weaponHandler?.CurrentWeaponId ?? "" });
                    break;
                case PlayerState.HeavyAttack:
                    _canCancelCurrentState = false;
                    SetAnimation("HeavyAttack");
                    PerformAttack(_heavyAttackDef);
                    EventBus.Publish(new PlayerAttackEvent { AttackType = "heavy", ComboStep = 0, WeaponId = _weaponHandler?.CurrentWeaponId ?? "" });
                    break;
                case PlayerState.ChargingHeavy:
                    _canCancelCurrentState = true;
                    _heavyChargeTimer = 0f;
                    SetAnimation("ChargingHeavy");
                    break;
                case PlayerState.ChargedHeavyAttack:
                    _canCancelCurrentState = false;
                    SetAnimation("ChargedHeavyAttack");
                    PerformAttack(_chargedHeavyDef);
                    EventBus.Publish(new PlayerAttackEvent { AttackType = "charged_heavy", ComboStep = 0, WeaponId = _weaponHandler?.CurrentWeaponId ?? "", IsCharged = true });
                    break;
                case PlayerState.Dash:
                    _canCancelCurrentState = false;
                    StartDash();
                    SetAnimation("Dash");
                    SetIFrames(_dashIFramesDuration);
                    EventBus.Publish(new PlayerDashEvent { Direction = _dashDirection, Distance = _dashDistance });
                    break;
                case PlayerState.Parry:
                    _canCancelCurrentState = true;
                    _isInParryWindow = true;
                    _parryWindowTimer = _parryWindowDuration;
                    SetAnimation("Parry");
                    EventBus.Publish(new ParryAttemptEvent { DefenderInstanceId = gameObject.GetInstanceID(), Timestamp = Time.time });
                    break;
                case PlayerState.ParrySuccess:
                    _canCancelCurrentState = true;
                    SetAnimation("ParrySuccess");
                    SetIFrames(_parrySuccessDuration);
                    CombatFeedback.Instance?.TriggerHitStop(0.15f);
                    CombatFeedback.Instance?.TriggerCameraShake(0.3f, 5f);
                    break;
                case PlayerState.HitReaction:
                    _canCancelCurrentState = false;
                    SetAnimation("HitReaction");
                    break;
                case PlayerState.Knockback:
                    _canCancelCurrentState = false;
                    SetAnimation("Knockback");
                    break;
                case PlayerState.Execution:
                    _canCancelCurrentState = false;
                    SetAnimation("Execution");
                    break;
                case PlayerState.AbilityCast:
                    _canCancelCurrentState = false;
                    SetAnimation("AbilityCast");
                    break;
                case PlayerState.Ultimate:
                    _canCancelCurrentState = false;
                    SetAnimation("Ultimate");
                    break;
                case PlayerState.Dead:
                    _canCancelCurrentState = false;
                    SetAnimation("Death");
                    EventBus.Publish(new PlayerDeathEvent
                    {
                        DeathPosition = transform.position,
                        DeathCause = "combat",
                        RunDurationSeconds = Core.GameManager.Instance?.CurrentRun?.RunDurationSeconds ?? 0
                    });
                    break;
                case PlayerState.Interacting:
                    _canCancelCurrentState = true;
                    SetAnimation("Interact");
                    break;
            }
        }

        private void ExitState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Parry:
                    _isInParryWindow = false;
                    break;
                case PlayerState.Dash:
                    EndDash();
                    break;
            }
        }

        private void UpdateCurrentState(float dt)
        {
            switch (_currentState)
            {
                case PlayerState.Idle:
                case PlayerState.Moving:
                    UpdateMovement(dt);
                    break;
                case PlayerState.LightAttack1:
                case PlayerState.LightAttack2:
                case PlayerState.LightAttack3:
                case PlayerState.HeavyAttack:
                case PlayerState.ChargedHeavyAttack:
                    UpdateAttackState(dt);
                    break;
                case PlayerState.ChargingHeavy:
                    UpdateCharging(dt);
                    break;
                case PlayerState.Dash:
                    UpdateDash(dt);
                    break;
                case PlayerState.Parry:
                    UpdateParry(dt);
                    break;
                case PlayerState.HitReaction:
                    UpdateHitReaction(dt);
                    break;
                case PlayerState.Knockback:
                    UpdateKnockback(dt);
                    break;
                case PlayerState.Execution:
                    UpdateExecution(dt);
                    break;
            }
        }

        #endregion

        #region Movement

        private void UpdateMovement(float dt)
        {
            if (!_inputHandler.HasMoveInput)
            {
                // Decelerate to stop
                _currentSpeed = Mathf.Max(0, _currentSpeed - _deceleration * dt);
                _moveVelocity = transform.forward * _currentSpeed;
                if (_currentSpeed <= 0.01f)
                {
                    if (_currentState != PlayerState.Idle)
                        SetState(PlayerState.Idle);
                }
            }
            else
            {
                // Accelerate toward target
                float targetSpeed = _moveSpeed * (_inputHandler.MoveInput.magnitude > 0.9f ? _sprintMultiplier : 1f);
                _currentSpeed = Mathf.Min(targetSpeed, _currentSpeed + _acceleration * dt);
                _moveVelocity = _inputHandler.MoveDirectionWorld * _currentSpeed;

                // Rotate toward movement direction
                if (_inputHandler.MoveDirectionWorld.sqrMagnitude > 0.01f)
                {
                    float targetAngle = Mathf.Atan2(_inputHandler.MoveDirectionWorld.x, _inputHandler.MoveDirectionWorld.z) * Mathf.Rad2Deg;
                    float currentAngle = transform.eulerAngles.y;
                    float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
                    float rotateAmount = Mathf.Sign(deltaAngle) * Mathf.Min(Mathf.Abs(deltaAngle), _turnSpeed * dt);
                    transform.Rotate(0, rotateAmount, 0);
                }

                if (_currentState != PlayerState.Moving)
                    SetState(PlayerState.Moving);
            }

            // Apply movement via character controller or transform
            transform.position += _moveVelocity * dt;
        }

        #endregion

        #region Dash

        private void StartDash()
        {
            _dashDirection = _inputHandler.HasMoveInput ? _inputHandler.MoveDirectionWorld : transform.forward;
            _dashStartPos = transform.position;
            _dashEndPos = _dashStartPos + _dashDirection * _dashDistance;
        }

        private void UpdateDash(float dt)
        {
            float progress = _stateTimer / _dashDuration;
            if (progress >= 1f)
            {
                SetState(PlayerState.Idle);
                return;
            }

            // Smooth dash with ease-out
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            Vector3 newPos = Vector3.Lerp(_dashStartPos, _dashEndPos, easedProgress);
            transform.position = newPos;

            // Leave dash trail
            _weaponHandler?.CreateDashTrail(transform.position);
        }

        private void EndDash()
        {
            // Snap to end position if close enough
            float dist = Vector3.Distance(transform.position, _dashEndPos);
            if (dist < 0.5f)
                transform.position = _dashEndPos;
        }

        #endregion

        #region Attacks

        private void PerformAttack(AttackDefinition attackDef)
        {
            if (attackDef == null || _hitboxManager == null) return;

            // Enable hitbox for this attack
            _hitboxManager.EnableHitbox(attackDef.HitboxName, attackDef.ActiveStartPercent, attackDef.ActiveEndPercent);

            // Rotate toward aim direction
            if (_inputHandler.AimDirectionWorld.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(_inputHandler.AimDirectionWorld.x, _inputHandler.AimDirectionWorld.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, targetAngle, 0);
            }

            // Create weapon trail
            _weaponHandler?.StartTrail();
        }

        private void UpdateAttackState(float dt)
        {
            // Check for animation completion via state timer
            // In a real implementation, this would use Animator state info
            float attackDuration = GetAttackDuration(_currentState);
            if (_stateTimer >= attackDuration)
            {
                // Attack complete - return to idle/moving
                _hitboxManager?.DisableAllHitboxes();
                _weaponHandler?.StopTrail();
                SetState(_inputHandler.HasMoveInput ? PlayerState.Moving : PlayerState.Idle);
                return;
            }

            // Check cancel window (typically last 30% of attack animation)
            float cancelStartPercent = 0.7f;
            if (_stateTimer >= attackDuration * cancelStartPercent)
            {
                _canCancelCurrentState = true;
            }

            // Allow movement during attack (reduced)
            float movementReduction = 0.3f;
            if (_inputHandler.HasMoveInput)
            {
                Vector3 reducedMove = _inputHandler.MoveDirectionWorld * _moveSpeed * movementReduction * dt;
                transform.position += reducedMove;
            }
        }

        private void UpdateCharging(float dt)
        {
            _heavyChargeTimer += dt;

            // Check if heavy attack button is still held
            if (!_heavyChargeHeld || _heavyChargeTimer >= _heavyChargeDuration + 0.5f)
            {
                // Release charged attack
                if (_heavyChargeTimer >= _heavyChargeDuration)
                    SetState(PlayerState.ChargedHeavyAttack);
                else
                    SetState(PlayerState.HeavyAttack);
            }

            // Allow slow rotation during charge
            if (_inputHandler.HasMoveInput)
            {
                float targetAngle = Mathf.Atan2(_inputHandler.MoveDirectionWorld.x, _inputHandler.MoveDirectionWorld.z) * Mathf.Rad2Deg;
                float currentAngle = transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
                transform.Rotate(0, Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), _turnSpeed * 0.5f * dt), 0);
            }
        }

        private float GetAttackDuration(PlayerState state)
        {
            return state switch
            {
                PlayerState.LightAttack1 => 0.35f,
                PlayerState.LightAttack2 => 0.30f,
                PlayerState.LightAttack3 => 0.45f,
                PlayerState.HeavyAttack => 0.55f,
                PlayerState.ChargedHeavyAttack => 0.70f,
                _ => 0.5f
            };
        }

        #endregion

        #region Parry

        private void UpdateParry(float dt)
        {
            _parryWindowTimer -= dt;
            if (_parryWindowTimer <= 0f)
            {
                _isInParryWindow = false;
                SetState(PlayerState.Idle);
            }
        }

        /// <summary>
        /// Called when an incoming attack hits during parry window.
        /// </summary>
        public bool TryParry(int attackerInstanceId)
        {
            if (!_isInParryWindow) return false;

            // Successful parry!
            _isInParryWindow = false;
            SetState(PlayerState.ParrySuccess);

            EventBus.Publish(new ParrySuccessEvent
            {
                DefenderInstanceId = gameObject.GetInstanceID(),
                AttackerInstanceId = attackerInstanceId,
                ParryWindowRemaining = _parryWindowTimer
            });

            return true;
        }

        #endregion

        #region Hit Reactions

        private void UpdateHitReaction(float dt)
        {
            if (_stateTimer >= 0.3f)
            {
                SetState(_inputHandler.HasMoveInput ? PlayerState.Moving : PlayerState.Idle);
            }
        }

        private void UpdateKnockback(float dt)
        {
            if (_stateTimer >= 0.5f)
            {
                SetState(PlayerState.Idle);
            }
        }

        #endregion

        #region Execution

        private void UpdateExecution(float dt)
        {
            if (_stateTimer >= 1.2f)
            {
                SetState(PlayerState.Idle);
            }
        }

        #endregion

        #region Input Processing

        private void ProcessInputBuffer()
        {
            if (!IsAlive || !_inputHandler.InputEnabled) return;

            // Priority order: Ultimate > Dash > Parry > Heavy Attack > Light Attack > Ability > Interact

            // Ultimate
            if (_inputHandler.InputBuffer.Ultimate.Consume() && _canCancelCurrentState && _ultimateCooldownTimer.TryUse())
            {
                SetState(PlayerState.Ultimate);
                return;
            }

            // Dash - highest priority cancel
            if (_inputHandler.InputBuffer.Dash.Consume())
            {
                if (_canCancelCurrentState && _dashCooldownTimer.TryUse())
                {
                    SetState(PlayerState.Dash);
                    return;
                }
            }

            // Parry
            if (_inputHandler.InputBuffer.Parry.Consume())
            {
                if (_canCancelCurrentState && _parryCooldownTimer.TryUse())
                {
                    SetState(PlayerState.Parry);
                    return;
                }
            }

            // Heavy attack
            if (_inputHandler.InputBuffer.HeavyAttack.Consume())
            {
                if (_canCancelCurrentState || IsInNeutralState())
                {
                    // Start charging heavy
                    _heavyChargeHeld = true;
                    SetState(PlayerState.ChargingHeavy);
                    return;
                }
            }

            // Light attack - advances combo or starts new combo
            if (_inputHandler.InputBuffer.LightAttack.Consume())
            {
                if (_canCancelCurrentState || IsInNeutralState())
                {
                    AdvanceCombo();
                    return;
                }
            }

            // Ability
            if (_inputHandler.InputBuffer.RelicAbility.Consume())
            {
                if (_canCancelCurrentState && _abilityCooldownTimer.TryUse())
                {
                    SetState(PlayerState.AbilityCast);
                    return;
                }
            }

            // Interact
            if (_inputHandler.InputBuffer.Interact.Consume())
            {
                if (_canCancelCurrentState || IsInNeutralState())
                {
                    SetState(PlayerState.Interacting);
                    return;
                }
            }
        }

        private bool IsInNeutralState()
        {
            return _currentState == PlayerState.Idle || _currentState == PlayerState.Moving;
        }

        private void AdvanceCombo()
        {
            switch (_comboStep)
            {
                case 0:
                    SetState(PlayerState.LightAttack1);
                    break;
                case 1:
                    SetState(PlayerState.LightAttack2);
                    break;
                case 2:
                    SetState(PlayerState.LightAttack3);
                    break;
                default:
                    _comboStep = 0;
                    SetState(PlayerState.LightAttack1);
                    break;
            }
        }

        private void UpdateComboReset(float dt)
        {
            if (_comboStep > 0 && _currentState != PlayerState.LightAttack1 && 
                _currentState != PlayerState.LightAttack2 && _currentState != PlayerState.LightAttack3)
            {
                _comboResetTimer -= dt;
                if (_comboResetTimer <= 0f)
                    _comboStep = 0;
            }
        }

        #endregion

        #region Damage and Health

        /// <summary>
        /// Apply damage to the player with hit reactions and feedback.
        /// </summary>
        public void TakeDamage(float damage, Vector3 hitDirection, string source = "", int attackerId = 0)
        {
            if (_isInIFrames || !IsAlive) return;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            // Trigger feedback
            CombatFeedback.Instance?.TriggerHitStop(0.05f);
            CombatFeedback.Instance?.TriggerCameraShake(damage / _maxHealth, 3f);
            TriggerDamageFlash();

            EventBus.Publish(new DamageEvent
            {
                TargetInstanceId = gameObject.GetInstanceID(),
                Damage = damage,
                IsCritical = false,
                HitPosition = transform.position + Vector3.up * 1f,
                HitDirection = hitDirection,
                DamageSource = source,
                AttackerInstanceId = attackerId
            });

            // Determine hit reaction
            if (damage >= _maxHealth * 0.3f)
            {
                SetState(PlayerState.Knockback);
                ApplyKnockback(hitDirection, damage * 0.1f);
            }
            else if (_canCancelCurrentState || IsInNeutralState())
            {
                SetState(PlayerState.HitReaction);
            }

            // Check death
            if (_currentHealth <= 0f)
            {
                SetState(PlayerState.Dead);
            }
        }

        /// <summary>
        /// Heal the player.
        /// </summary>
        public void Heal(float amount)
        {
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void ApplyKnockback(Vector3 direction, float force)
        {
            Vector3 knockback = direction.normalized * force;
            knockback.y = 0f;
            transform.position += knockback;
        }

        private void TriggerDamageFlash()
        {
            _damageFlashTimer = 0.1f;
        }

        private void UpdateDamageFlash(float dt)
        {
            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= dt;
                // In full implementation, swap to damage flash material
            }
        }

        #endregion

        #region IFrames

        private void SetIFrames(float duration)
        {
            _isInIFrames = true;
            _iFramesTimer = duration;
        }

        private void UpdateIFrames(float dt)
        {
            if (_isInIFrames)
            {
                _iFramesTimer -= dt;
                if (_iFramesTimer <= 0f)
                    _isInIFrames = false;

                // Visual feedback for IFrames (flicker or ghost effect)
                // Toggle renderer visibility periodically
                if (_renderers != null && _renderers.Length > 0)
                {
                    bool visible = ((int)(Time.time * 15f) % 2) == 0;
                    foreach (var r in _renderers)
                        r.enabled = visible;
                }
            }
            else if (_renderers != null)
            {
                foreach (var r in _renderers)
                    r.enabled = true;
            }
        }

        #endregion

        #region Utility

        private void UpdateStateTimer(float dt)
        {
            _stateTimer += dt;
        }

        private void UpdateCooldowns(float dt)
        {
            _dashCooldownTimer.Tick(dt);
            _parryCooldownTimer.Tick(dt);
            _ultimateCooldownTimer.Tick(dt);
            _abilityCooldownTimer.Tick(dt);
        }

        private void SetAnimation(string animName)
        {
            if (_animator != null)
                _animator.Play(animName);
        }

        /// <summary>
        /// Reset combo step (called externally when combo is interrupted).
        /// </summary>
        public void ResetCombo()
        {
            _comboStep = 0;
        }

        /// <summary>
        /// Force transition to a specific state (for external systems like relics).
        /// </summary>
        public void ForceState(PlayerState state)
        {
            SetState(state);
        }

        /// <summary>
        /// Get dash cooldown progress for UI display.
        /// </summary>
        public float DashCooldownProgress => _dashCooldownTimer.Normalized;

        /// <summary>
        /// Get parry cooldown progress for UI display.
        /// </summary>
        public float ParryCooldownProgress => _parryCooldownTimer.Normalized;

        /// <summary>
        /// Get ultimate cooldown progress for UI display.
        /// </summary>
        public float UltimateCooldownProgress => _ultimateCooldownTimer.Normalized;

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw movement direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, _moveVelocity);

            // Draw aim direction
            Gizmos.color = Color.cyan;
            if (_inputHandler != null)
                Gizmos.DrawRay(transform.position, _inputHandler.AimDirectionWorld * 3f);

            // Draw parry window indicator
            if (_isInParryWindow)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }

            // Draw IFrame indicator
            if (_isInIFrames)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
                Gizmos.DrawSphere(transform.position, 0.5f);
            }
        }

        #endregion
    }
}
