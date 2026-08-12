using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AlligatorChaseTrigger : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField]
    private AlligatorEnemy alligator;

    [SerializeField]
    private Collider triggerVolume;

    #endregion

    #region Settings

    [Header("Settings")]
    [SerializeField]
    private bool activateOnce = true;

    [SerializeField]
    private bool disableAfterActivation = true;

    #endregion

    #region Runtime

    private bool activated;
    private bool initialized;

    #endregion

    #region Properties

    public bool IsActivated => activated;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
        ConfigureTrigger();

        initialized = ValidateReferences();
    }

    private void OnEnable()
    {
        if (triggerVolume == null)
        {
            return;
        }

        if (!activated)
        {
            triggerVolume.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanActivate(other))
        {
            return;
        }

        Activate();
    }

    private void OnDestroy()
    {
        initialized = false;
    }

    #endregion

    #region Activation

    public void Activate()
    {
        if (!initialized ||
            alligator == null)
        {
            return;
        }

        if (activateOnce &&
            activated)
        {
            return;
        }

        activated = true;

        alligator.StartChase();

        if (disableAfterActivation &&
            triggerVolume != null)
        {
            triggerVolume.enabled = false;
        }
    }

    public void ResetTrigger()
    {
        activated = false;

        if (triggerVolume != null)
        {
            triggerVolume.enabled = true;
        }

        alligator?.ResetChase();
    }

    #endregion

    #region Detection

    private bool CanActivate(Collider other)
    {
        if (!initialized ||
            other == null ||
            alligator == null)
        {
            return false;
        }

        if (activateOnce &&
            activated)
        {
            return false;
        }

        UltimatePlayerMovement movement =
            other.GetComponentInParent<UltimatePlayerMovement>();

        if (movement == null)
        {
            return false;
        }

        if (!movement.isActiveAndEnabled ||
            !movement.gameObject.activeInHierarchy)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Setup

    private void CacheReferences()
    {
        if (triggerVolume == null)
        {
            triggerVolume = GetComponent<Collider>();
        }
    }

    private void ConfigureTrigger()
    {
        if (triggerVolume == null)
        {
            return;
        }

        triggerVolume.isTrigger = true;
    }

    private bool ValidateReferences()
    {
        if (triggerVolume == null)
        {
            return false;
        }

        if (alligator == null)
        {
            return false;
        }

        return true;
    }

    #endregion
}