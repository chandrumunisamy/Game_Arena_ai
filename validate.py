#!/usr/bin/env python3
"""RELICFALL Project Validation Script

Verifies that the Unity project structure is complete and all 
required files, systems, and documentation are present.
"""

import os
import sys

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))

def check_file(path, description):
    """Check that a required file exists."""
    full_path = os.path.join(PROJECT_ROOT, path)
    exists = os.path.exists(full_path)
    status = "✅" if exists else "❌ MISSING"
    print(f"  {status} {description}: {path}")
    return exists

def check_directory(path, description):
    """Check that a required directory exists."""
    full_path = os.path.join(PROJECT_ROOT, path)
    exists = os.path.isdir(full_path)
    status = "✅" if exists else "❌ MISSING"
    print(f"  {status} {description}: {path}")
    return exists

def check_cs_class(filepath, classname):
    """Check that a C# file contains a specific class definition."""
    full_path = os.path.join(PROJECT_ROOT, filepath)
    if not os.path.exists(full_path):
        return False
    with open(full_path, 'r') as f:
        content = f.read()
    found = f"class {classname}" in content or f"public class {classname}" in content or f"static class {classname}" in content
    status = "✅" if found else "❌ NOT FOUND"
    print(f"  {status} Class {classname} in {filepath}")
    return found

