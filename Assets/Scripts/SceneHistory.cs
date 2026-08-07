using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHistory
{
    public static string PreviousSceneName { get; private set; }

    public static void LoadScene(string sceneName)
    {
        PreviousSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(sceneName);
    }

    public static void LoadPreviousScene()
    {
        if (string.IsNullOrEmpty(PreviousSceneName))
        {
            Debug.LogWarning("No previous scene has been recorded.");
            return;
        }

        SceneManager.LoadScene(PreviousSceneName);
    }
}