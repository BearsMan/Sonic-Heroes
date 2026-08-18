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

        private readonly struct RouteNode
        {
            public RouteNode(
                OrcaRoutePoint point,
                Vector3 worldPosition,
                Quaternion worldRotation)
            {
                Point = point;
                WorldPosition = worldPosition;
                WorldRotation = worldRotation;
            }

            public OrcaRoutePoint Point { get; }

            public Vector3 WorldPosition { get; }

            public Quaternion WorldRotation { get; }
        }

        #endregion

        #region Constants

        private const string DefaultRouteRootName = "Route";
        private const string DefaultVisualRootName = "Visual";
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

        #region Procedural Animation

        [Header("Procedural Animation")]
        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private string visualRootName = DefaultVisualRootName;

        [SerializeField]
        private bool enableProceduralSwim = true;

        [SerializeField, Min(0f)]
        private float bobAmplitude = 0.12f;

        [SerializeField, Min(0f)]
        private float bobFrequency = 1.2f;

        [SerializeField, Min(0f)]
        private float swayAngle = 4f;

        [SerializeField, Min(0f)]
        private float swayFrequency = 1.5f;

        [SerializeField, Min(0f)]
        private float bankAngle = 12f;

        [SerializeField, Min(0f)]
        private float bankResponsiveness = 6f;

        [SerializeField, Min(0f)]
        private float pitchAngle = 10f;

        [SerializeField, Min(0f)]
        private float pitchResponsiveness = 6f;

        [SerializeField, Min(0f)]
        private float breachPitch = 18f;

        [SerializeField, Min(0f)]
        private float divePitch = 20f;

        [SerializeField]
        private bool randomizeAnimationPhase = true;

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

        private readonly List<RouteNode> routeNodes =
            new List<RouteNode>();

        private Rigidbody body;

        private OrcaState state;

        private int currentPointIndex;

        private float waitTimer;

        private bool initialized;

        private Vector3 safePosition;
        private Quaternion safeRotation;

        private Vector3 previousBodyPosition;
        private Vector3 currentVelocity;
        private Vector3 previousForward;

        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation;

        private float animationPhase;
        private float currentBank;
        private float currentPitch;

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

            UpdateMotionData();
        }

        private void LateUpdate()
        {
            if (!initialized ||
                !enableProceduralSwim)
            {
                return;
            }

            UpdateProceduralAnimation();
        }

        private void OnDisable()
        {
            StopSwimLoop();
            RestoreVisualTransform();
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

            bobAmplitude =
                Mathf.Max(
                    0f,
                    bobAmplitude);

            bobFrequency =
                Mathf.Max(
                    0f,
                    bobFrequency);

            swayAngle =
                Mathf.Max(
                    0f,
                    swayAngle);

            swayFrequency =
                Mathf.Max(
                    0f,
                    swayFrequency);

            bankAngle =
                Mathf.Max(
                    0f,
                    bankAngle);

            bankResponsiveness =
                Mathf.Max(
                    0f,
                    bankResponsiveness);

            pitchAngle =
                Mathf.Max(
                    0f,
                    pitchAngle);

            pitchResponsiveness =
                Mathf.Max(
                    0f,
                    pitchResponsiveness);

            breachPitch =
                Mathf.Max(
                    0f,
                    breachPitch);

            divePitch =
                Mathf.Max(
                    0f,
                    divePitch);
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            CacheComponents();
            CacheVisualRoot();
            ConfigurePhysics();
            BuildRoute();

            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            CaptureSafeTransform();
            CaptureVisualTransform();

            if (snapToRouteStart)
            {
                SnapToStart();
            }

            previousBodyPosition =
                body.position;

            previousForward =
                transform.forward;

            animationPhase =
                randomizeAnimationPhase
                    ? UnityEngine.Random.Range(
                        0f,
                        Mathf.PI * 2f)
                    : 0f;

            PlayDefaultAnimation();
            StartSwimLoop();

            initialized = true;

            if (activationMode ==
                ActivationMode.Automatic)
            {
                BeginSequence();
            }
            else
            {
                state =
                    OrcaState.Idle;
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

        private void CacheVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot =
                FindChildRecursive(
                    transform,
                    visualRootName);

            if (visualRoot != null)
            {
                return;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(
                    true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer.transform == transform)
                {
                    continue;
                }

                visualRoot =
                    renderer.transform;

                return;
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
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        private void BuildRoute()
        {
            routeNodes.Clear();

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

                Vector3 worldPosition =
                    child.position;

                Quaternion worldRotation =
                    child.rotation;

                if (!IsFinite(worldPosition) ||
                    !IsFinite(worldRotation))
                {
                    continue;
                }

                routeNodes.Add(
                    new RouteNode(
                        point,
                        worldPosition,
                        worldRotation));
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

            if (routeNodes.Count < 2)
            {
                Debug.LogError(
                    $"{nameof(Orca)} requires at least two valid route points under '{routeRoot.name}'.",
                    this);

                return false;
            }

            return true;
        }

        #endregion

        #region Route Rebuild

        public void RebuildRoute()
        {
            if (!initialized)
            {
                return;
            }

            BuildRoute();

            if (routeNodes.Count < 2)
            {
                Debug.LogError(
                    $"{nameof(Orca)} could not rebuild a valid route.",
                    this);

                StopSequence();
                return;
            }

            currentPointIndex =
                Mathf.Clamp(
                    currentPointIndex,
                    0,
                    routeNodes.Count - 1);
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
            RestoreVisualTransform();
        }

        #endregion

        #region Waiting

        private void UpdateWaiting()
        {
            waitTimer -=
                Time.fixedDeltaTime;

            if (waitTimer <= 0f)
            {
                state =
                    OrcaState.Moving;
            }
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
                routeNodes.Count)
            {
                CompleteSequence();
                return;
            }

            RouteNode node =
                routeNodes[currentPointIndex];

            OrcaRoutePoint point =
                node.Point;

            if (point == null)
            {
                AdvancePoint();
                return;
            }

            Vector3 targetPosition =
                node.WorldPosition;

            Vector3 currentPosition =
                body.position;

            Vector3 toTarget =
                targetPosition -
                currentPosition;

            float reachDistance =
                point.ReachDistanceOverride > 0f
                    ? point.ReachDistanceOverride
                    : waypointReachDistance;

            if (toTarget.sqrMagnitude <=
                reachDistance *
                reachDistance)
            {
                HandlePointReached(
                    node);

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

            if (!IsFinite(nextPosition))
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

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);

            if (preserveRoll)
            {
                Vector3 euler =
                    targetRotation.eulerAngles;

                euler.z =
                    body.rotation.eulerAngles.z;

                targetRotation =
                    Quaternion.Euler(
                        euler);
            }

            Quaternion nextRotation =
                Quaternion.Slerp(
                    body.rotation,
                    targetRotation,
                    turnSpeed *
                    Time.fixedDeltaTime);

            if (IsFinite(nextRotation))
            {
                body.MoveRotation(
                    nextRotation);
            }
        }

        private void UpdateMotionData()
        {
            if (body == null)
            {
                return;
            }

            float deltaTime =
                Time.fixedDeltaTime;

            if (deltaTime <=
                Mathf.Epsilon)
            {
                return;
            }

            Vector3 position =
                body.position;

            currentVelocity =
                IsFinite(position) &&
                IsFinite(previousBodyPosition)
                    ? (position -
                        previousBodyPosition) /
                        deltaTime
                    : Vector3.zero;

            previousBodyPosition =
                position;
        }

        private void AdvancePoint()
        {
            currentPointIndex++;

            if (currentPointIndex >=
                routeNodes.Count)
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
            currentVelocity = Vector3.zero;
        }

        #endregion

        #region Procedural Animation

        private void CaptureVisualTransform()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualBaseLocalPosition =
                visualRoot.localPosition;

            visualBaseLocalRotation =
                visualRoot.localRotation;
        }

        private void RestoreVisualTransform()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition =
                visualBaseLocalPosition;

            visualRoot.localRotation =
                visualBaseLocalRotation;

            currentBank =
                0f;

            currentPitch =
                0f;
        }

        private void UpdateProceduralAnimation()
        {
            if (visualRoot == null)
            {
                return;
            }

            float time =
                Time.time +
                animationPhase;

            float speedFactor =
                Mathf.Clamp01(
                    currentVelocity.magnitude /
                    Mathf.Max(
                        defaultMoveSpeed,
                        0.01f));

            float bob =
                Mathf.Sin(
                    time *
                    bobFrequency *
                    Mathf.PI *
                    2f) *
                bobAmplitude *
                Mathf.Lerp(
                    0.35f,
                    1f,
                    speedFactor);

            float sway =
                Mathf.Sin(
                    time *
                    swayFrequency *
                    Mathf.PI *
                    2f) *
                swayAngle *
                Mathf.Lerp(
                    0.35f,
                    1f,
                    speedFactor);

            Vector3 forward =
                transform.forward;

            Vector3 previousFlat =
                Vector3.ProjectOnPlane(
                    previousForward,
                    Vector3.up)
                .normalized;

            Vector3 currentFlat =
                Vector3.ProjectOnPlane(
                    forward,
                    Vector3.up)
                .normalized;

            float turnAmount =
                0f;

            if (previousFlat.sqrMagnitude >
                    Mathf.Epsilon &&
                currentFlat.sqrMagnitude >
                    Mathf.Epsilon)
            {
                turnAmount =
                    Vector3.SignedAngle(
                        previousFlat,
                        currentFlat,
                        Vector3.up);
            }

            float targetBank =
                Mathf.Clamp(
                    -turnAmount *
                        4f,
                    -bankAngle,
                    bankAngle);

            float targetPitch =
                Mathf.Clamp(
                    currentVelocity.y,
                    -pitchAngle,
                    pitchAngle);

            OrcaRoutePoint.RoutePointType pointType =
                GetCurrentPointType();

            if (pointType ==
                    OrcaRoutePoint.RoutePointType.Breach ||
                pointType ==
                    OrcaRoutePoint.RoutePointType.Air)
            {
                targetPitch =
                    Mathf.Max(
                        targetPitch,
                        breachPitch);
            }
            else if (pointType ==
                OrcaRoutePoint.RoutePointType.Dive)
            {
                targetPitch =
                    -Mathf.Max(
                        Mathf.Abs(
                            targetPitch),
                        divePitch);
            }

            currentBank =
                Mathf.Lerp(
                    currentBank,
                    targetBank,
                    1f -
                    Mathf.Exp(
                        -bankResponsiveness *
                        Time.deltaTime));

            currentPitch =
                Mathf.Lerp(
                    currentPitch,
                    targetPitch,
                    1f -
                    Mathf.Exp(
                        -pitchResponsiveness *
                        Time.deltaTime));

            visualRoot.localPosition =
                visualBaseLocalPosition +
                Vector3.up *
                bob;

            visualRoot.localRotation =
                visualBaseLocalRotation *
                Quaternion.Euler(
                    currentPitch,
                    sway,
                    currentBank);

            previousForward =
                forward;
        }

        private OrcaRoutePoint.RoutePointType GetCurrentPointType()
        {
            if (currentPointIndex < 0 ||
                currentPointIndex >=
                    routeNodes.Count)
            {
                return OrcaRoutePoint.RoutePointType.Swim;
            }

            OrcaRoutePoint point =
                routeNodes[currentPointIndex].Point;

            return point != null
                ? point.Type
                : OrcaRoutePoint.RoutePointType.Swim;
        }

        #endregion

        #region Route Events

        private void HandlePointReached(
            RouteNode node)
        {
            OrcaRoutePoint point =
                node.Point;

            if (point == null)
            {
                return;
            }

            ApplyPointAnimation(
                point);

            switch (point.Type)
            {
                case OrcaRoutePoint.RoutePointType.Surface:
                    SpawnEffect(
                        point.EffectOverride != null
                            ? point.EffectOverride
                            : defaultBreachSplash,
                        node.WorldPosition,
                        node.WorldRotation);

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
                        node.WorldPosition,
                        node.WorldRotation);

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
                        node.WorldPosition,
                        node.WorldRotation);

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
            if (audioSource != null &&
                audioSource.clip ==
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
                Mathf.Clamp01(
                    volume));
        }

        private static void SpawnEffect(
            GameObject effect,
            Vector3 position,
            Quaternion rotation)
        {
            if (effect == null ||
                !IsFinite(position) ||
                !IsFinite(rotation))
            {
                return;
            }

            Instantiate(
                effect,
                position,
                rotation);
        }

        #endregion

        #region Animation Event Hooks

        public void SplashFX_Start()
        {
            SpawnEffect(
                defaultBreachSplash,
                transform.position,
                transform.rotation);
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
                transform.position,
                transform.rotation);
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

            if (!IsFinite(body.position) ||
                !IsFinite(body.rotation) ||
                !IsFinite(body.linearVelocity) ||
                !IsFinite(body.angularVelocity))
            {
                return false;
            }

            Vector3 scale =
                transform.lossyScale;

            return
                IsFinite(scale) &&
                !Mathf.Approximately(
                    scale.x,
                    0f) &&
                !Mathf.Approximately(
                    scale.y,
                    0f) &&
                !Mathf.Approximately(
                    scale.z,
                    0f);
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
            if (routeNodes.Count == 0 ||
                body == null)
            {
                return;
            }

            RouteNode first =
                routeNodes[0];

            Vector3 position =
                first.WorldPosition;

            if (!IsFinite(position))
            {
                return;
            }

            body.position =
                position;

            transform.position =
                position;

            if (routeNodes.Count > 1)
            {
                Vector3 direction =
                    routeNodes[1].WorldPosition -
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

            previousBodyPosition =
                position;

            previousForward =
                transform.forward;

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
                float.IsFinite(
                    value.x) &&
                float.IsFinite(
                    value.y) &&
                float.IsFinite(
                    value.z);
        }

        private static bool IsFinite(
            Quaternion value)
        {
            return
                float.IsFinite(
                    value.x) &&
                float.IsFinite(
                    value.y) &&
                float.IsFinite(
                    value.z) &&
                float.IsFinite(
                    value.w);
        }

        private void OnDrawGizmos()
        {
            Transform root = routeRoot;

            if (root == null)
            {
                root = FindChildRecursive(transform, routeRootName);
            }

            if (root == null || root.childCount == 0)
            {
                return;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform point = root.GetChild(index);

                if (point == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(point.position, 0.25f);

                if (index >= root.childCount - 1)
                {
                    continue;
                }

                Transform next = root.GetChild(index + 1);

                if (next != null)
                {
                    Gizmos.DrawLine(
                        point.position,
                        next.position);
                }
            }
        }

        #endregion
    }
}
