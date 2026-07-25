using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Corruption;
using Relicfall.Relics;

namespace Relicfall.Runs
{
    /// <summary>
    /// Mutable runtime data for a single run.
    /// All state that should be lost on death or reset on extraction.
    /// </summary>
    public class RunData
    {
        public string RealmId;
        public string WeaponId;
        public float RunStartTime;
        public float RunEndTime;
        public float RunDurationSeconds;
        public float Corruption;
        public List<string> RelicsCollected = new();
        public int EnemiesKilled;
        public int RoomsCompleted;
        public int BossesDefeated;
        public float HealthPctAtStart;
        public bool IsFailed;
        public string DeathCause;
        public float ResourcesEarned;
        public float ResourcesBanked;
        public int ExtractionSkips; // Number of times player skipped extraction
        public List<string> RoomsVisited = new();
        public List<string> UpgradesCollected = new();
        public List<string> EliteModifiersEncountered = new();
        public int CurrentRoomIndex;
        public string CurrentRoomId;
        public float DamageDealtTotal;
        public float DamageReceivedTotal;
        public int CriticalHits;
        public int ParriesSuccessful;
        public int DashesPerformed;
        public int ExecutionsPerformed;

        // Route state
        public List<RouteNode> AvailableRoutes = new();
        public RouteNode CurrentRoute;
    }

    /// <summary>
    /// Route node representing a room choice in the run layout.
    /// Contains preview information the player can see before choosing.
    /// </summary>
    public class RouteNode
    {
        public string NodeId;
        public RoomType Type;
        public string RoomDefinitionId;
        public float DangerLevel; // 1-5 scale
        public RewardCategory PossibleReward;
        public float CorruptionIncrease;
        public bool HasElite;
        public bool HasHealing;
        public bool IsExtractionPoint;
        public bool IsUnknownEvent;
        public string PreviewInfo; // Partial information shown to player
        public List<RouteNode> NextRoutes = new(); // Routes available after this room
        public bool IsCompleted;
        public int Depth; // How deep in the run this room is (0 = start)

        public string GetPreviewText()
        {
            string text = $"[{Type}]";
            if (HasElite) text += " ⚠ Elite";
            if (HasHealing) text += " ❤ Recovery";
            if (IsExtractionPoint) text += " 🚪 Extraction";
            if (IsUnknownEvent) text += " ❓ Unknown";
            text += $" Danger: {DangerLevel}";
            if (!IsUnknownEvent)
                text += $" Reward: {PossibleReward}";
            return text;
        }
    }

    public enum RoomType
    {
        StartRoom,
        Combat,
        EliteCombat,
        Reward,
        RiskRoom,
        Rest,
        Extraction,
        BossArena,
        Challenge,
        UnknownEvent
    }

    public enum RewardCategory
    {
        Relic,
        WeaponUpgrade,
        Health,
        Economy,
        Mixed,
        Unknown
    }

    /// <summary>
    /// Room definition ScriptableObject for handcrafted modular rooms.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomDef", menuName = "RELICFALL/Rooms/Room Definition")]
    public class RoomDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string RoomId;
        public string RoomName;
        public RoomType Type;
        public RealmType Realm;

        [Header("Layout")]
        public GameObject RoomPrefab;
        public Vector3 RoomSize = new Vector3(20f, 5f, 20f);
        public Transform[] EntrancePoints;
        public Transform[] ExitPoints;
        public Transform[] EnemySpawnPoints;
        public Transform[] RewardSpawnPoints;
        public Transform[] ExtractionPoint;
        public Transform[] BossSpawnPoint;
        public Transform[] HazardSpawnPoints;
        public Transform[] PropSpawnPoints;

        [Header("Encounter")]
        public EncounterDefinition NormalEncounter;
        public EncounterDefinition CorruptionEncounter; // Alternate encounter at high corruption
        public float CorruptionThresholdForVariant = 50f;
        public float CorruptionIncrease = 5f;
        public bool UseCorruptionVariant = false;

