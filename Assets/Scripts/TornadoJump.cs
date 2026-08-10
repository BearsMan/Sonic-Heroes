using System;
using UnityEngine;

public class TornadoJump : MonoBehaviour
{
    #region Events

    public event Action<TornadoJump> PoleActionStarted;
    public event Action<TornadoJump> PoleActionFinished;

    #endregion

    #region Runtime References

    private UltimatePlayerMovement movement;
    private TornadoPole activePole;

    #endregion

    #region Runtime State

    private bool initialized;
    private bool performingPoleAction;
    private bool shuttingDown;

    #endregion

    #region Properties

    public bool IsInitialized =>
        initialized;

    public bool IsPerformingPoleAction =>
        performingPoleAction;

    public TornadoPole ActivePole =>
        activePole;

    public bool CanUsePole =>
        initialized &&
        !performingPoleAction &&
        !shuttingDown &&
        isActiveAndEnabled &&
        gameObject.activeInHierarchy &&
        movement != null &&
        movement.isActiveAndEnabled &&
        movement.gameObject.activeInHierarchy;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!initialized &&
            !shuttingDown)
        {
            Initialize();
        }
    }

    private void OnDisable()
    {
        if (shuttingDown)
        {
            return;
        }

        CancelPoleAction();
    }

    private void OnDestroy()
    {
        shuttingDown =
            true;

        performingPoleAction =
            false;

        activePole =
            null;

        movement =
            null;

        PoleActionStarted =
            null;

        PoleActionFinished =
            null;

        initialized =
            false;
    }

    #endregion

    #region Initialization

    private bool Initialize()
    {
        if (initialized)
        {
            return true;
        }

        if (shuttingDown)
        {
            return false;
        }

        movement =
            GetComponent<UltimatePlayerMovement>();

        movement ??=
            GetComponentInParent<UltimatePlayerMovement>();

        movement ??=
            GetComponentInChildren<UltimatePlayerMovement>(
                includeInactive: true);

        if (movement == null)
        {
            initialized =
                false;

            return false;
        }

        initialized =
            true;

        return true;
    }

    #endregion

    #region Pole Action

    public bool BeginPoleAction(
        TornadoPole pole)
    {
        if (pole == null)
        {
            return false;
        }

        if (!initialized &&
            !Initialize())
        {
            return false;
        }

        if (!CanUsePole)
        {
            return false;
        }

        activePole =
            pole;

        performingPoleAction =
            true;

        PoleActionStarted?.Invoke(
            this);

        return true;
    }

    public bool TransferToPole()
    {
        if (!ValidateActivePoleAction())
        {
            CancelPoleAction();

            return false;
        }

        return true;
    }

    public void FinishPoleAction()
    {
        if (!performingPoleAction &&
            activePole == null)
        {
            return;
        }

        performingPoleAction =
            false;

        activePole =
            null;

        PoleActionFinished?.Invoke(
            this);
    }

    public void CancelPoleAction()
    {
        if (!performingPoleAction &&
            activePole == null)
        {
            return;
        }

        performingPoleAction =
            false;

        activePole =
            null;

        PoleActionFinished?.Invoke(
            this);
    }

    #endregion

    #region Validation

    private bool ValidateActivePoleAction()
    {
        if (!initialized ||
            shuttingDown ||
            !performingPoleAction)
        {
            return false;
        }

        if (movement == null ||
            !movement.isActiveAndEnabled ||
            !movement.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (activePole == null ||
            !activePole.isActiveAndEnabled ||
            !activePole.gameObject.activeInHierarchy)
        {
            return false;
        }

        return true;
    }

    #endregion
}