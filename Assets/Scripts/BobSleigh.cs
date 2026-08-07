using UnityEngine;

[DisallowMultipleComponent]
public sealed class BobSleigh : TrackVehicle
{
    #region Inspector

    [Header("Bobsleigh")]
    [SerializeField, Min(0f)] private float hardImpactThreshold = 5f;
    [SerializeField, Range(0f, 1f)] private float impactSpeedRetention = 0.4f;
    [SerializeField] private ParticleSystem snowTrail;
    [SerializeField] private AudioClip hardImpactClip;

    [Header("Bobsleigh Safety")]
    [SerializeField, Min(0f)] private float minimumSnowTrailSpeed = 1f;

    #endregion

    #region Unity Lifecycle

    protected override void OnValidate()
    {
        base.OnValidate();

        hardImpactThreshold =
            Mathf.Max(
                0f,
                hardImpactThreshold);

        impactSpeedRetention =
            Mathf.Clamp01(
                impactSpeedRetention);

        minimumSnowTrailSpeed =
            Mathf.Max(
                0f,
                minimumSnowTrailSpeed);
    }

    protected override void OnDestroy()
    {
        snowTrail = null;
        hardImpactClip = null;

        base.OnDestroy();
    }

    #endregion

    #region Overrides

    protected override void OnDrivingStarted()
    {
        UpdateSnowTrail();
    }

    protected override void OnDrivingPhysicsUpdated()
    {
        UpdateSnowTrail();
    }

    protected override void OnAirbornePhysicsUpdated()
    {
        StopSnowTrail();
    }

    protected override void OnVehicleJumped()
    {
        StopSnowTrail();
    }

    protected override void OnVehicleStopped()
    {
        StopSnowTrail();
    }

    protected override void OnTrackCompleted()
    {
        StopSnowTrail();
    }

    protected override void OnVehicleCollision(
        Collision collision)
    {
        if (collision == null ||
            VehicleRigidbody == null ||
            collision.relativeVelocity.magnitude <
                hardImpactThreshold)
        {
            return;
        }

        Vector3 velocity =
            VehicleRigidbody.linearVelocity;

        velocity.x *=
            impactSpeedRetention;

        velocity.z *=
            impactSpeedRetention;

        VehicleRigidbody.linearVelocity =
            velocity;

        PlayOneShot(
            hardImpactClip);
    }

    #endregion

    #region Presentation

    private void UpdateSnowTrail()
    {
        if (snowTrail == null)
            return;

        bool shouldPlay =
            IsGrounded &&
            CurrentSpeed >=
                minimumSnowTrailSpeed;

        if (shouldPlay)
        {
            if (!snowTrail.isPlaying)
            {
                snowTrail.Play();
            }
        }
        else
        {
            StopSnowTrail();
        }
    }

    private void StopSnowTrail()
    {
        snowTrail?.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting);
    }

    #endregion
}
