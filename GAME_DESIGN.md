# RELICFALL Game Design Document

## Core Loop

```
Hub (Prepare) → Enter Realm → Combat Room → Route Choice → Combat Room → Extraction Decision → 
  Extract (Bank Resources → Hub) OR Continue (Higher Risk/Reward → Deeper Rooms) → 
  Boss Arena → Final Extraction → Hub
```

## Central Decision: The Extraction Risk

After major encounters, the player faces a choice:

| Option | Effect |
|---|---|
| **Extract** | Bank all earned resources, return to hub, advance narrative |
| **Continue** | Stay in the realm, next rooms have multiplied rewards, corruption increases |
| **Sacrifice Relic** | Remove last relic, reduce corruption by 15%, stay in run |
| **Accept Scar** | Permanent consequence for temporary combat boost, stay in run |
| **Challenge Boss Early** | Skip remaining rooms, go directly to boss |
| **Convert Health → Reward** | Sacrifice current HP to improve reward quality |

This decision recurs throughout the run and is NEVER cosmetic.

## Relic Corruption System (THE CORE MECHANIC)

Every relic has two effects:

1. **Benefit** — Powerful player upgrade (behavioral, not just +5% damage)
2. **Corruption** — Makes the world more dangerous (enemies, hazards, arena)

### Example Relics

| Relic | Benefit | Corruption |
|---|---|---|
| Mirror Fang | Dash creates attacking clone | Enemies also create clones |
| Clockbreaker | Parry slows nearby enemies | Hazards accelerate |
| Blood Crown | Crits cause area explosions | Wounded enemies gain attack speed |
| Devouring Gauntlet | Executions increase damage permanently | Healing decreases after each execution |
| Hollow Coin | Reward rooms offer extra choice | One choice may be cursed |

### Synergy Tags
Relics share tags that create synergies: Dash, Clone, Fire, Bleed, Critical, Parry, Projectile, Execution, Corruption, Summon, Shockwave, etc.

Two relics with matching tags can create emergent builds (e.g., Mirror Fang [Dash, Clone] + Shadowstep [Dash, Stealth] → invisible clone dash).

## Corruption Progression

| Corruption Level | Name | Effects |
|---|---|---|
| 0-24% | Stable | Basic encounters, low distortion |
| 25-49% | Distorted | Enemy modifiers, more hazards, floating debris |
| 50-74% | Dangerous | Mutated enemies, aggressive rooms, stronger rewards |
| 75-99% | Critical | Elite invasions, healing penalties, arena transformations |
| 100% | Collapsed | Emergency escape/boss sequence, extreme risk/reward |

## Weapons

### Chain Blade (Starting Weapon)
- Fast, mobile, crowd control, medium range
- 3-hit combo: Slash → Slash → Spin
- Heavy: Sweeping chain attack
- Special: Chain Pull (tether enemy toward you)
- Dash Attack: Dash Slash

### Great Blade (Unlockable)
- Slow, heavy, parry-focused, high stagger
- 3-hit combo: Cleaves with increasing stagger
- Heavy: Charged slam with shockwave
- Special: Guard Counter (block + retaliatory strike)
- Upgrade Path: Shockwave series

### Arcane Pistol & Dagger (Unlockable)
- Hybrid ranged/melee, precision, execution-focused
- Combo: Shot → Dagger → Marked Execute
- Heavy: Charged shot
- Special: Mark-and-Execute (mark target, execute for massive damage)
- Dash Attack: Dash Shot (ranged attack during dash)

Each weapon has at least 10 meaningful upgrades that alter behavior, not just numbers.

## Enemies (10 Core Types)

| Enemy | Role | Key Behavior |
|---|---|---|
| Sword Guard | Basic melee | Standard combo, readable telegraph |
| Shield Guard | Defensive | Blocks attacks, vulnerable during recovery |
| Spear Guard | Mid-range | Thrust attacks, keeps distance |
| Archer | Ranged | Arrow shots from distance, moves to flank |
| Corrupted Mage | Support | Area effects, buffs other enemies |
| Heavy Knight | Slow powerhouse | Massive swings, high stagger resistance |
| Assassin | Flanker | Fast, appears from behind, hit-and-run |
| Summoner | Spawner | Creates minor minions, fragile itself |
| Living Statue | Tank | High health, slow, devastating slam attacks |
| Corruption Beast | Aggressive | Fast, unpredictable, corruption-linked attacks |

## Elite Modifiers (Behaviour-Changing, Not Just Stats)

| Modifier | Behaviour Change |
|---|---|
| Mirrored | Creates weaker clone that mimics attacks |
| Frenzied | 50% faster attacks, never retreats |
| Armoured | Blocks 50% of attacks, stagger-resistant |
| Vampiric | Heals 30% of damage dealt |
| Explosive | Explodes on death, damaging nearby |
| Teleporting | Teleports to flanking positions |
| Time-Shifted | Delayed attack echoes |
| Corruption-Linked | Gets stronger as corruption rises |
| Summoning | Summons minor minions periodically |
| Shielded | Rechargeable shield absorbs hits |

## Bosses

### Realm 1: The Oath-Breaker King
- Polearm combat, summons royal guards, breaks arena sections
- Uses player's stolen relics against them
- 3 phases with increasing aggression

### Realm 2: The Thirteenth Regent
- Time distortion, delayed attack echoes, accelerated hazards
- Phase transitions based on corruption
- 3 phases with increasing time manipulation

### Realm 3: The Hollow Saint
- Living statue, converts healing into hazards, sacred→corrupted zones
- Forces movement around the arena
- 3 phases with increasing zone manipulation

### Final Boss
- Reacts to player's permanent progression
- Uses multiple relic categories
- Different patterns based on player choices

## Realms

### Realm 1: The Shattered Court
- Ruined palace, eclipse sky, floating architecture, crimson corruption, gold ornamentation
- Royal guard enemies, balanced encounters, arena collapse hazards

### Realm 2: The Drowned Dominion
- Flooded palace, dark water, bioluminescent corruption, broken ships
- Slowing water zones, rising water hazards, amphibious enemies

### Realm 3: The Verdant Maw
- Cursed forest kingdom, giant roots, consumed settlements, toxic spores
- Growing hazards, area denial, poison, root traps

## Permanent Progression (Primarily Unlocks)

- Weapon unlocks (not stat grinding)
- Starting relic choices (new options, not more power)
- New relics added to pool
- Additional extraction options
- Additional route choices
- New NPCs in hub
- Difficulty modifiers (1-10 scale)
- Cosmetic hub upgrades
- Lore archive access

## Run Duration Target: 25-40 Minutes

A run includes: start room, branching choices, normal encounters, elite encounters, reward rooms, risk rooms, rest rooms, extraction points, realm boss, optional challenges, final extraction decision.
