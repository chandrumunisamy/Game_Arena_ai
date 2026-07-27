using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Runs;
using Relicfall.Saving;

namespace Relicfall.Progression
{
    /// <summary>
    /// Permanent progression system that primarily unlocks options.
    /// Includes weapon unlocks, starting relic choices, new relic pool additions,
    /// extraction options, route choices, NPCs, realm modifiers, difficulty system,
    /// cosmetic hub upgrades, lore archive, and challenge mode.
    /// Avoids excessive permanent stat grinding.
    /// </summary>
    public class PermanentProgression : MonoBehaviour
    {
        [Header("Starting Configuration")]
        [SerializeField] private string _defaultWeaponId = "chain_blade";
        [SerializeField] private float _startingCurrency = 0f;

        [Header("Unlock Costs")]
        [SerializeField] private int _weaponUnlockCost = 100;
        [SerializeField] private int _startingRelicUnlockCost = 50;
        [SerializeField] private int _hubUpgradeCost = 75;

        // Runtime state
        public int RunsCompleted { get; private set; }
        public int BossesDefeated { get; private set; }
        public float Currency { get; private set; }
        public int DifficultyLevel { get; private set; }
        public int ScarCount { get; private set; }

        private List<string> _weaponsUnlocked = new();
        private List<string> _relicsDiscovered = new();
        private List<string> _hubUpgrades = new();
        private List<string> _startingRelicChoices = new();
        private List<string> _unlockedExtractionOptions = new();
        private List<string> _unlockedRouteChoices = new();
        private List<string> _unlockedNpcs = new();
        private List<string> _unlockedRealmModifiers = new();
        private List<string> _cosmeticUpgrades = new();
        private List<string> _loreEntries = new();
        private Dictionary<string, int> _bossDefeatCounts = new();
        private Dictionary<string, int> _weaponUsageCounts = new();
        private Dictionary<string, float> _factionFavor = new();
        private Dictionary<string, bool> _achievements = new();

        // Actions for UI updates
        public System.Action OnProgressionChanged;
        public System.Action<string> OnWeaponUnlocked;
        public System.Action<string> OnRelicDiscovered;
        public System.Action<string> OnNpcUnlocked;
        public System.Action OnDifficultyUnlocked;

        private void Awake()
        {
            // Default weapon is always unlocked
            _weaponsUnlocked.Add(_defaultWeaponId);
        }

        /// <summary>
        /// Initialize progression from saved data.
        /// </summary>
        public void InitializeFromSave(ProgressionSaveData saveData)
        {
            if (saveData == null)
            {
                RunsCompleted = 0;
                BossesDefeated = 0;
                Currency = _startingCurrency;
                DifficultyLevel = 0;
                ScarCount = 0;
                return;
            }

            var data = saveData;
            RunsCompleted = data.RunsCompleted;
            BossesDefeated = data.BossesDefeated;
            Currency = data.Currency;
            DifficultyLevel = data.DifficultyLevel;
            ScarCount = data.ScarCount;

            _weaponsUnlocked = data.WeaponsUnlocked ?? new List<string> { _defaultWeaponId };
            _relicsDiscovered = data.RelicsDiscovered ?? new List<string>();
            _hubUpgrades = data.HubUpgrades ?? new List<string>();
            _startingRelicChoices = data.StartingRelicChoices ?? new List<string>();
            _unlockedExtractionOptions = data.UnlockedExtractionOptions ?? new List<string>();
            _unlockedRouteChoices = data.UnlockedRouteChoices ?? new List<string>();
            _unlockedNpcs = data.UnlockedNpcs ?? new List<string>();
            _cosmeticUpgrades = data.CosmeticUpgrades ?? new List<string>();
            _loreEntries = data.LoreEntries ?? new List<string>();

            // Convert dictionary save format
            if (data.BossDefeatCounts != null)
                foreach (var entry in data.BossDefeatCounts.Entries)
                    _bossDefeatCounts[entry.Key] = entry.Value;

            if (data.WeaponUsageCounts != null)
                foreach (var entry in data.WeaponUsageCounts.Entries)
                    _weaponUsageCounts[entry.Key] = entry.Value;

            OnProgressionChanged?.Invoke();
        }

