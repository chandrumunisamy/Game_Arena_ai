# RELICFALL Architecture Document

## Design Principles

1. **No God Classes** — Each system has a clear single responsibility
2. **Composition Over Inheritance** — Components are assembled, not inherited
3. **Immutable Definitions** — ScriptableObjects for data, plain C# for state
4. **Event-Driven Communication** — EventBus for cross-system messaging
5. **Object Pooling** — Pooled creation for performance-critical objects
6. **Dependency Boundaries** — Systems communicate through events, not direct references

## Module Structure

### Core Layer
- **EventBus** — Typed event publish/subscribe system for decoupled communication
- **GameMath** — Shared math utilities for damage, knockback, hit detection
- **GameTimer/CooldownTimer** — Timer abstractions for cooldowns and durations
- **BufferedInput** — Input buffering system with configurable windows
- **GameObjectPool/PoolManager** — Object pooling for VFX, projectiles, enemies
- **GameManager** — Central state machine for game lifecycle (Hub → Run → Extraction → Death)

### Combat Layer
- **AttackDefinition** — ScriptableObject defining all attack parameters
- **HitboxManager** — Enables/disables hitboxes based on attack state
- **Hurtbox** — Receives damage and passes to HealthComponent
- **HealthComponent** — Health tracking, damage reception, death trigger
- **StaggerComponent** — Stagger buildup and threshold management
- **CombatFeedback** — Hit stop, camera shake, damage flash, impact particles
- **CameraShake** — Adjustable global screen shake intensity

### Player Layer
- **PlayerController** — Central state machine for player actions
  - States: Idle, Moving, LightAttack1-3, HeavyAttack, ChargingHeavy, Dash, Parry, ParrySuccess, HitReaction, Knockback, Execution, AbilityCast, Ultimate, Dead, Interacting
  - Each state has enter/exit/update with clear timing
- **PlayerInputHandler** — New Input System integration with buffering
  - Converts raw input to world-space directions
  - Supports keyboard+mouse and controller
  - Aim assist for controller input
- **WeaponHandler** — Runtime weapon management
  - Three weapon families: Chain Blade, Great Blade, Arcane Pistol & Dagger
  - Weapon-specific special moves (Chain Pull, Shockwave, Mark & Execute)
  - Trail rendering and model swapping
- **RelicManager** — Active relic tracking and synergy detection
  - Collects, removes, and queries active relics
  - Calculates cumulative modifiers (damage, critical, healing, etc.)
  - Detects synergies based on tag combinations

### Enemy Layer
- **EnemyController** — AI state machine for all enemy types
  - States: Idle, Patrol, Alert, Chase, Telegraph, Attacking, Recovery, Retreat, Block, Staggered, HitReaction, Death, Spawn, EliteEntrance
  - Gradual rotation (no instant tracking)
  - Telegraph before every attack
  - Recovery after attacks
- **EnemyDefinition** — ScriptableObject for enemy type data
- **EnemyGroupCoordinator** — Prevents all enemies attacking simultaneously
- **GroundTelegraph** — Visual attack telegraph on arena floor
- **EliteModifier** — Behaviour-changing elite modifiers (not just stat increases)

### Boss Layer
- **BossController** — Extends EnemyController with phases
  - Multiple phases triggered by health thresholds
  - Invulnerability during phase transitions
  - Arena mechanic management
  - Corruption-sensitive attacks
- **ArenaMechanic** — Boss arena hazards and environmental effects

### Relic Layer
- **RelicDefinition** — ScriptableObject with benefit + corruption + synergy tags
- **RelicDataGenerator** — Programmatic generation of 50+ relic definitions
- **ActiveRelic** — Runtime mutable relic instance
- **SynergyEffect** — Tag-based synergy detection and bonus calculation

### Corruption Layer
- **CorruptionTracker** — Tracks corruption level during runs
  - Thresholds: 0-24% (Stable), 25-49% (Distorted), 50-74% (Dangerous), 75-99% (Critical), 100% (Collapsed)
  - Modifies: enemy stats, hazard frequency, reward quality, healing effectiveness, visual distortion
