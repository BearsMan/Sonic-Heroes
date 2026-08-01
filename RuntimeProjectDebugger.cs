using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RuntimeProjectDebugger : MonoBehaviour
{
    [Header("Automatic Tracing")]
    [SerializeField]
    private bool traceAllSceneObjects = true;

    [SerializeField]
    private bool includeInactiveObjects = true;

    [SerializeField]
    private bool traceNewlyLoadedScenes = true;

    [Header("Console Monitoring")]
    [SerializeField]
    private bool logExceptions = true;

    [SerializeField]
    private bool logErrors = true;

    [SerializeField]
    private bool logAssertions = true;

    [Header("Validation")]
    [SerializeField]
    private bool reportInactiveObjectsOnStart;

    [SerializeField]
    private bool reportMissingScripts = true;

    [Header("Performance")]
    [SerializeField, Min(1)]
    private int maximumObjectsToTrace = 10000;

    private readonly HashSet<GameObject> tracedObjects = new();

    private static RuntimeProjectDebugger instance;

    public static RuntimeProjectDebugger Instance =>
        instance;

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        Application.logMessageReceived +=
            HandleUnityLog;

        if (traceNewlyLoadedScenes)
        {
            SceneManager.sceneLoaded +=
                HandleSceneLoaded;
        }

        ScanAllLoadedScenes();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        Application.logMessageReceived -=
            HandleUnityLog;

        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

        instance = null;
    }

    [ContextMenu("Scan All Loaded Scenes")]
    public void ScanAllLoadedScenes()
    {
        int tracedCount = 0;

        for (int sceneIndex = 0;
             sceneIndex < SceneManager.sceneCount;
             sceneIndex++)
        {
            Scene scene =
                SceneManager.GetSceneAt(
                    sceneIndex);

            if (!scene.isLoaded)
                continue;

            tracedCount +=
                ScanScene(
                    scene,
                    maximumObjectsToTrace -
                    tracedCount);

            if (tracedCount >=
                maximumObjectsToTrace)
            {
                Debug.LogWarning(
                    $"RuntimeProjectDebugger reached its limit of " +
                    $"{maximumObjectsToTrace} traced objects.",
                    this);

                break;
            }
        }

        Debug.Log(
            $"RuntimeProjectDebugger scan complete. " +
            $"Tracked objects: {tracedObjects.Count}.",
            this);
    }

    private int ScanScene(
        Scene scene,
        int remainingCapacity)
    {
        if (remainingCapacity <= 0)
            return 0;

        int tracedCount = 0;

        GameObject[] rootObjects =
            scene.GetRootGameObjects();

        foreach (GameObject rootObject in rootObjects)
        {
            tracedCount +=
                ScanHierarchy(
                    rootObject,
                    remainingCapacity -
                    tracedCount);

            if (tracedCount >=
                remainingCapacity)
            {
                break;
            }
        }

        return tracedCount;
    }

    private int ScanHierarchy(
        GameObject rootObject,
        int remainingCapacity)
    {
        if (rootObject == null ||
            remainingCapacity <= 0)
        {
            return 0;
        }

        int tracedCount = 0;

        Transform[] transforms =
            rootObject.GetComponentsInChildren<Transform>(
                includeInactiveObjects);

        foreach (Transform currentTransform in transforms)
        {
            if (currentTransform == null)
                continue;

            GameObject currentObject =
                currentTransform.gameObject;

            if (reportMissingScripts)
            {
                ReportMissingScripts(
                    currentObject);
            }

            if (reportInactiveObjectsOnStart &&
                !currentObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"INACTIVE OBJECT: " +
                    $"{GetHierarchyPath(currentTransform)} | " +
                    $"activeSelf={currentObject.activeSelf}",
                    currentObject);
            }

            if (!traceAllSceneObjects)
                continue;

            if (tracedObjects.Contains(currentObject))
            {
                continue;
            }

            if (currentObject.GetComponent<RuntimeDisableTrace>() ==
                null)
            {
                currentObject.AddComponent<RuntimeDisableTrace>();
            }

            tracedObjects.Add(currentObject);

            tracedCount++;

            if (tracedCount >=
                remainingCapacity)
            {
                break;
            }
        }

        return tracedCount;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode)
    {
        if (!traceNewlyLoadedScenes)
            return;

        Debug.Log(
            $"SCENE LOADED: {scene.name} | Mode: {loadMode}",
            this);

        ScanScene(
            scene,
            maximumObjectsToTrace -
            tracedObjects.Count);
    }

    private void HandleUnityLog(
        string message,
        string stackTrace,
        LogType logType)
    {
        bool shouldReport =
            logType switch
            {
                LogType.Exception =>
                    logExceptions,

                LogType.Error =>
                    logErrors,

                LogType.Assert =>
                    logAssertions,

                _ =>
                    false
            };

        if (!shouldReport)
            return;

        System.Diagnostics.Debug.WriteLine(
            $"UNITY {logType}: {message}\n{stackTrace}");
    }

    private static void ReportMissingScripts(
        GameObject targetObject)
    {
        Component[] components =
            targetObject.GetComponents<Component>();

        for (int index = 0;
             index < components.Length;
             index++)
        {
            if (components[index] != null)
                continue;

            Debug.LogError(
                $"MISSING SCRIPT: " +
                $"{GetHierarchyPath(targetObject.transform)} | " +
                $"Component index: {index}",
                targetObject);
        }
    }

    private static string GetHierarchyPath(
        Transform target)
    {
        if (target == null)
            return "<null>";

        string path =
            target.name;

        Transform current =
            target.parent;

        while (current != null)
        {
            path =
                $"{current.name}/{path}";

            current =
                current.parent;
        }

        return path;
    }
}