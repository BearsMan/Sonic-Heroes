using UnityEngine;

public class HomingAttack : MonoBehaviour
{
    [Header("State")]
    public bool homingAttackAvailable = true;
    public bool homingAttackUsed = false;

    [Header("References")]
    [SerializeField] private UltimatePlayerMovement movement;
    [SerializeField] private TeamActionController actionController;
    [SerializeField] private Animator anim;

    private void Start()
    {
        if (movement == null)
        {
            movement = GetComponentInParent<UltimatePlayerMovement>();
        }

        if (actionController == null)
        {
            actionController = GetComponentInParent<TeamActionController>();
        }

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (movement == null)
        {
            Debug.LogError("HomingAttack could not find UltimatePlayerMovement.");
        }

        if (actionController == null)
        {
            Debug.LogError("HomingAttack could not find TeamActionController.");
        }

        if (anim == null)
        {
            Debug.LogError("HomingAttack could not find Animator.");
        }
    }

    private void Update()
    {
        if (movement == null)
            return;

        // Reset when the player lands.
        if (movement.IsGrounded)
        {
            homingAttackAvailable = true;
            homingAttackUsed = false;

            if (anim != null)
            {
                anim.SetBool("Spin", false);
                anim.SetBool("Dive Roll", false);
            }

            return;
        }

        // Already used during this jump.
        if (homingAttackUsed)
        {
            return;
        }

        // Start the Homing Attack.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (actionController != null)
            {
                bool accepted = actionController.TryBeginAction(
                    TeamActionController.TeamAction.HomingAttack,
                    TeamActionController.TeamFormation.Speed,
                    mustBeGrounded: false,
                    mustBeAirborne: true);

                if (!accepted)
                {
                    return;
                }
            }

            homingAttackUsed = true;
            homingAttackAvailable = false;

            if (anim != null)
            {
                anim.SetBool("Spin", true);
            }
        }
    }

    public void ResetHomingAttack()
    {
        homingAttackUsed = false;
        homingAttackAvailable = true;

        if (anim != null)
        {
            anim.SetBool("Spin", false);
        }

        if (actionController != null)
        {
            actionController.EndAction();
        }
    }
}