        [Header("Extraction")]
        public bool IsExtractionAvailable = false;
        public ExtractionOptions ExtractionConfig;

        [Header("Route Preview")]
        public string DefaultPreviewInfo;
        public string CorruptionPreviewInfo;

        [Header("Visual")]
        public Color BaseAmbientColor;
        public Color CorruptedAmbientColor;
        public GameObject[] CorruptionProps; // Props that appear at high corruption
        public GameObject[] NormalProps;
        public float FogDensityNormal = 0.01f;
        public float FogDensityCorrupted = 0.04f;
    }

    public enum RealmType
    {
        ShatteredCourt,
        DrownedDominion,
        VerdantMaw
    }

    /// <summary>
    /// Encounter definition for enemy spawns within a room.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterDef", menuName = "RELICFALL/Rooms/Encounter Definition")]
    public class EncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string EncounterId;
        public string EncounterName;
        public int DifficultyLevel = 1;

        [Header("Enemy Spawns")]
        public EnemySpawnEntry[] EnemySpawns;
        public float SpawnDelayBetweenEnemies = 0.5f;
        public bool SpawnAllAtStart = false;
        public int MaxConcurrentEnemies = 4;
        public float EliteSpawnChance = 0f;
        public EliteModifier[] PossibleEliteModifiers;

        [Header("Phases")]
        public bool HasPhases = false;
        public EncounterPhase[] Phases;

        [Header("Rewards")]
        public RewardDefinition Reward;
        public float CorruptionIncreaseOnComplete = 5f;
    }

    [System.Serializable]
    public class EnemySpawnEntry
    {
        public string EnemyDefinitionId;
        public int Count = 1;
        public float SpawnDelay;
        public bool IsRequired = true;
        public bool IsElite = false;
        public EliteModifier EliteModifier;
        public float CorruptionVariantChance = 0f;
    }

    [System.Serializable]
    public class EncounterPhase
    {
        public int PhaseNumber;
        public EnemySpawnEntry[] PhaseEnemies;
        public float PhaseStartDelay;
        public string PhaseStartTrigger; // "all_dead", "timer", "health_threshold"
        public float TriggerValue;
    }

    /// <summary>
    /// Extraction options available to the player at extraction points.
    /// </summary>
    [System.Serializable]
    public class ExtractionOptions
    {
        public bool CanExtract = true;
        public bool CanContinue = true;
        public bool CanSacrificeRelic = true;
        public bool CanAcceptScar = true;
        public bool CanChallengeBossEarly = false;
        public bool CanConvertHealthToReward = false;

        public float ExtractRewardMultiplier = 1f;
        public float ContinueRewardMultiplier = 1.5f;
        public float SacrificeCorruptionReduction = 15f;
        public float ScarBonusDuration = 30f;
        public float HealthConversionRatio = 0.2f;

        public string[] GetAvailableOptions()
        {
            var options = new List<string>();
            if (CanExtract) options.Add("extract");
            if (CanContinue) options.Add("continue");
            if (CanSacrificeRelic) options.Add("sacrifice_relic");
            if (CanAcceptScar) options.Add("scar_benefit");
            if (CanChallengeBossEarly) options.Add("challenge_boss_early");
            if (CanConvertHealthToReward) options.Add("health_to_reward");
            return options.ToArray();
        }
    }

    /// <summary>
    /// Reward definition for room completion.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardDef", menuName = "RELICFALL/Rooms/Reward Definition")]
    public class RewardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string RewardId;
        public string RewardName;

        [Header("Type")]
        public RewardType Type;
        public int ChoicesOffered = 3;
        public float QualityMultiplier = 1f;

        [Header("Relic Reward")]
        public RelicRarityFilter RelicFilter;

        [Header("Health Reward")]
        public float HealthRestorePercent = 30f;

        [Header("Economy Reward")]
        public int CurrencyAmount = 50;

        [Header("Upgrade Reward")]
        public string[] EligibleUpgradeIds;

        [Header("Conditional")]
        public float MinCorruptionForAppearance;
        public float RewardQualityAtHighCorruption;
    }

    public enum RewardType
    {
        RelicChoice,
        WeaponUpgradeChoice,
        HealthRestore,
        Economy,
        MixedChoice
    }

    public enum RelicRarityFilter
    {
        Any,
        CommonAndUncommon,
        UncommonAndRare,
        RareAndEpic,
        EpicAndLegendary
    }

    /// <summary>
    /// Run generator that creates the sequence of rooms for a run.
    /// Uses handcrafted rooms combined into variable layouts.
    /// Not fully random procedural - rooms are hand-authored, sequences are variable.
    /// </summary>
    public class RunGenerator
    {
        private Dictionary<RealmType, List<RoomDefinition>> _roomPool = new();
        private System.Random _random;

        public RunGenerator()
        {
            _random = new System.Random();
        }

        /// <summary>
        /// Generate a complete run layout for a specific realm.
        /// </summary>
        public List<RouteNode> GenerateRun(RealmType realm, int depthTarget = 8)
        {
            var routeNodes = new List<RouteNode>();
            int currentDepth = 0;

            // Start room
            var startNode = CreateNode("start", RoomType.StartRoom, currentDepth, 
                "Enter the cursed realm", RewardCategory.Unknown, 0f, false, false, false, false);
            routeNodes.Add(startNode);
            currentDepth++;

            // Generate rooms until reaching depth target
            while (currentDepth < depthTarget)
            {
                // Every 3 rooms, offer route choice
                if (currentDepth % 3 == 0 && currentDepth > 0)
                {
                    // Create branching paths
                    var branch1 = GenerateRoomNode(realm, currentDepth, RouteBias.Safe);
                    var branch2 = GenerateRoomNode(realm, currentDepth, RouteBias.Risky);
                    routeNodes.Add(branch1);
                    routeNodes.Add(branch2);

                    // Connect previous node's next routes to the branches
                    var previous = routeNodes[routeNodes.Count - 3];
                    previous.NextRoutes.Add(branch1);
                    previous.NextRoutes.Add(branch2);
                }
                else
                {
                    var node = GenerateRoomNode(realm, currentDepth, RouteBias.Neutral);
                    routeNodes.Add(node);
                }

                currentDepth++;
            }

            // Boss room at the end
            var bossNode = CreateNode("boss", RoomType.BossArena, depthTarget,
                "Face the realm's guardian", RewardCategory.Relic, 15f, false, false, false, false);
            routeNodes.Add(bossNode);

            // Final extraction
            var finalExtraction = CreateNode("final_extraction", RoomType.Extraction, depthTarget + 1,
                "Escape with your stolen relics", RewardCategory.Unknown, 0f, false, true, true, false);
            routeNodes.Add(finalExtraction);

            // Link all nodes
            for (int i = 0; i < routeNodes.Count - 1; i++)
            {
                if (routeNodes[i].NextRoutes.Count == 0)
                    routeNodes[i].NextRoutes.Add(routeNodes[i + 1]);
            }

            return routeNodes;
        }

        private RouteNode GenerateRoomNode(RealmType realm, int depth, RouteBias bias)
        {
            // Choose room type based on depth and bias
            RoomType type = ChooseRoomType(depth, bias);

            // Generate room properties
            float danger = CalculateDanger(depth, bias);
            RewardCategory reward = ChooseRewardCategory(type, depth);
            float corruptionIncrease = CalculateCorruptionIncrease(depth);
            bool hasElite = ShouldHaveElite(depth, bias);
            bool hasHealing = ShouldHaveHealing(depth);
            bool isExtraction = ShouldBeExtractionPoint(depth);
            bool isUnknown = ShouldBeUnknownEvent(depth);

            string preview = GeneratePreviewText(type, danger, reward, hasElite, hasHealing, isExtraction, isUnknown);

            return CreateNode(
                $"room_{depth}_{_random.Next()}",
                type,
                depth,
                preview,
                reward,
                corruptionIncrease,
                hasElite,
                hasHealing,
                isExtraction,
                isUnknown
            );
        }

        private RoomType ChooseRoomType(int depth, RouteBias bias)
        {
            if (bias == RouteBias.Safe)
            {
                if (depth % 4 == 0) return RoomType.Rest;
                if (depth % 5 == 0) return RoomType.Reward;
                return RoomType.Combat;
            }
            else if (bias == RouteBias.Risky)
            {
                if (depth % 3 == 0) return RoomType.EliteCombat;
                if (depth % 4 == 0) return RoomType.RiskRoom;
                if (depth % 5 == 0) return RoomType.UnknownEvent;
                return RoomType.Combat;
            }
            else
            {
                // Neutral - balanced distribution
                float roll = (float)_random.NextDouble();
                if (roll < 0.5f) return RoomType.Combat;
                if (roll < 0.65f) return RoomType.EliteCombat;
                if (roll < 0.8f) return RoomType.Reward;
                if (roll < 0.9f) return RoomType.Rest;
                if (roll < 0.95f) return RoomType.RiskRoom;
                return RoomType.UnknownEvent;
            }
        }

        private float CalculateDanger(int depth, RouteBias bias)
        {
            float baseDanger = 1f + depth * 0.3f;
            if (bias == RouteBias.Risky) baseDanger += 1f;
            if (bias == RouteBias.Safe) baseDanger -= 0.5f;
            return Mathf.Clamp(baseDanger, 1f, 5f);
        }

        private RewardCategory ChooseRewardCategory(RoomType type, int depth)
        {
            switch (type)
            {
                case RoomType.Reward: return RewardCategory.Mixed;
                case RoomType.RiskRoom: return RewardCategory.Relic;
                case RoomType.EliteCombat: return RewardCategory.WeaponUpgrade;
                case RoomType.Combat: return RewardCategory.Economy;
                case RoomType.BossArena: return RewardCategory.Relic;
                default: return RewardCategory.Unknown;
            }
        }

        private float CalculateCorruptionIncrease(int depth)
        {
            return 3f + depth * 0.5f;
        }

        private bool ShouldHaveElite(int depth, RouteBias bias)
        {
            if (bias == RouteBias.Risky) return depth >= 3;
            if (bias == RouteBias.Safe) return depth >= 6;
            return depth >= 4 && (float)_random.NextDouble() < 0.2f;
        }

        private bool ShouldHaveHealing(int depth)
        {
            return depth % 3 == 0;
        }

        private bool ShouldBeExtractionPoint(int depth)
        {
            return depth == 4 || depth == 6;
        }

        private bool ShouldBeUnknownEvent(int depth)
        {
            return depth >= 5 && (float)_random.NextDouble() < 0.1f;
        }

        private string GeneratePreviewText(RoomType type, float danger, RewardCategory reward, bool hasElite, bool hasHealing, bool isExtraction, bool isUnknown)
        {
            string text = type.ToString();
            text += $" | Danger: {danger:F1}";
            if (hasElite) text += " | ⚠ Elite";
            if (hasHealing) text += " | ❤ Recovery";
            if (isExtraction) text += " | 🚪 Extraction";
            if (isUnknown) text += " | ❓ Unknown";
            if (!isUnknown) text += $" | Reward: {reward}";
            return text;
        }

        private RouteNode CreateNode(string id, RoomType type, int depth, string preview, RewardCategory reward, float corruption, bool hasElite, bool hasHealing, bool isExtraction, bool isUnknown)
        {
            return new RouteNode
            {
                NodeId = id,
                Type = type,
                DangerLevel = Mathf.Clamp(1f + depth * 0.3f, 1f, 5f),
                PossibleReward = reward,
                CorruptionIncrease = corruption,
                HasElite = hasElite,
                HasHealing = hasHealing,
                IsExtractionPoint = isExtraction,
                IsUnknownEvent = isUnknown,
                PreviewInfo = preview,
                Depth = depth,
                IsCompleted = false
            };
        }
    }

    public enum RouteBias
    {
        Safe,
        Risky,
        Neutral
    }
}
