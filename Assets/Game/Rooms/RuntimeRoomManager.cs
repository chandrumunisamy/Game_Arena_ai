using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Corruption;
using Relicfall.Runs;
using Relicfall.Enemies;
using Relicfall.Relics;
using Relicfall.Combat;

namespace Relicfall.Rooms
{
    /// <summary>
    /// Runtime room manager that handles room loading, encounter spawning,
    /// completion, and transitions between rooms during a run.
    /// </summary>
    public class RuntimeRoomManager : MonoBehaviour
    {
        [Header("Room Pool")]
        [SerializeField] private RoomDefinition[] _shatteredCourtRooms;
        [SerializeField] private RoomDefinition[] _drownedDominionRooms;
        [SerializeField] private RoomDefinition[] _verdantMawRooms;
        [SerializeField] private RoomDefinition _bossArenaTemplate;

        [Header("Current Room")]
        [SerializeField] private GameObject _currentRoomInstance;
        [SerializeField] private RoomDefinition _currentRoomDefinition;
        [SerializeField] private EncounterDefinition _currentEncounter;

        private List<EnemyController> _activeEnemies = new();
        private EnemyGroupCoordinator _enemyCoordinator;
        private CorruptionTracker _corruption;
        private RelicManager _relicManager;
        private RunData _currentRun;
        private bool _roomCompleted;
        private float _roomTimer;

        public RoomDefinition CurrentRoom => _currentRoomDefinition;
        public bool IsRoomCompleted => _roomCompleted;
        public List<EnemyController> ActiveEnemies => _activeEnemies;
        public int RemainingEnemyCount => _activeEnemies.Count;

        public System.Action<RoomDefinition> OnRoomLoaded;
        public System.Action OnRoomCompleted;
        public System.Action<RouteNode[]> OnRouteChoiceOffered;

