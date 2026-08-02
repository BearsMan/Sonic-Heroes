# Sonic Heroes Project Coding Standard

## Purpose

This document defines the coding standards for the Sonic Heroes Unity project. The goals are consistency, automatic setup where practical, strong validation, clear debugging, graceful failure, and maintainable architecture.

## 1. Core Principles

1. **One responsibility per script.**
   - `TeamSetup` owns spawning and initial team setup.
   - `CharacterSwitch` owns leader and formation changes.
   - `FollowerNavigation` owns follower movement.
   - `UltimatePlayerMovement` owns player movement states.
   - `CameraController` owns camera behavior.
   - `HUD` owns interface updates.

2. **Resolve references automatically when reliable.**
   - Prefer existing Inspector assignments.
   - Then check the same GameObject, parents, children, known hierarchy roots, singletons, and finally scene-wide searches.
   - Never perform scene-wide searches every frame.

3. **Validate before changing runtime state.**
   - Resolve references.
   - Validate configuration.
   - Stop immediately when required data is missing.
   - Log a clear error.
   - Leave the system in a consistent state.

4. **Fail gracefully.**
   - Use early returns.
   - Return `bool` from initialization methods.
   - Centralize cleanup.
   - Use `try/finally` for temporary state flags.
   - Preserve failed runtime objects when useful for debugging.

5. **Never fail silently.**
   - Every unexpected required failure should produce a specific, clickable Unity Console message.

---

## 2. Standard Script Layout

Use this order whenever practical:

```text
Using directives
Attributes
Class declaration
Constants
Serialized fields
Private cached references
Private runtime state
Public properties
Events
Unity lifecycle methods
Public API
Initialization methods
Automatic reference resolution
Validation
Runtime helpers
Cleanup
OnValidate
Gizmos
```

Example:

```csharp
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExampleController : MonoBehaviour
{
    private const string RequiredChildName = "RequiredChild";

    [Header("References")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField, Min(0f)] private float speed = 5f;

    private Rigidbody body;
    private bool isInitialized;

    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        CacheComponents();
        ResolveReferences();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
            return;
    }

    private void CacheComponents()
    {
        body ??= GetComponent<Rigidbody>();
    }

    private void ResolveReferences()
    {
        if (target == null)
        {
            target = FindDescendantByName(
                transform,
                RequiredChildName);
        }
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &= ValidateReference(
            body,
            "Rigidbody");

        valid &= ValidateReference(
            target,
            "Target");

        return valid;
    }

    private bool ValidateReference(
        Object reference,
        string displayName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            $"{nameof(ExampleController)} {displayName} is missing.",
            this);

        return false;
    }

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                includeInactive: true);

        foreach (Transform descendant in descendants)
        {
            if (descendant != null &&
                descendant.name == objectName)
            {
                return descendant;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
    }
}
```

---

### Region organization for large scripts

Use `#region` and `#endregion` to separate logical systems in large scripts when doing so improves navigation.

Recommended layout:

```csharp
#region Constants
#endregion

#region Inspector
#endregion

#region Runtime State
#endregion

#region Public API
#endregion

#region Unity Lifecycle
#endregion

#region Initialization
#endregion

#region Validation
#endregion

#region Cleanup
#endregion

#region Input
#endregion

#region State Machine
#endregion

#region State Updates
#endregion

#region Movement
#endregion

#region Actions
#endregion

#region Targeting
#endregion

#region Animation
#endregion

#region Utilities
#endregion
```

Region rules:

- Use regions for large or multi-system scripts, not automatically for every small script.
- Give each region one clear responsibility.
- Keep related fields and methods together.
- Use the same region names and order across comparable gameplay scripts.
- Do not use regions to hide methods that should be moved into a separate component.
- Do not create regions containing only one trivial member unless consistency clearly improves navigation.
- Always close every region with `#endregion`.
- Keep methods inside each region ordered consistently.

---

## 3. Naming Conventions

### Classes, structs, methods, events, and constants

Use PascalCase:

```csharp
TeamSetup
CharacterSwitchState
InitializeTeam()
LeaderChanged
GroundCheckName
```

### Fields and local variables

Use camelCase:

```csharp
leaderSlot
currentLeaderType
isInitialized
```

### Project-specific prefixes

Project prefixes are allowed when they add useful meaning.

```csharp
private CharacterSwitch shCharacterSwitch;
```

