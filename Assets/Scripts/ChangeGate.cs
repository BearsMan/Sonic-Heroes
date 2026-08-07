using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class ChangeGate : MonoBehaviour
{
    [Header("Gate Type")]
    [SerializeField] private CHARACTERTYPES targetLeader = CHARACTERTYPES.Speed;

    [Header("Gate Visuals")]
    [SerializeField] private GameObject speedVisual;
    [SerializeField] private GameObject flyVisual;
    [SerializeField] private GameObject powerVisual;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableAfterUse;

    [Header("Character Swaps")]
    [SerializeField] private CharacterSwitch characterSwitch;

    private bool hasActivated;
    public event Action<CHARACTERTYPES> GateActivated;

    private void Awake()
    {
        if (characterSwitch == null)
        {
            characterSwitch =
                UnityEngine.Object.FindAnyObjectByType<CharacterSwitch>();
        }

        ConfigureVisuals();
        ValidateConfiguration();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasActivated && disableAfterUse)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (characterSwitch == null)
        {
            characterSwitch =
                other.GetComponentInParent<CharacterSwitch>();
        }

        if (characterSwitch == null)
        {
            Debug.LogWarning(
                "ChangeGate could not find CharacterSwitch.",
                this);

            return;
        }

        if (!characterSwitch.CanSwitch)
            return;

        if (!characterSwitch.SetLeader(targetLeader))
            return;

        GateActivated?.Invoke(targetLeader);

        hasActivated = true;

        if (disableAfterUse)
            gameObject.SetActive(false);
    }

    private void ConfigureVisuals()
    {
        SetVisualActive(
            speedVisual,
            targetLeader == CHARACTERTYPES.Speed);

        SetVisualActive(
            flyVisual,
            targetLeader == CHARACTERTYPES.Fly);

        SetVisualActive(
            powerVisual,
            targetLeader == CHARACTERTYPES.Power);
    }

    private static void SetVisualActive(
        GameObject visual,
        bool active)
    {
        if (visual != null)
            visual.SetActive(active);
    }

    private void ValidateConfiguration()
    {
        if (!IsSupportedLeader(targetLeader))
        {
            Debug.LogError(
                $"ChangeGate has unsupported leader type: {targetLeader}.",
                this);
        }

        if (speedVisual == null &&
            flyVisual == null &&
            powerVisual == null)
        {
            Debug.LogWarning(
                "ChangeGate has no visual objects assigned.",
                this);
        }

        Collider gateCollider =
            GetComponent<Collider>();

        if (gateCollider != null &&
            !gateCollider.isTrigger)
        {
            Debug.LogWarning(
                "ChangeGate Collider should have Is Trigger enabled.",
                gateCollider);
        }
    }

    private static bool IsSupportedLeader(
        CHARACTERTYPES characterType)
    {
        return
            characterType == CHARACTERTYPES.Speed ||
            characterType == CHARACTERTYPES.Fly ||
            characterType == CHARACTERTYPES.Power;
    }

    private void OnValidate()
    {
        ConfigureVisuals();
    }
}