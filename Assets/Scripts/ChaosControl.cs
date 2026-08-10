using System.Collections;
using UnityEngine;

public class ChaosControl : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float teleportDistance = 12f;

    [SerializeField, Min(0f)]
    private float cooldown = 1.5f;

    [SerializeField, Min(0f)]
    private float teleportDelay = 0.1f;

    [SerializeField]
    private LayerMask obstacleLayers = ~0;

    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip chaosControlClip;

    [SerializeField]
    private GameObject teleportEffect;

    private bool isUsingChaosControl;
    private float nextUseTime;

    private void Awake()
    {
        body ??=
            GetComponentInParent<Rigidbody>();

        animator ??=
            GetComponentInChildren<Animator>();

        audioSource ??=
            GetComponentInParent<AudioSource>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            TryUseChaosControl();
        }
    }

    public bool TryUseChaosControl()
    {
        if (!CanUseChaosControl())
        {
            return false;
        }

        StartCoroutine(
            ChaosControlRoutine());

        return true;
    }

    private bool CanUseChaosControl()
    {
        if (isUsingChaosControl)
        {
            return false;
        }

        if (Time.time < nextUseTime)
        {
            return false;
        }

        if (body == null)
        {
            return false;
        }

        return true;
    }

    private IEnumerator ChaosControlRoutine()
    {
        isUsingChaosControl =
            true;

        Vector3 destination =
            FindTeleportDestination();

        PlayPresentation();

        body.linearVelocity =
            Vector3.zero;

        if (teleportDelay > 0f)
        {
            yield return
                new WaitForSeconds(
                    teleportDelay);
        }

        body.position =
            destination;

        body.linearVelocity =
            Vector3.zero;

        body.angularVelocity =
            Vector3.zero;

        Physics.SyncTransforms();

        SpawnEffect(
            destination);

        nextUseTime =
            Time.time +
            cooldown;

        isUsingChaosControl =
            false;
    }

    private Vector3 FindTeleportDestination()
    {
        Vector3 origin =
            body.position;

        Vector3 direction =
            body.transform.forward;

        direction.y =
            0f;

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            direction =
                Vector3.forward;
        }

        direction.Normalize();

        if (Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                teleportDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return
                origin +
                direction *
                Mathf.Max(
                    0f,
                    hit.distance - 0.5f);
        }

        return
            origin +
            direction *
                teleportDistance;
    }

    private void PlayPresentation()
    {
        if (animator != null &&
            animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(
                "Chaos Control");
        }

        if (audioSource != null &&
            chaosControlClip != null)
        {
            audioSource.PlayOneShot(
                chaosControlClip);
        }
    }

    private void SpawnEffect(
        Vector3 position)
    {
        if (teleportEffect == null)
        {
            return;
        }

        Instantiate(
            teleportEffect,
            position,
            Quaternion.identity);
    }
}