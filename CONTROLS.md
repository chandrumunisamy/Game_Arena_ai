# RELICFALL Controls Reference

## Default Keyboard & Mouse Controls

| Action | Default Binding | Notes |
|---|---|---|
| **Move** | W/A/S/D | Eight-directional movement, camera-relative |
| **Light Attack** | Left Mouse Button | Three-hit combo with buffering |
| **Heavy Attack** | Right Mouse Button | Hold to charge; tap for quick heavy |
| **Dash** | Space | Short-range dash with IFrames |
| **Parry** | Shift | Timing-based defensive action |
| **Relic Ability** | Q | Uses currently equipped relic power |
| **Secondary Ability** | E | Weapon-specific secondary action |
| **Ultimate** | R | High-impact ability with long cooldown |
| **Interact** | F | Pick up items, open doors, talk to NPCs |
| **Run Info** | Tab | Shows current run statistics and relics |
| **Pause** | Escape | Opens pause menu |

## Default Controller Controls

| Action | Default Binding | Notes |
|---|---|---|
| **Move** | Left Stick | Eight-directional, camera-relative |
| **Aim** | Right Stick | Aim direction for attacks and abilities |
| **Light Attack** | X / A (West) | Three-hit combo with buffering |
| **Heavy Attack** | Right Trigger | Hold for charged heavy |
| **Dash** | Left Shoulder (LB) | Short dash with IFrames |
| **Parry** | Right Shoulder (RB) | Timing-based parry |
| **Relic Ability** | Y (North) | Relic-specific power |
| **Secondary Ability** | B (East) | Weapon secondary |
| **Ultimate** | Left Trigger (LT) | Long cooldown high-impact |
| **Interact** | Left Stick Press | Contextual interaction |
| **Pause** | Start | Opens pause menu |
| **Run Info** | Select / Back | Shows run stats overlay |

## Combat Input System

### Input Buffering
- All combat actions support input buffering (0.15-0.25s window)
- Pressing an action during the last 30% of an attack animation queues the next action
- This prevents "dead time" between attacks and makes combat feel responsive

### Cancel Windows
- Light attacks can cancel into: Dash, Parry, next combo step
- Heavy attacks can cancel into: Dash (during early phase)
- Dash and Parry can interrupt most attack states
- No action is animation-locked beyond its cancel window

### Parry Mechanics
- Parry window: 0.3 seconds (adjustable via relics)
- Successful parry: Staggers attacker, opens counter-attack window (1.5s)
- Failed parry: Brief vulnerability, no IFrames
- Parry can deflect all parryable attacks (marked by telegraph color)

### Combo System
- 3-step light combo with increasing damage and speed
- Combo resets after 0.8 seconds of no attack input
- Combo finishers have wider hitboxes and more stagger damage
- Weapon family determines combo behavior:
  - **Chain Blade:** Fast, wide sweeps, third hit is a spin
  - **Great Blade:** Slow, heavy hits, third hit is a slam
  - **Arcane Pistol & Dagger:** Alternating shot/dagger, third hit is an execution attempt

## Aim Assist

- **Mouse:** Precise raycast to ground plane; no aim assist needed
- **Controller:** Aim assist snaps direction toward nearest enemy within 15° cone
- Aim assist strength is adjustable in Settings (0-100%)
- Aim assist does NOT auto-target; it only adjusts the aim direction slightly

## Rebinding

All controls can be fully rebound in Settings → Controls:
- Keyboard bindings: Per-key per-action
- Mouse bindings: Button and scroll wheel
- Controller bindings: Per-button per-action
- Multiple bindings per action supported (e.g., both Space and Shift for Dash)
- Conflicting bindings are highlighted
- Reset to defaults button available

## Accessibility

- High-contrast telegraph mode (yellow/red attack indicators)
- Colorblind modes (Protanopia, Deuteranopia, Tritanopia)
- Adjustable text sizes (Small, Medium, Large)
- Controller vibration toggle
- Screen shake intensity slider (0-200%)
- Aim assist strength slider
