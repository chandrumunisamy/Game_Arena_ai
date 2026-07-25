using UnityEngine;
using Relicfall.Core.Events;
using Relicfall.Core.Utils;
using Relicfall.Corruption;
using Relicfall.Runs;
using Relicfall.Player;
using Relicfall.Saving;
using Relicfall.Audio;
using Relicfall.UI;

namespace Relicfall.Core
{
    /// <summary>
    /// Central game manager that orchestrates the game lifecycle.
    /// Handles game state transitions, run management, and scene flow.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Boot,
            Hub,
            RunActive,
            RunPaused,
            ExtractionOffered,
            RunComplete,
            RunFailed,
            BossArena,
            Transitioning
        }

        public GameState CurrentState { get; private set; } = GameState.Boot;
        public RunData CurrentRun { get; private set; }
        public PermanentProgression Progression { get; private set; }
        public CorruptionTracker Corruption { get; private set; }

        [Header("Configuration")]
        [SerializeField] private float _transitionDelay = 0.5f;
        [SerializeField] private float _bossIntroDelay = 1.5f;

        [Header("Scene References")]
        [SerializeField] private GameObject _hubRoot;
        [SerializeField] private GameObject _runRoot;

        private SaveManager _saveManager;
        private MusicSystem _musicSystem;
        private PlayerInputBuffer _inputBuffer;
        private bool _initialized;

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSystems();
        }

        private void InitializeSystems()
        {
            // Initialize core systems in order
            _saveManager = GetComponent<SaveManager>() ?? gameObject.AddComponent<SaveManager>();
            _musicSystem = GetComponent<MusicSystem>() ?? gameObject.AddComponent<MusicSystem>();
            Progression = GetComponent<PermanentProgression>() ?? gameObject.AddComponent<PermanentProgression>();
            Corruption = new CorruptionTracker();

            // Load save data
            var saveData = _saveManager.LoadGame();
            Progression.InitializeFromSave(saveData?.Progression);

            // Set up event subscriptions
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<ExtractionChosenEvent>(OnExtractionChosen);
            EventBus.Subscribe<CorruptionThresholdEvent>(OnCorruptionThreshold);
            EventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);

            _initialized = true;
            SetState(GameState.Hub);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<ExtractionChosenEvent>(OnExtractionChosen);
            EventBus.Unsubscribe<CorruptionThresholdEvent>(OnCorruptionThreshold);
            EventBus.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
        }

        /// <summary>
        /// Start a new run from the hub.
        /// </summary>
        public void StartRun(string realmId, string weaponId)
        {
            if (CurrentState != GameState.Hub) return;

            CurrentRun = new RunData
            {
                RealmId = realmId,
                WeaponId = weaponId,
                RunStartTime = Time.time,
                Corruption = 0f,
                RelicsCollected = new System.Collections.Generic.List<string>(),
                EnemiesKilled = 0,
                RoomsCompleted = 0,
                BossesDefeated = 0,
                HealthPctAtStart = 100f
            };

            Corruption.Reset();
            EventBus.Publish(new RunStartedEvent { RealmId = realmId, WeaponId = weaponId });
            SetState(GameState.RunActive);
        }

        /// <summary>
        /// Pause or unpause the game during a run.
        /// </summary>
        public void TogglePause()
        {
            if (CurrentState == GameState.RunActive)
            {
                SetState(GameState.RunPaused);
                Time.timeScale = 0f;
            }
            else if (CurrentState == GameState.RunPaused)
            {
                SetState(GameState.RunActive);
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Offer extraction choices to the player.
        /// </summary>
        public void OfferExtraction()
        {
            if (CurrentState != GameState.RunActive) return;
            SetState(GameState.ExtractionOffered);
        }

        /// <summary>
        /// Complete a run successfully (player extracted).
        /// </summary>
        public void CompleteRun()
        {
            if (CurrentRun == null) return;

            CurrentRun.RunEndTime = Time.time;
            CurrentRun.RunDurationSeconds = CurrentRun.RunEndTime - CurrentRun.RunStartTime;

            // Bank permanent resources
            Progression.BankRunResults(CurrentRun);

            // Auto-save
            _saveManager.AutoSave();

            EventBus.Publish(new SaveRequestedEvent { IsAutoSave = true });
            SetState(GameState.RunComplete);
        }

        /// <summary>
        /// Transition back to hub after run completion or failure.
        /// </summary>
        public void ReturnToHub()
        {
            SetState(GameState.Hub);
            Corruption.Reset();
            CurrentRun = null;
        }

        // Event handlers
        private void OnPlayerDeath(PlayerDeathEvent e)
        {
            if (CurrentRun == null) return;

            CurrentRun.RunEndTime = Time.time;
            CurrentRun.RunDurationSeconds = e.RunDurationSeconds;
            CurrentRun.DeathCause = e.DeathCause;
            CurrentRun.IsFailed = true;

            // Keep discovered relic knowledge even on death
            foreach (var relicId in CurrentRun.RelicsCollected)
                Progression.DiscoverRelic(relicId);

            SetState(GameState.RunFailed);
        }

        private void OnBossDefeated(BossDefeatedEvent e)
        {
            if (CurrentRun == null) return;
            CurrentRun.BossesDefeated++;
            Progression.RecordBossDefeat(e.BossId, e.RunNumber);
        }

        private void OnExtractionChosen(ExtractionChosenEvent e)
        {
            if (e.ChosenOption == "extract")
            {
                CompleteRun();
            }
            else if (e.ChosenOption == "continue")
            {
                SetState(GameState.RunActive);
            }
            else if (e.ChosenOption == "sacrifice_relic")
            {
                // Reduce corruption by sacrificing a relic
                var lastRelic = CurrentRun.RelicsCollected.Count > 0 
                    ? CurrentRun.RelicsCollected[CurrentRun.RelicsCollected.Count - 1] 
                    : null;
                if (lastRelic != null)
                {
                    Corruption.Reduce(15f);
                    CurrentRun.RelicsCollected.RemoveAt(CurrentRun.RelicsCollected.Count - 1);
                }
                SetState(GameState.RunActive);
            }
            else if (e.ChosenOption == "scar_benefit")
            {
                // Accept permanent scar for temporary buff
                Progression.AcceptScar();
                Corruption.Reduce(5f);
                SetState(GameState.RunActive);
            }
            else if (e.ChosenOption == "challenge_boss_early")
            {
                SetState(GameState.BossArena);
            }
        }

        private void OnCorruptionThreshold(CorruptionThresholdEvent e)
        {
            _musicSystem.SetCorruptionIntensity(e.CorruptionLevel);

            if (e.CorruptionLevel >= 100f)
            {
                EventBus.Publish(new RealmCollapseEvent { TimeRemaining = 60f });
                // Start realm collapse timer
            }
        }

        private void OnSettingsChanged(SettingsChangedEvent e)
        {
            if (e.SettingName == "screen_shake")
                CameraShake.SetGlobalIntensity(float.Parse(e.NewValue));
            else if (e.SettingName == "vibration")
                VibrationEnabled = e.NewValue == "1";
        }

        public bool VibrationEnabled { get; private set; } = true;

        /// <summary>
        /// Set the save manager reference (called by SceneBootstrap).
        /// </summary>
        public void SetSaveManager(SaveManager saveManager)
        {
            _saveManager = saveManager;
        }

        private void SetState(GameState newState)
        {
            var previous = CurrentState;
            CurrentState = newState;
            Debug.Log($"GameManager: State {previous} -> {newState}");
        }

        private void Update()
        {
            if (!_initialized) return;

            // Handle pause input
            if (UnityEngine.InputSystem.InputSystem.GetKeyDown(UnityEngine.InputSystem.Key.Escape))
            {
                TogglePause();
            }

            // Update corruption during runs
            if (CurrentState == GameState.RunActive && CurrentRun != null)
            {
                Corruption.Tick(Time.deltaTime);
                CurrentRun.Corruption = Corruption.CurrentLevel;
            }
        }
    }
}
