using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ScoreSystem : MonoBehaviour
{
    public GameObject Player;
    public GameObject NormalHUD;
    public GameObject scoreSystem;
    public TMP_Text SpeedCharacterScore;
    public TMP_Text FlyingCharacterScore;
    public TMP_Text PowerCharacterScore;
    public TMP_Text TimeScore;
    public TMP_Text RingCountScore;
    public TMP_Text TimeBonusScore; // The faster in time that the player goes the better the grade will be
    public TMP_Text FinalScore;
    public Image FinalGrade; //This displays the actual grade based on the time you have beaten the level
    public static int prospectiveScore;
    public static float finalScore;
    public static int SpeedScore;
    public static int FlyScore;
    public static int PowerScore;
    public Sprite gradeA, gradeB, gradeC, gradeD, gradeE;
    // Start is called before the first frame update
    void Start()
    {
        prospectiveScore = 0;
        finalScore = 0;

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void StartEndLevelSequence()
    {
        NormalHUD.gameObject.SetActive(false);
        scoreSystem.SetActive(true);
        FindFirstObjectByType<UltimatePlayerMovement>().enabled = false;
        StartCoroutine(EndLevelSequence());
    }
    public IEnumerator EndLevelSequence()
    {
        //Display Team Score Per Level
        int SpeedCharScore = GameInstance.speedScore;
        int FlyingCharScore = GameInstance.flyScore;
        int PowerCharScore = GameInstance.powerScore;

        //Time Calculation
        float TimeScoreInt = HUD.timer;
        float SecondScoreint = 0;
        float MinutesScoreint = 0;
        float MilliSecondScoreint = 0;
        while (TimeScoreInt > 1)
        {
            TimeScoreInt -= 1;
            SecondScoreint += 1;
            if (SecondScoreint == 60)
            {
                SecondScoreint -= 60;
                MinutesScoreint += 1;
            }
        }
        MilliSecondScoreint = Mathf.RoundToInt(TimeScoreInt * 100);

        //Time Score Calculation
        int RingCountint = GameInstance.currentRings;
        float TimeBonusint = GameInstance.bonusScore;
        float BonusTimeint = HUD.timer; // The faster in time that the player goes the better the grade will be
        float TotalTimeint = HUD.timer;
        float FinalScoreint = HUD.timer;

        if (TotalTimeint >= 625)
        {
            BonusTimeint = 0;
        }

        else
        {
            float secondCount = 625 - TotalTimeint;
            BonusTimeint = secondCount * 80;
        }

        BonusTimeint = Mathf.RoundToInt(BonusTimeint);
        
        //Displays Main Team Score
        //Display Time Score and Ring Count Scores with Time Bonus Score
        SpeedCharacterScore.text = GameInstance.speedScore.ToString();
        FlyingCharacterScore.text = GameInstance.flyScore.ToString();
        PowerCharacterScore.text = GameInstance.powerScore.ToString();
        TimeScore.text = MinutesScoreint.ToString("00") + ":" + SecondScoreint.ToString("00") + ":" + MilliSecondScoreint.ToString("00");
        RingCountScore.text = GameInstance.currentRings.ToString();
        TimeBonusScore.text = BonusTimeint.ToString();



        
        //1 Second = 100 

        //Display List of Score System
        //1. Lists each character's score
        //2.Adds powerups to character scores
        //3.Displays Time, Rings, TimeBonus
        //4.Adds Rings to TimeBonus, subtracts Time from TimeBonus
        //5.Add character scores, TimeBonus into FinalScore
        //6.Display FinalScore
        //7.Display letter grade

        // Every minute subtracts 1,000 points from a starting timebonusscore of 10,000

        
        finalScore = 0;
        finalScore = SpeedCharScore + FlyingCharScore + PowerCharScore;
        finalScore += 10 * GameInstance.currentRings;
        finalScore += BonusTimeint;

        FinalScore.text = finalScore.ToString();

        //Change Grade Based On Score Requirement Chart
        
        Ranking ranks = Resources.Load<Ranking>("Level Rankings/" + SceneManager.GetActiveScene().name);
        int[] scores = new int[0];



        

        switch (TeamSetup.pc.CurrentTeam.SpeedCharacter.gameObject.name)
        {

            //Displays who is the leader of the selected team
            case "Sonic The Hedgehog":
                scores = ranks.teamSonic;
                break;
            case "Shadow The Hedgehog":
                scores = ranks.teamDark;
                break;
            case "Amy Rose":
                scores = ranks.teamRose;
                break;
            case "Espio the Chameleon":
                scores = ranks.teamChaotix;
                break;
        }
        
        
        if (finalScore > scores[0])
            FinalGrade.sprite = gradeA;

        else if (finalScore > scores[1])
            FinalGrade.sprite = gradeB;

        else if (finalScore > scores[2])
            FinalGrade.sprite = gradeC;

        else if (finalScore > scores[3])
            FinalGrade.sprite = gradeD;

        else
            FinalGrade.sprite = gradeE;

        


        yield return null;
    }
}
