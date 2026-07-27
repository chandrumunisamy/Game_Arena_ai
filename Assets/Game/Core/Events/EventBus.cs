using System;
using System.Collections.Generic;

namespace Relicfall.Core.Events
{
    /// <summary>
    /// Central event bus for decoupled communication between game systems.
    /// Events are typed and support multiple listeners.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Action<object>>> _subscribers = new();

        /// <summary>
        /// Subscribe to events of a specific type.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = new List<Action<object>>();

            _subscribers[type].Add(e => handler((T)e));
        }

        /// <summary>
        /// Unsubscribe from events of a specific type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var list))
            {
                // Remove by creating a wrapper comparison
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Target == handler.Target && list[i].Method == handler.Method)
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Publish an event to all subscribers of its type.
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    try
                    {
                        list[i](eventData);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"EventBus handler error for {type.Name}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Clear all subscriptions. Use when loading a new scene or resetting state.
        /// </summary>
        public static void ClearAll()
        {
            _subscribers.Clear();
        }

        /// <summary>
        /// Clear subscriptions for a specific event type.
        /// </summary>
        public static void Clear<T>() where T : struct
        {
            var type = typeof(T);
            if (_subscribers.ContainsKey(type))
                _subscribers.Remove(type);
        }
    }

    // === Event Types ===

    // Combat Events
    public struct DamageEvent
    {
        public int TargetInstanceId;
        public float Damage;
        public bool IsCritical;
        public bool IsParryable;
        public UnityEngine.Vector3 HitPosition;
        public UnityEngine.Vector3 HitDirection;
        public string DamageSource;
        public int AttackerInstanceId;
    }

    public struct HitStopEvent
    {
        public float Duration;
        public int TargetInstanceId;
    }

    public struct ParrySuccessEvent
    {
        public int DefenderInstanceId;
        public int AttackerInstanceId;
        public float ParryWindowRemaining;
    }

    public struct ParryAttemptEvent
    {
        public int DefenderInstanceId;
        public float Timestamp;
    }

    public struct EnemyStaggerEvent
    {
        public int EnemyInstanceId;
        public float StaggerDuration;
    }

    public struct EnemyDeathEvent
    {
        public int EnemyInstanceId;
        public string EnemyType;
        public bool IsElite;
        public bool IsExecution;
        public UnityEngine.Vector3 DeathPosition;
    }

    public struct PlayerDeathEvent
    {
        public UnityEngine.Vector3 DeathPosition;
        public string DeathCause;
        public float RunDurationSeconds;
    }

    // Player Events
    public struct PlayerAttackEvent
    {
        public string AttackType;
        public int ComboStep;
        public string WeaponId;
        public bool IsHeavy;
        public bool IsCharged;
    }

    public struct PlayerDashEvent
    {
        public UnityEngine.Vector3 Direction;
        public float Distance;
    }

    public struct PlayerComboAdvanceEvent
    {
        public int ComboStep;
        public string WeaponId;
    }

    public struct WeaponChangeEvent
    {
        public string PreviousWeaponId;
        public string NewWeaponId;
    }

    // Relic Events
    public struct RelicCollectedEvent
    {
        public string RelicId;
        public string Rarity;
        public float CorruptionIncrease;
    }

    public struct RelicActivatedEvent
    {
        public string RelicId;
    }

    public struct RelicSynergyEvent
    {
        public string RelicId1;
        public string RelicId2;
        public string SynergyTag;
    }

    // Corruption Events
    public struct CorruptionThresholdEvent
    {
        public float CorruptionLevel;
        public int ThresholdIndex;
    }

    public struct CorruptionChangedEvent
    {
        public float PreviousLevel;
        public float NewLevel;
        public float Delta;
    }

    public struct RealmCollapseEvent
    {
        public float TimeRemaining;
    }

    // Run Events
    public struct RunStartedEvent
    {
        public string RealmId;
        public string WeaponId;
    }

    public struct RoomCompletedEvent
    {
        public string RoomId;
        public string RoomType;
    }

    public struct RoomEnteredEvent
    {
        public string RoomId;
        public string RoomType;
        public float CorruptionAtEntry;
    }

    public struct RouteChoiceEvent
    {
        public int ChosenRouteIndex;
        public string RouteType;
        public string RouteInfo;
    }

    // Extraction Events
    public struct ExtractionOfferedEvent
    {
        public string RoomId;
        public string[] Options;
    }

    public struct ExtractionChosenEvent
    {
        public string ChosenOption;
        public float ResourcesBanked;
        public float ResourcesLost;
    }

    // Progression Events
    public struct PermanentUnlockEvent
    {
        public string UnlockId;
        public string UnlockType;
    }

    public struct BossDefeatedEvent
    {
        public string BossId;
        public int RunNumber;
    }

    // UI Events
    public struct UINavigationEvent
    {
        public string Target;
    }

    public struct SettingsChangedEvent
    {
        public string SettingName;
        public string NewValue;
    }

    // Audio Events
    public struct MusicLayerEvent
    {
        public string LayerName;
        public bool ShouldPlay;
        public float Intensity;
    }

    public struct SFXPlayEvent
    {
        public string SfxId;
        public UnityEngine.Vector3 Position;
        public float Volume;
        public float Pitch;
    }

    // Save Events
    public struct SaveRequestedEvent
    {
        public bool IsAutoSave;
    }

    public struct SaveCompletedEvent
    {
        public bool Success;
        public string SavePath;
    }
}
