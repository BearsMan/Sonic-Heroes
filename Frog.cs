using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Health))]
public class Frog : MonoBehaviour
{
    public Transform player;
    public Health frogHealth;
    public NavMeshAgent agent;
    public Vector3 origin;
    public float croakTimer = 0f;
    public AudioClip croakSoundTime = null;
    private Animator anim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        origin = transform.position;
        player = GameObject.FindWithTag("Player").transform;
        anim = GetComponent<Animator>();
        anim.SetBool("Croak", true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayCroakSound()
    {
        if (croakSoundTime != null)
        {
            player.GetComponent("Croak");
        }
    }
}