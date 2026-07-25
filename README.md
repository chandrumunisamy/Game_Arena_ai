# RELICFALL

## Game Summary

RELICFALL is a stylized 3D isometric action roguelite where you play as a supernatural relic thief entering cursed kingdoms. You kill guardians, steal reality-warping relics, and face the central risk decision: *"Escape now and bank the rewards, or steal one more relic and risk losing the run?"*

Every stolen relic has two linked effects — a powerful benefit for you, and a corruption effect that makes the world more dangerous. This risk-reward loop drives the entire experience.

**Genre:** Isometric Action Roguelite with Extraction Mechanics  
**Engine:** Unity 6.3 LTS (URP)  
**Platform:** Windows PC / Steam Deck  
**Price Target:** $14.99  
**Rating:** Single-player, offline, no multiplayer, no microtransactions

## Unity Version

- **Unity 6.3 LTS** (2024.3 LTS release)
- Universal Render Pipeline (URP)
- C# with New Input System

## How to Open

1. Install Unity Hub
2. Install Unity 6.3 LTS (2024.3.Xf1)
3. Open Unity Hub → Add project from disk
4. Select the `Game_Arena_ai` folder
5. Unity will resolve packages on first open (may take 5-10 minutes)
6. Once loaded, open `Assets/Scenes/Hub.unity` to begin

## How to Play

### Keyboard & Mouse (Default)
| Key | Action |
|---|---|
| W/A/S/D | Move |
| Left Mouse | Light Attack |
| Right Mouse | Heavy Attack |
| Space | Dash |
| Shift | Parry |
| Q | Relic Ability |
| E | Secondary Ability |
| R | Ultimate |
| F | Interact |
| Tab | Run Info |
| Escape | Pause |

### Controller (Default)
| Input | Action |
|---|---|
| Left Stick | Move |
| Right Stick | Aim |
| X/A Button | Light Attack |
| Right Trigger | Heavy Attack |
| Left Shoulder | Dash |
| Right Shoulder | Parry |
| Y Button | Relic Ability |
| B Button | Secondary Ability |
| Left Trigger | Ultimate |
| Left Stick Press | Interact |
| Start | Pause |
| Select | Run Info |

All controls are rebindable in Settings → Controls.

## How to Build

### Windows Build
1. Open project in Unity 6.3 LTS
2. File → Build Settings
3. Target Platform: Windows
4. Architecture: x64
5. Scenes in Build:
   - Assets/Scenes/Hub.unity
   - Assets/Scenes/GameRun.unity
   - Assets/Scenes/BossArena.unity
6. Player Settings → Product Name: RELICFALL
7. Player Settings → Company Name: Relicfall Studios
8. Build → Choose output folder → Build

### Steam Deck Compatibility
- Use borderless windowed mode
- Target 60 FPS at 1280x800
- Enable controller support through Input System
- Ensure UI scales for 16:10 aspect ratio

## Project Structure

```
Assets/
  Game/                    # Core game code
    Core/                  # Framework, events, utilities, pooling
    Combat/                # Damage, hit detection, state machines, feedback
    Player/                # Player controller, weapons, animation, stats
    Enemies/               # Enemy AI, behaviours, elite system, spawning
    Bosses/                # Boss definitions, phase system, arena mechanics
    Relics/                # Relic definitions, runtime, synergy (50+ relics)
    Corruption/            # Corruption tracker, modifiers, visual system
    Runs/                  # Run data, generation, flow management
    Rooms/                 # Room definitions, runtime manager, encounters
    Progression/           # Permanent unlocks, currency, difficulty
    Narrative/             # Dialogue, NPC relationships, story tracking
    UI/                    # HUD, menus, run summary
    Audio/                 # Music system, SFX manager, ambience
    Saving/                # Save manager, versioned data, migration
    Settings/              # Graphics, audio, accessibility, controls
    Tools/                 # Editor tools, validation
    Tests/                 # Automated test suite
  Art/                     # Visual assets
    Characters/            # Player, enemies, bosses
    Environments/          # Realm rooms, hub, props
    VFX/                   # Particle effects, trails, telegraphs
    Animations/            # Animation clips
    Materials/             # Shaders, material definitions
    Textures/              # Texture assets
    Audio/                 # Music, SFX, ambience
    UI/                    # UI sprites, icons
    Icons/                 # Relic icons, weapon icons
  ScriptableObjects/       # Data definitions (immutable)
  ThirdParty/              # Licensed third-party assets
  Scenes/                  # Unity scene files
  Resources/               # Runtime-loadable assets
Packages/                  # Package manifest and lock files
ProjectSettings/           # Unity project configuration
```

## License Notes

- All third-party assets use verified commercial-use licenses (CC0, etc.)
- See `Assets/ThirdParty/ASSET_LICENSES.md` for complete asset license records
- See `Assets/ThirdParty/CREDITS.txt` for attribution
- No pirated, ripped, or non-commercial assets are used
- AI-generated assets are cleaned and validated before use

## Known Limitations

See `KNOWN_ISSUES.md` for detailed current limitations.
