using UnityEngine;

public class EggmanShips : MonoBehaviour
{
    public enum ShipType
    {
        SmallShip,
        LowFlyingShip,
        BigShip,
        MantaRayShip
    }

    public enum ShipMode
    {
        Disabled,
        Gameplay,
        Cinematic,
        Background
    }

    public enum CinematicEndMode
    {
        Hold,
        Loop,
        Disable
    }

    #region Identity

    [Header("Identity")]
    [SerializeField]
    private ShipType shipType =
        ShipType.SmallShip;

    [SerializeField]
    private ShipMode currentMode =
        ShipMode.Gameplay;

    #endregion

    #region Gameplay Stages

    [Header("Gameplay Stages")]
    [SerializeField]
    private int eggFleetStageIndex = 13;

    [SerializeField]
    private int finalFortressStageIndex = 14;

    #endregion

    #region Flying Ships

    [Header("Flying Ships")]
    [SerializeField, Min(0f)]
    private float smallShipCruiseSpeed = 20f;

    [SerializeField, Min(0f)]
    private float lowFlyingShipCruiseSpeed = 12f;

    [SerializeField, Min(0f)]
    private float mantaRayCruiseSpeed = 8f;

    [SerializeField, Min(0f)]
    private float smallShipHoverHeight = 14f;

    [SerializeField, Min(0f)]
    private float lowFlyingShipHoverHeight = 3f;

    [SerializeField, Min(0f)]
    private float mantaRayHoverHeight = 20f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 3f;

    [SerializeField, Min(0f)]
    private float hoverCorrectionSpeed = 8f;

    [SerializeField, Min(0f)]
    private float bankAngle = 20f;

    [SerializeField, Min(0f)]
    private float bankSpeed = 3f;

    [SerializeField]
    private LayerMask flightSurfaceMask = ~0;

    [SerializeField]
    private bool maintainHoverHeight = true;

    [SerializeField]
    private bool enableBanking = true;

    #endregion

    #region Gameplay Path

    [Header("Gameplay Path")]
    [SerializeField]
    private Transform[] gameplayWaypoints;

    [SerializeField, Min(0.01f)]
    private float gameplayWaypointRadius = 3f;

    [SerializeField]
    private bool loopGameplayPath = true;

    [SerializeField]
    private bool flyForwardWithoutWaypoints = true;

    #endregion

    #region Big Ship

    [Header("Big Ship")]
    [SerializeField]
    private BigShipController bigShipController;

    [SerializeField]
    private ShipTurret[] turrets;

    [SerializeField]
    private ShipCannon[] cannons;

    #endregion

    #region Low Flying Ship

    [Header("Low Flying Ship")]
    [SerializeField]
    private Collider platformCollider;

    [SerializeField]
    private bool enablePlatformCollider = true;

    #endregion

    #region Cinematic

    [Header("Cinematic")]
    [SerializeField]
    private Transform[] cinematicWaypoints;

    [SerializeField, Min(0f)]
    private float cinematicSpeed = 10f;

    [SerializeField, Min(0f)]
    private float cinematicTurnSpeed = 2f;

    [SerializeField, Min(0.01f)]
    private float cinematicWaypointRadius = 1f;

    [SerializeField]
    private CinematicEndMode cinematicEndMode =
        CinematicEndMode.Hold;

    [SerializeField]
    private bool weaponsEnabledDuringCinematic;

    #endregion

    #region Background

    [Header("Background")]
    [SerializeField]
    private Vector3 backgroundDirection =
        Vector3.forward;

    [SerializeField, Min(0f)]
    private float backgroundSpeed = 5f;

    #endregion

    #region Physics

    [Header("Collision")]
    [SerializeField]
    private bool gameplayCollisions = true;

    [SerializeField]
    private bool cinematicCollisions;

    [SerializeField]
    private bool backgroundCollisions;

    private Rigidbody body;
    private Collider[] shipColliders;

    #endregion

    #region Runtime

    private int gameplayWaypointIndex;
    private int cinematicWaypointIndex;

    private float startingAltitude;
    private float currentBankAngle;

    private bool cinematicComplete;
    private bool initialized;

    #endregion

    #region Properties

    public ShipType Type =>
        shipType;

