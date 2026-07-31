using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterSwitch))]
public sealed class TeamSetup : MonoBehaviour
{
    private const float CharacterFacingAngle = 180f;
    private const float SuperRingDrainInterval = 3f;

    [Header("Team")]
    [FormerlySerializedAs("CurrentTeam")]
    [SerializeField] private TeamComposition currentTeam;

    [Header("Character Parents")]
    [FormerlySerializedAs("player")]
    [SerializeField] private Transform playerParent;

    [FormerlySerializedAs("leftTeamMember")]
    [SerializeField] private Transform flyingCharacterParent;

    [FormerlySerializedAs("rightTeamMember")]
    [SerializeField] private Transform powerCharacterParent;

    [Header("Formation Positions")]
    [FormerlySerializedAs("pos1")]
    [SerializeField] private GameObject positionOne;

    [FormerlySerializedAs("pos2")]
    [SerializeField] private GameObject positionTwo;

    [Header("HUD")]
    [FormerlySerializedAs("HUD")]
    [SerializeField] private HUD hud;

    public static TeamSetup Instance { get; private set; }

    // Compatibility with scripts that still use TeamSetup.pc.
    public static TeamSetup pc => Instance;

    public TeamComposition CurrentTeam => currentTeam;

    public PlayableTeam CurrentPlayableTeam =>
        currentTeam != null
            ? currentTeam.PlayableTeam
            : default;

    public GameObject PositionOne => positionOne;
    public GameObject PositionTwo => positionTwo;
    public Transform PlayerParent => playerParent;
    public Transform FlyingCharacterParent => flyingCharacterParent;
    public Transform PowerCharacterParent => powerCharacterParent;
    public GameObject HUDObject => hud != null ? hud.gameObject : null;
    public bool IsSuperFormActive => superFormRingCountdown != null;

