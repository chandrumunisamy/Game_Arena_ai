using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Core.Pooling;
using Relicfall.Player;

namespace Relicfall.Combat
{
    /// <summary>
    /// Attack definition ScriptableObject defining all parameters for an attack.
    /// Used by both player and enemy attacks.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackDef", menuName = "RELICFALL/Combat/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string AttackId;
        public string AttackName;
        public string Description;

        [Header("Damage")]
        public float BaseDamage = 10f;
        public float CriticalMultiplier = 2f;
        public float CriticalChance = 0.05f;
        public bool IsHeavy = false;
        public bool IsParryable = true;

        [Header("Hitbox")]
        public string HitboxName = "weapon";
        public HitboxShape Shape = HitboxShape.Box;
        public Vector3 HitboxCenter = new Vector3(0.5f, 1f, 0f);
        public Vector3 HitboxSize = new Vector3(1f, 0.8f, 1.5f);
        public float HitboxRadius = 1f;
        public float HitboxLength = 2f;
        public float ActiveStartPercent = 0.2f;
        public float ActiveEndPercent = 0.6f;

        [Header("Knockback")]
        public float KnockbackForce = 2f;
        public float KnockbackUpwardRatio = 0.2f;
        public bool HasKnockback = true;

        [Header("Stagger")]
        public float StaggerDamage = 10f;
        public bool HasStagger = true;

        [Header("Cancel")]
        public float CancelStartPercent = 0.6f;
        public bool CanCancelIntoDash = true;
        public bool CanCancelIntoParry = true;
        public bool CanCancelIntoNextAttack = true;

        [Header("Movement")]
        public float MovementDuringAttack = 0.3f;
        public float LungeDistance = 0.5f;

        [Header("VFX")]
        public GameObject TrailPrefab;
        public GameObject ImpactPrefab;
        public string ImpactSoundId;

