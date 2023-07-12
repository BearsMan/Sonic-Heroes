using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
public class TeamBlastVideos : MonoBehaviour
{
    
    public GameObject teamBlastVideoPlayer;
    public VideoPlayer vplayer;
    public AudioSource stageMusic;
    public AudioSource omochaoDialog;
    // Start is called before the first frame update
    void Start()
    {
        


    }

    private void Update()
    {
        
    }

    [System.Obsolete]
    public void SpecialAttack()
    {
        teamBlastVideoPlayer.SetActive(true);
        vplayer.clip = FindFirstObjectByType<TeamSetup>().CurrentTeam.teamBlast;
        
        
        StartCoroutine(PlayVideo());
    }

    [System.Obsolete]
    public IEnumerator PlayVideo()
    {
        vplayer.Play();
        stageMusic.volume = 0;
        omochaoDialog.volume = 0;
        yield return new WaitForSeconds(1);
        while (vplayer.isPlaying)
        {
            yield return new WaitForEndOfFrame();
        }
        teamBlastVideoPlayer.SetActive(false);
        yield return new WaitForSeconds(1);

        Health[] allEnemies = FindObjectsOfType<Health>();
        Transform player = FindFirstObjectByType<UltimatePlayerMovement>().transform;

        foreach (Health enemy in allEnemies)
        {
            float distance = Vector3.Distance(player.position,enemy.transform.position);
            if (distance<30)
            {
                enemy.TakeDamage(10);
            }
            
        }

        stageMusic.volume = 1;
        omochaoDialog.volume = 1;
    }
}