- **CorruptionModifier** — Per-relic corruption effects on the world
- **CorruptionLightingData** — Visual corruption parameters for rendering

### Run Layer
- **RunData** — Mutable runtime state for a single run
- **RunGenerator** — Creates room sequence from handcrafted pool + variable layout
- **RouteNode** — Room choice with preview information

### Room Layer
- **RoomDefinition** — ScriptableObject for handcrafted modular rooms
- **EncounterDefinition** — Enemy spawns and phases within a room
- **RuntimeRoomManager** — Room loading, spawning, completion, transitions

### Progression Layer
- **PermanentProgression** — Unlocks that persist across runs
  - Primarily unlocks OPTIONS (weapons, relics, NPCs, routes)
  - Small stat increases acceptable but not the primary focus
  - Difficulty system with controlled steps

### Narrative Layer
- **NarrativeManager** — Story state tracking across runs
  - NPC relationships (6 hub characters)
  - Boss encounter history
  - Death cause tracking
  - Weapon preference tracking
- **DialogueDefinition** — Short in-engine dialogue with conditions

### UI Layer
- **HUDController** — Health, abilities, corruption, currency, boss health
- **PauseMenu** — Settings, controls rebinding, quit
- **RunSummaryUI** — Statistics after death or extraction
- **RouteSelectionUI** — Branch choice with preview information
- **ExtractionChoiceUI** — Risk/reward extraction decision
- **RelicSelectionUI** — Reward choice after encounters
- **HubUI** — Weapon selection, upgrades, archive, NPCs, training

### Audio Layer
- **MusicSystem** — Adaptive layered music (exploration, combat, corruption, boss, victory, death)
- **SFXManager** — Pooled SFX with layered impact sounds

### Save Layer
- **SaveManager** — Versioned save system with autosave and backup
- **SaveData** — Plain serializable data structure (no ScriptableObject mutation)
- Version migration for future updates

### Settings Layer
- **SettingsManager** — Full configuration support
  - Graphics: resolution, fullscreen, quality, shadows, AA, render scale
  - Audio: master, music, SFX, ambience volumes
  - Accessibility: screen shake, vibration, aim assist, text size, colorblind
  - Controls: full rebinding

## Data Flow

```
PlayerInput → PlayerInputHandler → BufferedInput → PlayerController → 
  WeaponHandler → HitboxManager → Hurtbox → HealthComponent → CombatFeedback

RelicDefinition → RelicManager → PlayerController (modifiers)
                     ↓
               CorruptionTracker → EnemyController (scaling)
                     ↓
               RuntimeRoomManager (visuals)

RunGenerator → RouteNode → RuntimeRoomManager → EncounterDefinition → 
  EnemyController → EnemyGroupCoordinator

GameManager orchestrates: Hub ↔ Run ↔ Extraction ↔ Death ↔ Hub
SaveManager persists: Progression, Settings, Narrative, Statistics
```

## Naming Conventions

- ScriptableObjects: `*Definition` (e.g., `WeaponDefinition`, `RelicDefinition`)
- Runtime classes: Descriptive noun (e.g., `CorruptionTracker`, `PlayerController`)
- Events: `*Event` (e.g., `DamageEvent`, `RelicCollectedEvent`)
- Save data: `*SaveData` (e.g., `ProgressionSaveData`, `SettingsSaveData`)
- Components: `*Component` (e.g., `HealthComponent`, `StaggerComponent`)
- Enums: Singular (e.g., `PlayerState`, `RoomType`)

## Threading & Performance

- All game logic runs on main thread (no multi-threading for simplicity)
- Object pooling reduces GC pressure
- Enemy AI is lightweight state machines (no heavy pathfinding)
- VFX use pooled particle systems
- Camera avoids unnecessary overdraw
- Target: 60 FPS with <1ms GC spikes
