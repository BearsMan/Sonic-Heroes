using UnityEngine;

public class EggPawnAnimator : MonoBehaviour
{
    private EggPawn eggPawn;

    private void Awake()
    {
        eggPawn = GetComponentInParent<EggPawn>();

        if (eggPawn == null)
        {
            Debug.LogError("EggPawnAnimator requires an EggPawn component in a parent object.", this);
        }
    }

    public void DamagePlayer()
    {
        if (eggPawn == null)
        {
            return;
        }

        eggPawn.DamagePlayer();
    }
}