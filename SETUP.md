# RELICFALL Setup Guide

## Prerequisites

- **Unity Hub** (latest stable version)
- **Unity 6.3 LTS** (2024.3 LTS release, installed via Unity Hub)
- **Git** (for version control)
- **Windows 10/11** (primary development platform)

## Initial Setup

### 1. Clone the Repository
```bash
git clone https://github.com/chandrumunisamy/Game_Arena_ai.git
cd Game_Arena_ai
```

### 2. Open in Unity
1. Launch Unity Hub
2. Click "Add" → "Add project from disk"
3. Navigate to the cloned repository folder
4. Select the folder and confirm

### 3. Unity Package Resolution
On first open, Unity will:
- Download and resolve packages from `Packages/manifest.json`
- This includes URP, Input System, TextMeshPro, Cinemachine, etc.
- Wait 5-10 minutes for resolution to complete

### 4. Verify Setup
After package resolution:
- Check that `Assets/Scenes/Hub.unity` loads without errors
- Check that all C# scripts compile without errors
- Open the Console window (Window → General → Console) for any issues

### 5. Run Tests
- Window → General → Test Runner
- Select "Edit Mode" tab
- Run all tests to verify core system functionality

## Development Workflow

### Code Organization
- All game code is in `Assets/Game/` with namespace `Relicfall`
- ScriptableObject definitions are immutable data in `Assets/ScriptableObjects/`
- Runtime state uses plain C# classes, not ScriptableObject mutation
- Assembly definitions separate game code from test code

### Adding New Content
- **New Relic:** Create `RelicDefinition` ScriptableObject, add to `RelicDataGenerator`
- **New Enemy:** Create `EnemyDefinition` ScriptableObject, implement in `EnemyController`
- **New Room:** Create `RoomDefinition` ScriptableObject, add to room pool
- **New Boss:** Create `BossDefinition` ScriptableObject, implement phase logic

### Build & Test Cycle
1. Make code changes
2. Run Edit Mode tests
3. Play-test in editor (start from Hub scene)
4. Validate save/load works
5. Check controller input
6. Profile performance (Window → Analysis → Profiler)

## Performance Targets

- 60 FPS at 1080p on GTX 1650-class hardware
- Steam Deck: 60 FPS at 1280x800
- No major GC spikes during combat
- Object pooling for frequently spawned/despawned objects

## Git Configuration

The `.gitignore` excludes:
- Library/ (Unity generated)
- Build/ outputs
- OS-specific files
- IDE configuration
- Python caches

Important: Do NOT commit Library/ or Temp/ folders.
