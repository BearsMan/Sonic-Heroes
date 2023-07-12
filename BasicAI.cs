using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BasicAI : MonoBehaviour
{
    public Transform targetposition;
    public NavMeshAgent agent;
    public enum STATE
    {
        Idle,
        Move,
        Special,
        Attack,
        Dead,
        Inair
    }

    public STATE curState = STATE.Move;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        switch (curState)
        {
            case STATE.Idle:
                IdleState();
                break;
            case STATE.Move:
                MoveState();
                break;
            case STATE.Special:
                SpecialState();
                break;
            case STATE.Attack:
                AttackState();
                break;
            case STATE.Inair:
                InairState();
                break;
            case STATE.Dead:
                DeadState();
                break;


        }
    }

    public void SetState(STATE newState)
    {
        curState = newState;
    }

    #region states
    public void IdleState()
    {

    }

    public void MoveState()
    {
        agent.SetDestination(targetposition.position);

    }
    public void SpecialState()
    {

    }
    public void AttackState()
    {

    }
    public void InairState()
    {

    }
    public void DeadState()
    {

    }




    #endregion
}