        private void Start()
        {
            _corruption = new CorruptionTracker();
            _relicManager = GetComponent<RelicManager>() ?? gameObject.AddComponent<RelicManager>();

            EventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        /// <summary>
        /// Load a room for the current run position.
        /// </summary>
        public void LoadRoom(RouteNode routeNode)
        {
            // Clean up previous room
            if (_currentRoomInstance != null)
                Destroy(_currentRoomInstance);

            _roomCompleted = false;
            _roomTimer = 0f;

            // Select room definition based on type and realm
            var roomDef = SelectRoomDefinition(routeNode);
            _currentRoomDefinition = roomDef;

            // Instantiate room
            if (roomDef?.RoomPrefab != null)
            {
                _currentRoomInstance = Instantiate(roomDef.RoomPrefab);
            }
            else
            {
                // Generate a procedural room from template
                _currentRoomInstance = GenerateRoomFromTemplate(routeNode, roomDef);
            }

            // Apply corruption visual modifications
            ApplyCorruptionToRoom();

            // Select and spawn encounter
            var encounter = SelectEncounter(routeNode, roomDef);
            _currentEncounter = encounter;
            SpawnEncounter(encounter);

            // Publish room entered event
            EventBus.Publish(new RoomEnteredEvent
            {
                RoomId = routeNode.NodeId,
                RoomType = routeNode.Type.ToString(),
                CorruptionAtEntry = _corruption.CurrentLevel
            });

            OnRoomLoaded?.Invoke(roomDef);
        }

        private RoomDefinition SelectRoomDefinition(RouteNode routeNode)
        {
            // Select from appropriate realm pool
            var realm = Core.GameManager.Instance?.CurrentRun?.RealmId;
            RoomDefinition[] pool = realm == "shattered_court" ? _shatteredCourtRooms :
                                     realm == "drowned_dominion" ? _drownedDominionRooms :
                                     realm == "verdant_maw" ? _verdantMawRooms :
                                     _shatteredCourtRooms;

            // Filter by room type
            var candidates = new List<RoomDefinition>();
            foreach (var room in pool)
            {
                if (room.Type == routeNode.Type)
                    candidates.Add(room);
            }

            if (candidates.Count == 0)
            {
                // Fallback: use any room of close type
                foreach (var room in pool)
                    candidates.Add(room);
            }

            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        private EncounterDefinition SelectEncounter(RouteNode routeNode, RoomDefinition roomDef)
        {
            // Use corruption variant if corruption is high enough
            float corruption = _corruption.CurrentLevel;
            if (roomDef != null && corruption >= roomDef.CorruptionThresholdForVariant && roomDef.CorruptionEncounter != null)
                return roomDef.CorruptionEncounter;

            // Use normal encounter
            if (roomDef?.NormalEncounter != null)
                return roomDef.NormalEncounter;

            // Generate encounter based on route node type
            return GenerateEncounterForType(routeNode);
        }

        private EncounterDefinition GenerateEncounterForType(RouteNode routeNode)
        {
            var encounter = EncounterDefinition.CreateInstance<EncounterDefinition>();
            encounter.EncounterId = $"encounter_{routeNode.NodeId}";
            encounter.DifficultyLevel = (int)routeNode.DangerLevel;

            switch (routeNode.Type)
            {
                case RoomType.Combat:
                    encounter.EnemySpawns = GenerateCombatSpawns(routeNode.DangerLevel);
                    encounter.MaxConcurrentEnemies = 4;
                    break;
                case RoomType.EliteCombat:
                    encounter.EnemySpawns = GenerateEliteSpawns(routeNode.DangerLevel);
                    encounter.MaxConcurrentEnemies = 3;
                    encounter.EliteSpawnChance = 1f;
                    break;
                case RoomType.RiskRoom:
                    encounter.EnemySpawns = GenerateRiskRoomSpawns(routeNode.DangerLevel);
                    encounter.MaxConcurrentEnemies = 6;
                    break;
                case RoomType.StartRoom:
                    encounter.EnemySpawns = GenerateStartRoomSpawns();
                    encounter.MaxConcurrentEnemies = 3;
                    break;
            }

            return encounter;
        }

        private EnemySpawnEntry[] GenerateCombatSpawns(float danger)
        {
            int count = Mathf.Clamp((int)(danger * 1.5f), 3, 8);
            var spawns = new EnemySpawnEntry[count];
            for (int i = 0; i < count; i++)
            {
                spawns[i] = new EnemySpawnEntry
                {
                    EnemyDefinitionId = GetRandomEnemyId(),
                    Count = 1,
                    SpawnDelay = i * 0.5f,
                    IsRequired = true
                };
            }
            return spawns;
        }

        private EnemySpawnEntry[] GenerateEliteSpawns(float danger)
        {
            var spawns = new EnemySpawnEntry[3];
            spawns[0] = new EnemySpawnEntry
            {
                EnemyDefinitionId = GetRandomEnemyId(),
                Count = 2,
                SpawnDelay = 0f,
                IsRequired = true
            };
            spawns[1] = new EnemySpawnEntry
            {
                EnemyDefinitionId = GetRandomEnemyId(),
                Count = 1,
                SpawnDelay = 1f,
                IsElite = true,
                EliteModifier = EliteModifier.Frenzied,
                IsRequired = true
            };
            spawns[2] = new EnemySpawnEntry
            {
                EnemyDefinitionId = GetRandomEnemyId(),
                Count = 1,
                SpawnDelay = 2f
            };
            return spawns;
        }

        private EnemySpawnEntry[] GenerateRiskRoomSpawns(float danger)
        {
            int count = Mathf.Clamp((int)(danger * 2f), 4, 10);
            var spawns = new EnemySpawnEntry[count];
            for (int i = 0; i < count; i++)
            {
                spawns[i] = new EnemySpawnEntry
                {
                    EnemyDefinitionId = GetRandomEnemyId(),
                    Count = 1,
                    SpawnDelay = i * 0.3f,
                    IsRequired = true,
                    CorruptionVariantChance = 0.3f
                };
            }
            return spawns;
        }

        private EnemySpawnEntry[] GenerateStartRoomSpawns()
        {
            return new EnemySpawnEntry[]
            {
                new EnemySpawnEntry { EnemyDefinitionId = "sword_guard", Count = 2, SpawnDelay = 0f, IsRequired = true },
                new EnemySpawnEntry { EnemyDefinitionId = "sword_guard", Count = 1, SpawnDelay = 1f }
            };
        }

        private string GetRandomEnemyId()
        {
            string[] basicEnemies = { "sword_guard", "shield_guard", "spear_guard", "archer" };
            string[] advancedEnemies = { "corrupted_mage", "heavy_knight", "assassin" };
            string[] specialEnemies = { "summoner", "living_statue", "corruption_beast" };

            float corruption = _corruption.CurrentLevel;

            if (corruption >= 75f && Random.value < 0.3f)
                return specialEnemies[Random.Range(0, specialEnemies.Length)];
            if (corruption >= 50f && Random.value < 0.4f)
                return advancedEnemies[Random.Range(0, advancedEnemies.Length)];
            if (Random.value < 0.7f)
                return basicEnemies[Random.Range(0, basicEnemies.Length)];
            return advancedEnemies[Random.Range(0, advancedEnemies.Length)];
        }

        private void SpawnEncounter(EncounterDefinition encounter)
        {
            if (encounter?.EnemySpawns == null) return;

            _activeEnemies.Clear();
            _enemyCoordinator = gameObject.AddComponent<EnemyGroupCoordinator>();

            foreach (var spawn in encounter.EnemySpawns)
            {
                for (int i = 0; i < spawn.Count; i++)
                {
                    SpawnEnemy(spawn, i);
                }
            }
        }

        private void SpawnEnemy(EnemySpawnEntry spawn, int index)
        {
            // Load enemy prefab and instantiate
            var enemyPrefab = Resources.Load<GameObject>($"Enemies/{spawn.EnemyDefinitionId}");
            if (enemyPrefab == null)
            {
                // Create a basic enemy as fallback
                enemyPrefab = CreateBasicEnemyPrefab(spawn.EnemyDefinitionId);
            }

            // Find spawn point
            Vector3 spawnPos = GetSpawnPosition(index);

            var enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            var enemyController = enemyObj.GetComponent<EnemyController>();
            if (enemyController == null)
                enemyController = enemyObj.AddComponent<EnemyController>();

            // Configure enemy
            var enemyDef = LoadEnemyDefinition(spawn.EnemyDefinitionId);
            enemyController.SetCorruption(_corruption.CurrentLevel);

            if (spawn.IsElite)
            {
                enemyController.ConfigureElite(spawn.EliteModifier);
            }

            _activeEnemies.Add(enemyController);
            _enemyCoordinator.RegisterEnemy(enemyController);

            // Play spawn sound
            EventBus.Publish(new SFXPlayEvent { SfxId = "enemy_spawn", Position = spawnPos });
        }

        private Vector3 GetSpawnPosition(int index)
        {
            // Generate spawn positions around the room perimeter
            float radius = 8f;
            float angle = (index * 45f) + Random.Range(-10f, 10f);
            return new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );
        }

        private GameObject CreateBasicEnemyPrefab(string enemyType)
        {
            var obj = new GameObject($"Enemy_{enemyType}");
            obj.layer = 9; // EnemyHitbox

            // Add required components
            var marker = obj.AddComponent<EnemyMarker>();
            marker.EnemyType = enemyType;

            var health = obj.AddComponent<HealthComponent>();
            var stagger = obj.AddComponent<StaggerComponent>();
            var hurtboxCollider = obj.AddComponent<CapsuleCollider>();
            hurtboxCollider.radius = 0.3f;
            hurtboxCollider.height = 1.8f;
            hurtboxCollider.isTrigger = true;

            // Create visual representation
            var visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualObj.transform.SetParent(obj.transform);
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            // Remove primitive collider from visual (using trigger collider on parent)
            Destroy(visualObj.GetComponent<CapsuleCollider>());

            // Color based on enemy type
            var renderer = visualObj.GetComponent<Renderer>();
            renderer.material.color = GetEnemyColor(enemyType);

            return obj;
        }

        private Color GetEnemyColor(string enemyType)
        {
            return enemyType switch
            {
                "sword_guard" => new Color(0.4f, 0.4f, 0.5f),
                "shield_guard" => new Color(0.5f, 0.5f, 0.55f),
                "spear_guard" => new Color(0.45f, 0.45f, 0.5f),
                "archer" => new Color(0.35f, 0.4f, 0.45f),
                "corrupted_mage" => new Color(0.6f, 0.2f, 0.4f),
                "heavy_knight" => new Color(0.3f, 0.3f, 0.4f),
                "assassin" => new Color(0.2f, 0.2f, 0.25f),
                "summoner" => new Color(0.5f, 0.15f, 0.3f),
                "living_statue" => new Color(0.6f, 0.6f, 0.55f),
                "corruption_beast" => new Color(0.8f, 0.2f, 0.3f),
                _ => new Color(0.4f, 0.4f, 0.45f)
            };
        }

        private EnemyDefinition LoadEnemyDefinition(string enemyId)
        {
            // Load from Resources or create default
            var def = Resources.Load<EnemyDefinition>($"EnemyDefinitions/{enemyId}");
            if (def != null) return def;

            // Create default definition
            def = EnemyDefinition.CreateInstance<EnemyDefinition>();
            def.EnemyId = enemyId;
            def.BaseHealth = 50f;
            def.BaseDamage = 8f;
            def.BaseSpeed = 2f;
            def.DetectionRange = 8f;
            def.AttackRange = 1.5f;
            def.TelegraphDuration = 0.5f;
            return def;
        }

        private GameObject GenerateRoomFromTemplate(RouteNode routeNode, RoomDefinition roomDef)
        {
            var roomObj = new GameObject($"Room_{routeNode.NodeId}");

            // Create floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(roomObj.transform);
            floor.transform.localScale = new Vector3(2f, 1f, 2f); // 20x20 unit floor
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.material.color = GetRealmFloorColor();

            // Create walls
            CreateArenaWalls(roomObj.transform, 20f);

            // Create spawn point markers
            for (int i = 0; i < 8; i++)
            {
                var marker = new GameObject($"SpawnPoint_{i}");
                marker.transform.SetParent(roomObj.transform);
                float angle = i * 45f;
                marker.transform.position = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 8f, 0, Mathf.Sin(angle * Mathf.Deg2Rad) * 8f);
            }

            // Add environmental props based on realm
            AddRealmProps(roomObj.transform, routeNode);

            return roomObj;
        }

