# RELICFALL Changelog

## v0.1.0 - Initial Implementation (2024-07-25)

### Added

**Core Framework**
- EventBus system for decoupled cross-system communication (25+ event types)
- GameMath utility library (damage calculation, remap, hit detection, knockback)
- Timer system (GameTimer, CooldownTimer) for cooldowns and durations
- BufferedInput system with configurable windows for responsive combat
- PlayerInputBuffer combining all buffered inputs
- GameObjectPool and PoolManager for performance-critical object reuse

**Combat System**
- AttackDefinition ScriptableObject for all attack parameters
- HitboxManager with enable/disable based on attack progress
- Hurtbox and HealthComponent for damage reception
- StaggerComponent for stagger buildup and threshold management
- CombatFeedback with hit stop, camera shake, damage flash, impact particles
- CameraShake with global adjustable intensity
- ImpactType and MaterialType enums for feedback classification

**Player System**
- PlayerController with full state machine (16 states)
- PlayerInputHandler with New Input System integration
- Keyboard+mouse and controller support with aim assist
- Input buffering with configurable windows per action
- Light attack combo (3 steps) with cancel windows
- Heavy attack and charged heavy attack
- Dash with IFrames and cooldown
- Parry with window duration and success counter
- Hit reactions, knockback, and death states
- WeaponHandler with three weapon families
- Chain Blade (fast, crowd control, chain pull, area spin)
- Great Blade (heavy, parry-focused, shockwave upgrade)
- Arcane Pistol & Dagger (hybrid ranged/melee, mark & execute, dash shot)
- Projectile system for ranged attacks
- ExecutionMark for mark-and-execute mechanic

**Camera System**
- IsometricCameraController with fixed elevated perspective
- Wall avoidance (camera doesn't get stuck behind geometry)
- Player outline when obscured
- Combat zoom and boss zoom
- Smooth follow with look-ahead

**Enemy System**
- EnemyController with full AI state machine (15 states)
- EnemyDefinition ScriptableObject for all enemy data
- 10 core enemy types (Sword Guard, Shield Guard, Spear Guard, Archer, Mage, Heavy Knight, Assassin, Summoner, Living Statue, Corruption Beast)
- EnemyGroupCoordinator prevents unfair simultaneous attacks
- Ground telegraph system for readable attack warnings
- Elite modifier system (10 behaviour-changing modifiers)
- Corruption variant scaling
- EnemyMarker for identification

**Boss System**
- BossController extending EnemyController with phases
- BossDefinition with multi-phase data
- ArenaMechanic system (9 mechanic types)
- Three realm bosses:
  - Oath-Breaker King (polearm, royal guard summons, relic counters)
  - Thirteenth Regent (time distortion, attack echoes)
  - Hollow Saint (sacred zones, anti-healing)
- FinalBoss that adapts to player progression
- AttackEcho for delayed attack echoes
- SacredZone with benefit→corruption transition

**Relic System**
- RelicDefinition ScriptableObject with benefit + corruption + synergy tags
- 50+ relics across 11 categories:
  - 10 Offensive relics
  - 8 Defensive relics
  - 6 Mobility relics
  - 4 Parry relics
  - 4 Critical relics
  - 3 Summon relics
  - 4 Status effect relics
  - 4 Economy relics
  - 4 Corruption manipulation relics
  - 5 Cursed (high-risk) relics
  - 2 Extraction relics
- RelicManager with collection, removal, synergy detection
- Tag-based synergy system
- RelicDataGenerator for programmatic generation
- Weighted random selection based on rarity and conditions

**Corruption System**
- CorruptionTracker with threshold system (5 tiers)
- Corruption affects: enemies, hazards, rewards, healing, elites, visuals, music
- CorruptionModifier per relic
- CorruptionLightingData for visual distortion
- Passive corruption growth during runs
- Realm collapse at 100% with emergency timer

**Run System**
- RunData for mutable runtime state
- RunGenerator creating room sequences
- RouteNode with preview information
- ExtractionOptions with 6 choice types
- RoomDefinition ScriptableObject
- EncounterDefinition ScriptableObject

**Room System**
- RuntimeRoomManager for room loading, spawning, completion
- Procedural room generation from templates (fallback)
- Corruption visual modifications (debris, cracks)
- Enemy spawn system with position distribution
- Room completion tracking and transition logic

**Progression System**
- PermanentProgression primarily unlocks options
- Weapon unlocks, relic discoveries, NPC unlocks
- Difficulty system (1-10 scale) with scaling modifiers
- Scar system for permanent consequences
- Currency management
- Auto-unlock milestones based on runs/bosses

**Save System**
- SaveManager with versioned JSON serialization
- Autosave with configurable interval
- Backup saves for corruption recovery
- Save migration between versions
- Complete SaveData structure covering all game state
- No runtime ScriptableObject mutation

**Audio System**
- MusicSystem with adaptive layers (exploration, combat, corruption, boss, victory, death)
- SFXManager with pooled audio sources
- Layered impact sounds (5 layers per hit)

**UI System (Code)**
- HUDController (health, abilities, corruption, boss health, currency)
- PauseMenu (settings, controls, quit)
- RunSummaryUI (statistics display)
- RouteSelectionUI (branch choice with preview)
- ExtractionChoiceUI (risk/reward decision)
- RelicSelectionUI (reward choice after encounters)
- HubUI (weapon selection, upgrades, archive, NPCs)
- NpcDialogueUI (short conversations with portraits)

**Settings System**
- SettingsManager with full configuration
- Graphics: resolution, fullscreen, quality, shadows, AA, render scale
- Audio: 4 volume sliders
- Accessibility: screen shake, vibration, aim assist, text size, colorblind
- All settings persist across sessions

**Narrative System**
- NarrativeManager tracking story across runs
- 6 hub NPCs (Blacksmith, Scholar, Priest, Oracle, Veteran, Vault Keeper)
- Boss introduction dialogue
- Death reactions, extraction reactions
- NPC relationship tracking

**Automated Tests**
- 31 edit-mode tests covering:
  - Damage calculations (8 tests)
  - Corruption thresholds (13 tests)
  - Relic combinations (8 tests)
  - Route generation (4 tests)
  - Save serialization (2 tests)
  - Progression (3 tests)
  - Input/timer (5 tests)

### Documentation
- README.md (project summary, how to open, controls, structure)
- SETUP.md (prerequisites, development workflow)
- CONTROLS.md (full control reference)
- ARCHITECTURE.md (module structure, data flow, conventions)
- GAME_DESIGN.md (core loop, relic system, weapons, enemies, bosses)
- BUILD_INSTRUCTIONS.md (step-by-step Windows build guide)
- TEST_REPORT.md (automated test results)
- KNOWN_ISSUES.md (current limitations and resolution priority)
- CHANGELOG.md (this file)
- ASSET_LICENSES.md (third-party license tracking)
- CREDITS.txt (attribution)

### Project Configuration
- Unity 6.3 LTS project structure
- URP package configuration
- New Input System package
- Project settings (quality, physics, tags, layers, editor)
- Assembly definitions (Game, Tests)
- .gitignore for Unity