In this project, `sh` means Sonic Heroes. Use it consistently.

### Boolean names

Use names that describe a state or permission:

```csharp
isInitialized
isChangingFormation
isGrounded
canSwitch
preserveFailedTeamForDebugging
```

Avoid vague names such as `flag`, `check`, or `status`.

---

## 4. Inspector Fields

Organize related fields with `[Header]`.

```csharp
[Header("Formation Slots")]
[SerializeField] private Transform leaderSlot;
[SerializeField] private Transform leftFollowerSlot;
[SerializeField] private Transform rightFollowerSlot;
```

Use range attributes:

```csharp
[SerializeField, Min(0f)]
private float stoppingDistance = 0.1f;

[SerializeField, Range(-89f, 0f)]
private float minimumPitch = -30f;
```

Keep serialized fields private. Expose read-only properties when another script needs access:

```csharp
public Transform Target => target;
public bool IsInitialized => isInitialized;
```

---

## 5. Required Components

Use `[RequireComponent]` for components that must exist on the same GameObject:

```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
```

Use `[DisallowMultipleComponent]` when only one instance should exist per GameObject.

Required components must still be cached and validated:

```csharp
private void CacheComponents()
{
    agent ??= GetComponent<NavMeshAgent>();
    body ??= GetComponent<Rigidbody>();
}
```

---

## 6. Initialization

### `Awake()`

Use it to:

- Protect singletons
- Cache same-object components
- Resolve references that do not depend on spawned objects
- Initialize reusable cached values

### `Start()`

Use it to:

- Validate the completed scene setup
- Run initialization that depends on other objects completing `Awake()`
- Disable the component safely when required setup is invalid

```csharp
private void Start()
{
    if (!ValidateConfiguration())
    {
        enabled = false;
        return;
    }

    InitializeSystem();
}
```

### Public initialization methods

Return `bool` when initialization can fail:

```csharp
public bool Initialize(
    Transform followTarget)
{
    if (followTarget == null)
    {
        Debug.LogError(
            $"{nameof(FollowerNavigation)} received no follow target.",
            this);

        return false;
    }

    isInitialized = true;
    return true;
}
```

Set `isInitialized = true` only after every required step succeeds.

---

## 7. Automatic Reference Resolution

Preferred lookup order:

1. Existing Inspector assignment
2. Same GameObject
3. Parent
4. Children
5. Known hierarchy root
6. Singleton
7. Scene-wide search as a last resort

Example:

```csharp
private void ResolveDependencies()
{
    if (teamSetup == null)
        teamSetup = GetComponent<TeamSetup>();

    if (teamSetup == null)
        teamSetup = TeamSetup.Instance;

    if (hud == null)
    {
        hud =
            Object.FindAnyObjectByType<HUD>(
                FindObjectsInactive.Include);
    }
}
```

Cache results and avoid repeated searches in `Update`, `FixedUpdate`, or `LateUpdate`.

---

## 8. Permanent Helper Objects

Current permanent helper names:

```text
GroundCheck
LeftPos
RightPos
```

Protect them while clearing spawned slot children:

```csharp
private static bool IsPermanentSlotHelper(
    Transform child)
{
    if (child == null)
        return false;

    return
        child.name == GroundCheckName ||
        child.name == LeftFollowTargetName ||
        child.name == RightFollowTargetName;
}
```

Long-term improvement: replace name-only protection with marker components.

```csharp
public sealed class FormationHelper : MonoBehaviour
{
}
```

Then:

```csharp
if (child.GetComponent<FormationHelper>() != null)
    continue;
```

---

## 9. Validation

Every core system should have one centralized validation method.

```csharp
private bool ValidateConfiguration()
{
    bool valid = true;

    valid &= ValidateReference(
        leaderSlot,
        "Leader Slot");

    valid &= ValidateReference(
        leftFollowerSlot,
        "Left Follower Slot");

    return valid;
}
```

Shared validation helper:

```csharp
private bool ValidateReference(
    Object reference,
    string displayName)
{
    if (reference != null)
        return true;

    Debug.LogError(
        $"{nameof(TeamSetup)} {displayName} is not assigned.",
        this);

    return false;
}
```

Use errors for required systems and warnings for optional systems.

Use `&=` intentionally when every missing reference should be reported.

---

## 10. Null Safety

Every public method should assume it may receive invalid data.

