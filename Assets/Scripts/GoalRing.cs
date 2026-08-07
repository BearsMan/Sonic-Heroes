using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalRing : MonoBehaviour
{
    public GameObject LevelEndHUD;
    public string NextLevelName;
    public string specialStageSceneName;
    [SerializeField] private bool isSpecialStageGoal = false;
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

            if (!isSpecialStageGoal)
            {
                GameInstance.CreateSave();
            }

            StartCoroutine(EndOfLevel());
        }
    }

    public IEnumerator EndOfLevel()
    {
        if (isSpecialStageGoal)
        {
            // Future celebration:
            // - Play "Special Stage Clear!"
            // - Show Chaos Emerald
            // - Play victory music
            // - Show score bonus

            yield return new WaitForSeconds(2f);

            SpecialStageManager manager = FindAnyObjectByType<SpecialStageManager>();

            if (manager != null)
            {
                manager.CompleteStage();
            }

            yield break;
        }
        ScoreSystem sys = Object.FindAnyObjectByType<ScoreSystem>();

        if (sys != null)
        {
            sys.StartEndLevelSequence();
        }

        // Displays Final Level Stats Here
        yield return new WaitForSeconds(3);

        if (GameInstance.hasSpecialKey)
        {
            GameInstance.returnScene = NextLevelName;
            SceneManager.LoadScene(specialStageSceneName);
        }

        else
        {
            SceneManager.LoadScene(NextLevelName);
        }
    }
}
