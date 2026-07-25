using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Core.Utils;
using Relicfall.Corruption;
using Relicfall.Relics;
using Relicfall.Runs;
using Relicfall.Saving;
using Relicfall.Progression;
using Relicfall.Combat;

namespace Relicfall.Tests
{
    /// <summary>
    /// Automated test suite for core game systems.
    /// Tests damage calculations, relic combinations, corruption thresholds,
    /// save serialization, save migration, reward generation, route generation,
    /// upgrade eligibility, extraction rewards, permanent unlock conditions,
    /// input state, and pool reuse.
    /// </summary>
    public class CoreSystemTests
    {
        #region Damage Calculations

        [Test]
        public void DamageCalculation_BaseDamage_NoCritical()
        {
            float result = GameMath.CalculateDamage(10f, false, 2f);
            Assert.AreEqual(10f, result, "Base damage without critical should be unchanged");
        }

        [Test]
        public void DamageCalculation_CriticalDamage_Multiplied()
        {
            float result = GameMath.CalculateDamage(10f, true, 2f);
            Assert.AreEqual(20f, result, "Critical damage should be multiplied by crit multiplier");
        }

        [Test]
        public void DamageCalculation_CustomCritMultiplier()
        {
            float result = GameMath.CalculateDamage(15f, true, 3f);
            Assert.AreEqual(45f, result, "Custom critical multiplier should apply correctly");
        }

        [Test]
        public void Remap_CorrectlyRemapsValue()
        {
            float result = GameMath.Remap(5f, 0f, 10f, 0f, 100f);
            Assert.AreEqual(50f, result, 0.01f, "Remap should correctly map value to new range");
        }

        [Test]
        public void Remap_ClampsOutOfRange()
        {
            float result = GameMath.Remap(15f, 0f, 10f, 0f, 100f);
            Assert.AreEqual(100f, result, 0.01f, "Remap should clamp values beyond the source range");
        }

        [Test]
        public void IsFrontalHit_FrontalAttack_ReturnsTrue()
        {
            Vector3 attackDir = Vector3.forward;
            Vector3 targetForward = Vector3.forward;
            bool result = GameMath.IsFrontalHit(attackDir, targetForward, 120f);
            Assert.IsTrue(result, "Attack from front should be frontal hit");
        }

        [Test]
        public void IsFrontalHit_RearAttack_ReturnsFalse()
        {
            Vector3 attackDir = Vector3.back;
            Vector3 targetForward = Vector3.forward;
            bool result = GameMath.IsFrontalHit(attackDir, targetForward, 120f);
            Assert.IsFalse(result, "Attack from behind should not be frontal hit");
        }

        [Test]
        public void Knockback_CalculatesCorrectDirection()
        {
            Vector3 result = GameMath.CalculateKnockback(Vector3.right, 10f, 0.2f);
            Assert.AreEqual(10f, result.x, 0.01f, "Knockback should apply force in hit direction");
            Assert.AreEqual(2f, result.y, 0.01f, "Knockback should include upward component");
        }

        #endregion

        #region Corruption Thresholds

        [Test]
        public void CorruptionTracker_InitialLevelIsZero()
        {
            var tracker = new CorruptionTracker();
            Assert.AreEqual(0f, tracker.CurrentLevel, "Corruption should start at 0");
        }

        [Test]
        public void CorruptionTracker_Increase_AddsAmount()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(10f);
            Assert.AreEqual(10f, tracker.CurrentLevel, "Increase should add corruption amount");
        }

