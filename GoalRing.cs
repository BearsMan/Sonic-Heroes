using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalRing : MonoBehaviour
{
    public GameObject LevelEndHUD;
    public string NextLevelName;
    private bool LoadingNextScene = false;
    public AudioClip clip;
    private GameObject source;
    // Start is called before the first frame update
    void Start()
    {
        source = Resources.Load<GameObject>("Audio Object");
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out UltimatePlayerMovement player) && LoadingNextScene == false)
        {
            LoadingNextScene = true;
            GameObject ao = Instantiate(source, transform.position, Quaternion.identity);
            ao.GetComponent<AudioObject>().Setup(clip, transform);
            GameInstance.CreateSave();
            StartCoroutine(EndOfLevel());
        }
    }

    public IEnumerator EndOfLevel()
    {
        
        ScoreSystem sys = FindFirstObjectByType<ScoreSystem>();
        sys.StartEndLevelSequence();
        //Displays Final Level Stats Here
        yield return new WaitForSeconds(3);
        SceneManager.LoadScene(NextLevelName);
    }
}
