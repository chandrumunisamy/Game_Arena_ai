using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Relicfall.Core;
using Relicfall.Core.Camera;
using Relicfall.Core.Events;
using Relicfall.Core.Pooling;
using Relicfall.Core.Utils;
using Relicfall.Player;
using Relicfall.Combat;
using Relicfall.Corruption;
using Relicfall.Relics;
using Relicfall.Runs;
using Relicfall.Rooms;
using Relicfall.Audio;
using Relicfall.Saving;
using Relicfall.Settings;
using Relicfall.Narrative;
using Relicfall.Progression;
using Relicfall.UI;
using Relicfall.VFX;

namespace Relicfall.Core
{
    /// <summary>
    /// Scene bootstrap that creates and connects all game systems when a scene loads.
    /// This is the "glue" that connects all the modular systems together.
    /// Creates GameObjects, attaches components, sets up references, and initializes.
    /// 
    /// Attach this to a GameObject named "Bootstrap" in every scene.
    /// It will create all necessary managers and connections.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Scene Type")]
        [SerializeField] private SceneType _sceneType = SceneType.Hub;

        [Header("Player Configuration")]
        [SerializeField] private string _startingWeaponId = "chain_blade";
        [SerializeField] private float _playerMoveSpeed = 5f;
        [SerializeField] private float _playerMaxHealth = 100f;
        [SerializeField] private Vector3 _playerSpawnPosition = new Vector3(0f, 0f, 0f);

        [Header("Camera Configuration")]
        [SerializeField] private float _cameraHeight = 12f;
        [SerializeField] private float _cameraDistance = 10f;
        [SerializeField] private float _cameraAngle = 35f;
        [SerializeField] private Vector3 _cameraOffset = new Vector3(-3f, 12f, -6f);

        [Header("Arena Configuration")]
        [SerializeField] private float _arenaSize = 20f;
        [SerializeField] private float _wallHeight = 4f;
        [SerializeField] private Color _floorColor = new Color(0.25f, 0.22f, 0.2f);
        [SerializeField] private Color _wallColor = new Color(0.3f, 0.3f, 0.35f);

        [Header("Lighting")]
        [SerializeField] private Color _ambientColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private float _lightIntensity = 1f;
        [SerializeField] private Vector3 _lightDirection = new Vector3(50f, -30f, -45f);

        [Header("Enemy Spawn (Run Scenes)")]
        [SerializeField] private string[] _initialEnemyTypes = new[] { "sword_guard", "sword_guard", "spear_guard" };
        [SerializeField] private float _enemySpawnRadius = 8f;

        [Header("Hub Configuration")]
        [SerializeField] private Vector3 _forgePosition = new Vector3(-5f, 0f, 3f);
        [SerializeField] private Vector3 _archivePosition = new Vector3(5f, 0f, 3f);
        [SerializeField] private Vector3 _chapelPosition = new Vector3(0f, 0f, -5f);
        [SerializeField] private Vector3 _portalPosition = new Vector3(0f, 0f, 5f);

        public enum SceneType
        {
            Hub,
            Run,
            BossArena,
            Training
        }

        private GameObject _player;
        private PlayerController _playerController;
        private PlayerInputHandler _inputHandler;
        private WeaponHandler _weaponHandler;
        private HitboxManager _hitboxManager;
        private HealthComponent _playerHealth;
        private StaggerComponent _playerStagger;
        private RelicManager _relicManager;
        private IsometricCameraController _cameraController;
        private CombatFeedback _combatFeedback;
        private PoolManager _poolManager;
        private MusicSystem _musicSystem;
        private SFXManager _sfxManager;
        private VFXManager _vfxManager;
        private RuntimeRoomManager _roomManager;
        private NarrativeManager _narrativeManager;
        private GameManager _gameManager;
        private SaveManager _saveManager;
        private SettingsManager _settingsManager;

        private void Awake()
        {
            Debug.Log($"RELICFALL SceneBootstrap initializing ({_sceneType})");
            CreateAllSystems();
        }

        private void CreateAllSystems()
        {
            // 1. Global managers (singleton pattern - persists across scenes)
            CreateGlobalManagers();

            // 2. Scene-specific setup
            switch (_sceneType)
            {
                case SceneType.Hub:
                    SetupHubScene();
                    break;
                case SceneType.Run:
                    SetupRunScene();
                    break;
                case SceneType.BossArena:
                    SetupBossScene();
                    break;
                case SceneType.Training:
                    SetupTrainingScene();
                    break;
            }

            // 3. Connect all references
            ConnectReferences();

            // 4. Initialize all systems
            InitializeSystems();

            Debug.Log("RELICFALL SceneBootstrap complete - all systems initialized");
        }

