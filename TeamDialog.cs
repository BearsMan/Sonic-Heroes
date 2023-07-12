using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TeamDialog : MonoBehaviour
{
    public int teamIndex = 0;
    public AudioClip dialog1;
    public AudioClip dialog2;
    public AudioClip dialog3;
    public AudioClip omochaodialog;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void TeamSelected()
    {
        StartCoroutine(TeamSelectedCoroutine());
    }
    public IEnumerator TeamSelectedCoroutine()
    {

        AudioSource asource = GetComponent<AudioSource>();
        asource.clip = dialog1;
        asource.Play();
        while (asource.isPlaying)
        {
            yield return new WaitForSeconds(Time.deltaTime);
        }
        asource.clip = dialog2;
        asource.Play();
        while (asource.isPlaying)
        {
            yield return new WaitForSeconds(Time.deltaTime);
        }
        asource.clip = dialog3;
        asource.Play();
        while (asource.isPlaying)
        {
            yield return new WaitForSeconds(Time.deltaTime);
        }
    }
}

//0 = Team Sonic
//1 = Team Dark
//2 = Team Rose
//3 = Team Chaotix