```csharp
public bool SetLeader(
    CHARACTERTYPES leaderType)
{
    if (!isInitialized)
        return false;

    if (!IsSupportedType(leaderType))
    {
        Debug.LogWarning(
            $"Unsupported leader type: {leaderType}.",
            this);

        return false;
    }

    return true;
}
```

Avoid chained access without checks.

Use null-conditional access only for genuinely optional operations:

```csharp
hud?.UpdateRings();
```

---

## 11. Logging

Logs should explain:

- Which script failed
- Which reference or operation failed
- Which object was involved
- Whether the operation stopped

Good:

```csharp
Debug.LogError(
    $"TeamSetup could not find '{GroundCheckName}' " +
    $"under leader slot '{leaderSlot.name}'.",
    this);
```

Weak:

```csharp
Debug.LogError("Missing");
```

Pass a Unity object as the log context so the Console entry is clickable.

Do not print the same expected message every frame.

---

## 12. Safe State Flags

Temporary state flags must always reset:

```csharp
private void ApplyLeader(
    CHARACTERTYPES leaderType)
{
    if (isChangingFormation)
        return;

    isChangingFormation = true;

    try
    {
        // Formation logic.
    }
    finally
    {
        isChangingFormation = false;
    }
}
```

---

## 13. Centralized Cleanup

Do not repeat cleanup code across failure branches.

```csharp
private void CleanupFailedTeam(
    Transform speedCharacter,
    Transform flyingCharacter,
    Transform powerCharacter)
{
    if (preserveFailedTeamForDebugging)
    {
        Debug.LogWarning(
            "The failed team was preserved for debugging.",
            this);

        return;
    }

    DestroyCharacter(speedCharacter);
    DestroyCharacter(flyingCharacter);
    DestroyCharacter(powerCharacter);
}
```

---

## 14. Events

Subscribe in `OnEnable()` and unsubscribe in `OnDisable()`.

```csharp
private void OnEnable()
{
    if (shCharacterSwitch != null)
    {
        shCharacterSwitch.SuperFormChanged +=
            HandleSuperFormChanged;
    }
}

private void OnDisable()
{
    if (shCharacterSwitch != null)
    {
        shCharacterSwitch.SuperFormChanged -=
            HandleSuperFormChanged;
    }
}
```

Do not leave active subscriptions after an object is disabled or destroyed.

---

## 15. Coroutines

Store coroutine references:

```csharp
private Coroutine ringDrainRoutine;
```

Stop an existing coroutine before starting another and clear the reference when finished.

Cache reusable waits:

```csharp
private WaitForSeconds ringDrainWait;
```

Stop appropriate coroutines during `OnDisable()` or `OnDestroy()`.

---

## 16. Unity Update Methods

Use:

- `Update()` for input, animation parameters, and non-physics state
- `FixedUpdate()` for Rigidbody physics
- `LateUpdate()` for cameras and post-movement behavior

Use early returns:

```csharp
private void Update()
{
    if (!isInitialized)
        return;

    if (isExternallyMoving)
        return;

    FollowTarget();
}
```

Never perform expensive scene searches in an update loop.

---

## 17. NavMesh Safety

Before using an agent, verify:

```csharp
agent != null
agent.enabled
agent.isOnNavMesh
```

```csharp
private void FollowTarget()
{
    if (target == null ||
        agent == null ||
        !agent.enabled ||
        !agent.isOnNavMesh)
    {
        return;
    }

    agent.SetDestination(
        target.position);
}
```

Configure the agent in one method and keep movement separate from rotation.

---

## 18. Singleton Safety

Reject duplicates clearly:

```csharp
private void Awake()
{
    if (Instance != null &&
        Instance != this)
    {
        Debug.LogError(
            $"Duplicate {nameof(TeamSetup)} detected.",
            this);

        enabled = false;
        return;
    }

    Instance = this;
}
```

Clear the singleton in `OnDestroy()`:

```csharp
if (Instance == this)
    Instance = null;
```

Only use singletons for intentionally global systems.

---

## 19. `OnValidate()`

Use `OnValidate()` to clamp unsafe Inspector values and repair invalid defaults.

```csharp
private void OnValidate()
{
    stoppingDistance =
        Mathf.Max(
            0f,
            stoppingDistance);

    if (!IsSupportedLeader(startingLeader))
    {
        startingLeader =
            CHARACTERTYPES.Speed;
    }
}
```

Do not spawn runtime objects or perform gameplay actions in `OnValidate()`.

---

