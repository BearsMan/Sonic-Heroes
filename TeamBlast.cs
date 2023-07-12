using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TeamBlast : MonoBehaviour
{
    public GameObject teamBlastUltimateAttack;
    public AudioClip teamBlast;
    public bool ulimateAttack;
    public bool sonicOverDrive, chaosInferno, flowerFestival, chaotixRecital, superSonicPower; //These are the names of all Ultimate Attacks
    public bool isTeamSonic, isTeamDark, isTeamRose, isTeamChaotix, isTeamSuperSonic;
    public Sprite Heroes_TeamBlastSonic, Heroes_TeamBlastDark, Heroes_TeamBlastRose, Heroes_TeamBlastChaotix, Super_Sonic_Power_Team_Blast;

    // Start is called before the first frame update
    void Start()
    {
        if (Heroes_TeamBlastSonic)
        {
            GetComponent<SpriteRenderer>().sprite = Heroes_TeamBlastSonic;
            Debug.Log("Did Team Sonic's Team Blast Work");
        }
        if (Heroes_TeamBlastDark)
        {
            GetComponent<SpriteRenderer>().sprite = Heroes_TeamBlastDark;
            Debug.Log("Did Team Dark's Team Blast Work");
        }
        if (Heroes_TeamBlastRose)
        {
            GetComponent<SpriteRenderer>().sprite = Heroes_TeamBlastRose;
            Debug.Log("Did Team Rose's Team Blast Work");
        }
        if (Heroes_TeamBlastChaotix)
        {
            GetComponent<SpriteRenderer>().sprite = Heroes_TeamBlastChaotix;
            Debug.Log("Did Team Chaotix's Team Blast Work");
        }
        if (Super_Sonic_Power_Team_Blast)
        {
            GetComponent<SpriteRenderer>().sprite = Super_Sonic_Power_Team_Blast;
            Debug.Log("Did Team Super Sonic's Team Blast Work");
        }

    }

    

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SpecialAttack()
    {
        GetComponent<AudioSource>().Play();
    }

    

    //Team Blast only triggers when power bar is full
    //The power bar resets after it is finished being used
}
