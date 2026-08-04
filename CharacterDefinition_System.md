# CharacterDefinition System

## Overview

The CharacterDefinition system provides a centralized, data-driven
architecture for all playable characters in Team Sonic, Team Dark, Team
Rose, and Team Chaotix.

Each playable character owns a single **CharacterDefinition**
ScriptableObject that stores the character's identity and references to
all gameplay profiles.

## How it works

CharacterDefinition contains:

-   Character ID
-   Display Name
-   Character Type
-   Movement Profile
-   Ability Profile
-   Animator Profile
-   Presentation Profile

`UltimatePlayerMovement` contains a serialized `CharacterDefinition`
reference.

When a valid CharacterDefinition is assigned:

1.  UltimatePlayerMovement loads the CharacterDefinition.
2.  Movement, ability, and animation profiles are applied.
3.  CharacterSwitch reads the CharacterDefinition from each spawned
    teammate.
4.  CharacterSwitch stores the Speed, Fly, and Power definitions.
5.  Team switching preserves the correct data for the active leader.

## Character Types

-   Speed
-   Fly
-   Power
-   Special

## Folder Structure

``` text
Assets
└── Resources
    └── Teams
        ├── Team Sonic
        │   ├── Prefabs
        │   ├── Character Definitions
        │   ├── Movement Profiles
        │   ├── Ability Profiles
        │   ├── Animator Profiles
        │   └── Presentation Profiles
        ├── Team Dark
        ├── Team Rose
        └── Team Chaotix
```

## CharacterDefinition Assets

Create one CharacterDefinition asset for every playable character.

### Team Sonic

-   Sonic
-   Tails
-   Knuckles

### Team Dark

-   Shadow
-   Rouge
-   Omega

### Team Rose

-   Amy
-   Cream
-   Big

### Team Chaotix

-   Espio
-   Charmy
-   Vector

## Required Fields

Each CharacterDefinition should include:

-   Character ID
-   Display Name
-   Character Type
-   Movement Profile
-   Ability Profile

Optional:

-   Animator Profile
-   Presentation Profile

## Prefab Setup

Each playable prefab should contain:

-   UltimatePlayerMovement
-   Rigidbody
-   GroundCheck
-   CharacterDefinition assignment

Assign the matching CharacterDefinition to the UltimatePlayerMovement
component.

## Runtime Flow

1.  TeamSetup spawns the characters.
2.  UltimatePlayerMovement loads CharacterDefinition.
3.  CharacterSwitch reads each CharacterDefinition.
4.  Leader switching applies the correct character data.
5.  Movement and animation profiles are updated.

## Validation

The system reports errors when:

-   CharacterDefinition is missing.
-   Character ID is empty.
-   Movement Profile is missing.
-   Ability Profile is missing.

## Verification Checklist

-   Create all 12 CharacterDefinition assets.
-   Assign Movement Profile.
-   Assign Ability Profile.
-   Assign CharacterDefinition to every playable prefab.
-   Save prefabs.
-   Allow Unity to compile.
-   Test team spawning and leader switching.
