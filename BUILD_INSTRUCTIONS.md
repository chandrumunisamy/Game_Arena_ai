# RELICFALL Build Instructions

## Prerequisites

1. Unity 6.3 LTS installed via Unity Hub
2. Project opened and packages resolved
3. All scenes loaded without errors

## Windows Build (Primary Target)

### Step-by-Step

1. Open the project in Unity 6.3 LTS
2. Go to **File → Build Settings**
3. Verify all scenes are in the build:
   - `Assets/Scenes/Hub.unity` (index 0)
   - `Assets/Scenes/GameRun.unity` (index 1)  
   - `Assets/Scenes/BossArena.unity` (index 2)
4. Select **Platform: Windows**
5. Select **Architecture: x64**
6. Click **Switch Platform** (if not already on Windows)
7. Click **Player Settings** to configure:
   - Product Name: `RELICFALL`
   - Company Name: `Relicfall Studios`
   - Default Icon: Set to `Assets/Art/UI/game_icon.png`
   - Resolution: Default 1920x1080
   - Fullscreen: Fullscreen Window
   - Run in Background: No
   - Display Resolution Dialog: Disabled
   - Supported Orientations: Landscape only
8. Click **Build**
9. Choose output directory (e.g., `Builds/Windows/`)
10. Wait for build to complete
11. Test the built executable

### Build Verification Checklist

After building, verify the following work correctly:

- [ ] Game launches without crashes
- [ ] New game starts from Hub scene
- [ ] WASD movement works
- [ ] Light attack (LMB) works
- [ ] Heavy attack (RMB) works
- [ ] Dash (Space) works
- [ ] Parry (Shift) works
- [ ] Controller input works (if connected)
- [ ] Pause menu opens (Escape)
- [ ] Settings can be changed and persist
- [ ] Save file is created on first run
- [ ] Save loads correctly on restart
- [ ] Full run from hub to boss to extraction
- [ ] Death triggers death summary
- [ ] Extraction triggers banking of rewards
- [ ] Permanent progression persists across runs
- [ ] Controller navigation in menus works
- [ ] Controls rebinding works
- [ ] Resolution changes work
- [ ] V-sync toggle works
- [ ] Quit and resume works

## Steam Deck Build

1. Build as Windows x64
2. In Steam, add as Non-Steam Game
3. Configure launch options for Proton compatibility
4. Recommended settings for Steam Deck:
   - Borderless Windowed mode
   - 1280x800 resolution
   - Medium graphics quality
   - Target 60 FPS (frame rate limit in settings)

## Build Size Optimization

- Compress textures: Use ASTC/BC7 for relevant platforms
- Compress audio: Use Vorbis for music, ADPCM for short SFX
- Strip unused engine code: Enable Managed Stripping Level = High
- Remove unused assets: Run Asset Usage Detection before build

## Command Line Build (Optional)

For automated builds:
```bash
Unity.exe -batchmode -nographics -quit -projectPath "C:\Game_Arena_ai" -buildTarget Win64 -executeMethod BuildScript.BuildWindows
```

Create `Assets/Game/Tools/BuildScript.cs` for custom build automation.