## 20. Debug Utilities

Keep reusable debug scripts in a dedicated folder:

```text
Assets
└── Scripts
    └── Debug
        ├── RuntimeDisableTrace.cs
        └── RuntimeProjectDebugger.cs
```

Debug tools may trace lifecycle events, report missing scripts, validate scene objects, and preserve failed runtime state. They should be easy to disable for release builds.

---

## 21. Public API

Prefer intentional methods and read-only state:

```csharp
public bool SetLeader(
    CHARACTERTYPES leaderType)

public bool Initialize(
    Transform followTarget)

public void SetInputEnabled(
    bool enabled)

public bool IsInitialized => isInitialized;
```

Avoid public fields.

Use events for meaningful state changes.

---

## 22. Method Responsibilities

Methods should do one clear job:

```text
ResolveReferences()
ValidateConfiguration()
SpawnCharacter()
AssignToSlot()
RefreshControllers()
CleanupFailedTeam()
```

Large operations should coordinate smaller helpers instead of containing every detail.

---

## 23. Formatting

Use four spaces for indentation and opening braces on a new line.

Break long conditions and calls consistently:

```csharp
bool configured =
    shCharacterSwitch.ConfigureTeam(
        speedCharacter,
        flyingCharacter,
        powerCharacter,
        team.SpeedCharacterPrefab,
        team.SuperCharacterPrefab,
        startingLeader);
```

Keep one blank line between methods. Avoid unnecessary blank lines inside short methods.

---

## 24. Comments

Comments should explain **why**, not repeat the code.

Useful:

```csharp
// Verify that clearing the slots did not remove permanent helpers.
if (!ResolveSlotHelpers())
{
    return false;
}
```

Not useful:

```csharp
// Set initialized to true.
initialized = true;
```

---

## 25. Future-Proofing Checklist

Before adding a feature, ask:

1. Which script owns this responsibility?
2. Can required references resolve automatically?
3. What happens when a reference is missing?
4. Can initialization report success or failure?
5. What state must be restored after failure?
6. What should be logged?
7. What cleanup is required?
8. Does it need event unsubscription?
9. Does it need `OnValidate()`?
10. Can it be tested independently?

---

## 26. GitHub Review Checklist

Before committing:

- [ ] Project compiles with no errors
- [ ] No new unintended warnings
- [ ] References resolve automatically where practical
- [ ] Required Inspector references are validated
- [ ] Initialization methods report success or failure
- [ ] No silent failure paths
- [ ] Temporary flags reset safely
- [ ] Events unsubscribe correctly
- [ ] Coroutines stop correctly
- [ ] Permanent helper objects are preserved
- [ ] Failed objects can be inspected during development
- [ ] No repeated scene searches in update loops
- [ ] `OnValidate()` protects unsafe values
- [ ] Logs contain useful context
- [ ] Formatting follows this standard
- [ ] Play Mode behavior was tested
- [ ] Leader switching was tested
- [ ] Scene reload was tested
- [ ] The commit message clearly describes the change

---

## 27. Suggested Folder Structure

```text
Assets
└── Scripts
    ├── Characters
    │   ├── Movement
    │   ├── Followers
    │   └── Abilities
    ├── Team
    │   ├── TeamSetup.cs
    │   ├── CharacterSwitch.cs
    │   └── TeamActionController.cs
    ├── Camera
    │   └── CameraController.cs
    ├── UI
    │   ├── HUD.cs
    │   └── PauseMenu.cs
    ├── Systems
    │   ├── GameInstance.cs
    │   └── UpdateBank.cs
    └── Debug
        ├── RuntimeDisableTrace.cs
        └── RuntimeProjectDebugger.cs
```

---

## 28. Commit Message Style

Use action-based commit messages:

```text
Refactor TeamSetup initialization and validation
Add automatic follower target resolution
Improve CharacterSwitch formation safety
Fix permanent helper deletion during team rebuild
```

Optional body:

```text
- Added automatic helper resolution
- Preserved formation helper transforms
- Added centralized failed-team cleanup
- Improved null validation
- Added debugging preservation
```

---

## 29. Project Standard Summary

Every core script should:

```text
Resolve automatically
Validate early
Fail safely
Log clearly
Clean up centrally
Restore temporary state
Protect permanent helpers
Avoid silent errors
Keep responsibilities separate
Remain easy to test
```

Use this standard for all new scripts and future rewrites in the Sonic Heroes project.

