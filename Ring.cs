using UnityEngine;
public class Ring : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private AudioClip clip;
    [SerializeField] private Sprite ringPickUp;
    [SerializeField] private Collider physicalCollider;
    [SerializeField, Min(0f)] private float teamBlastGaugeAmount = 2f;

    [Header("Light Speed Dash")]
    [SerializeField] private bool canBeDashedThrough = true;
    [SerializeField, Min(0.1f)] private float chainRange = 5f;

    [Header("Phase")]
    [SerializeField, Min(0f)] private float phaseRecoveryTime = 1f;

    [Header("Debug")]
    [SerializeField] private bool logPickups;

    private GameObject audioObjectPrefab;
    private bool isPhased;
    private bool isCollected;

    public bool CanBeDashedThrough =>
        canBeDashedThrough &&
        !isCollected &&
        enabled &&
        gameObject.activeInHierarchy;

    public float ChainRange => chainRange;

    public Vector3 DashPosition =>
        physicalCollider != null
            ? physicalCollider.bounds.center
            : transform.position;

    private void Awake()
    {
        ResolveReferences();
        LoadAudioObject();
    }

    private void ResolveReferences()
    {
        if (physicalCollider == null)
            physicalCollider = GetComponent<Collider>();
        if (physicalCollider == null)
            physicalCollider = GetComponentInChildren<Collider>();
    }

    private void LoadAudioObject()
    {
        audioObjectPrefab = Resources.Load<GameObject>("Audio Object");
        if (audioObjectPrefab == null)
        {
            Debug.LogWarning(
                "Ring could not find Resources/Audio Object.",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || isPhased || other == null)
            return;
        TryCollect(other);
    }

    public bool TryCollect(Collider collector)
    {
        if (isCollected || isPhased || collector == null)
            return false;
        UltimatePlayerMovement player =
            collector.GetComponentInParent<UltimatePlayerMovement>();
        FollowerNavigation follower =
            collector.GetComponentInParent<FollowerNavigation>();
        if (player == null && follower == null)
            return false;
        CharacterType characterType =
            collector.GetComponentInParent<CharacterType>();
        if (characterType == null)
        {
            Debug.LogWarning(
                $"Ring could not find CharacterType on {collector.name}.",
                this);
            return false;
        }
        Collect(characterType);
        return true;
    }

    private void Collect(CharacterType characterType)
    {
        if (isCollected || characterType == null)
            return;
        isCollected = true;
        if (physicalCollider != null)
            physicalCollider.enabled = false;
        PlayPickupSound();
        GameInstance.AddRings(characterType.type);
        TeamBlast teamBlast = Object.FindAnyObjectByType<TeamBlast>();
        if (teamBlast != null)
            teamBlast.AddGauge(teamBlastGaugeAmount);
        HUD hud = Object.FindAnyObjectByType<HUD>();
        if (hud != null && ringPickUp != null)
            hud.ShowPickUp(ringPickUp);
        if (logPickups)
        {
            Debug.Log(
                $"Ring collected. Rings: {GameInstance.currentRings}",
                this);
        }
        Destroy(gameObject);
    }

    private void PlayPickupSound()
    {
        if (audioObjectPrefab == null || clip == null)
            return;
        GameObject audioObject = Instantiate(
            audioObjectPrefab,
            transform.position,
            Quaternion.identity);
        if (audioObject.TryGetComponent(out AudioObject audio))
            audio.Setup(clip, transform);
        else
            Destroy(audioObject);
    }

    public void StartPhase()
    {
        if (isCollected)
            return;
        ResolveReferences();
        CancelInvoke(nameof(EndPhase));
        isPhased = true;
        if (physicalCollider != null)
            physicalCollider.isTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isPhased || other == null)
            return;
        UltimatePlayerMovement player =
            other.GetComponentInParent<UltimatePlayerMovement>();
        if (player == null)
            return;
        if (physicalCollider != null)
            physicalCollider.isTrigger = false;
        CancelInvoke(nameof(EndPhase));
        Invoke(nameof(EndPhase), phaseRecoveryTime);
    }

    private void EndPhase()
    {
        isPhased = false;
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void OnValidate()
    {
        chainRange = Mathf.Max(0.1f, chainRange);
        teamBlastGaugeAmount = Mathf.Max(0f, teamBlastGaugeAmount);
        phaseRecoveryTime = Mathf.Max(0f, phaseRecoveryTime);
        ResolveReferences();
    }
}