using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Combat;

namespace Relicfall.Player
{
    /// <summary>
    /// Weapon definition ScriptableObject. Each weapon family has unique moves,
    /// timing, and feel. Weapons are unlocked through permanent progression.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDef", menuName = "RELICFALL/Weapons/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string WeaponId;
        public string WeaponName;
        public string Description;
        public WeaponFamily Family;

        [Header("Visuals")]
        public GameObject WeaponModelPrefab;
        public Sprite Icon;
        public string WeaponTrailMaterialId;
        public Color WeaponEnergyColor;

        [Header("Audio")]
        public string SwingSoundId;
        public string HeavySwingSoundId;
        public string ImpactSoundId;
        public string HeavyImpactSoundId;
        public string EquipSoundId;

        [Header("Combat Parameters")]
        public float BaseDamage = 10f;
        public float HeavyDamageMultiplier = 2f;
        public float AttackSpeedMultiplier = 1f;
        public float CriticalChance = 0.05f;
        public float CriticalMultiplier = 2f;
        public float StaggerDamageMultiplier = 1f;
        public float Range = 2f;
        public float AttackAngle = 90f;
        public float DashAttackDamageMultiplier = 0.7f;

        [Header("Combo")]
        public int MaxComboSteps = 3;
        public float ComboResetWindow = 0.8f;
        public float ComboDamageIncreasePerStep = 0.15f;
        public float ComboSpeedIncreasePerStep = 0.1f;

        [Header("Heavy Attack")]
        public float HeavyAttackDamage = 20f;
        public float HeavyAttackSpeed = 0.55f;
        public float HeavyChargeDuration = 0.6f;
        public float HeavyKnockbackMultiplier = 2f;
        public float HeavyStaggerMultiplier = 2f;

        [Header("Special")]
        public float DashSpeedMultiplier = 1.5f;
        public float DashDamageMultiplier = 0.7f;
        public float ParryWindowBonus = 0f;

        [Header("Relic Interactions")]
        public string[] CompatibleRelicTags;
        public string[] IncompatibleRelicTags;

        [Header("Upgrades")]
        public WeaponUpgradeDefinition[] AvailableUpgrades;

