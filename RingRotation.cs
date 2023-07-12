using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class RingRotation : MonoBehaviour
{
    public float startSpeed = 40;
    public float endSpeed = 10;
    public int degrees = 0;
    public int rotationDegrees = 0;
    public bool justAdded;
    public Rigidbody RB;
    public TMP_Text LevelAdd;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Spin());
    }

    // Update is called once per frame
    void Update()
    {

    }
    IEnumerator Spin()
    {
        LevelAdd.text = GameInstance.goalring.ToString();
        RB.angularVelocity = new Vector3(0, startSpeed, 0);
        while (rotationDegrees < 10)
        {

            if (Mathf.Abs(transform.rotation.y) > 0.9f && justAdded == false)
            {
                rotationDegrees += 1;
                justAdded = true;
                RB.angularVelocity = new Vector3(0, RB.angularVelocity.y - 1, 0);

            }

            if (Mathf.Abs(transform.rotation.y) < 0.9f)
            {
                justAdded = false;
            }

            yield return new WaitForSeconds(Time.deltaTime);



        }
        RB.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.Euler(0, 180, 0);
        GameInstance.goalring += 1;
        LevelAdd.text = GameInstance.goalring.ToString();


    }

}