        private void CreateArenaWalls(Transform parent, float size)
        {
            float wallHeight = 4f;
            float wallThickness = 0.5f;

            // Create 4 walls
            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(parent);
                wall.layer = 14; // Environment layer

                float angle = i * 90f;
                Vector3 wallPos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * size,
                    wallHeight / 2f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * size
                );
                wall.transform.position = wallPos;
                wall.transform.rotation = Quaternion.Euler(0, angle + 90f, 0);
                wall.transform.localScale = new Vector3(size * 2f, wallHeight, wallThickness);

                var wallRenderer = wall.GetComponent<Renderer>();
                wallRenderer.material.color = new Color(0.3f, 0.3f, 0.35f);
            }
        }

        private Color GetRealmFloorColor()
        {
            var realm = Core.GameManager.Instance?.CurrentRun?.RealmId;
            return realm switch
            {
                "shattered_court" => new Color(0.25f, 0.22f, 0.2f),
                "drowned_dominion" => new Color(0.15f, 0.2f, 0.25f),
                "verdant_maw" => new Color(0.2f, 0.25f, 0.18f),
                _ => new Color(0.25f, 0.22f, 0.2f)
            };
        }

        private void AddRealmProps(Transform parent, RouteNode routeNode)
        {
            // Add realm-specific environmental props
            // These are simple geometric representations for prototyping
            int propCount = Random.Range(3, 8);
            for (int i = 0; i < propCount; i++)
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.transform.SetParent(parent);
                prop.layer = 14; // Environment/Props

                float x = Random.Range(-8f, 8f);
                float z = Random.Range(-8f, 8f);
                float height = Random.Range(0.5f, 2f);
                prop.transform.position = new Vector3(x, height / 2f, z);
                prop.transform.localScale = new Vector3(
                    Random.Range(0.5f, 2f),
                    height,
                    Random.Range(0.5f, 2f)
                );

                var renderer = prop.GetComponent<Renderer>();
                renderer.material.color = new Color(0.35f, 0.3f, 0.25f);
            }
        }

        private void ApplyCorruptionToRoom()
        {
            float corruption = _corruption.CurrentLevel;
            if (corruption < 25f) return;

            // Add corruption visual elements
            // Floating debris
            int debrisCount = (int)(corruption / 10f);
            for (int i = 0; i < debrisCount; i++)
            {
                var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.transform.SetParent(_currentRoomInstance?.transform);
                float height = Random.Range(2f, 6f);
                debris.transform.position = new Vector3(
                    Random.Range(-8f, 8f),
                    height,
                    Random.Range(-8f, 8f)
                );
                debris.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                debris.GetComponent<Renderer>().material.color = new Color(0.85f, 0.2f, 0.3f, 0.5f);
                debris.GetComponent<Renderer>().material.SetFloat("_Mode", 3); // Transparent mode

                // Add slow floating animation
                var anim = debris.AddComponent<CorruptionDebrisAnimator>();
                anim.Initialize(height, Random.Range(0.2f, 0.5f));
            }

            // Floor cracks (ground telegraph decals)
            if (corruption >= 50f)
            {
                // Add red/crimson floor patches
                int crackCount = (int)(corruption / 15f);
                for (int i = 0; i < crackCount; i++)
                {
                    var crack = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    crack.transform.SetParent(_currentRoomInstance?.transform);
                    crack.transform.position = new Vector3(
                        Random.Range(-7f, 7f),
                        0.01f,
                        Random.Range(-7f, 7f)
                    );
                    crack.transform.localScale = new Vector3(
                        Random.Range(1f, 3f),
                        1f,
                        Random.Range(1f, 3f)
                    );
                    crack.GetComponent<Renderer>().material.color = new Color(0.5f, 0.1f, 0.15f, 0.4f);
                }
            }
        }

        private void OnEnemyDeath(EnemyDeathEvent e)
        {
            // Remove from active enemies
            _activeEnemies.RemoveAll(en => en != null && en.gameObject.GetInstanceID() == e.EnemyInstanceId);

            // Update coordinator
            var deadEnemy = _activeEnemies.Find(en => en.gameObject.GetInstanceID() == e.EnemyInstanceId);
            if (deadEnemy != null)
                _enemyCoordinator?.UnregisterEnemy(deadEnemy);

            // Check room completion
            if (_activeEnemies.Count == 0 || _activeEnemies.TrueForAll(en => en == null || !en.IsAlive))
            {
                CompleteRoom();
            }
        }

        private void OnBossDefeated(BossDefeatedEvent e)
        {
            CompleteRoom();
        }

        /// <summary>
        /// Complete the current room and offer next choices.
        /// </summary>
        private void CompleteRoom()
        {
            if (_roomCompleted) return;
            _roomCompleted = true;

            // Increase corruption
            _corruption.AddRoomCorruption(_currentRoomDefinition?.CorruptionIncrease ?? 5f);

            // Update run data
            if (_currentRun != null)
            {
                _currentRun.RoomsCompleted++;
                _currentRun.EnemiesKilled += _activeEnemies.Count;
            }

            EventBus.Publish(new RoomCompletedEvent
            {
                RoomId = _currentRoomDefinition?.RoomId ?? "unknown",
                RoomType = _currentRoomDefinition?.Type.ToString() ?? "unknown"
            });

            OnRoomCompleted?.Invoke();

            // Offer route choices or extraction
            OfferNextChoices();
        }

        private void OfferNextChoices()
        {
            var routeNode = _currentRun?.CurrentRoute;
            if (routeNode == null) return;

            // If this is an extraction point, offer extraction
            if (routeNode.IsExtractionPoint)
            {
                Core.GameManager.Instance?.OfferExtraction();
                return;
            }

            // If there are next routes, offer choice
            if (routeNode.NextRoutes.Count > 1)
            {
                OnRouteChoiceOffered?.Invoke(routeNode.NextRoutes.ToArray());
            }
            else if (routeNode.NextRoutes.Count == 1)
            {
                LoadRoom(routeNode.NextRoutes[0]);
            }
        }

        /// <summary>
        /// Set the current run data.
        /// </summary>
        public void SetCurrentRun(RunData run)
        {
            _currentRun = run;
        }

        private void Update()
        {
            _roomTimer += Time.deltaTime;

            // Update active enemies
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] == null)
                    _activeEnemies.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Simple animator for corruption debris floating in the air.
    /// </summary>
    public class CorruptionDebrisAnimator : MonoBehaviour
    {
        private float _baseHeight;
        private float _floatSpeed;
        private float _offset;

        public void Initialize(float baseHeight, float floatSpeed)
        {
            _baseHeight = baseHeight;
            _floatSpeed = floatSpeed;
            _offset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float y = _baseHeight + Mathf.Sin(Time.time * _floatSpeed + _offset) * 0.5f;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            transform.Rotate(0, 15f * Time.deltaTime, 0);
        }
    }
}
