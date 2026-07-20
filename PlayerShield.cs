using UnityEngine;

public class PlayerShield : MonoBehaviour
{
    [SerializeField] private GameObject shieldVisual;

    public bool HasShield { get; private set; }

    public void ActivateShield()
    {
        HasShield = true;

        if (shieldVisual != null)
        {
            shieldVisual.SetActive(true);
        }

        Debug.Log("Shield Activated");
    }

    public bool AbsorbHit()
    {
        if (!HasShield)
            return false;

        HasShield = false;

        if (shieldVisual != null)
        {
            shieldVisual.SetActive(false);
        }

        Debug.Log("Shield Broken");

        return true;
    }

    public void RemoveShield()
    {
        HasShield = false;

        if (shieldVisual != null)
        {
            shieldVisual.SetActive(false);
        }
    }
}