using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonicHeroes
{
    public class Orca : MonoBehaviour
    {
        #region Types

        public enum ActivationMode
        {
            Automatic,
            Triggered
        }

        public enum PlaybackMode
        {
            Once,
            Loop
        }

        private enum OrcaState
        {
            Idle,
            Waiting,
            Moving,
            Completed
        }

        #endregion

        #region Constants

        private const string DefaultRouteRootName = "Route";
        private const string DefaultSwimStateName = "Swim";

        #endregion

        #region Route

        [Header("Route")]
        [SerializeField]
        private Transform routeRoot;

        [SerializeField]
        private string routeRootName = DefaultRouteRootName;

        [SerializeField]
        private ActivationMode activationMode = ActivationMode.Automatic;

        [SerializeField]
        private PlaybackMode playbackMode = PlaybackMode.Loop;

        [SerializeField, Min(0f)]
        private float startDelay;

        [SerializeField]
        private bool snapToRouteStart = true;

        #endregion

        #region Movement

        [Header("Movement")]
        [SerializeField, Min(0.01f)]
        private float defaultMoveSpeed = 18f;

        [SerializeField, Min(0.01f)]
        private float defaultTurnSpeed = 8f;

        [SerializeField, Min(0.001f)]
        private float waypointReachDistance = 0.25f;

        [SerializeField]
        private bool faceTravelDirection = true;

        [SerializeField]
        private bool preserveRoll;

        #endregion

        #region Presentation

        [Header("Presentation")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string defaultSwimState = DefaultSwimStateName;

        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private AudioClip swimLoop;

        [SerializeField]
        private AudioClip defaultBreachSound;

        [SerializeField]
        private AudioClip defaultDiveSound;

        [SerializeField]
        private GameObject defaultBreachSplash;

        [SerializeField]
        private GameObject defaultDiveSplash;

        #endregion

        #region Runtime

        private readonly List<OrcaRoutePoint> routePoints =
            new List<OrcaRoutePoint>();

        private Rigidbody body;

        private OrcaState state;

        private int currentPointIndex;

        private float waitTimer;

        private bool initialized;

        private Vector3 safePosition;
        private Quaternion safeRotation;

        #endregion

        #region Properties

        public bool IsPlaying =>
            initialized &&
            (state == OrcaState.Waiting ||
             state == OrcaState.Moving);

        public bool IsComplete =>
            state == OrcaState.Completed;

        public int CurrentPointIndex =>
            currentPointIndex;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            switch (state)
            {
                case OrcaState.Waiting:
                    UpdateWaiting();
                    break;

                case OrcaState.Moving:
                    UpdateMovement();
                    break;
            }
        }

        private void OnDisable()
        {
            StopSwimLoop();
        }

        private void OnDestroy()
        {
            StopSwimLoop();

            initialized = false;
        }

        private void OnValidate()
        {
            defaultMoveSpeed =
                Mathf.Max(
                    0.01f,
                    defaultMoveSpeed);

            defaultTurnSpeed =
                Mathf.Max(
                    0.01f,
                    defaultTurnSpeed);

            waypointReachDistance =
                Mathf.Max(
                    0.001f,
                    waypointReachDistance);

            startDelay =
                Mathf.Max(
                    0f,
                    startDelay);
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            CacheComponents();
            ConfigurePhysics();
            BuildRoute();

            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            CaptureSafeTransform();

            if (snapToRouteStart)
            {
                SnapToStart();
            }

            PlayDefaultAnimation();
            StartSwimLoop();

            initialized = true;

            if (activationMode == ActivationMode.Automatic)
            {
                BeginSequence();
            }
            else
            {
                state = OrcaState.Idle;
            }
        }

        private void CacheComponents()
        {
            body =
                GetComponent<Rigidbody>();

            if (animator == null)
            {
                animator =
                    GetComponentInChildren<Animator>(
                        true);
            }

            if (audioSource == null)
            {
                audioSource =
                    GetComponent<AudioSource>();
            }
        }

        private void ConfigurePhysics()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;

            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        private void BuildRoute()
        {
            routePoints.Clear();

            if (routeRoot == null)
            {
                routeRoot =
                    FindChildRecursive(
                        transform,
                        routeRootName);
            }

            if (routeRoot == null)
            {
                return;
            }

            for (int index = 0;
                index < routeRoot.childCount;
                index++)
            {
                Transform child =
                    routeRoot.GetChild(
                        index);

                if (child == null)
                {
                    continue;
                }

                OrcaRoutePoint point =
                    child.GetComponent<OrcaRoutePoint>();

                if (point == null)
                {
                    point =
                        child.gameObject.AddComponent<OrcaRoutePoint>();
                }

                routePoints.Add(
                    point);
            }
        }

        private bool ValidateSetup()
        {
            if (body == null)
            {
                Debug.LogError(
                    $"{nameof(Orca)} requires a Rigidbody.",
                    this);

                return false;
            }

            if (routeRoot == null)
            {
                Debug.LogError(
                    $"{nameof(Orca)} could not find route root '{routeRootName}'.",
                    this);

                return false;
            }

            if (routePoints.Count < 2)
            {
                Debug.LogError(
                    $"{nameof(Orca)} requires at least two route points under '{routeRoot.name}'.",
                    this);

                return false;
            }

            return true;
        }

        #endregion

        #region Sequence Control

        public void BeginSequence()
        {
            if (!initialized)
            {
                return;
            }

            currentPointIndex =
                0;

            waitTimer =
                startDelay;

            if (snapToRouteStart)
            {
                SnapToStart();
            }

            state =
                waitTimer > 0f
                    ? OrcaState.Waiting
                    : OrcaState.Moving;
        }

        public void PlaySequence()
        {
            BeginSequence();
        }

        public void StopSequence()
        {
            if (!initialized)
            {
                return;
            }

            state =
                OrcaState.Idle;

            StopMotion();
        }

        public void RestartSequence()
        {
            if (!initialized)
            {
                return;
            }

            ResetSequence();
            BeginSequence();
        }

        public void ResetSequence()
        {
            if (!initialized)
            {
                return;
            }

            currentPointIndex =
                0;

            waitTimer =
                0f;

            state =
                OrcaState.Idle;

            StopMotion();
            SnapToStart();
        }

        #endregion

        #region Waiting

        private void UpdateWaiting()
        {
            waitTimer -=
                Time.fixedDeltaTime;

            if (waitTimer > 0f)
            {
                return;
            }

            state =
                OrcaState.Moving;
        }

        #endregion

        #region Movement

        private void UpdateMovement()
        {
            if (!ValidateRuntimeTransform())
            {
                RecoverTransform();
                return;
            }

            if (currentPointIndex >=
                routePoints.Count)
            {
                CompleteSequence();
                return;
            }

            OrcaRoutePoint point =
                routePoints[currentPointIndex];

            if (point == null)
            {
                AdvancePoint();
                return;
            }

            Vector3 targetPosition =
                point.transform.position;

            if (!IsFinite(
                targetPosition))
            {
                AdvancePoint();
                return;
            }

            Vector3 currentPosition =
                body.position;

            Vector3 toTarget =
                targetPosition -
                currentPosition;

            float reachDistance =
                point.ReachDistanceOverride > 0f
                    ? point.ReachDistanceOverride
                    : waypointReachDistance;

            float reachDistanceSquared =
                reachDistance *
                reachDistance;

            if (toTarget.sqrMagnitude <=
                reachDistanceSquared)
            {
                HandlePointReached(
                    point);

                AdvancePoint();
                return;
            }

            float moveSpeed =
                point.MoveSpeedOverride > 0f
                    ? point.MoveSpeedOverride
                    : defaultMoveSpeed;

            float turnSpeed =
                point.TurnSpeedOverride > 0f
                    ? point.TurnSpeedOverride
                    : defaultTurnSpeed;

            if (faceTravelDirection)
            {
                UpdateRotation(
                    toTarget,
                    turnSpeed);
            }

            Vector3 nextPosition =
                Vector3.MoveTowards(
                    currentPosition,
                    targetPosition,
                    moveSpeed *
                    Time.fixedDeltaTime);

            if (!IsFinite(
                nextPosition))
            {
                RecoverTransform();
                return;
            }

            body.MovePosition(
                nextPosition);

            safePosition =
                nextPosition;

            safeRotation =
                body.rotation;
        }

        private void UpdateRotation(
            Vector3 direction,
            float turnSpeed)
        {
            if (!IsFinite(direction) ||
                direction.sqrMagnitude <=
                Mathf.Epsilon)
            {
                return;
            }

            Vector3 normalizedDirection =
                direction.normalized;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    normalizedDirection,
                    Vector3.up);

            if (preserveRoll)
            {
                Vector3 targetEuler =
                    targetRotation.eulerAngles;

                targetEuler.z =
                    body.rotation.eulerAngles.z;

                targetRotation =
                    Quaternion.Euler(
                        targetEuler);
            }

            Quaternion nextRotation =
                Quaternion.Slerp(
                    body.rotation,
                    targetRotation,
                    turnSpeed *
                    Time.fixedDeltaTime);

            if (!IsFinite(
                nextRotation))
            {
                return;
            }

            body.MoveRotation(
                nextRotation);
        }

        private void AdvancePoint()
        {
            currentPointIndex++;

            if (currentPointIndex >=
                routePoints.Count)
            {
                CompleteSequence();
            }
        }

        private void CompleteSequence()
        {
            StopMotion();

            if (playbackMode ==
                PlaybackMode.Loop)
            {
                ResetSequence();
                BeginSequence();

                return;
            }

            state =
                OrcaState.Completed;
        }

        private void StopMotion()
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

        #region Route Events

        private void HandlePointReached(
            OrcaRoutePoint point)
        {
            if (point == null)
            {
                return;
            }

            ApplyPointAnimation(
                point);

            switch (point.Type)
            {
                case OrcaRoutePoint.RoutePointType.Swim:
                    break;

                case OrcaRoutePoint.RoutePointType.Surface:
                    SpawnEffect(
                        point.EffectOverride != null
                            ? point.EffectOverride
                            : defaultBreachSplash,
                        point.transform);

                    break;

                case OrcaRoutePoint.RoutePointType.Breach:
                    PlayOneShot(
                        point.AudioOverride != null
                            ? point.AudioOverride
                            : defaultBreachSound,
                        point.Volume);

                    SpawnEffect(
                        point.EffectOverride != null
                            ? point.EffectOverride
                            : defaultBreachSplash,
                        point.transform);

                    break;

                case OrcaRoutePoint.RoutePointType.Air:
                    break;

                case OrcaRoutePoint.RoutePointType.Dive:
                    PlayOneShot(
                        point.AudioOverride != null
                            ? point.AudioOverride
                            : defaultDiveSound,
                        point.Volume);

                    SpawnEffect(
                        point.EffectOverride != null
                            ? point.EffectOverride
                            : defaultDiveSplash,
                        point.transform);

                    break;

                case OrcaRoutePoint.RoutePointType.Exit:
                    break;
            }

            point.InvokeReached();

            if (point.PauseDuration > 0f)
            {
                waitTimer =
                    point.PauseDuration;

                state =
                    OrcaState.Waiting;
            }
        }

        private void ApplyPointAnimation(
            OrcaRoutePoint point)
        {
            if (animator == null ||
                point == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                point.AnimationTrigger))
            {
                animator.SetTrigger(
                    point.AnimationTrigger);

                return;
            }

            if (!string.IsNullOrWhiteSpace(
                point.AnimationState))
            {
                animator.Play(
                    point.AnimationState,
                    0,
                    0f);
            }
        }

        #endregion

        #region Presentation

        private void PlayDefaultAnimation()
        {
            if (animator == null ||
                string.IsNullOrWhiteSpace(
                    defaultSwimState))
            {
                return;
            }

            animator.Play(
                defaultSwimState,
                0,
                0f);
        }

        private void StartSwimLoop()
        {
            if (audioSource == null ||
                swimLoop == null)
            {
                return;
            }

            audioSource.loop =
                true;

            audioSource.clip =
                swimLoop;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private void StopSwimLoop()
        {
            if (audioSource == null)
            {
                return;
            }

            if (audioSource.clip ==
                swimLoop)
            {
                audioSource.Stop();
            }
        }

        private void PlayOneShot(
            AudioClip clip,
            float volume)
        {
            if (audioSource == null ||
                clip == null)
            {
                return;
            }

            audioSource.PlayOneShot(
                clip,
                Mathf.Clamp01(volume));
        }

        private static void SpawnEffect(
            GameObject effect,
            Transform source)
        {
            if (effect == null ||
                source == null)
            {
                return;
            }

            Instantiate(
                effect,
                source.position,
                source.rotation);
        }

        #endregion

        #region Animation Event Hooks

        public void SplashFX_Start()
        {
            SpawnEffect(
                defaultBreachSplash,
                transform);
        }

        public void Play_Breach_Sound()
        {
            PlayOneShot(
                defaultBreachSound,
                1f);
        }

        public void SplashFX_Land()
        {
            SpawnEffect(
                defaultDiveSplash,
                transform);
        }

        public void Play_Impact_Sound()
        {
            PlayOneShot(
                defaultDiveSound,
                1f);
        }

        #endregion

        #region Recovery

        private void CaptureSafeTransform()
        {
            safePosition =
                body != null
                    ? body.position
                    : transform.position;

            safeRotation =
                body != null
                    ? body.rotation
                    : transform.rotation;
        }

        private bool ValidateRuntimeTransform()
        {
            if (body == null)
            {
                return false;
            }

            if (!IsFinite(
                body.position) ||
                !IsFinite(
                    body.rotation))
            {
                return false;
            }

            if (!IsFinite(
                body.linearVelocity) ||
                !IsFinite(
                    body.angularVelocity))
            {
                return false;
            }

            Vector3 scale =
                transform.lossyScale;

            if (!IsFinite(scale) ||
                Mathf.Approximately(
                    scale.x,
                    0f) ||
                Mathf.Approximately(
                    scale.y,
                    0f) ||
                Mathf.Approximately(
                    scale.z,
                    0f))
            {
                return false;
            }

            return true;
        }

        private void RecoverTransform()
        {
            if (body == null)
            {
                return;
            }

            StopMotion();

            Vector3 recoveryPosition =
                IsFinite(safePosition)
                    ? safePosition
                    : transform.position;

            Quaternion recoveryRotation =
                IsFinite(safeRotation)
                    ? safeRotation
                    : Quaternion.identity;

            body.position =
                recoveryPosition;

            body.rotation =
                recoveryRotation;

            transform.position =
                recoveryPosition;

            transform.rotation =
                recoveryRotation;
        }

        private void SnapToStart()
        {
            if (routePoints.Count == 0 ||
                body == null)
            {
                return;
            }

            OrcaRoutePoint first =
                routePoints[0];

            if (first == null)
            {
                return;
            }

            Vector3 position =
                first.transform.position;

            if (!IsFinite(position))
            {
                return;
            }

            body.position =
                position;

            transform.position =
                position;

            if (routePoints.Count > 1 &&
                routePoints[1] != null)
            {
                Vector3 direction =
                    routePoints[1].transform.position -
                    position;

                if (direction.sqrMagnitude >
                    Mathf.Epsilon)
                {
                    Quaternion rotation =
                        Quaternion.LookRotation(
                            direction.normalized,
                            Vector3.up);

                    if (IsFinite(rotation))
                    {
                        body.rotation =
                            rotation;

                        transform.rotation =
                            rotation;
                    }
                }
            }

            CaptureSafeTransform();
        }

        #endregion

        #region Helpers

        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null ||
                string.IsNullOrWhiteSpace(
                    childName))
            {
                return null;
            }

            for (int index = 0;
                index < root.childCount;
                index++)
            {
                Transform child =
                    root.GetChild(
                        index);

                if (child == null)
                {
                    continue;
                }

                if (child.name.Equals(
                    childName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform nested =
                    FindChildRecursive(
                        child,
                        childName);

                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

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
}
