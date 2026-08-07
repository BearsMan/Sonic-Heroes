using UnityEngine;

public class LightSpeedDashRing : MonoBehaviour
{
    [Header("Light Speed Dash")]
    [SerializeField] private bool canBeDashedThrough = true;
    [SerializeField, Min(0.1f)] private float chainRange = 5f;

    public bool CanBeDashedThrough =>
        canBeDashedThrough &&
        enabled &&
        gameObject.activeInHierarchy;

    public float ChainRange => chainRange;

    public Vector3 DashPosition
    {
        get
        {
            Collider ringCollider = GetComponent<Collider>();
            return ringCollider != null
                ? ringCollider.bounds.center
                : transform.position;
        }
    }

    private void OnValidate()
    {
        chainRange = Mathf.Max(0.1f, chainRange);
    }
}