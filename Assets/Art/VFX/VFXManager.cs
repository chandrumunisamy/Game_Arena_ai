using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Pooling;

using Relicfall.Core.Events;

using Relicfall.Combat;

using Relicfall.Corruption;

using Relicfall.Enemies;
using Relicfall.Runs;


using UnityEngine.Rendering;

using UnityEngine.Rendering.Universal;

namespace Relicfall.VFX
{
    /// <summary>
    /// VFX manager that handles visual effects for combat, relics, corruption,
    /// and environmental changes. Uses modular VFX with pooling for performance.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        [Header("Impact VFX")]
        [SerializeField] private GameObject _lightImpactPrefab;
        [SerializeField] private GameObject _heavyImpactPrefab;
        [SerializeField] private GameObject _criticalImpactPrefab;
        [SerializeField] private GameObject _parryImpactPrefab;

        [Header("Weapon Trails")]
        [SerializeField] private GameObject _chainBladeTrailPrefab;
        [SerializeField] private GameObject _greatBladeTrailPrefab;
        [SerializeField] private GameObject _pistolTrailPrefab;

        [Header("Relic VFX")]
        [SerializeField] private GameObject _relicPickupPrefab;
        [SerializeField] private GameObject _relicAuraPrefab;
        [SerializeField] private GameObject _corruptionPulsePrefab;

        [Header("Environmental VFX")]
        [SerializeField] private GameObject _corruptionCrackPrefab;
        [SerializeField] private GameObject _floatingDebrisPrefab;
        [SerializeField] private GameObject _portalPrefab;
        [SerializeField] private GameObject _groundTelegraphPrefab;

        [Header("Boss VFX")]
        [SerializeField] private GameObject _bossPhaseTransitionPrefab;
        [SerializeField] private GameObject _shockwaveRingPrefab;
        [SerializeField] private GameObject _arenaCollapsePrefab;

        private Dictionary<string, GameObjectPool> _vfxPools = new();

        public void PlayImpactEffect(ImpactType type, Vector3 position, Vector3 direction)
  {
            CombatFeedback.Instance.SpawnImpactEffect(type, position, direction);
        }

        public void PlayWeaponTrail(string weaponId, Vector3 start, Vector3 end)
        {
            string poolId = $"trail_{weaponId}";
            if (!_vfxPools.TryGetValue(poolId, out var pool))
            {
                var prefab = weaponId switch
                {
                    "chain_blade" => _chainBladeTrailPrefab,
                    "great_blade" => _greatBladeTrailPrefab,
                    "arcane_pistol_dagger" => _pistolTrailPrefab,
                    _ => _chainBladeTrailPrefab
                };
                pool = new GameObjectPool(prefab, transform, 5, 20);
                _vfxPools[poolId] = pool;
            }
            var trailObj = pool.Get(start, Quaternion.LookRotation(end - start));
            if (trailObj != null)
            {
                var trailRenderer = trailObj.GetComponent<TrailRenderer>();
                if (trailRenderer != null)
                    trailRenderer.SetPositions(start, end);
            }
        }

        public void PlayRelicPickup(string relicId, Vector3 position)
        {
            EventBus.Publish(new RelicCollectedEvent
            {
                RelicId = relicId,
                Rarity = "common",
                CorruptionIncrease = 0f
            });
        }

        public void PlayCorruptionPulse(Vector3 position, float intensity)
        {
            if (_corruptionPulsePrefab != null)
            {
                var pulse = Instantiate(_corruptionPulsePrefab, position, Quaternion.identity);
                pulse.transform.localScale *= intensity;
                Destroy(pulse, 2f);
            }
        }

        public void PlayGroundTelegraph(Vector3 position, Vector3 direction, float duration, float range)
        {
            if (_groundTelegraphPrefab != null)
            {
                var telegraph = Instantiate(_groundTelegraphPrefab, position, Quaternion.LookRotation(direction));
                var gt = telegraph.GetComponent<GroundTelegraph>();
                if (gt != null)
                    gt.Initialize(duration, direction, range);
                else
                    Destroy(telegraph, duration + 0.2f);
            }
        }

        public void PlayBossPhaseTransition(Vector3 position)
        {
            if (_bossPhaseTransitionPrefab != null)
            {
                var transition = Instantiate(_bossPhaseTransitionPrefab, position, Quaternion.identity);
                Destroy(transition, 3f);
            }
        }

        public void PlayShockwaveRing(Vector3 center, float radius)
        {
            if (_shockwaveRingPrefab != null)
            {
                var ring = Instantiate(_shockwaveRingPrefab, center, Quaternion.identity);
                ring.transform.localScale = Vector3.one * radius;
                Destroy(ring, 1.5f);
            }
        }
    }

    /// <summary>
    /// Simple trail renderer for weapon slashes.
    /// </summary>
    public class TrailRenderer : MonoBehaviour
    {
        [SerializeField] private float _trailDuration = 0.3f;
        [SerializeField] private float _trailWidth = 0.1f;
        [SerializeField] private Color _trailColor = new Color(0f, 0.9f, 1f, 0.7f);
        [SerializeField] private Material _trailMaterial;

        private List<Vector3> _positions = new();
        private float _timer;
        private LineRenderer _lineRenderer;

        public void SetPositions(Vector3 start, Vector3 end)
        {
            _positions.Clear();
            _positions.Add(start);
            _positions.Add(end);

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.startWidth = _trailWidth;
                _lineRenderer.endWidth = 0f;
                _lineRenderer.material = _trailMaterial ?? new Material(Shader.Find("Unlit/Color")) { color = _trailColor };
            }

            _lineRenderer.positionCount = _positions.Count;
            _lineRenderer.SetPositions(_positions.ToArray());
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _trailDuration)
                Destroy(gameObject);
        }
    }
}
