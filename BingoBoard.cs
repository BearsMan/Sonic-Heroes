using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BingoBoard : MonoBehaviour
{
    public List<GameObject> boardNumbers;
    public GameObject bingoBoard;
    public bool addRingsFromBoard;
    public bool removeRingsFromBoard;
    public bool addRingBonus;
    public bool showRings = true;
    public int ringCount = 0;
    public float ringBonus = 1f;
    public float timer = 0;
    public float maxRings = 0;
    // Start is called before the first frame update
    void Start()
    {
        boardNumbers = new List<GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        updateboard();
    }

    private void OnTriggerEnter(Collider other)
    {
        
    }
    void updateboard()
    {
        int i = 0;
        while (i < boardNumbers.Count)
        {
            if (boardNumbers[i].GetComponent<BingoBoardNumbers>().triggered == true)
            {
                boardNumbers[i].GetComponent<BingoBoardNumbers>().boardNumberMesh.SetActive(true);
            }
            else
            {
                boardNumbers[i].GetComponent<BingoBoardNumbers>().boardNumberMesh.SetActive(false);
            }
            i += 1;
        }
    }
    

    
}
