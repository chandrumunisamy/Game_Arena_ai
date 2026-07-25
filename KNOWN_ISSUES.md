# RELICFALL Known Issues

## Critical Issues

1. **No Unity Editor Build Available in Sandbox**
   - The sandbox environment does not have Unity editor installed
   - A Windows build must be created by opening the project in Unity 6.3 LTS on a local machine
   - Follow BUILD_INSTRUCTIONS.md for build steps

2. **Scene Files Require Unity Editor for Final Assembly**
   - Scene YAML files need Unity editor to properly create GameObject hierarchies
   - The current scene files contain basic structure but need editor validation

## Moderate Issues

3. **3D Character Models Not Yet Created**
   - Player character uses capsule primitive placeholder
   - Enemy models use capsule primitives with color coding
   - Boss models need custom mesh creation or free asset integration
   - Replacement requires Blender or asset sourcing

4. **Animation Clips Not Yet Imported**
   - Animator calls reference animation names but clips are not yet created
   - Mixamo animations need to be downloaded and imported
   - Animation state machine needs Unity Animator window configuration

5. **Audio Clips Not Yet Sourced**
   - Music and SFX system code is complete but audio files need sourcing
   - Recommended: Freesound.org (CC0) for SFX, compose or license music
   - Audio library in SFXManager needs clip references populated

6. **URP Renderer Data Not Configured**
   - URP Asset needs to be created and configured in Unity editor
   - Post-processing features (motion blur, chromatic aberration) need setup
   - Custom shaders need Shader Graph creation

7. **InputActionAsset Not Created**
   - PlayerInputHandler creates inline InputActions
   - For proper rebinding support, a .inputactions asset file is needed
   - Created through Unity Input System editor window

## Minor Issues

8. **Relic Pickup 3D Models Not Created**
   - RelicDefinition references PickupModelPrefab but models not yet made
   - Icons referenced but sprite assets not created
   - Can use AI generation for relic/crystal/mask/crown models

9. **Room Prefabs Not Created as Unity Assets**
   - RuntimeRoomManager generates rooms procedurally from primitives
   - Handcrafted room prefabs need to be built in Unity editor
   - Modular room snapping architecture not yet implemented

10. **UI Canvas Not Assembled in Scenes**
    - UI code (HUD, Pause, Route Selection, etc.) exists but needs Canvas setup
    - Requires Unity UI system scene assembly
    - Controller navigation setup needed in EventSystem

11. **EnemyDefinition ScriptableObjects Not Created as Assets**
    - Enemy definitions are generated at runtime via LoadEnemyDefinition
    - Should be converted to .asset files for better performance and editor editing

12. **VFX Prefabs Not Created**
    - CombatFeedback references VFX prefabs that need creation
    - Weapon trails, impact particles, dash trails need Unity VFX system
    - Ground telegraph visuals need implementation

## Design Decisions & Compromises

- **Procedural room generation** used instead of handcrafted prefabs due to sandbox limitations
- **Inline InputActions** used instead of InputActionAsset due to no Unity editor access
- **Primitive-based enemy/player models** used as placeholders during prototyping
- **Runtime relic data generation** used instead of ScriptableObject assets for rapid iteration
- **Code-first approach** ensures all game logic is functional before asset integration

## Resolution Priority

1. Open project in Unity 6.3 LTS editor
2. Create URP Asset and configure renderer
3. Create InputActionAsset for proper rebinding
4. Import animations from Mixamo
5. Source audio clips from Freesound (CC0)
6. Create or source character/enemy/boss 3D models
7. Build room prefabs as Unity assets
8. Assemble UI Canvas in scenes
9. Create VFX prefabs for combat feedback
10. Create ScriptableObject .asset files for definitions
11. Perform combat timing and responsiveness passes
12. Build Windows executable and validate