        /// <summary>
        /// Bank run results into permanent progression.
        /// Called when player successfully extracts.
        /// </summary>
        public void BankRunResults(RunData run)
        {
            RunsCompleted++;

            // Bank currency (scaled by extraction multiplier)
            float bankedCurrency = run.ResourcesEarned * (1f + run.ExtractionSkips * 0.5f);
            Currency += bankedCurrency;

            // Record discovered relics
            foreach (var relicId in run.RelicsCollected)
                DiscoverRelic(relicId);

            // Record boss defeats
            BossesDefeated += run.BossesDefeated;

            // Record weapon usage
            _weaponUsageCounts.TryGetValue(run.WeaponId, out int count);
            _weaponUsageCounts[run.WeaponId] = count + 1;

            // Unlock progression based on run completion
            CheckProgressionUnlocks();

            // Unlock difficulty after first successful completion
            if (RunsCompleted == 1 && DifficultyLevel == 0)
            {
                DifficultyLevel = 1;
                OnDifficultyUnlocked?.Invoke();
            }

            OnProgressionChanged?.Invoke();
        }

        /// <summary>
        /// Discover a relic (persists across runs, even on death).
        /// </summary>
        public void DiscoverRelic(string relicId)
        {
            if (!_relicsDiscovered.Contains(relicId))
            {
                _relicsDiscovered.Add(relicId);
                OnRelicDiscovered?.Invoke(relicId);
            }
        }

        /// <summary>
        /// Record a boss defeat for narrative tracking.
        /// </summary>
        public void RecordBossDefeat(string bossId, int runNumber)
        {
            _bossDefeatCounts.TryGetValue(bossId, out int count);
            _bossDefeatCounts[bossId] = count + 1;
        }

        /// <summary>
        /// Unlock a weapon by spending currency.
        /// </summary>
        public bool UnlockWeapon(string weaponId)
        {
            if (_weaponsUnlocked.Contains(weaponId)) return false;
            if (Currency < _weaponUnlockCost) return false;

            Currency -= _weaponUnlockCost;
            _weaponsUnlocked.Add(weaponId);
            OnWeaponUnlocked?.Invoke(weaponId);
            OnProgressionChanged?.Invoke();
            EventBus.Publish(new PermanentUnlockEvent { UnlockId = weaponId, UnlockType = "weapon" });
            return true;
        }

        /// <summary>
        /// Accept a permanent scar for a temporary benefit.
        /// Scars persist across runs as permanent consequences.
        /// </summary>
        public void AcceptScar()
        {
            ScarCount++;
            // Each scar provides a different permanent effect
            // Examples: reduced max health, increased corruption gain, reduced healing
            OnProgressionChanged?.Invoke();
        }

        /// <summary>
        /// Check if a weapon is unlocked.
        /// </summary>
        public bool IsWeaponUnlocked(string weaponId) => _weaponsUnlocked.Contains(weaponId);

        /// <summary>
        /// Check if a relic has been discovered.
        /// </summary>
        public bool IsRelicDiscovered(string relicId) => _relicsDiscovered.Contains(relicId);

        /// <summary>
        /// Get all unlocked weapon IDs.
        /// </summary>
        public List<string> GetUnlockedWeaponIds() => new List<string>(_weaponsUnlocked);

        /// <summary>
        /// Get all discovered relic IDs.
        /// </summary>
        public List<string> GetDiscoveredRelicIds() => new List<string>(_relicsDiscovered);

        /// <summary>
        /// Get hub upgrade IDs.
        /// </summary>
        public List<string> GetHubUpgradeIds() => new List<string>(_hubUpgrades);

        /// <summary>
        /// Purchase a hub upgrade.
        /// </summary>
        public bool PurchaseHubUpgrade(string upgradeId)
        {
            if (_hubUpgrades.Contains(upgradeId)) return false;
            if (Currency < _hubUpgradeCost) return false;

            Currency -= _hubUpgradeCost;
            _hubUpgrades.Add(upgradeId);
            OnProgressionChanged?.Invoke();
            EventBus.Publish(new PermanentUnlockEvent { UnlockId = upgradeId, UnlockType = "hub_upgrade" });
            return true;
        }

        /// <summary>
        /// Set difficulty level (0-10 scale, unlocks after first completion).
        /// </summary>
        public void SetDifficultyLevel(int level)
        {
            if (level < 1 || level > 10) return;
            if (RunsCompleted < 1) return; // Must complete at least one run
            DifficultyLevel = level;
            OnProgressionChanged?.Invoke();
        }

        /// <summary>
        /// Get difficulty modifiers based on current difficulty level.
        /// </summary>
        public DifficultyModifiers GetCurrentDifficultyModifiers()
        {
            return new DifficultyModifiers
            {
                CorruptionGainMultiplier = 1f + DifficultyLevel * 0.1f,
                EnemyHealthMultiplier = 1f + DifficultyLevel * 0.05f,
                EnemyDamageMultiplier = 1f + DifficultyLevel * 0.05f,
                EliteChanceBonus = DifficultyLevel * 0.02f,
                HealingReduction = DifficultyLevel * 0.03f,
                RewardMultiplier = 1f + DifficultyLevel * 0.1f,
                ExtractionPointReduction = DifficultyLevel >= 5,
                BossDamageBonus = DifficultyLevel * 0.1f,
                RealmCollapseSpeedBonus = DifficultyLevel * 0.05f
            };
        }