    public ShipMode Mode =>
        currentMode;

    public bool IsCinematicComplete =>
        cinematicComplete;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        ConfigurePhysics();

        startingAltitude =
            transform.position.y;

        gameplayWaypointIndex = 0;
        cinematicWaypointIndex = 0;

        currentBankAngle = 0f;
        cinematicComplete = false;
        initialized = true;

        ApplyMode(currentMode);
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        ConfigurePhysics();
        ApplyMode(currentMode);
    }

    private void FixedUpdate()
    {
        if (!initialized ||
            currentMode != ShipMode.Gameplay)
        {
            return;
        }

        if (shipType == ShipType.BigShip)
        {
            return;
        }

        UpdateFlyingShip();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        switch (currentMode)
        {
            case ShipMode.Cinematic:
                UpdateCinematic();
                break;

            case ShipMode.Background:
                UpdateBackground();
                break;
        }
    }

    #endregion

    #region Public Activation

    public bool ActivateForGameplay(
        int stageIndex)
    {
        if (!IsGameplayStage(stageIndex))
        {
            DisableShip();
            return false;
        }

        gameplayWaypointIndex = 0;
        cinematicComplete = false;

        SetMode(ShipMode.Gameplay);

        return true;
    }

    public void ActivateForCinematic()
    {
        cinematicWaypointIndex = 0;
        cinematicComplete = false;

        SetMode(ShipMode.Cinematic);
    }

    public void ActivateForBackground()
    {
        SetMode(ShipMode.Background);
    }

    public void DisableShip()
    {
        SetMode(ShipMode.Disabled);
    }

    public void SetMode(
        ShipMode mode)
    {
        currentMode = mode;

        ApplyMode(mode);
    }

    public bool IsGameplayStage(
        int stageIndex)
    {
        return
            stageIndex == eggFleetStageIndex ||
            stageIndex == finalFortressStageIndex;
    }

    #endregion

    #region Mode Configuration

    private void ApplyMode(
        ShipMode mode)
    {
        ConfigurePhysics();

        switch (mode)
        {
            case ShipMode.Disabled:
                ConfigureDisabled();
                break;

            case ShipMode.Gameplay:
                ConfigureGameplay();
                break;

            case ShipMode.Cinematic:
                ConfigureCinematic();
                break;

            case ShipMode.Background:
                ConfigureBackground();
                break;
        }
    }

    private void ConfigureDisabled()
    {
        SetBigShipEnabled(false);
        SetWeaponsEnabled(false);
        ConfigurePlatform(false);
        SetCollidersEnabled(false);
    }

    private void ConfigureGameplay()
    {
        SetCollidersEnabled(
            gameplayCollisions);

        bool bigShip =
            shipType == ShipType.BigShip;

        SetBigShipEnabled(bigShip);
        SetWeaponsEnabled(bigShip);

        ConfigurePlatform(
            shipType ==
                ShipType.LowFlyingShip &&
            enablePlatformCollider);
    }

    private void ConfigureCinematic()
    {
        SetBigShipEnabled(false);

        SetWeaponsEnabled(
            weaponsEnabledDuringCinematic);

        ConfigurePlatform(false);

        SetCollidersEnabled(
            cinematicCollisions);
    }

    private void ConfigureBackground()
    {
        SetBigShipEnabled(false);
        SetWeaponsEnabled(false);
        ConfigurePlatform(false);

        SetCollidersEnabled(
            backgroundCollisions);
    }

    #endregion

    #region Flying Ship Movement

    private void UpdateFlyingShip()
    {
        if (!CanMove())
        {
            return;
        }

        Vector3 direction =
            GetGameplayDirection();

        if (!IsFinite(direction) ||
            direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion rotation =
            CalculateFlyingRotation(
                direction);

        Vector3 position =
            CalculateFlyingPosition(
                rotation);

        if (maintainHoverHeight)
        {
            position =
                ApplyHover(position);
        }

        if (enableBanking)
        {
            rotation =
                ApplyBanking(
                    rotation,
                    direction);
        }

        body.MovePosition(position);
        body.MoveRotation(rotation);
    }

    private Vector3 GetGameplayDirection()
    {
        if (gameplayWaypoints == null ||
            gameplayWaypoints.Length == 0)
        {
            return
                flyForwardWithoutWaypoints
                    ? transform.forward
                    : Vector3.zero;
        }

        if (gameplayWaypointIndex < 0 ||
            gameplayWaypointIndex >=
            gameplayWaypoints.Length)
        {
            gameplayWaypointIndex = 0;
        }

        Transform waypoint =
            gameplayWaypoints[
                gameplayWaypointIndex];

        if (waypoint == null)
        {
            AdvanceGameplayWaypoint();

            return transform.forward;
        }

        Vector3 direction =
            waypoint.position -
            body.position;

        if (!IsFinite(direction))
        {
            return Vector3.zero;
        }

        if (direction.magnitude <=
            gameplayWaypointRadius)
        {
            AdvanceGameplayWaypoint();

            return transform.forward;
        }

        return direction.normalized;
    }

    private void AdvanceGameplayWaypoint()
    {
        gameplayWaypointIndex++;

        if (gameplayWaypointIndex <
            gameplayWaypoints.Length)
        {
            return;
        }

        gameplayWaypointIndex =
            loopGameplayPath
                ? 0
                : gameplayWaypoints.Length - 1;
    }

    private Quaternion CalculateFlyingRotation(
        Vector3 direction)
    {
        Vector3 horizontal =
            Vector3.ProjectOnPlane(
                direction,
                Vector3.up);

        if (horizontal.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return body.rotation;
        }

        Quaternion desired =
            Quaternion.LookRotation(
                horizontal.normalized,
                Vector3.up);

        return Quaternion.RotateTowards(
            body.rotation,
            desired,
            turnSpeed *
            Time.fixedDeltaTime);
    }

    private Vector3 CalculateFlyingPosition(
        Quaternion rotation)
    {
        float speed =
            GetCruiseSpeed();

        Vector3 forward =
            rotation * Vector3.forward;

        Vector3 position =
            body.position +
            forward.normalized *
            speed *
            Time.fixedDeltaTime;

        return
            IsFinite(position)
                ? position
                : body.position;
    }

    #endregion

    #region Hover

    private Vector3 ApplyHover(
        Vector3 position)
    {
        float hoverHeight =
            GetHoverHeight();

        if (hoverHeight <= 0f)
        {
            return position;
        }

        float rayDistance =
            hoverHeight + 25f;

        if (Physics.Raycast(
            body.position,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            flightSurfaceMask,
            QueryTriggerInteraction.Ignore))
        {
            float desiredY =
                hit.point.y +
                hoverHeight;

            position.y =
                Mathf.MoveTowards(
                    body.position.y,
                    desiredY,
                    hoverCorrectionSpeed *
                    Time.fixedDeltaTime);
        }
        else
        {
            position.y =
                startingAltitude;
        }

        return position;
    }

    #endregion

    #region Banking

    private Quaternion ApplyBanking(
        Quaternion rotation,
        Vector3 direction)
    {
        Vector3 localDirection =
            transform.InverseTransformDirection(
                direction.normalized);

        float targetBank =
            Mathf.Clamp(
                -localDirection.x *
                bankAngle,
                -bankAngle,
                bankAngle);

        currentBankAngle =
            Mathf.Lerp(
                currentBankAngle,
                targetBank,
                bankSpeed *
                Time.fixedDeltaTime);

        Vector3 euler =
            rotation.eulerAngles;

        euler.z =
            currentBankAngle;

        return Quaternion.Euler(euler);
    }

    #endregion

    #region Cinematic

    private void UpdateCinematic()
    {
        if (cinematicComplete)
        {
            return;
        }

        if (cinematicWaypoints == null ||
            cinematicWaypoints.Length == 0)
        {
            cinematicComplete = true;
            return;
        }

        if (cinematicWaypointIndex < 0 ||
            cinematicWaypointIndex >=
            cinematicWaypoints.Length)
        {
            cinematicWaypointIndex = 0;
        }

        Transform waypoint =
            cinematicWaypoints[
                cinematicWaypointIndex];

        if (waypoint == null)
        {
            AdvanceCinematicWaypoint();
            return;
        }

        Vector3 direction =
            waypoint.position -
            transform.position;

        if (!IsFinite(direction))
        {
            return;
        }

        if (direction.magnitude <=
            cinematicWaypointRadius)
        {
            AdvanceCinematicWaypoint();
            return;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                waypoint.position,
                cinematicSpeed *
                Time.deltaTime);

        if (direction.sqrMagnitude >
            Mathf.Epsilon)
        {
            Quaternion desired =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);

            transform.rotation =
                Quaternion.RotateTowards(
                    transform.rotation,
                    desired,
                    cinematicTurnSpeed *
                    Time.deltaTime);
        }
    }

    private void AdvanceCinematicWaypoint()
    {
        cinematicWaypointIndex++;

        if (cinematicWaypointIndex <
            cinematicWaypoints.Length)
        {
            return;
        }

        switch (cinematicEndMode)
        {
            case CinematicEndMode.Hold:
                cinematicWaypointIndex =
                    cinematicWaypoints.Length - 1;

                cinematicComplete = true;
                break;

            case CinematicEndMode.Loop:
                cinematicWaypointIndex = 0;
                break;

            case CinematicEndMode.Disable:
                cinematicComplete = true;
                DisableShip();
                break;
        }
    }

    #endregion

    #region Background

    private void UpdateBackground()
    {
        if (!IsFinite(backgroundDirection) ||
            backgroundDirection.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        transform.position +=
            backgroundDirection.normalized *
            backgroundSpeed *
            Time.deltaTime;
    }

    #endregion

    #region Type Settings

    private float GetCruiseSpeed()
    {
        return shipType switch
        {
            ShipType.SmallShip =>
                smallShipCruiseSpeed,

            ShipType.LowFlyingShip =>
                lowFlyingShipCruiseSpeed,

            ShipType.MantaRayShip =>
                mantaRayCruiseSpeed,

            _ =>
                0f
        };
    }

    private float GetHoverHeight()
    {
        return shipType switch
        {
            ShipType.SmallShip =>
                smallShipHoverHeight,

            ShipType.LowFlyingShip =>
                lowFlyingShipHoverHeight,

            ShipType.MantaRayShip =>
                mantaRayHoverHeight,

            _ =>
                0f
        };
    }

    #endregion

    #region Components

    private void CacheComponents()
    {
        body =
            GetComponent<Rigidbody>();

        if (bigShipController == null)
        {
            bigShipController =
                GetComponent<BigShipController>();
        }

        if (turrets == null ||
            turrets.Length == 0)
        {
            turrets =
                GetComponentsInChildren<ShipTurret>(
                    true);
        }

        if (cannons == null ||
            cannons.Length == 0)
        {
            cannons =
                GetComponentsInChildren<ShipCannon>(
                    true);
        }

        shipColliders =
            GetComponentsInChildren<Collider>(
                true);
    }

    private void ConfigurePhysics()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = false;
        body.isKinematic = true;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
    }

    private bool CanMove()
    {
        return
            body != null &&
            gameObject.activeInHierarchy &&
            IsFinite(body.position) &&
            IsFinite(body.rotation);
    }

    private void SetBigShipEnabled(
        bool enabled)
    {
        if (bigShipController != null)
        {
            bigShipController.enabled =
                enabled;
        }
    }

    private void SetWeaponsEnabled(
        bool enabled)
    {
        if (turrets != null)
        {
            foreach (ShipTurret turret in turrets)
            {
                if (turret != null)
                {
                    turret.enabled =
                        enabled;
                }
            }
        }

        if (cannons != null)
        {
            foreach (ShipCannon cannon in cannons)
            {
                if (cannon != null)
                {
                    cannon.enabled =
                        enabled;
                }
            }
        }
    }

    private void ConfigurePlatform(
        bool enabled)
    {
        if (platformCollider != null)
        {
            platformCollider.enabled =
                enabled;
        }
    }

    private void SetCollidersEnabled(
        bool enabled)
    {
        if (shipColliders == null)
        {
            return;
        }

        foreach (Collider collider
            in shipColliders)
        {
            if (collider != null)
            {
                collider.enabled =
                    enabled;
            }
        }
    }

    #endregion

    #region Safety

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    private static bool IsFinite(
        Quaternion value)
    {
        return
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }

    #endregion
}