        #region Global Managers

        private void CreateGlobalManagers()
        {
            // GameManager (singleton)
            var gameManagerObj = new GameObject("GameManager");
            _gameManager = gameManagerObj.AddComponent<GameManager>();
            DontDestroyOnLoad(gameManagerObj);

            // SaveManager (on GameManager)
            _saveManager = gameManagerObj.AddComponent<SaveManager>();

            // SettingsManager (on GameManager)
            _settingsManager = gameManagerObj.AddComponent<SettingsManager>();

            // PoolManager (singleton)
            _poolManager = PoolManager.Instance;

            // CombatFeedback (singleton)
            _combatFeedback = CombatFeedback.Instance;

            // NarrativeManager (singleton)
            var narrativeObj = new GameObject("NarrativeManager");
            _narrativeManager = narrativeObj.AddComponent<NarrativeManager>();
            DontDestroyOnLoad(narrativeObj);
        }

        #endregion

        #region Scene Setup

        private void SetupHubScene()
        {
            // Camera
            CreateCamera();

            // Lighting
            CreateLighting();

            // Hub floor and walls
            CreateHubEnvironment();

            // Hub interactable points
            CreateHubInteractables();

            // Player (for hub movement/preview)
            CreatePlayer();

            // UI - Hub overlay
            CreateHubUI();

            // Audio
            CreateAudioSystem();

            // VFX
            CreateVFXSystem();
        }

        private void SetupRunScene()
        {
            // Camera
            CreateCamera();

            // Lighting (corruption-aware)
            CreateLighting();

            // Run arena
            CreateRunArena();

            // Player
            CreatePlayer();

            // Room manager
            CreateRoomManager();

            // Spawn initial enemies
            SpawnInitialEnemies();

            // UI - HUD
            CreateHUD();

            // Audio
            CreateAudioSystem();

            // VFX
            CreateVFXSystem();
        }

        private void SetupBossScene()
        {
            // Camera
            CreateCamera();

            // Lighting (dramatic)
            CreateDramaticLighting();

            // Boss arena
            CreateBossArena();

            // Player
            CreatePlayer();

            // Boss
            // Boss is spawned by room manager or directly

            // UI - Boss HUD
            CreateBossHUD();

            // Audio
            CreateAudioSystem();

            // VFX
            CreateVFXSystem();
        }

        private void SetupTrainingScene()
        {
            CreateCamera();
            CreateLighting();
            CreateTrainingEnvironment();
            CreatePlayer();
            CreateHUD();
            CreateAudioSystem();
            CreateVFXSystem();

            // Spawn training dummy
            CreateTrainingDummy();
        }

        #endregion

        #region Create Functions

        private void CreateCamera()
        {
            var cameraObj = new GameObject("IsometricCamera");
            cameraObj.tag = "MainCamera";

            var cam = cameraObj.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // Add URP camera data
            var urpData = cameraObj.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderType = CameraRenderType.Base;
            urpData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urpData.antialiasingQuality = AntialiasingQuality.Medium;

            // Add audio listener
            cameraObj.AddComponent<AudioListener>();

            // Add isometric camera controller
            _cameraController = cameraObj.AddComponent<IsometricCameraController>();

            // Position camera for isometric view
            cameraObj.transform.position = _cameraOffset;
            cameraObj.transform.rotation = Quaternion.Euler(_cameraAngle, 45f, 0f);
        }

        private void CreateLighting()
        {
            // Directional light
            var lightObj = new GameObject("DirectionalLight");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = _ambientColor;
            light.intensity = _lightIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.LightShadowResolution.Medium;
            lightObj.transform.rotation = Quaternion.Euler(_lightDirection);

            // Ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientColor;

            // Fog (corruption-influenced)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.4f, 0.1f, 0.15f);
        }

        private void CreateDramaticLighting()
        {
            // Boss lighting - more dramatic, darker
            var lightObj = new GameObject("BossLight");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.25f, 0.15f, 0.2f);
            light.intensity = 0.8f;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.LightShadowResolution.High;
            lightObj.transform.rotation = Quaternion.Euler(60f, -45f, -30f);

