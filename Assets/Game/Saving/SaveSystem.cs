using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Relicfall.Core.Events;

namespace Relicfall.Saving
{
    /// <summary>
    /// Versioned save system for persistent game state.
    /// Implements autosave, backup saves, corruption recovery,
    /// and save version migration. No runtime ScriptableObject mutation.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string _saveFolder = "Saves";
        [SerializeField] private string _saveFileName = "relicfall_save";
        [SerializeField] private string _backupFileName = "relicfall_save_backup";
        [SerializeField] private int _maxBackups = 3;
        [SerializeField] private float _autoSaveInterval = 60f;

        private string _savePath;
        private string _backupPath;
        private float _autoSaveTimer;
        private int _currentSaveVersion = 1;

        public SaveData CurrentData { get; private set; }
        public bool HasSaveData { get; private set; }

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, _saveFolder);
            if (!Directory.Exists(_savePath))
                Directory.CreateDirectory(_savePath);

            EventBus.Subscribe<SaveRequestedEvent>(OnSaveRequested);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SaveRequestedEvent>(OnSaveRequested);
        }

        private void Update()
        {
            // Autosave timer
            _autoSaveTimer -= Time.deltaTime;
            if (_autoSaveTimer <= 0f)
            {
                AutoSave();
                _autoSaveTimer = _autoSaveInterval;
            }
        }

        /// <summary>
        /// Load game save data from disk.
        /// </summary>
        public SaveData LoadGame()
        {
            string fullPath = Path.Combine(_savePath, _saveFileName + ".json");

            if (!File.Exists(fullPath))
            {
                // Try backup
                fullPath = Path.Combine(_savePath, _backupFileName + ".json");
                if (!File.Exists(fullPath))
                {
                    HasSaveData = false;
                    return CreateNewSave();
                }
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                var data = JsonUtility.FromJson<SaveData>(json);

                // Version migration
                if (data.SaveVersion < _currentSaveVersion)
                    data = MigrateSave(data, data.SaveVersion, _currentSaveVersion);

                CurrentData = data;
                HasSaveData = true;
                Debug.Log($"Save loaded: version {data.SaveVersion}");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save load error: {ex}");

                // Try backup recovery
                string backupPath = Path.Combine(_savePath, _backupFileName + ".json");
                if (File.Exists(backupPath))
                {
                    try
                    {
                        string backupJson = File.ReadAllText(backupPath);
                        var backupData = JsonUtility.FromJson<SaveData>(backupJson);
                        CurrentData = backupData;
                        HasSaveData = true;
                        Debug.LogWarning("Recovered from backup save");
                        return backupData;
                    }
                    catch
                    {
                        Debug.LogError("Backup save also corrupted");
                    }
                }

                return CreateNewSave();
            }
        }

        /// <summary>
        /// Save game data to disk.
        /// </summary>
        public bool SaveGame(SaveData data)
        {
            if (data == null) return false;

            // Update version
            data.SaveVersion = _currentSaveVersion;
            data.LastSaveTime = DateTime.Now.ToString("o");

            try
            {
                string json = JsonUtility.ToJson(data, true);
                string fullPath = Path.Combine(_savePath, _saveFileName + ".json");

                // Create backup before writing
                if (File.Exists(fullPath))
                {
                    string backupPath = Path.Combine(_savePath, _backupFileName + ".json");
                    File.Copy(fullPath, backupPath, true);
                }

                // Write save
                File.WriteAllText(fullPath, json);

                CurrentData = data;
                HasSaveData = true;

                EventBus.Publish(new SaveCompletedEvent { Success = true, SavePath = fullPath });
                Debug.Log($"Game saved to {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save error: {ex}");
                EventBus.Publish(new SaveCompletedEvent { Success = false, SavePath = "" });
                return false;
            }
        }

        /// <summary>
        /// Autosave current game state.
        /// </summary>
        public void AutoSave()
        {
            if (CurrentData == null) return;
            SaveGame(CurrentData);
        }

        /// <summary>
        /// Create a new save data for a fresh game.
        /// </summary>
        private SaveData CreateNewSave()
        {
            var data = new SaveData
            {
                SaveVersion = _currentSaveVersion,
                CreatedTime = DateTime.Now.ToString("o"),
                LastSaveTime = DateTime.Now.ToString("o"),
                Progression = new ProgressionSaveData(),
                Settings = new SettingsSaveData(),
                InputBindings = new InputBindingsSaveData(),
                Statistics = new StatisticsSaveData(),
                Narrative = new NarrativeSaveData(),
                Achievements = new AchievementsSaveData()
            };

            CurrentData = data;
            HasSaveData = true;
            SaveGame(data);
            return data;
        }

        /// <summary>
        /// Migrate save data between versions.
        /// </summary>
        private SaveData MigrateSave(SaveData data, int fromVersion, int toVersion)
        {
            for (int v = fromVersion; v < toVersion; v++)
            {
                data = ApplyMigration(data, v);
            }
            data.SaveVersion = toVersion;
            return data;
        }

        private SaveData ApplyMigration(SaveData data, int fromVersion)
        {
            // Migration logic per version
            switch (fromVersion)
            {
                case 0:
                    // V0 -> V1: Add statistics and achievements fields
                    if (data.Statistics == null)
                        data.Statistics = new StatisticsSaveData();
                    if (data.Achievements == null)
                        data.Achievements = new AchievementsSaveData();
                    break;
            }
            return data;
        }

        /// <summary>
        /// Delete all save data (for debugging or corrupted save recovery).
        /// </summary>
        public void DeleteAllSaves()
        {
            string fullPath = Path.Combine(_savePath, _saveFileName + ".json");
            string backupPath = Path.Combine(_savePath, _backupFileName + ".json");

            if (File.Exists(fullPath)) File.Delete(fullPath);
            if (File.Exists(backupPath)) File.Delete(backupPath);

            CurrentData = null;
            HasSaveData = false;
        }

        /// <summary>
        /// Update save data from current game state before saving.
        /// </summary>
        public void UpdateFromGameState()
        {
            if (CurrentData == null) return;

            // Update progression
            var progression = Core.GameManager.Instance?.Progression;
            if (progression != null)
            {
                CurrentData.Progression.RunsCompleted = progression.RunsCompleted;
                CurrentData.Progression.BossesDefeated = progression.BossesDefeated;
                CurrentData.Progression.WeaponsUnlocked = progression.GetUnlockedWeaponIds();
                CurrentData.Progression.RelicsDiscovered = progression.GetDiscoveredRelicIds();
                CurrentData.Progression.Currency = progression.Currency;
                CurrentData.Progression.HubUpgrades = progression.GetHubUpgradeIds();
                CurrentData.Progression.DifficultyLevel = progression.DifficultyLevel;
                CurrentData.Progression.ScarCount = progression.ScarCount;
            }

            // Update settings
            // Updated by SettingsManager

            // Update statistics
            var run = Core.GameManager.Instance?.CurrentRun;
            if (run != null)
            {
                CurrentData.Statistics.TotalEnemiesKilled += run.EnemiesKilled;
                CurrentData.Statistics.TotalRoomsCompleted += run.RoomsCompleted;
                CurrentData.Statistics.TotalBossesDefeated += run.BossesDefeated;
                CurrentData.Statistics.TotalRelicsCollected += run.RelicsCollected.Count;
                CurrentData.Statistics.TotalRuns += 1;
            }
        }
    }

    /// <summary>
    /// Complete save data structure. Versioned for migration.
    /// All fields are serializable plain data - no ScriptableObject references.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int SaveVersion;
        public string CreatedTime;
        public string LastSaveTime;
        public ProgressionSaveData Progression;
        public SettingsSaveData Settings;
        public InputBindingsSaveData InputBindings;
        public StatisticsSaveData Statistics;
        public NarrativeSaveData Narrative;
        public AchievementsSaveData Achievements;
    }

    [Serializable]
    public class ProgressionSaveData
    {
        public int RunsCompleted;
        public int BossesDefeated;
        public List<string> WeaponsUnlocked = new();
        public List<string> RelicsDiscovered = new();
        public float Currency;
        public List<string> HubUpgrades = new();
        public int DifficultyLevel;
        public int ScarCount;
        public List<string> StartingRelicChoices = new();
        public List<string> UnlockedExtractionOptions = new();
        public List<string> UnlockedRouteChoices = new();
        public List<string> UnlockedNpcs = new();
        public DictionaryOfStringAndInt BossDefeatCounts = new();
        public DictionaryOfStringAndInt WeaponUsageCounts = new();
        public DictionaryOfStringAndFloat FavouredFactions = new();
        public List<string> UnlockedRealmModifiers = new();
        public List<string> CosmeticUpgrades = new();
        public List<string> LoreEntries = new();
    }

    [Serializable]
    public class DictionaryOfStringAndInt
    {
        public List<StringIntPair> Entries = new();
        public int Get(string key) => Entries.Find(e => e.Key == key)?.Value ?? 0;
        public void Set(string key, int value)
        {
            var entry = Entries.Find(e => e.Key == key);
            if (entry != null) entry.Value = value;
            else Entries.Add(new StringIntPair { Key = key, Value = value });
        }
    }

    [Serializable]
    public class DictionaryOfStringAndFloat
    {
        public List<StringFloatPair> Entries = new();
        public float Get(string key) => Entries.Find(e => e.Key == key)?.Value ?? 0f;
        public void Set(string key, float value)
        {
            var entry = Entries.Find(e => e.Key == key);
            if (entry != null) entry.Value = value;
            else Entries.Add(new StringFloatPair { Key = key, Value = value });
        }
    }

    [Serializable]
    public class StringIntPair { public string Key; public int Value; }
    [Serializable]
    public class StringFloatPair { public string Key; public float Value; }

    [Serializable]
    public class SettingsSaveData
    {
        public int ScreenWidth = 1920;
        public int ScreenHeight = 1080;
        public int FullscreenMode = 1; // 0=Windowed, 1=Fullscreen, 2=Borderless
        public int VSyncCount = 1;
        public int TargetFPS = 60;
        public int GraphicsQuality = 2;
        public int ShadowQuality = 2;
        public int EffectsQuality = 2;
        public int AntiAliasing = 2;
        public float RenderScale = 1f;
        public bool MotionBlur = true;
        public bool ChromaticAberration = false;
        public float MasterVolume = 1f;
        public float MusicVolume = 0.7f;
        public float SFXVolume = 1f;
        public float AmbienceVolume = 0.6f;
        public float UISFXVolume = 0.8f;
        public float ScreenShakeIntensity = 1f;
        public bool VibrationEnabled = true;
        public float AimAssistStrength = 0.5f;
        public int TextSize = 1;
        public bool SubtitlesEnabled = true;
        public bool HighContrastTelegraphs = false;
        public bool ColorblindMode = false;
        public int ColorblindType = 0;
    }

    [Serializable]
    public class InputBindingsSaveData
    {
        public List<BindingEntry> CustomBindings = new();
    }

    [Serializable]
    public class BindingEntry
    {
        public string ActionName;
        public string BindingPath;
        public string OverridePath;
    }

    [Serializable]
    public class StatisticsSaveData
    {
        public int TotalRuns;
        public int TotalEnemiesKilled;
        public int TotalRoomsCompleted;
        public int TotalBossesDefeated;
        public int TotalRelicsCollected;
        public int TotalCriticalHits;
        public int TotalParriesSuccessful;
        public int TotalExecutionsPerformed;
        public int TotalDeaths;
        public float TotalDamageDealt;
        public float TotalDamageReceived;
        public int ExtractionCount;
        public int DeathCount;
        public float TotalRunTime;
        public float BestRunTime;
        public int MostRelicsInOneRun;
        public int MostEnemiesKilledInOneRun;
        public float HighestCorruptionReached;
        public string MostUsedWeapon;
        public string LastDeathCause;
    }

    [Serializable]
    public class NarrativeSaveData
    {
        public List<string> DialogueProgress = new();
        public List<string> BossesDefeatedNarrative = new();
        public List<string> FailedExtractions = new();
        public List<string> RelicChoicesNarrative = new();
        public List<string> DeathReactions = new();
        public DictionaryOfStringAndFloat NpcRelationships = new();
        public List<string> UnlockedDialogue = new();
        public string CurrentRealmPreference;
        public int BossEncountersWithSameBoss;
    }

    [Serializable]
    public class AchievementsSaveData
    {
        public List<string> UnlockedAchievements = new();
        public DictionaryOfStringAndString AchievementDates = new();
    }

    [Serializable]
    public class DictionaryOfStringAndString
    {
        public List<StringStringPair> Entries = new();
        public string Get(string key) => Entries.Find(e => e.Key == key)?.Value ?? "";
        public void Set(string key, string value)
        {
            var entry = Entries.Find(e => e.Key == key);
            if (entry != null) entry.Value = value;
            else Entries.Add(new StringStringPair { Key = key, Value = value });
        }
    }

    [Serializable]
    public class StringStringPair { public string Key; public string Value; }
}
