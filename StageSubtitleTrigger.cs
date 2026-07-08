using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StageSubtitleTrigger : MonoBehaviour
{
    public SubtitleSystem subtitleSystem;

    public SonicHeroesTeam team;
    public StageID stageID;
    public SubtitleEventID eventID;

    public bool triggerOnce = true;

    private bool hasTriggered = true;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (subtitleSystem == null)
        {
            Debug.LogWarning("No SubtitleSystem assigned.");
            return;
        }

        subtitleSystem.PlayStageEvent(team, stageID, eventID);

        hasTriggered = true;
    }
}