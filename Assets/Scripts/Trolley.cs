using UnityEngine;

[DisallowMultipleComponent]
public sealed class Trolley : TrackVehicle
{
    #region Inspector

    [Header("Trolley")]
    [SerializeField] private AudioClip railLoop;
    [SerializeField] private AudioClip railImpactClip;
    [SerializeField, Min(0f)] private float railImpactThreshold = 4f;
    [SerializeField, Min(0f)] private float minimumSparkSpeed = 1f;
    [SerializeField] private ParticleSystem railSparks;

    #endregion

    #region Unity Lifecycle

    protected override void OnValidate()
    {
        base.OnValidate();

        railImpactThreshold =
            Mathf.Max(
                0f,
                railImpactThreshold);

        minimumSparkSpeed =
            Mathf.Max(
                0f,
                minimumSparkSpeed);
    }

    protected override void OnDestroy()
    {
        railLoop = null;
        railImpactClip = null;
        railSparks = null;

        base.OnDestroy();
    }

    #endregion

    #region Overrides

    protected override void OnDrivingStarted()
    {
        StartRailAudio();
        UpdateRailSparks();
    }

    protected override void OnDrivingPhysicsUpdated()
    {
        UpdateRailSparks();
    }

    protected override void OnAirbornePhysicsUpdated()
    {
        StopRailSparks();
    }

    protected override void OnVehicleJumped()
    {
        StopRailSparks();
    }

    protected override void OnVehicleStopped()
    {
        StopRailPresentation();
    }

    protected override void OnTrackCompleted()
    {
        StopRailPresentation();
    }

    protected override void OnVehicleCollision(
        Collision collision)
    {
        if (collision == null ||
            collision.relativeVelocity.magnitude <
                railImpactThreshold)
        {
            return;
        }

        railSparks?.Play();

        PlayOneShot(
            railImpactClip);
    }

    #endregion

    #region Presentation

    private void StartRailAudio()
    {
        if (VehicleAudioSource == null ||
            railLoop == null)
        {
            return;
        }

        VehicleAudioSource.clip =
            railLoop;

        VehicleAudioSource.loop =
            true;

        VehicleAudioSource.Play();
    }

    private void UpdateRailSparks()
    {
        if (railSparks == null)
            return;

        bool shouldPlay =
            IsGrounded &&
            CurrentSpeed >=
                minimumSparkSpeed;

        if (shouldPlay)
        {
            if (!railSparks.isPlaying)
            {
                railSparks.Play();
            }
        }
        else
        {
            StopRailSparks();
        }
    }

    private void StopRailPresentation()
    {
        StopRailSparks();

        if (VehicleAudioSource != null &&
            VehicleAudioSource.clip ==
                railLoop)
        {
            VehicleAudioSource.Stop();
        }
    }

    private void StopRailSparks()
    {
        railSparks?.Stop(
            true,
            ParticleSystemStopBehavior.StopEmitting);
    }

    #endregion
}