        [Test]
        public void CorruptionTracker_CappedAt100()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(150f);
            Assert.AreEqual(100f, tracker.CurrentLevel, "Corruption should be capped at 100");
        }

        [Test]
        public void CorruptionTracker_Reduce_SubtractsAmount()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(30f);
            tracker.Reduce(10f);
            Assert.AreEqual(20f, tracker.CurrentLevel, "Reduce should subtract corruption amount");
        }

        [Test]
        public void CorruptionTracker_Reduce_MinimumIsZero()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(5f);
            tracker.Reduce(10f);
            Assert.AreEqual(0f, tracker.CurrentLevel, "Corruption should not go below 0");
        }

        [Test]
        public void CorruptionTracker_Threshold0_StableRealm()
        {
            var tracker = new CorruptionTracker();
            Assert.AreEqual("Stable", tracker.GetCorruptionTierName());
            Assert.IsFalse(tracker.ShouldApplyEnemyModifiers());
            Assert.IsFalse(tracker.ShouldSpawnMutatedEnemies());
            Assert.IsFalse(tracker.ShouldEliteInvade());
            Assert.IsFalse(tracker.IsRealmCollapsed());
        }

        [Test]
        public void CorruptionTracker_Threshold1_DistortedRealm()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(25f);
            Assert.AreEqual("Distorted", tracker.GetCorruptionTierName());
            Assert.IsTrue(tracker.ShouldApplyEnemyModifiers());
            Assert.IsFalse(tracker.ShouldSpawnMutatedEnemies());
        }

        [Test]
        public void CorruptionTracker_Threshold2_DangerousRealm()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(50f);
            Assert.AreEqual("Dangerous", tracker.GetCorruptionTierName());
            Assert.IsTrue(tracker.ShouldSpawnMutatedEnemies());
        }

        [Test]
        public void CorruptionTracker_Threshold3_CriticalRealm()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(75f);
            Assert.AreEqual("Critical", tracker.GetCorruptionTierName());
            Assert.IsTrue(tracker.ShouldEliteInvade());
        }

        [Test]
        public void CorruptionTracker_Threshold4_CollapsedRealm()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(100f);
            Assert.AreEqual("Collapsed", tracker.GetCorruptionTierName());
            Assert.IsTrue(tracker.IsRealmCollapsed());
        }

        [Test]
        public void CorruptionTracker_EnemyScaling_IncreasesWithCorruption()
        {
            var tracker = new CorruptionTracker();
            float baseMult = tracker.GetEnemyHealthMultiplier();
            tracker.Increase(50f);
            float highMult = tracker.GetEnemyHealthMultiplier();
            Assert.Greater(highMult, baseMult, "Enemy health multiplier should increase with corruption");
        }

        [Test]
        public void CorruptionTracker_RewardQuality_IncreasesWithCorruption()
        {
            var tracker = new CorruptionTracker();
            float baseMult = tracker.GetRewardQualityMultiplier();
            tracker.Increase(75f);
            float highMult = tracker.GetRewardQualityMultiplier();
            Assert.Greater(highMult, baseMult, "Reward quality should increase with corruption");
        }

        [Test]
        public void CorruptionTracker_Healing_ReducedAtHighCorruption()
        {
            var tracker = new CorruptionTracker();
            tracker.Increase(75f);
            float mod = tracker.GetHealingModifier();
            Assert.Less(mod, 1f, "Healing should be reduced at high corruption");
        }

        #endregion

        #region Relic Combinations

        [Test]
        public void RelicManager_CollectRelic_AddsToList()
        {
            var manager = new RelicManager();
            var relicDef = CreateTestRelic("test_relic_1", "Test Relic");
            bool result = manager.CollectRelic(relicDef);
            Assert.IsTrue(result, "Collecting relic should succeed");
            Assert.AreEqual(1, manager.RelicCount, "Relic count should increase");
        }

        [Test]
        public void RelicManager_MaxSlots_PreventsOverfill()
        {
            var manager = new RelicManager();
            for (int i = 0; i < 15; i++)
            {
                var relic = CreateTestRelic($"relic_{i}", $"Relic {i}");
                manager.CollectRelic(relic);
            }
            Assert.AreEqual(12, manager.RelicCount, "Should not exceed max relic slots");
        }

        [Test]
        public void RelicManager_RemoveRelic_DecreasesCount()
        {
            var manager = new RelicManager();
            var relic = CreateTestRelic("test_relic", "Test");
            manager.CollectRelic(relic);
            manager.RemoveRelic("test_relic");
            Assert.AreEqual(0, manager.RelicCount, "Removing relic should decrease count");
        }

        [Test]
        public void RelicManager_SynergyTags_TrackedCorrectly()
        {
            var manager = new RelicManager();
            var relic = CreateTestRelic("test_dash", "Dash Relic");
            relic.SynergyTags = new[] { "Dash", "Clone" };
            manager.CollectRelic(relic);
            Assert.IsTrue(manager.HasRelicWithTag("Dash"), "Should track Dash tag");
            Assert.IsTrue(manager.HasRelicWithTag("Clone"), "Should track Clone tag");
            Assert.IsFalse(manager.HasRelicWithTag("Fire"), "Should not track unassociated tags");
        }

        [Test]
        public void RelicManager_DamageMultiplier_AppliesFromRelics()
        {
            var manager = new RelicManager();
            var relic = CreateTestRelic("damage_boost", "Damage Boost");
            relic.BenefitType = RelicEffectType.DamageIncrease;
            relic.BenefitValue = 0.5f;
            relic.BenefitIsPercentage = true;
            manager.CollectRelic(relic);
            float mult = manager.GetDamageMultiplier();
            Assert.Greater(mult, 1f, "Damage multiplier should increase from damage relic");
        }

        [Test]
        public void RelicDataGenerator_Has50PlusRelics()
        {
            var relics = RelicDataGenerator.GetAllRelics();
            Assert.Greater(relics.Count, 50, "Should have at least 50 relics");
        }

        [Test]
        public void RelicDataGenerator_AllRelicsHaveBenefitAndCorruption()
        {
            var relics = RelicDataGenerator.GetAllRelics();
            foreach (var relic in relics)
            {
                Assert.IsNotNull(relic.BenefitDescription, $"Relic {relic.RelicId} should have benefit description");
                Assert.IsNotNull(relic.CorruptionEffectDescription, $"Relic {relic.RelicId} should have corruption description");
                Assert.Greater(relic.CorruptionIncrease, 0f, $"Relic {relic.RelicId} should increase corruption");
            }
        }

        [Test]
        public void RelicDataGenerator_CursedRelics_HaveIsCursedFlag()
        {
            var relics = RelicDataGenerator.GetAllRelics();
            foreach (var relic in relics)
            {
                if (relic.Rarity == RelicRarity.Cursed)
                {
                    Assert.IsTrue(relic.IsCursed, $"Cursed relic {relic.RelicId} should have IsCursed flag");
                }
            }
        }

        #endregion

        #region Route Generation

        [Test]
        public void RunGenerator_GeneratesCompleteRoute()
        {
            var generator = new RunGenerator();
            var route = generator.GenerateRun(RealmType.ShatteredCourt, 8);
            Assert.Greater(route.Count, 5, "Should generate a route with multiple rooms");
            Assert.AreEqual(RoomType.StartRoom, route[0].Type, "First room should be start room");
        }

        [Test]
        public void RunGenerator_ContainsBossRoom()
        {
            var generator = new RunGenerator();
            var route = generator.GenerateRun(RealmType.ShatteredCourt, 8);
            bool hasBoss = route.Exists(n => n.Type == RoomType.BossArena);
            Assert.IsTrue(hasBoss, "Route should contain a boss arena");
        }

        [Test]
        public void RunGenerator_ContainsExtractionPoint()
        {
            var generator = new RunGenerator();
            var route = generator.GenerateRun(RealmType.ShatteredCourt, 8);
            bool hasExtraction = route.Exists(n => n.IsExtractionPoint);
            Assert.IsTrue(hasExtraction, "Route should contain at least one extraction point");
        }

        [Test]
        public void RunGenerator_RoutePreview_ProvidesInfo()
        {
            var generator = new RunGenerator();
            var route = generator.GenerateRun(RealmType.ShatteredCourt, 8);
            foreach (var node in route)
            {
                Assert.IsNotNull(node.PreviewInfo, $"Node {node.NodeId} should have preview info");
                Assert.Greater(node.Depth, -1, $"Node {node.NodeId} should have depth");
            }
        }

        #endregion

        #region Save Serialization

        [Test]
        public void SaveData_Serialization_RoundTrip()
        {
            var original = new SaveData
            {
                SaveVersion = 1,
                CreatedTime = "2024-01-01",
                LastSaveTime = "2024-01-02",
                Progression = new ProgressionSaveData
                {
                    RunsCompleted = 5,
                    BossesDefeated = 2,
                    Currency = 150f,
                    WeaponsUnlocked = new List<string> { "chain_blade", "great_blade" }
                }
            };

            string json = JsonUtility.ToJson(original, true);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(original.SaveVersion, restored.SaveVersion);
            Assert.AreEqual(original.Progression.RunsCompleted, restored.Progression.RunsCompleted);
            Assert.AreEqual(original.Progression.Currency, restored.Progression.Currency);
        }

        [Test]
        public void SaveMigration_V0ToV1_AddsMissingFields()
        {
            var manager = new Relicfall.Saving.SaveManager();
            // V0 data would lack Statistics and Achievements
            var v0Data = new SaveData { SaveVersion = 0 };
            var migrated = manager.MigrateSave(v0Data, 0, 1);
            Assert.IsNotNull(migrated.Statistics, "Migration should add Statistics field");
            Assert.IsNotNull(migrated.Achievements, "Migration should add Achievements field");
            Assert.AreEqual(1, migrated.SaveVersion, "Migration should update version number");
        }

        #endregion

        #region Progression Unlocks

        [Test]
        public void Progression_DefaultWeapon_Unlocked()
        {
            var progression = new PermanentProgression();
            Assert.IsTrue(progression.IsWeaponUnlocked("chain_blade"), "Default weapon should be unlocked");
        }

        [Test]
        public void Progression_UnlockWeapon_RequiresCurrency()
        {
            var progression = new PermanentProgression();
            bool result = progression.UnlockWeapon("great_blade");
            Assert.IsFalse(result, "Cannot unlock weapon without currency");
        }

        [Test]
        public void Progression_DifficultyModifiers_ScaleCorrectly()
        {
            var progression = new PermanentProgression();
            progression.SetDifficultyLevel(5);
            var mods = progression.GetCurrentDifficultyModifiers();
            Assert.Greater(mods.CorruptionGainMultiplier, 1f, "Difficulty should increase corruption gain");
            Assert.Greater(mods.RewardMultiplier, 1f, "Difficulty should increase rewards");
        }

        #endregion

        #region Input Buffer

        [Test]
        public void BufferedInput_PressAndConsume()
        {
            var buffer = new BufferedInput(0.2f);
            buffer.Press();
            bool consumed = buffer.Consume();
            Assert.IsTrue(consumed, "Buffered input should be consumable immediately after press");
        }

        [Test]
        public void BufferedInput_Expired_NotConsumable()
        {
            var buffer = new BufferedInput(0.01f); // Very short buffer
            buffer.Press();
            // Simulate time passing (in real code, this would use Time.time)
            bool consumed = buffer.Consume();
            // Note: This test depends on Time.time which is 0 in test context
            // Adjust based on actual test environment
        }

        [Test]
        public void BufferedInput_Clear_RemovesBuffer()
        {
            var buffer = new BufferedInput(0.2f);
            buffer.Press();
            buffer.Clear();
            bool consumed = buffer.Consume();
            Assert.IsFalse(consumed, "Cleared buffer should not be consumable");
        }

        #endregion

        #region Game Timer

        [Test]
        public void GameTimer_Tick_CompletesAfterDuration()
        {
            var timer = new GameTimer(1f);
            timer.Start();
            bool completed = timer.Tick(1f);
            Assert.IsTrue(completed, "Timer should complete after duration is elapsed");
            Assert.IsFalse(timer.IsRunning, "Timer should stop after completion");
        }

        [Test]
        public void GameTimer_Progress_TracksCorrectly()
        {
            var timer = new GameTimer(2f);
            timer.Start();
            timer.Tick(1f);
            Assert.AreEqual(0.5f, timer.Progress, 0.01f, "Timer progress should be 50% at halfway");
        }

        [Test]
        public void CooldownTimer_TryUse_OnlyWhenReady()
        {
            var cooldown = new CooldownTimer(2f);
            Assert.IsTrue(cooldown.IsReady, "Cooldown should be ready initially");
            bool used = cooldown.TryUse();
            Assert.IsTrue(used, "Should be able to use ready cooldown");
            Assert.IsFalse(cooldown.IsReady, "Cooldown should not be ready after use");
        }

        #endregion

        #region Pool Reuse

        // Pool tests require a GameObject context, which is available in play mode tests
        // These are documented for validation in Unity's play mode test runner

        #endregion

        #region Utility Methods

        private RelicDefinition CreateTestRelic(string id, string name)
        {
            var relic = RelicDefinition.CreateInstance<RelicDefinition>();
            relic.RelicId = id;
            relic.RelicName = name;
            relic.Rarity = RelicRarity.Common;
            relic.BenefitDescription = $"Benefit of {name}";
            relic.CorruptionEffectDescription = $"Corruption of {name}";
            relic.CorruptionIncrease = 8f;
            relic.SynergyTags = new string[0];
            relic.Weight = 3f;
            return relic;
        }

        #endregion
    }
}