        [Header("Unlock")]
        public bool IsDefaultWeapon = false;
        public string UnlockRequirement;
        public int UnlockCost = 0;
    }

    public enum WeaponFamily
    {
        ChainBlade,
        GreatBlade,
        ArcanePistolDagger
    }

    /// <summary>
    /// Weapon upgrade definition. Each upgrade modifies weapon behavior significantly.
    /// Not just +5% damage; upgrades alter moves and mechanics.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponUpgrade", menuName = "RELICFALL/Weapons/Weapon Upgrade")]
    public class WeaponUpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string UpgradeId;
        public string UpgradeName;
        public string Description;
        public Sprite Icon;
        public int Tier = 1;
        public string RequiredWeaponId;

        [Header("Modification")]
        public UpgradeType Type;
        public float Value;
        public string ModifiedMoveName;
        public bool AddsNewMove = false;
        public string NewMoveName;
        public AttackDefinition NewAttackDef;
        public string[] AddedTags;

        [Header("Requirements")]
        public string[] RequiredUpgradeIds;
        public int Cost = 1;

        public enum UpgradeType
        {
            NewComboStep,
            ModifiedComboStep,
            NewHeavyAttack,
            ModifiedHeavyAttack,
            NewSpecialMove,
            DamageModifier,
            SpeedModifier,
            RangeModifier,
            NewDashAttack,
            NewChargedAttack,
            NewPassiveEffect,
            NewRelicInteraction
        }
    }

    /// <summary>
    /// Runtime weapon handler that manages weapon state, trails, and model switching.
    /// Coordinates with the player controller for attack execution.
    /// </summary>
    public class WeaponHandler : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition _currentWeaponDef;
        [SerializeField] private Transform _weaponAttachPoint;
        [SerializeField] private GameObject _weaponModelInstance;
        [SerializeField] private TrailRenderer _weaponTrail;
        [SerializeField] private float _trailDuration = 0.2f;

        private WeaponFamily _currentFamily;
        private Dictionary<string, bool> _activeUpgrades = new();
        private Dictionary<string, float> _upgradeValues = new();
        private List<string> _currentRunUpgrades = new();

        public string CurrentWeaponId => _currentWeaponDef?.WeaponId ?? "chain_blade";
        public WeaponFamily CurrentFamily => _currentFamily;
        public WeaponDefinition CurrentWeaponDef => _currentWeaponDef;
        public List<string> ActiveUpgrades => _currentRunUpgrades;

        /// <summary>
        /// Equip a new weapon. Swaps model and updates combat parameters.
        /// </summary>
        public void EquipWeapon(WeaponDefinition weaponDef)
        {
            if (_currentWeaponDef != null)
                EventBus.Publish(new WeaponChangeEvent 
                { 
                    PreviousWeaponId = _currentWeaponDef.WeaponId, 
                    NewWeaponId = weaponDef.WeaponId 
                });

            _currentWeaponDef = weaponDef;
            _currentFamily = weaponDef.Family;

            // Swap weapon model
            if (_weaponModelInstance != null)
                Destroy(_weaponModelInstance);

            if (weaponDef.WeaponModelPrefab != null && _weaponAttachPoint != null)
            {
                _weaponModelInstance = Instantiate(weaponDef.WeaponModelPrefab, _weaponAttachPoint);
                _weaponModelInstance.transform.localPosition = Vector3.zero;
                _weaponModelInstance.transform.localRotation = Quaternion.identity;
            }

            // Configure trail
            if (_weaponTrail != null)
            {
                _weaponTrail.material = Resources.Load<Material>(weaponDef.WeaponTrailMaterialId);
                _weaponTrail.startColor = weaponDef.WeaponEnergyColor;
                _weaponTrail.endColor = new Color(weaponDef.WeaponEnergyColor.r, weaponDef.WeaponEnergyColor.g, weaponDef.WeaponEnergyColor.b, 0f);
            }
        }

        /// <summary>
        /// Start weapon trail for an attack.
        /// </summary>
        public void StartTrail()
        {
            if (_weaponTrail != null)
            {
                _weaponTrail.enabled = true;
                _weaponTrail.emitting = true;
                _weaponTrail.time = _trailDuration;
            }
        }

        /// <summary>
        /// Stop weapon trail after an attack.
        /// </summary>
        public void StopTrail()
        {
            if (_weaponTrail != null)
            {
                _weaponTrail.emitting = false;
            }
        }

        /// <summary>
        /// Create a dash trail effect at the current position.
        /// </summary>
        public void CreateDashTrail(Vector3 position)
        {
            if (_currentWeaponDef == null) return;

            var trailPrefab = Resources.Load<GameObject>($"VFX/DashTrail_{_currentWeaponDef.WeaponId}");
            if (trailPrefab != null)
            {
                var trail = Instantiate(trailPrefab, position, Quaternion.identity);
                Destroy(trail, 0.5f);
            }
        }

        /// <summary>
        /// Apply a weapon upgrade during a run.
        /// </summary>
        public void ApplyUpgrade(WeaponUpgradeDefinition upgrade)
        {
            if (upgrade.RequiredWeaponId != _currentWeaponDef.WeaponId) return;

            _currentRunUpgrades.Add(upgrade.UpgradeId);
            _activeUpgrades[upgrade.UpgradeId] = true;
            _upgradeValues[upgrade.UpgradeId] = upgrade.Value;

            // Apply the upgrade effect
            switch (upgrade.Type)
            {
                case WeaponUpgradeDefinition.UpgradeType.DamageModifier:
                    // Increase damage by value percent
                    break;
                case WeaponUpgradeDefinition.UpgradeType.SpeedModifier:
                    // Increase attack speed
                    break;
                case WeaponUpgradeDefinition.UpgradeType.RangeModifier:
                    // Increase weapon range
                    break;
                case WeaponUpgradeDefinition.UpgradeType.NewComboStep:
                    // Add a new combo step
                    break;
                case WeaponUpgradeDefinition.UpgradeType.NewSpecialMove:
                    // Add a new special move
                    break;
            }
        }

        /// <summary>
        /// Get modified damage for an attack based on upgrades and relics.
        /// </summary>
        public float GetModifiedDamage(float baseDamage, int comboStep = 0)
        {
            float damage = baseDamage;

            // Combo damage increase
            if (comboStep > 0 && _currentWeaponDef != null)
                damage *= 1f + _currentWeaponDef.ComboDamageIncreasePerStep * comboStep;

            // Upgrade modifiers
            foreach (var kvp in _activeUpgrades)
            {
                // Apply each upgrade's modification
            }

            return damage;
        }

        /// <summary>
        /// Reset all run-specific upgrades (when starting a new run).
        /// </summary>
        public void ResetRunUpgrades()
        {
            _currentRunUpgrades.Clear();
            _activeUpgrades.Clear();
            _upgradeValues.Clear();
        }

        // === Weapon-Specific Moves ===

        /// <summary>
        /// Chain Blade: Chain Pull / Tether attack.
        /// Pulls a target toward the player.
        /// </summary>
        public void ChainBladePull(Transform target)
        {
            if (_currentFamily != WeaponFamily.ChainBlade) return;

            var dir = (transform.position - target.position).normalized;
            float pullForce = 15f;

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(dir * pullForce, ForceMode.Impulse);

            // Visual tether line
            EventBus.Publish(new SFXPlayEvent { SfxId = "chainblade_pull", Position = transform.position });
        }

        /// <summary>
        /// Chain Blade: Area Spin attack.
        /// 360-degree spinning attack that hits all nearby enemies.
        /// </summary>
        public void ChainBladeSpin(float damage)
        {
            if (_currentFamily != WeaponFamily.ChainBlade) return;

            float radius = _currentWeaponDef?.Range * 1.5f ?? 3f;
            var hits = Physics.OverlapSphere(transform.position, radius, LayerMask.GetMask("EnemyHitbox"));

            foreach (var hit in hits)
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    Vector3 dir = (hit.transform.position - transform.position).normalized;
                    hurtbox.Health.TakeDamage(damage, dir, "chainblade_spin", gameObject.GetInstanceID());
                }
            }

            CombatFeedback.Instance.TriggerHitStop(ImpactType.Heavy);
            CombatFeedback.Instance.TriggerCameraShake(0.4f, 6f);

            EventBus.Publish(new SFXPlayEvent { SfxId = "chainblade_spin", Position = transform.position });
        }

        /// <summary>
        /// Great Blade: Shockwave upgrade path.
        /// Creates a ground shockwave from heavy attack impact.
        /// </summary>
        public void GreatBladeShockwave(float damage, Vector3 direction)
        {
            if (_currentFamily != WeaponFamily.GreatBlade) return;

            float radius = 4f;
            float forwardRange = 6f;
            Vector3 center = transform.position + direction * forwardRange * 0.5f;

            var hits = Physics.OverlapCapsule(transform.position, transform.position + direction * forwardRange, radius, LayerMask.GetMask("EnemyHitbox"));

            foreach (var hit in hits)
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    float dist = Vector3.Distance(hit.transform.position, transform.position);
                    float falloff = 1f - (dist / forwardRange);
                    hurtbox.Health.TakeDamage(damage * falloff, direction, "greatblade_shockwave", gameObject.GetInstanceID());
                }
            }

            CombatFeedback.Instance.TriggerHitStop(ImpactType.Heavy);
            CombatFeedback.Instance.TriggerCameraShake(0.6f, 8f);

            EventBus.Publish(new SFXPlayEvent { SfxId = "greatblade_shockwave", Position = transform.position });
        }

        /// <summary>
        /// Arcane Pistol & Dagger: Mark target for execution.
        /// </summary>
        public void MarkTarget(Transform target)
        {
            if (_currentFamily != WeaponFamily.ArcanePistolDagger) return;

            var markComponent = target.GetComponent<ExecutionMark>();
            if (markComponent == null)
                markComponent = target.gameObject.AddComponent<ExecutionMark>();

            markComponent.Mark(gameObject.GetInstanceID());
        }

        /// <summary>
        /// Arcane Pistol & Dagger: Execute marked target.
        /// </summary>
        public void ExecuteMarked(Transform target)
        {
            var mark = target.GetComponent<ExecutionMark>();
            if (mark == null || !mark.IsMarked) return;

            // High damage execution
            float executionDamage = 50f;
            var health = target.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(executionDamage, transform.forward, "arcane_execute", gameObject.GetInstanceID(), true);
            }

            mark.ClearMark();
            CombatFeedback.Instance.TriggerHitStop(ImpactType.Execution);
            CombatFeedback.Instance.TriggerCameraShake(0.5f, 7f);
        }

        /// <summary>
        /// Arcane Pistol & Dagger: Dash Shot.
        /// Fire a short-range shot while dashing.
        /// </summary>
        public void DashShot(Vector3 direction, float damage)
        {
            if (_currentFamily != WeaponFamily.ArcanePistolDagger) return;

            // Create projectile
            var projectilePrefab = Resources.Load<GameObject>("VFX/ArcaneBullet");
            if (projectilePrefab != null)
            {
                var bullet = Instantiate(projectilePrefab, transform.position + Vector3.up * 1f, Quaternion.LookRotation(direction));
                var projComponent = bullet.AddComponent<Projectile>();
                projComponent.Initialize(direction, damage * 0.5f, 8f, 15f, gameObject.GetInstanceID());
                Destroy(bullet, 1f);
            }
        }
    }

    /// <summary>
    /// Execution mark component for the Arcane Pistol & Dagger weapon.
    /// </summary>
    public class ExecutionMark : MonoBehaviour
    {
        public bool IsMarked { get; private set; }
        public int MarkerInstanceId { get; private set; }
        public float MarkTimer { get; private set; }

        private float _markDuration = 5f;

        public void Mark(int markerInstanceId)
        {
            IsMarked = true;
            MarkerInstanceId = markerInstanceId;
            MarkTimer = _markDuration;
        }

        public void ClearMark()
        {
            IsMarked = false;
            MarkerInstanceId = 0;
            MarkTimer = 0f;
        }

        private void Update()
        {
            if (IsMarked)
            {
                MarkTimer -= Time.deltaTime;
                if (MarkTimer <= 0f)
                    ClearMark();
            }
        }
    }

    /// <summary>
    /// Simple projectile component for ranged attacks.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private Vector3 _direction;
        private float _damage;
        private float _speed;
        private float _lifetime;
        private int _ownerInstanceId;
        private float _age;

        public void Initialize(Vector3 direction, float damage, float speed, float lifetime, int ownerInstanceId)
        {
            _direction = direction.normalized;
            _damage = damage;
            _speed = speed;
            _lifetime = lifetime;
            _ownerInstanceId = ownerInstanceId;
            _age = 0f;
        }

        private void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;
            _age += Time.deltaTime;

            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Check for hits
            var hits = Physics.OverlapSphere(transform.position, 0.3f, LayerMask.GetMask("EnemyHitbox"));
            foreach (var hit in hits)
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    hurtbox.Health.TakeDamage(_damage, _direction, "projectile", _ownerInstanceId);
                    CombatFeedback.Instance.SpawnImpactEffect(ImpactType.Light, transform.position, _direction);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