        private void CheckProgressionUnlocks()
        {
            // Unlock starting relic choice after 3 runs
            if (RunsCompleted >= 3 && !_startingRelicChoices.Contains("random_common"))
            {
                _startingRelicChoices.Add("random_common");
                EventBus.Publish(new PermanentUnlockEvent { UnlockId = "starting_relic_choice", UnlockType = "starting_option" });
            }

            // Unlock additional extraction options after 5 runs
            if (RunsCompleted >= 5 && !_unlockedExtractionOptions.Contains("sacrifice_relic"))
            {
                _unlockedExtractionOptions.Add("sacrifice_relic");
                EventBus.Publish(new PermanentUnlockEvent { UnlockId = "sacrifice_relic_extraction", UnlockType = "extraction_option" });
            }

            // Unlock additional route choices after 7 runs
            if (RunsCompleted >= 7 && !_unlockedRouteChoices.Contains("extended_preview"))
            {
                _unlockedRouteChoices.Add("extended_preview");
                EventBus.Publish(new PermanentUnlockEvent { UnlockId = "extended_route_preview", UnlockType = "route_choice" });
            }

            // Unlock NPCs at milestones
            if (RunsCompleted >= 2 && !_unlockedNpcs.Contains("blacksmith"))
            {
                _unlockedNpcs.Add("blacksmith");
                OnNpcUnlocked?.Invoke("blacksmith");
            }
            if (BossesDefeated >= 1 && !_unlockedNpcs.Contains("scholar"))
            {
                _unlockedNpcs.Add("scholar");
                OnNpcUnlocked?.Invoke("scholar");
            }
            if (BossesDefeated >= 2 && !_unlockedNpcs.Contains("priest"))
            {
                _unlockedNpcs.Add("priest");
                OnNpcUnlocked?.Invoke("priest");
            }
            if (RunsCompleted >= 10 && !_unlockedNpcs.Contains("oracle"))
            {
                _unlockedNpcs.Add("oracle");
                OnNpcUnlocked?.Invoke("oracle");
            }
            if (ScarCount >= 3 && !_unlockedNpcs.Contains("scarred_veteran"))
            {
                _unlockedNpcs.Add("scarred_veteran");
                OnNpcUnlocked?.Invoke("scarred_veteran");
            }
            if (BossesDefeated >= 3 && !_unlockedNpcs.Contains("relic_keeper"))
            {
                _unlockedNpcs.Add("relic_keeper");
                OnNpcUnlocked?.Invoke("relic_keeper");
            }

            // Add more relics to pool based on discoveries
            // Unlock realm modifiers after completing each realm
            if (_bossDefeatCounts.GetValueOrDefault("oath_breaker_king") >= 1 && !_unlockedRealmModifiers.Contains("court_harder"))
            {
                _unlockedRealmModifiers.Add("court_harder");
            }
            if (_bossDefeatCounts.GetValueOrDefault("thirteenth_regent") >= 1 && !_unlockedRealmModifiers.Contains("dominion_harder"))
            {
                _unlockedRealmModifiers.Add("dominion_harder");
            }
            if (_bossDefeatCounts.GetValueOrDefault("hollow_saint") >= 1 && !_unlockedRealmModifiers.Contains("maw_harder"))
            {
                _unlockedRealmModifiers.Add("maw_harder");
            }

            // Unlock second weapon after 2 runs
            if (RunsCompleted >= 2 && _weaponsUnlocked.Count == 1)
            {
                // Great Blade becomes available to unlock
                if (!_weaponsUnlocked.Contains("great_blade"))
                {
                    // Weapon is available but not yet purchased
                }
            }

            // Unlock third weapon after boss defeat
            if (BossesDefeated >= 1 && _weaponsUnlocked.Count <= 2)
            {
                if (!_weaponsUnlocked.Contains("arcane_pistol_dagger"))
                {
                    // Weapon is available but not yet purchased
                }
            }
        }
    }

    /// <summary>
    /// Difficulty modifiers that scale enemy challenge and rewards.
    /// </summary>
    public struct DifficultyModifiers
    {
        public float CorruptionGainMultiplier;
        public float EnemyHealthMultiplier;
        public float EnemyDamageMultiplier;
        public float EliteChanceBonus;
        public float HealingReduction;
        public float RewardMultiplier;
        public bool ExtractionPointReduction;
        public float BossDamageBonus;
        public float RealmCollapseSpeedBonus;
    }
}
