using UnityEngine;
using System.Collections.Generic;
using Relicfall.Core.Events;
using Relicfall.Saving;

namespace Relicfall.Narrative
{
    /// <summary>
    /// Dialogue definition for short in-engine conversations.
    /// Uses portraits/poses and short text, not expensive cinematics.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueDef", menuName = "RELICFALL/Narrative/Dialogue Definition")]
    public class DialogueDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DialogueId;
        public string SpeakerName;
        public Sprite SpeakerPortrait;
        public string[] Lines;
        public DialogueCondition[] Conditions;
        public string[] NextDialogueIds;
        public string TriggerEvent;
    }

    [System.Serializable]
    public class DialogueCondition
    {
        public string ConditionType; // "boss_defeated", "relic_collected", "death_count", "weapon_used", etc.
        public string ConditionValue;
        public int ConditionThreshold;
    }

    /// <summary>
    /// Narrative manager that tracks story state across runs and manages dialogue.
    /// Characters remember: bosses defeated, failed extractions, relics stolen,
    /// corruption choices, favoured factions, repeated deaths, weapon preferences.
    /// Dialogue must progress across runs and avoid repeating excessively.
    /// </summary>
    public class NarrativeManager : MonoBehaviour
    {
        [Header("Hub Characters")]
        [SerializeField] private NpcDefinition[] _hubNpcs;

        private NarrativeSaveData _narrativeState;
        private Dictionary<string, int> _dialogueProgress = new();
        private Dictionary<string, float> _npcRelationships = new();
        private HashSet<string> _seenDialogue = new();

        // NPC definitions (6 required hub characters)
        public static readonly NpcDefinition[] DefaultNpcs = new NpcDefinition[]
        {
            new NpcDefinition { NpcId = "blacksmith", Name = "The Forge-Master", Role = "Weapon upgrades and unlocks", Location = "Forge" },
            new NpcDefinition { NpcId = "scholar", Name = "The Archive Keeper", Role = "Relic knowledge and lore", Location = "Archive" },
            new NpcDefinition { NpcId = "priest", Name = "The Scarred Priest", Role = "Healing and corruption guidance", Location = "Chapel" },
            new NpcDefinition { NpcId = "oracle", Name = "The Blind Oracle", Role = "Route predictions and risk assessment", Location = "Observatory" },
            new NpcDefinition { NpcId = "scarred_veteran", Name = "The Veteran", Role = "Difficulty modifiers and challenge modes", Location = "Barracks" },
            new NpcDefinition { NpcId = "relic_keeper", Name = "The Vault Keeper", Role = "Relic archive and starting relic choices", Location = "Vault" }
        };

        public System.Action<string, string> OnDialogueStarted;
        public System.Action<string, string> OnDialogueLine;
        public System.Action<string> OnDialogueComplete;

        /// <summary>
        /// Initialize narrative state from saved data.
        /// </summary>
        public void InitializeFromSave(NarrativeSaveData saveData)
        {
            _narrativeState = saveData ?? new NarrativeSaveData();

            // Convert save format
            if (_narrativeState.DialogueProgress != null)
                foreach (var entry in _narrativeState.DialogueProgress)
                    _dialogueProgress[entry] = 1;

            if (_narrativeState.NpcRelationships != null)
                foreach (var entry in _narrativeState.NpcRelationships.Entries)
                    _npcRelationships[entry.Key] = entry.Value;
        }

        /// <summary>
        /// Get dialogue for a specific NPC at the current narrative state.
        /// Dialogue progresses and doesn't repeat excessively.
        /// </summary>
        public DialogueDefinition GetDialogueForNpc(string npcId)
        {
            // Check narrative state conditions
            var progression = Core.GameManager.Instance?.Progression;

            // Different dialogue based on:
            // - Runs completed
            // - Bosses defeated
            // - Recent death cause
            // - Current weapon
            // - Relic preferences
            // - NPC relationship level

            // Generate contextual dialogue
            string dialogueId = GenerateContextualDialogueId(npcId);
            return LoadDialogue(dialogueId);
        }

        /// <summary>
        /// Start a dialogue sequence with an NPC.
        /// </summary>
        public void StartDialogue(string npcId)
        {
            var dialogue = GetDialogueForNpc(npcId);
            if (dialogue == null) return;

            OnDialogueStarted?.Invoke(npcId, dialogue.DialogueId);
        }

        /// <summary>
        /// Play the next line of an active dialogue.
        /// </summary>
        public void AdvanceDialogue(string dialogueId)
        {
            if (!_dialogueProgress.ContainsKey(dialogueId))
                _dialogueProgress[dialogueId] = 0;

            int currentLine = _dialogueProgress[dialogueId];
            _dialogueProgress[dialogueId] = currentLine + 1;

            OnDialogueLine?.Invoke(dialogueId, $"line_{currentLine}");

            // Check if dialogue is complete
            _seenDialogue.Add(dialogueId);
        }

        /// <summary>
        /// Record a narrative reaction to a game event.
        /// </summary>
        public void RecordReaction(string eventType, string details)
        {
            switch (eventType)
            {
                case "boss_defeated":
                    _narrativeState.BossesDefeatedNarrative.Add(details);
                    // Increase relationship with scholar and priest
                    ModifyRelationship("scholar", 0.1f);
                    ModifyRelationship("priest", 0.15f);
                    break;
                case "extraction_failed":
                    _narrativeState.FailedExtractions.Add(details);
                    // Oracle and Veteran react to failed extractions
                    ModifyRelationship("oracle", -0.05f);
                    ModifyRelationship("scarred_veteran", 0.1f);
                    break;
                case "relic_choice":
                    _narrativeState.RelicChoicesNarrative.Add(details);
                    ModifyRelationship("relic_keeper", 0.05f);
                    break;
                case "death":
                    _narrativeState.DeathReactions.Add(details);
                    // Priest reacts to deaths
                    ModifyRelationship("priest", 0.1f);
                    break;
            }

            // Generate death-specific reactions
            _narrativeState.LastDeathCause = details;
        }

        /// <summary>
        /// Get boss introduction dialogue.
        /// </summary>
        public string GetBossIntroDialogue(string bossId)
        {
            int encounterCount = GetBossEncounterCount(bossId);

            // Different dialogue for first encounter vs repeated encounters
            if (encounterCount == 0)
                return GetFirstEncounterDialogue(bossId);
            else if (encounterCount < 3)
                return GetRepeatedEncounterDialogue(bossId, encounterCount);
            else
                return GetFrequentEncounterDialogue(bossId);
        }

        /// <summary>
        /// Get run completion reaction dialogue.
        /// </summary>
        public string GetRunCompletionDialogue(bool extracted, float corruption, int relicsCount)
        {
            if (extracted)
            {
                if (corruption > 75f) return "You escaped the collapsing realm. Few dare to return from such corruption.";
                if (relicsCount > 5) return "An impressive haul. The relics call to you, don't they?";
                return "A cautious extraction. Smart, but the depths hold greater power.";
            }
            else
            {
                return "Another fall. But each death teaches you something the living cannot know.";
            }
        }

        private void ModifyRelationship(string npcId, float delta)
        {
            _npcRelationships.TryGetValue(npcId, out float current);
            _npcRelationships[npcId] = Mathf.Clamp(current + delta, -1f, 1f);
        }

        private int GetBossEncounterCount(string bossId)
        {
            return _narrativeState.BossesDefeatedNarrative?.Count(e => e.Contains(bossId)) ?? 0;
        }

        private string GenerateContextualDialogueId(string npcId)
        {
            var progression = Core.GameManager.Instance?.Progression;
            int runs = progression?.RunsCompleted ?? 0;

            // Generate unique dialogue ID based on context
            // Prevents repeating the same dialogue
            string baseId = $"{npcId}_run_{runs}";

            // Check variations
            if (!_seenDialogue.Contains(baseId))
                return baseId;

            // Try alternative dialogue
            for (int i = 1; i <= 5; i++)
            {
                string altId = $"{npcId}_run_{runs}_alt_{i}";
                if (!_seenDialogue.Contains(altId))
                    return altId;
            }

            // Fallback to generic
            return $"{npcId}_generic";
        }

        private DialogueDefinition LoadDialogue(string dialogueId)
        {
            // In full implementation, load from asset database
            // For now, create a simple dialogue definition
            var def = DialogueDefinition.CreateInstance<DialogueDefinition>();
            def.DialogueId = dialogueId;
            def.SpeakerName = dialogueId.Split('_')[0];
            def.Lines = new string[] { $"Dialogue for {dialogueId} - contextual response based on run progress." };
            return def;
        }

        private string GetFirstEncounterDialogue(string bossId)
        {
            return bossId switch
            {
                "oath_breaker_king" => "The Oath-Breaker King rises. His crown is broken, his vow is shattered, but his wrath remains absolute.",
                "thirteenth_regent" => "The Thirteenth Regent steps from the folds of time. What was, what is, and what will be — all attack you at once.",
                "hollow_saint" => "The Hollow Saint awakens. It offers salvation that corrodes, and healing that burns.",
                _ => "A guardian of the cursed realm awaits."
            };
        }

        private string GetRepeatedEncounterDialogue(string bossId, int count)
        {
            return $"The {bossId.Replace('_', ' ')} recognizes you. Again you challenge the broken throne. This time, it will be different.";
        }

        private string GetFrequentEncounterDialogue(string bossId)
        {
            return $"Persistence or obsession? The {bossId.Replace('_', ' ')} no longer cares which. It simply awaits your inevitable return.";
        }
    }

    /// <summary>
    /// NPC definition for hub characters.
    /// </summary>
    [System.Serializable]
    public class NpcDefinition
    {
        public string NpcId;
        public string Name;
        public string Role;
        public string Location;
        public Sprite Portrait;
        public GameObject HubModelPrefab;
        public Color AccentColor;
    }
}
