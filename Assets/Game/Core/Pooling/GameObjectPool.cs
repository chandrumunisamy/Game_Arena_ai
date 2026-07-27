using System;
using System.Collections.Generic;
using UnityEngine;

namespace Relicfall.Core.Pooling
{
    /// <summary>
    /// Generic object pool for efficient reuse of frequently created/destroyed objects.
    /// Supports GameObjects, particles, projectiles, VFX, and other pooled items.
    /// </summary>
    public class GameObjectPool
    {
        private readonly Queue<GameObject> _available = new();
        private readonly HashSet<GameObject> _inUse = new();
        private readonly GameObject _prefab;
        private readonly Transform _poolRoot;
        private readonly int _maxSize;
        private int _currentSize;

        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;
        public int TotalCount => _currentSize;

        public GameObjectPool(GameObject prefab, Transform poolRoot, int initialSize = 10, int maxSize = 100)
        {
            _prefab = prefab;
            _poolRoot = poolRoot;
            _maxSize = maxSize;
            _currentSize = 0;

            // Pre-warm pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _available.Enqueue(obj);
            }
        }

        /// <summary>
        /// Get an object from the pool.
        /// </summary>
        public GameObject Get(Vector3 position = default, Quaternion rotation = default)
        {
            GameObject obj;

            if (_available.Count > 0)
            {
                obj = _available.Dequeue();
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
            }
            else if (_currentSize < _maxSize)
            {
                obj = CreateNew();
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            else
            {
                // Pool exhausted - reuse oldest active object
                obj = _available.Count > 0 ? _available.Dequeue() : null;
                if (obj == null)
                {
                    Debug.LogWarning($"Pool for {_prefab.name} exhausted (max: {_maxSize})");
                    return null;
                }
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }

            if (obj != null)
            {
                _inUse.Add(obj);
                var pooled = obj.GetComponent<PooledObject>();
                if (pooled != null)
                    pooled.OnPoolGet();
            }

            return obj;
        }

        /// <summary>
        /// Return an object to the pool.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (!_inUse.Contains(obj))
            {
                Debug.LogWarning($"Returning object not from this pool: {obj.name}");
                return;
            }

            _inUse.Remove(obj);
            _available.Enqueue(obj);
            obj.SetActive(false);
            obj.transform.SetParent(_poolRoot);

            var pooled = obj.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.OnPoolReturn();
        }

        /// <summary>
        /// Return all active objects to the pool.
        /// </summary>
        public void ReturnAll()
        {
            foreach (var obj in _inUse)
            {
                obj.SetActive(false);
                obj.transform.SetParent(_poolRoot);
                _available.Enqueue(obj);
            }
            _inUse.Clear();
        }

        private GameObject CreateNew()
        {
            var obj = UnityEngine.Object.Instantiate(_prefab, _poolRoot);
            obj.name = $"{_prefab.name}_pooled_{_currentSize}";
            _currentSize++;

            var pooled = obj.GetComponent<PooledObject>();
            if (pooled == null)
                pooled = obj.AddComponent<PooledObject>();
            pooled.Pool = this;

            return obj;
        }
    }

    /// <summary>
    /// Component attached to pooled objects to facilitate return to pool.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObjectPool Pool { get; set; }
        public float AutoReturnTime { get; set; } = -1f;
        private float _spawnTime;

        public void OnPoolGet()
        {
            _spawnTime = Time.time;
        }

        public void OnPoolReturn()
        {
            AutoReturnTime = -1f;
        }

        /// <summary>
        /// Return this object to its pool.
        /// </summary>
        public void ReturnToPool()
        {
            if (Pool != null)
                Pool.Return(gameObject);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// Schedule automatic return after specified duration.
        /// </summary>
        public void ScheduleReturn(float delay)
        {
            AutoReturnTime = delay;
        }

        private void Update()
        {
            if (AutoReturnTime > 0f && Time.time - _spawnTime >= AutoReturnTime)
            {
                ReturnToPool();
            }
        }
    }

    /// <summary>
    /// Central pool manager that manages multiple object pools.
    /// Attached to a GameObject in the scene.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager _instance;
        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PoolManager");
                    _instance = go.AddComponent<PoolManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, GameObjectPool> _pools = new();
        private Transform _poolRoot;

        private void Awake()
        {
            _poolRoot = transform;
        }

        /// <summary>
        /// Register a new pool for a prefab.
        /// </summary>
        public void RegisterPool(string poolId, GameObject prefab, int initialSize = 10, int maxSize = 100)
        {
            if (_pools.ContainsKey(poolId))
            {
                Debug.LogWarning($"Pool {poolId} already registered");
                return;
            }

            var pool = new GameObjectPool(prefab, _poolRoot, initialSize, maxSize);
            _pools[poolId] = pool;
        }

        /// <summary>
        /// Get a pooled object by pool ID.
        /// </summary>
        public GameObject Get(string poolId, Vector3 position = default, Quaternion rotation = default)
        {
            if (!_pools.TryGetValue(poolId, out var pool))
            {
                Debug.LogError($"Pool {poolId} not registered");
                return null;
            }
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return a pooled object by pool ID.
        /// </summary>
        public void Return(string poolId, GameObject obj)
        {
            if (!_pools.TryGetValue(poolId, out var pool))
            {
                Destroy(obj);
                return;
            }
            pool.Return(obj);
        }

        /// <summary>
        /// Return all objects in all pools.
        /// </summary>
        public void ReturnAll()
        {
            foreach (var pool in _pools.Values)
                pool.ReturnAll();
        }

        /// <summary>
        /// Get pool statistics.
        /// </summary>
        public string GetStats()
        {
            var result = "Pool Stats:\n";
            foreach (var kvp in _pools)
            {
                result += $"  {kvp.Key}: Available={kvp.Value.AvailableCount}, InUse={kvp.Value.InUseCount}, Total={kvp.Value.TotalCount}\n";
            }
            return result;
        }
    }
}