            // Point light for boss glow
            var pointLightObj = new GameObject("BossGlow");
            var pointLight = pointLightObj.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(0.85f, 0.2f, 0.3f);
            pointLight.intensity = 2f;
            pointLight.range = 10f;
            pointLightObj.transform.position = new Vector3(0f, 3f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.1f, 0.15f);
            RenderSettings.fogDensity = 0.04f;
        }

        private void CreatePlayer()
        {
            _player = new GameObject("Player");
            _player.tag = "Player";
            _player.layer = 8; // Player layer

            // Player visual (hooded thief with cyan energy)
            var playerVisual = CreatePlayerVisual();

            // Add components in order
            _playerHealth = _player.AddComponent<HealthComponent>();
            _playerStagger = _player.AddComponent<StaggerComponent>();
            _inputHandler = _player.AddComponent<PlayerInputHandler>();
            _hitboxManager = _player.AddComponent<HitboxManager>();
            _weaponHandler = _player.AddComponent<WeaponHandler>();
            _relicManager = _player.AddComponent<RelicManager>();
            _playerController = _player.AddComponent<PlayerController>();

            // Animator (placeholder - animations will be imported later)
            var animator = _player.AddComponent<Animator>();

            // Rigidbody for physics interactions
            var rb = _player.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.mass = 1f;
            rb.drag = 0f;

            // CapsuleCollider for player collision
            var capsuleCollider = _player.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.3f;
            capsuleCollider.height = 1.8f;
            capsuleCollider.center = new Vector3(0f, 0.9f, 0f);

            // Trail renderer for dash effects
            var trailObj = new GameObject("DashTrail");
            trailObj.transform.SetParent(_player.transform);
            var trailRenderer = trailObj.AddComponent<UnityEngine.TrailRenderer>();
            trailRenderer.time = 0.15f;
            trailRenderer.startWidth = 0.15f;
            trailRenderer.endWidth = 0f;
            trailRenderer.startColor = new Color(0f, 0.9f, 1f, 0.8f);
            trailRenderer.endColor = new Color(0f, 0.9f, 1f, 0f);
            trailRenderer.material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0f, 0.9f, 1f) };

            // Hurtbox collider (trigger for receiving damage)
            var hurtboxObj = new GameObject("Hurtbox");
            hurtboxObj.transform.SetParent(_player.transform);
            hurtboxObj.layer = 13; // PlayerHurtbox
            var hurtboxCol = hurtboxObj.AddComponent<CapsuleCollider>();
            hurtboxCol.radius = 0.35f;
            hurtboxCol.height = 1.6f;
            hurtboxCol.center = new Vector3(0f, 0.8f, 0f);
            hurtboxCol.isTrigger = true;
            hurtboxObj.AddComponent<Hurtbox>();

            // Position player
            _player.transform.position = _playerSpawnPosition;
        }

        private GameObject CreatePlayerVisual()
        {
            // Create a stylized player visual representation
            // Hooded thief with asymmetrical outfit and cyan energy

            var visualRoot = new GameObject("PlayerVisual");
            visualRoot.transform.SetParent(_player.transform);
            visualRoot.transform.localPosition = Vector3.zero;

            // Body (hooded figure)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(visualRoot.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.25f);
            var bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.color = new Color(0.15f, 0.15f, 0.18f); // Dark outfit
            body.GetComponent<Renderer>().material = bodyMat;
            Destroy(body.GetComponent<CapsuleCollider>());

            // Head (hooded)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(visualRoot.transform);
            head.transform.localPosition = new Vector3(0f, 1.15f, 0.05f);
            head.transform.localScale = new Vector3(0.25f, 0.25f, 0.2f);
            var headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            headMat.color = new Color(0.1f, 0.1f, 0.12f); // Dark hood
            head.GetComponent<Renderer>().material = headMat;
            Destroy(head.GetComponent<SphereCollider>());

            // Hood brim
            var hood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hood.transform.SetParent(visualRoot.transform);
            hood.transform.localPosition = new Vector3(0f, 1.2f, -0.1f);
            hood.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
            hood.GetComponent<Renderer>().material = headMat;
            Destroy(hood.GetComponent<CapsuleCollider>());

            // Cyan energy relic container (on chest - asymmetrical)
            var relicContainer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            relicContainer.transform.SetParent(visualRoot.transform);
            relicContainer.transform.localPosition = new Vector3(0.15f, 0.75f, 0.15f);
            relicContainer.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            var relicMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            relicMat.color = new Color(0f, 0.9f, 1f); // Cyan energy
            relicMat.SetColor("_EmissionColor", new Color(0f, 0.9f, 1f) * 2f);
            relicMat.SetFloat("_EmissionIntensity", 3f);
            relicContainer.GetComponent<Renderer>().material = relicMat;
            Destroy(relicContainer.GetComponent<SphereCollider>());

            // Point light for relic glow
            var glowLight = new GameObject("RelicGlow");
            glowLight.transform.SetParent(relicContainer.transform);
            var light = glowLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0f, 0.9f, 1f);
            light.intensity = 1.5f;
            light.range = 3f;

            // Weapon (on right side)
            var weaponObj = new GameObject("WeaponAttach");
            weaponObj.transform.SetParent(visualRoot.transform);
            weaponObj.transform.localPosition = new Vector3(0.2f, 0.6f, 0.3f);
            var weapon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            weapon.transform.SetParent(weaponObj.transform);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            weapon.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            var weaponMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            weaponMat.color = new Color(0.4f, 0.4f, 0.45f); // Dark metal
            weapon.GetComponent<Renderer>().material = weaponMat;
            Destroy(weapon.GetComponent<CapsuleCollider>());

            // SkinnedMeshRenderer reference for the main renderer
            // (In production, this would be a rigged model with proper mesh)
            // For prototyping, we use the child renderers

            return visualRoot;
        }

        private void CreateHubEnvironment()
        {
            // Hub floor (larger, warm tone)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "HubFloor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f); // 30x30 unit
            floor.layer = 14;
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.color = new Color(0.22f, 0.2f, 0.18f); // Warm stone
            floor.GetComponent<Renderer>().material = floorMat;
            floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Hub walls
            CreateWalls(30f, new Color(0.28f, 0.25f, 0.22f), "HubWall");

            // Hub decorative columns
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                column.name = $"HubColumn_{i}";
                column.transform.position = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * 12f,
                    1.5f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * 12f
                );
                column.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
                column.layer = 14;
                var colMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                colMat.color = new Color(0.35f, 0.3f, 0.25f); // Aged gold stone
                column.GetComponent<Renderer>().material = colMat;
                Destroy(column.GetComponent<CapsuleCollider>());
            }

            // Warm gold light points (interactable markers)
            CreateInteractablePoint("Forge", _forgePosition);
            CreateInteractablePoint("Archive", _archivePosition);
            CreateInteractablePoint("Chapel", _chapelPosition);
            CreateInteractablePoint("RealmPortal", _portalPosition);
        }

        private void CreateHubInteractables()
        {
            // Gold-lit interactable points
            var interactables = new[]
            {
                ("Forge", _forgePosition, "Weapon selection and upgrades"),
                ("Archive", _archivePosition, "Relic knowledge and lore"),
                ("Chapel", _chapelPosition, "Healing and corruption guidance"),
                ("Portal", _portalPosition, "Enter a cursed realm")
            };

            foreach (var (name, pos, desc) in interactables)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"Interact_{name}";
                marker.tag = "Interactable";
                marker.transform.position = pos + Vector3.up * 0.5f;
                marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.85f, 0.7f, 0.3f); // Warm gold
                mat.SetColor("_EmissionColor", new Color(0.85f, 0.7f, 0.3f) * 1f);
                marker.GetComponent<Renderer>().material = mat;
                Destroy(marker.GetComponent<SphereCollider>());
                marker.AddComponent<BoxCollider>().isTrigger = true;

                // Point light for interactable glow
                var glow = new GameObject($"{name}_Glow");
                glow.transform.SetParent(marker.transform);
                var light = glow.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.85f, 0.7f, 0.3f);
                light.intensity = 1f;
                light.range = 3f;
            }
        }

        private void CreateRunArena()
        {
            // Combat arena floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.localScale = new Vector3(_arenaSize / 10f, 1f, _arenaSize / 10f);
            floor.layer = 14;
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.color = _floorColor;
            floor.GetComponent<Renderer>().material = floorMat;
            floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Arena walls
            CreateWalls(_arenaSize, _wallColor, "ArenaWall");

            // Arena props (pillars, debris)
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"ArenaPillar_{i}";
                pillar.transform.position = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * (_arenaSize * 0.4f),
                    1.5f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * (_arenaSize * 0.4f)
                );
                pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
                pillar.layer = 14;
                var pillarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pillarMat.color = new Color(0.3f, 0.28f, 0.25f);
                pillar.GetComponent<Renderer>().material = pillarMat;
            }
        }

        private void CreateBossArena()
        {
            // Larger arena for boss
            float bossSize = 30f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BossFloor";
            floor.transform.localScale = new Vector3(bossSize / 10f, 1f, bossSize / 10f);
            floor.layer = 14;
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.color = new Color(0.2f, 0.15f, 0.18f);
            floor.GetComponent<Renderer>().material = floorMat;

            CreateWalls(bossSize, new Color(0.25f, 0.2f, 0.22f), "BossWall");

            // Boss spawn point marker (crimson glow)
            var bossSpawn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bossSpawn.name = "BossSpawnMarker";
            bossSpawn.transform.position = new Vector3(0f, 0.5f, -10f);
            bossSpawn.transform.localScale = new Vector3(1f, 1f, 1f);
            var bossMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bossMat.color = new Color(0.85f, 0.2f, 0.3f);
            bossMat.SetColor("_EmissionColor", new Color(0.85f, 0.2f, 0.3f) * 2f);
            bossSpawn.GetComponent<Renderer>().material = bossMat;
            Destroy(bossSpawn.GetComponent<SphereCollider>());
        }

        private void CreateTrainingEnvironment()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TrainingFloor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.layer = 14;
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.color = new Color(0.22f, 0.2f, 0.18f);
            floor.GetComponent<Renderer>().material = floorMat;

            CreateWalls(20f, new Color(0.3f, 0.3f, 0.35f), "TrainingWall");
        }

        private void CreateTrainingDummy()
        {
            var dummy = new GameObject("TrainingDummy");
            dummy.tag = "Enemy";
            dummy.layer = 9;

            var dummyBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dummyBody.transform.SetParent(dummy.transform);
            dummyBody.transform.localPosition = Vector3.zero;
            dummyBody.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);
            var dummyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dummyMat.color = new Color(0.5f, 0.5f, 0.45f);
            dummyBody.GetComponent<Renderer>().material = dummyMat;

            var health = dummy.AddComponent<HealthComponent>();
            health.SetMaxHealth(9999f); // Doesn't die

            var marker = dummy.AddComponent<EnemyMarker>();
            marker.IsAlive = true;
            marker.EnemyType = "training_dummy";

            var capsuleCollider = dummy.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.3f;
            capsuleCollider.height = 1.6f;
            capsuleCollider.center = new Vector3(0f, 0.8f, 0f);

            // Hurtbox
            var hurtboxObj = new GameObject("Hurtbox");
            hurtboxObj.transform.SetParent(dummy.transform);
            hurtboxObj.layer = 12; // EnemyHurtbox
            var hurtboxCol = hurtboxObj.AddComponent<CapsuleCollider>();
            hurtboxCol.isTrigger = true;
            hurtboxCol.radius = 0.35f;
            hurtboxCol.height = 1.6f;
            hurtboxCol.center = new Vector3(0f, 0.8f, 0f);
            var hurtbox = hurtboxObj.AddComponent<Hurtbox>();
        }

        private void CreateWalls(float size, Color color, string namePrefix)
        {
            float halfSize = size / 2f;
            float wallHeight = _wallHeight;

            // Four walls around the arena
            Vector3[] wallPositions = new Vector3[]
            {
                new Vector3(0f, wallHeight / 2f, halfSize),
                new Vector3(0f, wallHeight / 2f, -halfSize),
                new Vector3(halfSize, wallHeight / 2f, 0f),
                new Vector3(-halfSize, wallHeight / 2f, 0f)
            };

            Vector3[] wallScales = new Vector3[]
            {
                new Vector3(size + 2f, wallHeight, 0.5f),
                new Vector3(size + 2f, wallHeight, 0.5f),
                new Vector3(0.5f, wallHeight, size + 2f),
                new Vector3(0.5f, wallHeight, size + 2f)
            };

            for (int i = 0; i < 4; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"{namePrefix}_{i}";
                wall.transform.position = wallPositions[i];
                wall.transform.localScale = wallScales[i];
                wall.layer = 14; // Environment
                var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                wallMat.color = color;
                wall.GetComponent<Renderer>().material = wallMat;
                // Keep wall collider for collision
                wall.GetComponent<BoxCollider>().size = Vector3.one;
            }
        }

        private GameObject CreateInteractablePoint(string name, Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.85f, 0.7f, 0.3f);
            obj.GetComponent<Renderer>().material = mat;
            Destroy(obj.GetComponent<CapsuleCollider>());
            return obj;
        }

        private void SpawnInitialEnemies()
        {
            for (int i = 0; i < _initialEnemyTypes.Length; i++)
            {
                float angle = i * 120f + Random.Range(-20f, 20f);
                Vector3 spawnPos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * _enemySpawnRadius,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * _enemySpawnRadius
                );
                SpawnEnemy(_initialEnemyTypes[i], spawnPos);
            }
        }

        private void SpawnEnemy(string type, Vector3 position)
        {
            var enemy = new GameObject($"Enemy_{type}");
            enemy.tag = "Enemy";
            enemy.layer = 9;

            // Enemy visual
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(enemy.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
            var enemyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            enemyMat.color = GetEnemyColor(type);
            body.GetComponent<Renderer>().material = enemyMat;
            Destroy(body.GetComponent<CapsuleCollider>());

            // Enemy components
            var health = enemy.AddComponent<HealthComponent>();
            var stagger = enemy.AddComponent<StaggerComponent>();
            var hitboxMgr = enemy.AddComponent<HitboxManager>();
            var controller = enemy.AddComponent<EnemyController>();
            var marker = enemy.AddComponent<EnemyMarker>();
            marker.IsAlive = true;
            marker.EnemyType = type;

            // Collider
            var capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            // Hurtbox
            var hurtboxObj = new GameObject("Hurtbox");
            hurtboxObj.transform.SetParent(enemy.transform);
            hurtboxObj.layer = 12;
            var hurtboxCol = hurtboxObj.AddComponent<CapsuleCollider>();
            hurtboxCol.isTrigger = true;
            hurtboxCol.radius = 0.35f;
            hurtboxCol.height = 1.6f;
            hurtboxCol.center = new Vector3(0f, 0.8f, 0f);
            var hurtbox = hurtboxObj.AddComponent<Hurtbox>();

            enemy.transform.position = position;
        }

        private Color GetEnemyColor(string type)
        {
            return type switch
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

        private void CreateRoomManager()
        {
            var roomManagerObj = new GameObject("RoomManager");
            _roomManager = roomManagerObj.AddComponent<RuntimeRoomManager>();
        }

        private void CreateHUD()
        {
            var hudObj = new GameObject("HUD");
            var canvas = hudObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var canvasScaler = hudObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;

            hudObj.AddComponent<GraphicRaycaster>();

            var hud = hudObj.AddComponent<GameHUD>();

            // Create HUD sub-elements
            CreateHUDElements(hudObj);
        }

        private void CreateBossHUD()
        {
            CreateHUD(); // Same HUD system with boss health bar
        }

        private void CreateHubUI()
        {
            var hubUIObj = new GameObject("HubUI");
            var canvas = hubUIObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hubUIObj.AddComponent<CanvasScaler>();
            hubUIObj.AddComponent<GraphicRaycaster>();

            var hubUI = hubUIObj.AddComponent<HubUI>();
        }

        private void CreateHUDElements(GameObject hudObj)
        {
            // Health bar
            var healthBarObj = CreateUIElement("HealthBar", hudObj.transform,
                new Vector2(-860f, -500f), new Vector2(300f, 20f));
            var healthSlider = healthBarObj.AddComponent<Slider>();
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
            var healthFill = healthBarObj.AddComponent<Image>();
            healthFill.color = new Color(0.8f, 0.2f, 0.2f);

            // Corruption bar
            var corruptionBarObj = CreateUIElement("CorruptionBar", hudObj.transform,
                new Vector2(860f, -500f), new Vector2(200f, 15f));
            var corruptionSlider = corruptionBarObj.AddComponent<Slider>();
            corruptionSlider.minValue = 0f;
            corruptionSlider.maxValue = 1f;
            var corruptionFill = corruptionBarObj.AddComponent<Image>();
            corruptionFill.color = new Color(0.85f, 0.2f, 0.3f);

            // Cooldown indicators
            var dashCooldownObj = CreateUIElement("DashCooldown", hudObj.transform,
                new Vector2(-860f, -440f), new Vector2(40f, 40f));
            var dashImage = dashCooldownObj.AddComponent<Image>();
            dashImage.color = new Color(0f, 0.9f, 1f);

            var parryCooldownObj = CreateUIElement("ParryCooldown", hudObj.transform,
                new Vector2(-820f, -440f), new Vector2(40f, 40f));
            var parryImage = parryCooldownObj.AddComponent<Image>();
            parryImage.color = new Color(1f, 0.9f, 0f);

            // Relic slots container
            var relicSlotsObj = CreateUIElement("RelicSlots", hudObj.transform,
                new Vector2(-300f, -520f), new Vector2(600f, 40f));

            // Boss health bar (hidden initially)
            var bossHealthObj = CreateUIElement("BossHealthBar", hudObj.transform,
                new Vector2(0f, 480f), new Vector2(400f, 15f));
            bossHealthObj.SetActive(false);
        }

        private GameObject CreateUIElement(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rectTransform = obj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            obj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.7f);
            return obj;
        }

        private void CreateAudioSystem()
        {
            var audioObj = new GameObject("AudioSystem");
            _musicSystem = audioObj.AddComponent<MusicSystem>();
            _sfxManager = audioObj.AddComponent<SFXManager>();

            // Create audio sources for music layers
            for (int i = 0; i < 4; i++)
            {
                var sourceObj = new GameObject($"AudioSource_{i}");
                sourceObj.transform.SetParent(audioObj.transform);
                var source = sourceObj.AddComponent<AudioSource>();
                source.loop = true;
                source.volume = 0.5f;
                source.spatialBlend = 0f; // 2D for music
            }

            // SFX pool sources
            _sfxManager = audioObj.AddComponent<SFXManager>();
        }

        private void CreateVFXSystem()
        {
            var vfxObj = new GameObject("VFXSystem");
            _vfxManager = vfxObj.AddComponent<VFXManager>();
        }

        #endregion

        #region Connect References

        private void ConnectReferences()
        {
            // Connect camera to player
            if (_cameraController != null && _player != null)
            {
                // Camera finds player via tag
            }

            // Connect input handler to player controller
            if (_playerController != null && _inputHandler != null)
            {
                // Both are on the same GameObject, auto-connected via GetComponent
            }

            // Connect room manager to game manager
            if (_roomManager != null)
            {
                var run = _gameManager?.CurrentRun;
                if (run != null)
                    _roomManager.SetCurrentRun(run);
            }

            // Connect combat feedback to camera
            if (_combatFeedback != null && UnityEngine.Camera.main != null)
            {
                // Combat feedback uses Camera.main internally
            }

            // Connect save manager to game manager
            if (_gameManager != null)
            {
                _gameManager.SetSaveManager(_saveManager);
            }
        }

        private void InitializeSystems()
        {
            // Initialize settings
            _settingsManager?.ApplySettings(new Relicfall.Saving.SettingsSaveData());

            // Initialize music
            _musicSystem?.SetCorruptionIntensity(0f);

            // Initialize combat feedback
            CameraShake.SetGlobalIntensity(1f);
        }

        #endregion

        #region Runtime Update

        private void Update()
        {
            // Update corruption visuals if in run
            if (_sceneType == SceneType.Run || _sceneType == SceneType.BossArena)
            {
                UpdateCorruptionVisuals();
            }
        }

        private void UpdateCorruptionVisuals()
        {
            float corruption = _gameManager?.Corruption?.CurrentLevel ?? 0f;

            // Update fog density based on corruption
            float baseFog = 0.02f;
            float corruptionFog = corruption / 100f * 0.03f;
            RenderSettings.fogDensity = baseFog + corruptionFog;

            // Update ambient color toward crimson with corruption
            float t = corruption / 100f;
            Color baseAmbient = new Color(0.2f, 0.2f, 0.25f);
            Color corruptAmbient = new Color(0.3f, 0.1f, 0.15f);
            RenderSettings.ambientLight = Color.Lerp(baseAmbient, corruptAmbient, t);
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw player spawn point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_playerSpawnPosition, 0.5f);

            // Draw enemy spawn positions
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Vector3.zero, _enemySpawnRadius);

            // Draw interactable positions (hub)
            if (_sceneType == SceneType.Hub)
            {
                Gizmos.color = new Color(0.85f, 0.7f, 0.3f); // Gold
                Gizmos.DrawWireSphere(_forgePosition + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawWireSphere(_archivePosition + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawWireSphere(_chapelPosition + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawWireSphere(_portalPosition + Vector3.up * 0.5f, 0.5f);
            }
        }

        #endregion
    }
}
