using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Speed Formation aerial tornado attack.
/// Attach this to the player/team root that owns UltimatePlayerMovement.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TornadoJump : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackCenter;
    [SerializeField] private ParticleSystem tornadoEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tornadoSound;

    [Header("Input")]
    [SerializeField] private KeyCode attackKey = KeyCode.B;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float upwardSpeed = 8f;
    [SerializeField] private bool preserveGreaterUpwardSpeed = true;
    [SerializeField, Min(0.01f)] private float attackDuration = 0.55f;
    [SerializeField, Min(0f)] private float recoveryTime = 0.1f;

    [Header("Attack")]
    [SerializeField, Min(0.1f)] private float attackRadius = 2.25f;
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField]
    private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Collide;

    [Header("Animation")]
    [SerializeField] private string tornadoTrigger = "Tornado Jump";
    [SerializeField] private string tornadoBool = "TornadoJump";

    [Header("Debug")]
    [SerializeField] private bool drawAttackRadius = true;

    private readonly HashSet<GameObject> hitObjects = new();

    private Rigidbody body;
    private Coroutine attackRoutine;
    private bool usedThisJump;
    private bool isPerforming;
    private bool transferredToPole;

    public bool IsPerforming => isPerforming;
    public bool UsedThisJump => usedThisJump;

    private Vector3 AttackPosition =>
        attackCenter != null ? attackCenter.position : transform.position;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        if (movement == null)
            movement = GetComponent<UltimatePlayerMovement>();

        if (movement == null)
            movement = GetComponentInParent<UltimatePlayerMovement>();

        if (actionController == null)
            actionController = GetComponent<TeamActionController>();

        if (actionController == null)
            actionController = GetComponentInParent<TeamActionController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (movement == null)
            return;

        if (movement.isGrounded)
        {
            usedThisJump = false;

            if (isPerforming && !transferredToPole)
                FinishAttack();

            return;
        }

        if (Input.GetKeyDown(attackKey))
            TryStartTornadoJump();
    }

    public bool TryStartTornadoJump()
    {
        if (movement == null || body == null)
            return false;

        if (movement.isGrounded || usedThisJump || isPerforming)
            return false;

        if (actionController != null)
        {
            bool accepted = actionController.TryBeginAction(
                TeamActionController.TeamAction.TornadoJump,
                TeamActionController.TeamFormation.Speed,
                mustBeGrounded: false,
                mustBeAirborne: true,
                surrenderMovementControl: false);

            if (!accepted)
                return false;
        }

        usedThisJump = true;
        isPerforming = true;
        transferredToPole = false;
        hitObjects.Clear();

        ApplyUpwardLift();
        BeginPresentation();

        attackRoutine = StartCoroutine(TornadoRoutine());

        return true;
    }

    private void ApplyUpwardLift()
    {
        Vector3 velocity = body.linearVelocity;

        if (preserveGreaterUpwardSpeed)
            velocity.y = Mathf.Max(velocity.y, upwardSpeed);
        else
            velocity.y = upwardSpeed;

        body.linearVelocity = velocity;
    }

    private IEnumerator TornadoRoutine()
    {
        float elapsed = 0f;

        while (elapsed < attackDuration)
        {
            DetectTargets();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (recoveryTime > 0f)
            yield return new WaitForSeconds(recoveryTime);

        FinishAttack();
    }

    private void DetectTargets()
    {
        Collider[] hits = Physics.OverlapSphere(
            AttackPosition,
            attackRadius,
            targetLayers,
            triggerInteraction);

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            GameObject targetRoot =
                hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.gameObject
                    : hit.transform.root.gameObject;

            if (!hitObjects.Add(targetRoot))
                continue;

            TornadoPole pole = hit.GetComponentInParent<TornadoPole>();

            if (pole != null && pole.TryActivate(gameObject, this))
            {
                TransferToPole();
                return;
            }

            targetRoot.SendMessage(
                "OnTornadoHit",
                gameObject,
                SendMessageOptions.DontRequireReceiver);

            if (damage > 0)
            {
                targetRoot.SendMessage(
                    "TakeDamage",
                    damage,
                    SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void BeginPresentation()
    {
        if (animator != null)
        {
            if (!string.IsNullOrWhiteSpace(tornadoTrigger))
                animator.SetTrigger(tornadoTrigger);

            if (!string.IsNullOrWhiteSpace(tornadoBool))
                animator.SetBool(tornadoBool, true);
        }

        if (tornadoEffect != null)
            tornadoEffect.Play();

        if (audioSource != null && tornadoSound != null)
            audioSource.PlayOneShot(tornadoSound);
    }

    private void EndPresentation()
    {
        if (animator != null &&
            !string.IsNullOrWhiteSpace(tornadoBool))
        {
            animator.SetBool(tornadoBool, false);
        }

        if (tornadoEffect != null)
        {
            tornadoEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void TransferToPole()
    {
        if (!isPerforming)
            return;

        transferredToPole = true;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isPerforming = false;
        EndPresentation();
    }

    public void FinishAttack()
    {
        if (transferredToPole)
            return;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isPerforming = false;
        EndPresentation();

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.TornadoJump)
        {
            actionController.EndAction(
                restoreMovementControl: false);
        }
    }

    public void FinishPoleAction()
    {
        transferredToPole = false;
        isPerforming = false;
        EndPresentation();

        if (actionController != null &&
            actionController.CurrentAction ==
            TeamActionController.TeamAction.TornadoJump)
        {
            actionController.EndAction();
        }
        else
        {
            movement.EnableMovement();
        }
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        EndPresentation();

        if (!transferredToPole)
            FinishAttack();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAttackRadius)
            return;

        Vector3 center =
            attackCenter != null
                ? attackCenter.position
                : transform.position;

        Gizmos.DrawWireSphere(center, attackRadius);
    }
}