        [Header("Tags")]
        public string[] Tags = new string[0];
    }

    /// <summary>
    /// Hitbox shapes for attack detection.
    /// </summary>
    public enum HitboxShape
    {
        Box,
        Sphere,
        Capsule,
        Cone
    }

    /// <summary>
    /// Manages multiple hitboxes on a character for attack detection.
    /// Hitboxes are enabled/disabled based on current attack state.
    /// </summary>
    public class HitboxManager : MonoBehaviour
    {
        [SerializeField] private HitboxData[] _hitboxes;

        private Dictionary<string, HitboxData> _hitboxLookup;
        private Dictionary<string, Collider> _hitboxColliders;
        private List<Collider> _hitTargets = new();
        private float _attackProgress;
        private string _currentAttackHitbox;
        private float _activeStartPercent;
        private float _activeEndPercent;
        private bool _hitboxActive;
        private AttackDefinition _currentAttack;

        public List<Collider> HitTargets => _hitTargets;

        private void Awake()
        {
            InitializeHitboxes();
        }

        private void InitializeHitboxes()
        {
            _hitboxLookup = new Dictionary<string, HitboxData>();
            _hitboxColliders = new Dictionary<string, Collider>();

            // Create colliders for each hitbox
            foreach (var hitbox in _hitboxes)
            {
                _hitboxLookup[hitbox.Name] = hitbox;

                // Create or find collider
                var colliderObj = new GameObject($"Hitbox_{hitbox.Name}");
                colliderObj.transform.SetParent(transform);
                colliderObj.transform.localPosition = hitbox.Center;

                Collider col;
                switch (hitbox.Shape)
                {
                    case HitboxShape.Box:
                        var boxCol = colliderObj.AddComponent<BoxCollider>();
                        boxCol.size = hitbox.Size;
                        col = boxCol;
                        break;
                    case HitboxShape.Sphere:
                        var sphereCol = colliderObj.AddComponent<SphereCollider>();
                        sphereCol.radius = hitbox.Radius;
                        col = sphereCol;
                        break;
                    case HitboxShape.Capsule:
                        var capCol = colliderObj.AddComponent<CapsuleCollider>();
                        capCol.radius = hitbox.Radius;
                        capCol.height = hitbox.Length;
                        col = capCol;
                        break;
                    default:
                        var defCol = colliderObj.AddComponent<BoxCollider>();
                        defCol.size = hitbox.Size;
                        col = defCol;
                        break;
                }

                col.isTrigger = true;
                // Set layer to player hitbox (layer 10)
                colliderObj.gameObject.layer = 10;

                _hitboxColliders[hitbox.Name] = col;
                colliderObj.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Enable a specific hitbox for an attack.
        /// </summary>
        public void EnableHitbox(string name, float activeStart, float activeEnd)
        {
            _currentAttackHitbox = name;
            _activeStartPercent = activeStart;
            _activeEndPercent = activeEnd;
            _attackProgress = 0f;
            _hitboxActive = false;
            _hitTargets.Clear();
        }

        /// <summary>
        /// Update hitbox active state based on attack progress.
        /// </summary>
        public void UpdateHitboxProgress(float progress)
        {
            _attackProgress = progress;

            bool shouldBeActive = progress >= _activeStartPercent && progress <= _activeEndPercent;

            if (shouldBeActive && !_hitboxActive)
            {
                ActivateHitbox(_currentAttackHitbox);
            }
            else if (!shouldBeActive && _hitboxActive)
            {
                DeactivateHitbox(_currentAttackHitbox);
            }
        }

        /// <summary>
        /// Disable all hitboxes immediately.
        /// </summary>
        public void DisableAllHitboxes()
        {
            foreach (var kvp in _hitboxColliders)
                kvp.Value.gameObject.SetActive(false);
            _hitboxActive = false;
            _hitTargets.Clear();
        }

        private void ActivateHitbox(string name)
        {
            if (_hitboxColliders.TryGetValue(name, out var col))
            {
                col.gameObject.SetActive(true);
                _hitboxActive = true;
            }
        }

        private void DeactivateHitbox(string name)
        {
            if (_hitboxColliders.TryGetValue(name, out var col))
            {
                col.gameObject.SetActive(false);
                _hitboxActive = false;
            }
        }

        /// <summary>
        /// Trigger event when hitbox collides with a hurtbox.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!_hitboxActive) return;
            if (_hitTargets.Contains(other)) return; // Prevent multi-hit per attack

            // Check if this is a valid target
            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox == null) return;

            _hitTargets.Add(other);

            // Calculate damage
            float damage = _currentAttack?.BaseDamage ?? 10f;
            bool isCritical = Random.value < (_currentAttack?.CriticalChance ?? 0.05f);
            if (isCritical)
                damage *= _currentAttack?.CriticalMultiplier ?? 2f;

            // Apply damage to target
            var targetHealth = other.GetComponentInParent<HealthComponent>();
            if (targetHealth != null && targetHealth.IsAlive)
            {
                Vector3 hitDirection = (other.transform.position - transform.position).normalized;
                targetHealth.TakeDamage(damage, hitDirection, _currentAttack?.AttackId ?? "", gameObject.GetInstanceID(), isCritical);

                // Combat feedback
                ImpactType impactType = _currentAttack?.IsHeavy == true ? ImpactType.Heavy : ImpactType.Light;
                if (isCritical) impactType = ImpactType.Critical;

                CombatFeedback.Instance.TriggerHitStop(impactType);
                CombatFeedback.Instance.TriggerCameraShake(damage * 0.01f, 5f);
                CombatFeedback.Instance.SpawnImpactEffect(impactType, other.ClosestPoint(transform.position), hitDirection);

                // Knockback
                if (_currentAttack?.HasKnockback == true)
                {
                    var targetRb = other.GetComponentInParent<Rigidbody>();
                    if (targetRb != null)
                    {
                        Vector3 knockback = GameMath.CalculateKnockback(hitDirection, _currentAttack.KnockbackForce, _currentAttack.KnockbackUpwardRatio);
                        targetRb.AddForce(knockback, ForceMode.Impulse);
                    }
                }

                // Stagger
                if (_currentAttack?.HasStagger == true)
                {
                    var targetStagger = other.GetComponentInParent<StaggerComponent>();
                    if (targetStagger != null)
                    {
                        targetStagger.AddStaggerDamage(_currentAttack.StaggerDamage);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Hitbox data for a specific attack hitbox.
    /// </summary>
    [System.Serializable]
    public class HitboxData
    {
        public string Name;
        public HitboxShape Shape;
        public Vector3 Center;
        public Vector3 Size;
        public float Radius;
        public float Length;
        public LayerMask TargetLayers;
    }

    /// <summary>
    /// Hurtbox component for receiving damage.
    /// Attached to the body of characters who can be hit.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        [SerializeField] private HealthComponent _healthComponent;
        [SerializeField] private bool _isParryableZone = false;
        [SerializeField] private float _damageMultiplier = 1f;

        public HealthComponent Health => _healthComponent;
        public bool IsParryableZone => _isParryableZone;
        public float DamageMultiplier => _damageMultiplier;

        private void Awake()
        {
            if (_healthComponent == null)
                _healthComponent = GetComponentInParent<HealthComponent>();
        }
    }

    /// <summary>
    /// Health component for managing health, death, and damage reception.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float HealthPercent => _currentHealth / _maxHealth;
        public bool IsAlive => _currentHealth > 0f;

        public System.Action<float, float> OnHealthChanged;
        public System.Action OnDeath;
        public System.Action<float, Vector3> OnDamageTaken;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        /// <summary>
        /// Take damage with direction for knockback and reactions.
        /// </summary>
        public void TakeDamage(float damage, Vector3 hitDirection, string source = "", int attackerId = 0, bool isCritical = false)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnDamageTaken?.Invoke(damage, hitDirection);

            EventBus.Publish(new DamageEvent
            {
                TargetInstanceId = gameObject.GetInstanceID(),
                Damage = damage,
                IsCritical = isCritical,
                HitPosition = transform.position + Vector3.up,
                HitDirection = hitDirection,
                DamageSource = source,
                AttackerInstanceId = attackerId
            });

            if (_currentHealth <= 0f)
            {
                OnDeath?.Invoke();
            }
        }

        /// <summary>
        /// Heal the character.
        /// </summary>
        public void Heal(float amount)
        {
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        /// <summary>
        /// Set max health (for relic modifications).
        /// </summary>
        public void SetMaxHealth(float newMax)
        {
            _maxHealth = newMax;
            _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
        }
    }

    /// <summary>
    /// Stagger component for managing stagger buildup and staggered state.
    /// Enemies accumulate stagger damage; when threshold is reached, they enter staggered state.
    /// </summary>
    public class StaggerComponent : MonoBehaviour
    {
        [SerializeField] private float _staggerThreshold = 50f;
        [SerializeField] private float _staggerDuration = 2f;
        [SerializeField] private float _staggerDecayRate = 5f;
        [SerializeField] private float _currentStaggerDamage;

        public float CurrentStaggerDamage => _currentStaggerDamage;
        public float StaggerThreshold => _staggerThreshold;
        public bool IsStaggered => _currentStaggerDamage >= _staggerThreshold;
        public float StaggerPercent => _currentStaggerDamage / _staggerThreshold;

        public System.Action OnStaggered;

        /// <summary>
        /// Add stagger damage. Triggers stagger when threshold is reached.
        /// </summary>
        public void AddStaggerDamage(float damage)
        {
            _currentStaggerDamage += damage;

            if (_currentStaggerDamage >= _staggerThreshold)
            {
                _currentStaggerDamage = _staggerThreshold;
                OnStaggered?.Invoke();
                EventBus.Publish(new EnemyStaggerEvent
                {
                    EnemyInstanceId = gameObject.GetInstanceID(),
                    StaggerDuration = _staggerDuration
                });
            }
        }

        /// <summary>
        /// Decay stagger over time.
        /// </summary>
        public void DecayStagger(float dt)
        {
            if (!IsStaggered)
                _currentStaggerDamage = Mathf.Max(0, _currentStaggerDamage - _staggerDecayRate * dt);
        }

        /// <summary>
        /// Reset stagger completely.
        /// </summary>
        public void ResetStagger()
        {
            _currentStaggerDamage = 0f;
        }
    }
}
