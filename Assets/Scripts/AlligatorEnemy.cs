using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class AlligatorEnemy : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private Transform player;

    #endregion

    #region Chase

    [Header("Chase")]
    [SerializeField, Min(0f)]
    private float chaseSpeed = 16f;

    [SerializeField, Min(0f)]
    private float catchUpSpeed = 22f;

    [SerializeField, Min(0f)]
    private float catchUpDistance = 20f;

    [SerializeField, Min(0f)]
    private float minimumDistance = 5f;

    [SerializeField, Min(0f)]
    private float turnSpeed = 6f;

    [SerializeField]
    private bool lockHeight = true;

    #endregion

    #region Path

    [Header("Chase Path")]
    [SerializeField]
    private Transform[] chasePoints;

    [SerializeField, Min(0.1f)]
    private float waypointRadius = 4f;

    #endregion

    #region Catch

    [Header("Catch")]
    [SerializeField, Min(0f)]
    private float catchDistance = 3f;

    [SerializeField, Min(0f)]
    private float catchCooldown = 1f;

    #endregion

    #region Animation

    [Header("Animation")]
    [SerializeField]
    private string moveParameter = "Swim";

    [SerializeField]
    private string roarTrigger = "Roar";

    [SerializeField]
    private string biteTrigger = "Bite";

    #endregion

    #region Events

    [Header("Events")]
    [SerializeField]
    private UnityEvent onChaseStarted;

    [SerializeField]
    private UnityEvent onPlayerCaught;

    [SerializeField]
    private UnityEvent onChaseFinished;

    #endregion

    #region Runtime

    private int currentPoint;

    private float fixedHeight;
    private float nextCatchTime;

    private bool initialized;
    private bool chaseActive;
    private bool chaseFinished;

    #endregion

    #region Properties

    public bool IsChasing => chaseActive;

    public bool IsFinished => chaseFinished;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
        ConfigurePhysics();

        fixedHeight = transform.position.y;

        initialized = body != null;

        SetMovementAnimation(false);
    }

    private void FixedUpdate()
    {
        if (!initialized ||
            !chaseActive ||
            chaseFinished)
        {
            return;
        }

        UpdateChase();
    }

    private void OnDisable()
    {
        StopMovement();
        SetMovementAnimation(false);
    }

    private void OnDestroy()
    {
        chaseActive = false;
        initialized = false;

        StopMovement();
    }

    private void OnValidate()
    {
        chaseSpeed =
            Mathf.Max(0f, chaseSpeed);

        catchUpSpeed =
            Mathf.Max(chaseSpeed, catchUpSpeed);

        catchUpDistance =
            Mathf.Max(0f, catchUpDistance);

        minimumDistance =
            Mathf.Max(0f, minimumDistance);

        turnSpeed =
            Mathf.Max(0f, turnSpeed);

        waypointRadius =
            Mathf.Max(0.1f, waypointRadius);

        catchDistance =
            Mathf.Max(0f, catchDistance);

        catchCooldown =
            Mathf.Max(0f, catchCooldown);
    }

    #endregion

    #region Chase Control

    public void StartChase()
    {
        if (!initialized ||
            chaseActive)
        {
            return;
        }

        FindPlayer();

        currentPoint = 0;
        nextCatchTime = 0f;

        chaseFinished = false;
        chaseActive = true;

        ConfigurePhysics();

        SetMovementAnimation(true);
        SetAnimatorTrigger(roarTrigger);

        onChaseStarted?.Invoke();
    }

    public void StopChase()
    {
        if (!chaseActive)
        {
            return;
        }

        chaseActive = false;
        chaseFinished = true;

        StopMovement();
        SetMovementAnimation(false);

        onChaseFinished?.Invoke();
    }

    public void ResetChase()
    {
        chaseActive = false;
        chaseFinished = false;

        currentPoint = 0;
        nextCatchTime = 0f;

        StopMovement();
        SetMovementAnimation(false);
    }

    #endregion

    #region Chase

    private void UpdateChase()
    {
        if (body == null)
        {
            return;
        }

        if (!TryGetTargetPosition(
            out Vector3 target))
        {
            StopChase();
            return;
        }

        Vector3 direction =
            target - body.position;

        if (lockHeight)
        {
            direction.y = 0f;
        }

        if (!IsFinite(direction) ||
            direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            UpdateWaypoint();
            CheckPlayerCatch();
            return;
        }

        direction.Normalize();

        float speed =
            CalculateSpeed();

        Vector3 nextPosition =
            body.position +
            direction *
            speed *
            Time.fixedDeltaTime;

        if (lockHeight)
        {
            nextPosition.y =
                fixedHeight;
        }

        if (!IsFinite(nextPosition))
        {
            StopChase();
            return;
        }

        body.MovePosition(nextPosition);

        RotateTowards(direction);

        UpdateWaypoint();
        CheckPlayerCatch();
    }

    private float CalculateSpeed()
    {
        if (player == null)
        {
            return chaseSpeed;
        }

        float distance =
            Vector3.Distance(
                body.position,
                player.position);

        if (!float.IsFinite(distance))
        {
            return chaseSpeed;
        }

        if (distance >= catchUpDistance)
        {
            return catchUpSpeed;
        }

        if (distance <= minimumDistance)
        {
            return chaseSpeed;
        }

        return chaseSpeed;
    }

    private void RotateTowards(
        Vector3 direction)
    {
        Vector3 horizontalDirection =
            Vector3.ProjectOnPlane(
                direction,
                Vector3.up);

        if (horizontalDirection.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                horizontalDirection.normalized,
                Vector3.up);

        Quaternion rotation =
            Quaternion.Slerp(
                body.rotation,
                targetRotation,
                turnSpeed *
                Time.fixedDeltaTime);

        if (!IsFinite(rotation))
        {
            return;
        }

        body.MoveRotation(rotation);
    }

    #endregion

    #region Chase Path

    private bool TryGetTargetPosition(
        out Vector3 target)
    {
        target = body.position;

        if (chasePoints == null ||
            chasePoints.Length == 0)
        {
            if (player == null)
            {
                FindPlayer();
            }

            if (player == null)
            {
                return false;
            }

            target = player.position;

            return IsFinite(target);
        }

        while (currentPoint <
            chasePoints.Length)
        {
            Transform point =
                chasePoints[currentPoint];

            if (point != null)
            {
                target = point.position;

                return IsFinite(target);
            }

            currentPoint++;
        }

        return false;
    }

    private void UpdateWaypoint()
    {
        if (chasePoints == null ||
            chasePoints.Length == 0)
        {
            return;
        }

        if (currentPoint < 0 ||
            currentPoint >=
            chasePoints.Length)
        {
            StopChase();
            return;
        }

        Transform point =
            chasePoints[currentPoint];

        if (point == null)
        {
            AdvanceWaypoint();
            return;
        }

        Vector3 currentPosition =
            body.position;

        Vector3 pointPosition =
            point.position;

        if (lockHeight)
        {
            currentPosition.y = 0f;
            pointPosition.y = 0f;
        }

        float distance =
            Vector3.Distance(
                currentPosition,
                pointPosition);

        if (!float.IsFinite(distance))
        {
            return;
        }

        if (distance <= waypointRadius)
        {
            AdvanceWaypoint();
        }
    }

    private void AdvanceWaypoint()
    {
        currentPoint++;

        if (chasePoints == null ||
            currentPoint <
            chasePoints.Length)
        {
            return;
        }

        StopChase();
    }

    #endregion

    #region Player Catch

    private void CheckPlayerCatch()
    {
        if (player == null ||
            Time.time <
            nextCatchTime)
        {
            return;
        }

        float distance =
            Vector3.Distance(
                body.position,
                player.position);

        if (!float.IsFinite(distance) ||
            distance >
            catchDistance)
        {
            return;
        }

        CatchPlayer();
    }

    private void CatchPlayer()
    {
        if (Time.time <
            nextCatchTime)
        {
            return;
        }

        nextCatchTime =
            Time.time +
            catchCooldown;

        SetAnimatorTrigger(
            biteTrigger);

        onPlayerCaught?.Invoke();
    }

    #endregion

    #region References

    private void CacheReferences()
    {
        if (body == null)
        {
            body =
                GetComponent<Rigidbody>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        FindPlayer();
    }

    private void FindPlayer()
    {
        if (player != null)
        {
            return;
        }

        UltimatePlayerMovement movement = FindAnyObjectByType<UltimatePlayerMovement>();
              

        if (movement != null)
        {
            player =
                movement.transform;
        }
    }

    #endregion

    #region Physics

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
            CollisionDetectionMode
                .ContinuousSpeculative;
    }

    private void StopMovement()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;
    }

    #endregion

    #region Animation

    private void SetMovementAnimation(
        bool active)
    {
        if (!CanUseAnimator() ||
            string.IsNullOrWhiteSpace(
                moveParameter))
        {
            return;
        }

        animator.SetBool(
            moveParameter,
            active);
    }

    private void SetAnimatorTrigger(
        string parameter)
    {
        if (!CanUseAnimator() ||
            string.IsNullOrWhiteSpace(
                parameter))
        {
            return;
        }

        animator.SetTrigger(
            parameter);
    }

    private bool CanUseAnimator()
    {
        return
            animator != null &&
            animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController !=
            null;
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