using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Level Scores", menuName = "Level Scores", order = 1)]
public class Ranking : ScriptableObject
{
    public int[] teamSonic = new int[4];
    public int[] teamDark = new int[4];
    public int[] teamRose = new int[4];
    public int[] teamChaotix = new int[4];
}
