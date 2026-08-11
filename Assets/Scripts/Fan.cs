using UnityEngine;

public class Fan : MonoBehaviour
{
    #region Updraft

    [Header("Updraft")]
    [SerializeField]
    private Collider updraftVolume;

    [SerializeField]
    private bool activateOnStart = true;

    [SerializeField]
    private bool forceTrigger = true;

    #endregion

    #region Fan Visuals

    [Header("Fan Visuals")]
    [SerializeField]
    private Transform fanBlades;

    [SerializeField]
    private Vector3 rotationAxis = Vector3.up;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 360f;

    #endregion

    #region Effects

    [Header("Effects")]
    [SerializeField]
    private ParticleSystem windEffect;

    [SerializeField]
    private AudioSource fanAudio;

    #endregion

    #region Runtime

    private bool active;

    #endregion

    #region Public Properties

    public bool IsActive =>
        active;

    public Collider UpdraftVolume =>
        updraftVolume;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
        ConfigureUpdraft();

        SetActive(
            activateOnStart);
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        RotateFan();
    }

    private void OnEnable()
    {
        if (!active)
        {
            return;
        }

        SetEffectsActive(
            true);
    }

    private void OnDisable()
    {
        SetEffectsActive(
            false);
    }

    #endregion

    #region Setup

    private void CacheReferences()
    {
        if (updraftVolume == null)
        {
            updraftVolume =
                GetComponent<Collider>();
        }
    }

    private void ConfigureUpdraft()
    {
        if (updraftVolume == null)
        {
            return;
        }

        if (forceTrigger)
        {
            updraftVolume.isTrigger =
                true;
        }
    }

    #endregion

    #region Activation

    public void Activate()
    {
        SetActive(
            true);
    }

    public void Deactivate()
    {
        SetActive(
            false);
    }

    public void SetActive(
        bool value)
    {
        active =
            value;

        if (updraftVolume != null)
        {
            updraftVolume.enabled =
                active;
        }

        SetEffectsActive(
            active);
    }

    #endregion

    #region Rotation

    private void RotateFan()
    {
        if (fanBlades == null)
        {
            return;
        }

        if (!IsFinite(
            rotationAxis))
        {
            return;
        }

        if (rotationAxis.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        float rotationAmount =
            rotationSpeed *
            Time.deltaTime;

        if (!float.IsFinite(
            rotationAmount))
        {
            return;
        }

        fanBlades.Rotate(
            rotationAxis.normalized,
            rotationAmount,
            Space.Self);
    }

    #endregion

    #region Effects

    private void SetEffectsActive(
        bool value)
    {
        if (windEffect != null)
        {
            if (value)
            {
                if (!windEffect.isPlaying)
                {
                    windEffect.Play();
                }
            }
            else if (windEffect.isPlaying)
            {
                windEffect.Stop();
            }
        }

        if (fanAudio != null)
        {
            if (value)
            {
                if (!fanAudio.isPlaying)
                {
                    fanAudio.Play();
                }
            }
            else if (fanAudio.isPlaying)
            {
                fanAudio.Stop();
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

    #endregion
}