    private CharacterSwitch characterSwitch;
    private UltimatePlayerMovement playerMovement;
    private Coroutine superFormRingCountdown;
    private WaitForSeconds superRingDrainDelay;
    private bool teamInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Only one TeamSetup may exist in the scene.",
                this);

            enabled = false;
            return;
        }

        Instance = this;

        characterSwitch = GetComponent<CharacterSwitch>();
        superRingDrainDelay =
            new WaitForSeconds(SuperRingDrainInterval);

        ResolveReferences();
    }

    private void Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        InitializeTeam();
    }

    private void OnDisable()
    {
        StopSuperCountdown();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveReferences()
    {
        if (playerParent != null)
        {
            playerParent.TryGetComponent(
                out playerMovement);
        }

        if (hud == null)
        {
            hud = Object.FindAnyObjectByType<HUD>();
        }
    }

    private void InitializeTeam()
    {
        if (teamInitialized)
            return;

        teamInitialized = true;

        SpawnInitialTeam();
        InitializeFollowers();
        InitializeHUD();
        ApplyCurrentTeam();
        DetachCharacterParents();
    }

    private void SpawnInitialTeam()
    {
        GameObject speedCharacter =
            SpawnTeamMember(
                currentTeam.SpeedCharacterPrefab,
                playerParent);

        GameObject flyingCharacter =
            SpawnTeamMember(
                currentTeam.FlyingCharacterPrefab,
                flyingCharacterParent);

        GameObject powerCharacter =
            SpawnTeamMember(
                currentTeam.PowerCharacterPrefab,
                powerCharacterParent);

        characterSwitch.speedCharacter =
            speedCharacter.transform;

        characterSwitch.flyingCharacter =
            flyingCharacter.transform;

        characterSwitch.powerCharacter =
            powerCharacter.transform;

        characterSwitch.TeamMembers.Clear();
        characterSwitch.TeamMembers.Add(speedCharacter);
        characterSwitch.TeamMembers.Add(flyingCharacter);
        characterSwitch.TeamMembers.Add(powerCharacter);

        playerMovement?.SetupAnimation();
    }

    private GameObject SpawnTeamMember(
        GameObject characterPrefab,
        Transform parent)
    {
        GameObject character =
            Instantiate(characterPrefab, parent);

        character.transform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.Euler(
                0f,
                CharacterFacingAngle,
                0f));

        return character;
    }

    private void InitializeFollowers()
    {
        InitializeFollower(flyingCharacterParent);
        InitializeFollower(powerCharacterParent);
    }

    private void InitializeFollower(
        Transform followerParent)
    {
        if (followerParent.TryGetComponent(
            out FollowerNavigation follower))
        {
            follower.Setup();
        }
    }

    private void InitializeHUD()
    {
        if (hud == null)
            return;

        hud.Setup(currentTeam);
        hud.UpdateRings();
    }

    private void ApplyCurrentTeam()
    {
        GameInstance.currentTeam =
            (int)currentTeam.PlayableTeam;
    }

    private void DetachCharacterParents()
    {
        playerParent.SetParent(null);
        flyingCharacterParent.SetParent(null);
        powerCharacterParent.SetParent(null);
    }

    public void SwapForSuper()
    {
        EnterSuperForm();
    }

    public void EnterSuperForm()
    {
        if (!teamInitialized ||
            IsSuperFormActive)
        {
            return;
        }

        GameObject superCharacterPrefab =
            currentTeam.SuperCharacterPrefab;

        if (superCharacterPrefab == null)
        {
            Debug.LogWarning(
                $"Team '{currentTeam.name}' has no Super Character assigned.",
                currentTeam);

            return;
        }

        ReplaceSpeedCharacter(
            superCharacterPrefab);

        StartSuperCountdown();
    }

    public void SwapForSonic()
    {
        RestoreSpeedCharacter();
    }

    public void RestoreSpeedCharacter()
    {
        StopSuperCountdown();

        if (!teamInitialized ||
            currentTeam.SpeedCharacterPrefab == null)
        {
            return;
        }

        ReplaceSpeedCharacter(
            currentTeam.SpeedCharacterPrefab);

        UpdateRingDisplay();
    }

    private void ReplaceSpeedCharacter(
        GameObject characterPrefab)
    {
        if (characterPrefab == null)
            return;

        RemoveCurrentSpeedCharacter();

        GameObject replacement =
            SpawnTeamMember(
                characterPrefab,
                playerParent);

        characterSwitch.speedCharacter =
            replacement.transform;

        characterSwitch.TeamMembers.Insert(
            0,
            replacement);

        playerMovement?.SetupAnimation();
    }

    private void RemoveCurrentSpeedCharacter()
    {
        Transform currentSpeedCharacter =
            characterSwitch.speedCharacter;

        if (currentSpeedCharacter == null)
            return;

        characterSwitch.TeamMembers.Remove(
            currentSpeedCharacter.gameObject);

        characterSwitch.speedCharacter = null;

        Destroy(currentSpeedCharacter.gameObject);
    }

    private void StartSuperCountdown()
    {
        StopSuperCountdown();

        superFormRingCountdown =
            StartCoroutine(SuperCountdown());
    }

    private void StopSuperCountdown()
    {
        if (superFormRingCountdown == null)
            return;

        StopCoroutine(superFormRingCountdown);
        superFormRingCountdown = null;
    }

    private IEnumerator SuperCountdown()
    {
        while (GameInstance.currentRings > 0)
        {
            yield return superRingDrainDelay;

            GameInstance.currentRings =
                Mathf.Max(
                    0,
                    GameInstance.currentRings - 1);

            UpdateRingDisplay();
        }

        superFormRingCountdown = null;
        RestoreSpeedCharacter();
    }

    private void UpdateRingDisplay()
    {
        hud?.UpdateRings();
    }

    private bool ValidateSetup()
    {
        if (currentTeam == null)
        {
            Debug.LogError(
                "Current Team is not assigned.",
                this);

            return false;
        }

        if (playerParent == null ||
            flyingCharacterParent == null ||
            powerCharacterParent == null)
        {
            Debug.LogError(
                "One or more Character Parents are not assigned.",
                this);

            return false;
        }

        if (currentTeam.SpeedCharacterPrefab == null ||
            currentTeam.FlyingCharacterPrefab == null ||
            currentTeam.PowerCharacterPrefab == null)
        {
            Debug.LogError(
                $"Team '{currentTeam.name}' is missing one or more required character prefabs.",
                currentTeam);

            return false;
        }

        return true;
    }
}