def main():
    print("=" * 60)
    print("RELICFALL Project Validation")
    print("=" * 60)
    
    all_passed = True
    
    # === Documentation ===
    print("\n1. DOCUMENTATION")
    docs = [
        ("README.md", "Project readme"),
        ("SETUP.md", "Setup guide"),
        ("CONTROLS.md", "Controls reference"),
        ("ARCHITECTURE.md", "Architecture document"),
        ("GAME_DESIGN.md", "Game design document"),
        ("BUILD_INSTRUCTIONS.md", "Build instructions"),
        ("TEST_REPORT.md", "Test report"),
        ("KNOWN_ISSUES.md", "Known issues"),
        ("CHANGELOG.md", "Changelog"),
        ("Assets/ThirdParty/ASSET_LICENSES.md", "Asset licenses"),
        ("Assets/ThirdParty/CREDITS.txt", "Credits file"),
    ]
    for path, desc in docs:
        if not check_file(path, desc):
            all_passed = False
    
    # === Project Configuration ===
    print("\n2. PROJECT CONFIGURATION")
    configs = [
        ("Packages/manifest.json", "Package manifest"),
        ("ProjectSettings/ProjectSettings.asset", "Project settings"),
        ("ProjectSettings/EditorBuildSettings.asset", "Editor build settings"),
        ("ProjectSettings/TagManager.asset", "Tag manager"),
        ("ProjectSettings/QualitySettings.asset", "Quality settings"),
        (".gitignore", "Git ignore"),
    ]
    for path, desc in configs:
        if not check_file(path, desc):
            all_passed = False
    
    # === Scenes ===
    print("\n3. SCENES")
    scenes = [
        ("Assets/Scenes/Hub.unity", "Hub scene"),
        ("Assets/Scenes/GameRun.unity", "Game run scene"),
        ("Assets/Scenes/BossArena.unity", "Boss arena scene"),
    ]
    for path, desc in scenes:
        if not check_file(path, desc):
            all_passed = False
    
    # === Core Systems ===
    print("\n4. CORE SYSTEMS (C#)")
    core_systems = [
        ("Assets/Game/Core/Events/EventBus.cs", "EventBus", "EventBus"),
        ("Assets/Game/Core/Utils/GameUtils.cs", "GameMath", "GameMath"),
        ("Assets/Game/Core/Utils/GameUtils.cs", "BufferedInput", "BufferedInput"),
        ("Assets/Game/Core/Utils/GameUtils.cs", "PlayerInputBuffer", "PlayerInputBuffer"),
        ("Assets/Game/Core/Pooling/GameObjectPool.cs", "GameObjectPool", "GameObjectPool"),
        ("Assets/Game/Core/GameManager.cs", "GameManager", "GameManager"),
    ]
    for path, desc, classname in core_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Combat Systems ===
    print("\n5. COMBAT SYSTEMS (C#)")
    combat_systems = [
        ("Assets/Game/Combat/CombatFeedback.cs", "CombatFeedback", "CombatFeedback"),
        ("Assets/Game/Combat/HitDetection.cs", "AttackDefinition", "AttackDefinition"),
        ("Assets/Game/Combat/HitDetection.cs", "HitboxManager", "HitboxManager"),
        ("Assets/Game/Combat/HitDetection.cs", "HealthComponent", "HealthComponent"),
        ("Assets/Game/Combat/HitDetection.cs", "StaggerComponent", "StaggerComponent"),
    ]
    for path, desc, classname in combat_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Player Systems ===
    print("\n6. PLAYER SYSTEMS (C#)")
    player_systems = [
        ("Assets/Game/Player/Controller/PlayerController.cs", "PlayerController", "PlayerController"),
        ("Assets/Game/Player/Controller/PlayerInputHandler.cs", "PlayerInputHandler", "PlayerInputHandler"),
        ("Assets/Game/Player/Weapons/WeaponDefinitions.cs", "WeaponDefinition", "WeaponDefinition"),
        ("Assets/Game/Player/Weapons/WeaponDefinitions.cs", "WeaponHandler", "WeaponHandler"),
    ]
    for path, desc, classname in player_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Enemy Systems ===
    print("\n7. ENEMY SYSTEMS (C#)")
    enemy_systems = [
        ("Assets/Game/Enemies/Enemies.cs", "EnemyController", "EnemyController"),
        ("Assets/Game/Enemies/Enemies.cs", "EnemyDefinition", "EnemyDefinition"),
        ("Assets/Game/Enemies/Enemies.cs", "EnemyGroupCoordinator", "EnemyGroupCoordinator"),
    ]
    for path, desc, classname in enemy_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Boss Systems ===
    print("\n8. BOSS SYSTEMS (C#)")
    boss_systems = [
        ("Assets/Game/Bosses/BossSystem.cs", "BossController", "BossController"),
        ("Assets/Game/Bosses/BossSystem.cs", "BossDefinition", "BossDefinition"),
        ("Assets/Game/Bosses/BossSystem.cs", "OathBreakerKing", "OathBreakerKing"),
        ("Assets/Game/Bosses/BossSystem.cs", "ThirteenthRegent", "ThirteenthRegent"),
        ("Assets/Game/Bosses/BossSystem.cs", "HollowSaint", "HollowSaint"),
    ]
    for path, desc, classname in boss_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Relic System ===
    print("\n9. RELIC SYSTEM (C#)")
    relic_systems = [
        ("Assets/Game/Relics/Relics.cs", "RelicDefinition", "RelicDefinition"),
        ("Assets/Game/Relics/Relics.cs", "RelicManager", "RelicManager"),
        ("Assets/Game/Relics/Relics.cs", "RelicDataGenerator", "RelicDataGenerator"),
    ]
    for path, desc, classname in relic_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Corruption System ===
    print("\n10. CORRUPTION SYSTEM (C#)")
    corruption_systems = [
        ("Assets/Game/Corruption/CorruptionSystem.cs", "CorruptionTracker", "CorruptionTracker"),
    ]
    for path, desc, classname in corruption_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Run/Room System ===
    print("\n11. RUN & ROOM SYSTEMS (C#)")
    run_systems = [
        ("Assets/Game/Runs/RunSystem.cs", "RunData", "RunData"),
        ("Assets/Game/Runs/RunSystem.cs", "RunGenerator", "RunGenerator"),
        ("Assets/Game/Rooms/RuntimeRoomManager.cs", "RuntimeRoomManager", "RuntimeRoomManager"),
    ]
    for path, desc, classname in run_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Save/Progression/Settings ===
    print("\n12. SAVE/PROGRESSION/SETTINGS (C#)")
    support_systems = [
        ("Assets/Game/Saving/SaveSystem.cs", "SaveManager", "SaveManager"),
        ("Assets/Game/Saving/SaveSystem.cs", "SaveData", "SaveData"),
        ("Assets/Game/Progression/PermanentProgression.cs", "PermanentProgression", "PermanentProgression"),
        ("Assets/Game/Settings/SettingsManager.cs", "SettingsManager", "SettingsManager"),
    ]
    for path, desc, classname in support_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Audio ===
    print("\n13. AUDIO SYSTEM (C#)")
    audio_systems = [
        ("Assets/Game/Audio/AudioSystem.cs", "MusicSystem", "MusicSystem"),
        ("Assets/Game/Audio/AudioSystem.cs", "SFXManager", "SFXManager"),
    ]
    for path, desc, classname in audio_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === UI ===
    print("\n14. UI SYSTEM (C#)")
    ui_systems = [
        ("Assets/Game/UI/UI.cs", "GameHUD", "GameHUD"),
        ("Assets/Game/UI/UI.cs", "RouteSelectionUI", "RouteSelectionUI"),
        ("Assets/Game/UI/UI.cs", "ExtractionChoiceUI", "ExtractionChoiceUI"),
        ("Assets/Game/UI/UI.cs", "RunSummaryUI", "RunSummaryUI"),
    ]
    for path, desc, classname in ui_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Narrative ===
    print("\n15. NARRATIVE SYSTEM (C#)")
    narrative_systems = [
        ("Assets/Game/Narrative/NarrativeSystem.cs", "NarrativeManager", "NarrativeManager"),
    ]
    for path, desc, classname in narrative_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Tests ===
    print("\n16. TEST SUITE (C#)")
    test_systems = [
        ("Assets/Game/Tests/TestSuite.cs", "CoreSystemTests", "CoreSystemTests"),
    ]
    for path, desc, classname in test_systems:
        if not check_cs_class(path, classname):
            all_passed = False
    
    # === Key Directories ===
    print("\n17. DIRECTORY STRUCTURE")
    directories = [
        "Assets/Game/Core",
        "Assets/Game/Combat",
        "Assets/Game/Player",
        "Assets/Game/Enemies",
        "Assets/Game/Bosses",
        "Assets/Game/Relics",
        "Assets/Game/Corruption",
        "Assets/Game/Runs",
        "Assets/Game/Rooms",
        "Assets/Game/Progression",
        "Assets/Game/Narrative",
        "Assets/Game/UI",
        "Assets/Game/Audio",
        "Assets/Game/Saving",
        "Assets/Game/Settings",
        "Assets/Game/Tests",
        "Assets/Art/Characters",
        "Assets/Art/Environments",
        "Assets/Art/VFX",
        "Assets/Art/Materials",
        "Assets/Scenes",
        "Assets/ThirdParty",
        "Packages",
        "ProjectSettings",
    ]
    for d in directories:
        if not check_directory(d, "Directory"):
            all_passed = False
    
    # === Assembly Definitions ===
    print("\n18. ASSEMBLY DEFINITIONS")
    if not check_file("Assets/Game/Game.asmdef", "Game assembly definition"):
        all_passed = False
    if not check_file("Assets/Game/Tests/Tests.asmdef", "Tests assembly definition"):
        all_passed = False
    
    # === Input Actions ===
    print("\n19. INPUT ACTION ASSET")
    if not check_file("Assets/Game/Input/RELICFALLControls.inputactions", "Input actions asset"):
        all_passed = False
    
    # === Relic Count ===
    print("\n20. RELIC COUNT (50+ Required)")
    relic_path = os.path.join(PROJECT_ROOT, "Assets/Game/Relics/Relics.cs")
    if os.path.exists(relic_path):
        with open(relic_path, 'r') as f:
            content = f.read()
        # Count AddRelic calls
        relic_count = content.count("AddRelic(")
        status = "✅" if relic_count >= 50 else "❌ INSUFFICIENT"
        print(f"  {status} Relic definitions: {relic_count} (minimum: 50)")
        if relic_count < 50:
            all_passed = False
    else:
        print("  ❌ MISSING Relics.cs")
        all_passed = False
    
    # === C# Line Count ===
    print("\n21. CODE SIZE")
    total_lines = 0
    cs_count = 0
    for root, dirs, files in os.walk(PROJECT_ROOT):
        if '.git' in root or '.arena' in root:
            continue
        for f in files:
            if f.endswith('.cs') and not f.endswith('.meta'):
                fp = os.path.join(root, f)
                with open(fp, 'r') as fh:
                    lines = len(fh.readlines())
                    total_lines += lines
                    cs_count += 1
    print(f"  ✅ C# scripts: {cs_count}")
    print(f"  ✅ C# lines: {total_lines}")
    
    # === Result ===
    print("\n" + "=" * 60)
    if all_passed:
        print("✅ ALL VALIDATIONS PASSED")
    else:
        print("❌ SOME VALIDATIONS FAILED - see details above")
    print("=" * 60)
    
    return 0 if all_passed else 1

if __name__ == "__main__":
    sys.exit(main())
