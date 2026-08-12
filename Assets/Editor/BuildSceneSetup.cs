using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BuildSceneSetup
{
    #region Constants

    private const string SceneFolder =
        "Assets/Scenes";

    private const string MenuPath =
        "Sonic Heroes/Build/Configure Scene Order";

    private const string MainMenuScene =
        "Main Menu";

    private const string PauseMenuScene =
        "Pause Menu";

    #endregion

    #region Patterns

    private static readonly Regex StagePattern =
        new Regex(
            @"^Stage\s+(\d+)$",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled);

    #endregion

    #region Menu

    [MenuItem(MenuPath)]
    private static void ConfigureSceneOrder()
    {
        List<string> scenePaths =
            FindProjectScenes();

        if (scenePaths.Count == 0)
        {
            Debug.LogWarning(
                $"No scenes were found inside {SceneFolder}.");

            return;
        }

        List<string> orderedScenes =
            BuildOrderedSceneList(
                scenePaths);

        ApplySceneList(
            orderedScenes);

        LogSceneOrder(
            orderedScenes);
    }

    #endregion

    #region Discovery

    private static List<string> FindProjectScenes()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Scene",
                new[]
                {
                    SceneFolder
                });

        HashSet<string> uniquePaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (!IsValidScenePath(path))
            {
                continue;
            }

            uniquePaths.Add(path);
        }

        return uniquePaths.ToList();
    }

    private static bool IsValidScenePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!path.StartsWith(
            SceneFolder,
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.EndsWith(
            ".unity",
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Ordering

    private static List<string> BuildOrderedSceneList(
        List<string> scenePaths)
    {
        List<string> ordered =
            new List<string>(
                scenePaths.Count);

        HashSet<string> added =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        AddNamedScene(
            MainMenuScene,
            scenePaths,
            ordered,
            added);

        AddExistingNonStageScenes(
            scenePaths,
            ordered,
            added);

        AddStages(
            scenePaths,
            ordered,
            added);

        AddNamedScene(
            PauseMenuScene,
            scenePaths,
            ordered,
            added);

        AddRemainingScenes(
            scenePaths,
            ordered,
            added);

        return ordered;
    }

    private static void AddNamedScene(
        string sceneName,
        IEnumerable<string> scenePaths,
        List<string> ordered,
        HashSet<string> added)
    {
        string path =
            scenePaths.FirstOrDefault(
                path =>
                    GetSceneName(path).Equals(
                        sceneName,
                        StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddScene(
            path,
            ordered,
            added);
    }

    private static void AddExistingNonStageScenes(
        IEnumerable<string> availableScenes,
        List<string> ordered,
        HashSet<string> added)
    {
        EditorBuildSettingsScene[] existing =
            EditorBuildSettings.scenes;

        foreach (EditorBuildSettingsScene scene in existing)
        {
            if (scene == null ||
                string.IsNullOrWhiteSpace(
                    scene.path))
            {
                continue;
            }

            if (!availableScenes.Contains(
                scene.path,
                StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            string sceneName =
                GetSceneName(
                    scene.path);

            if (IsStage(sceneName) ||
                sceneName.Equals(
                    MainMenuScene,
                    StringComparison.OrdinalIgnoreCase) ||
                sceneName.Equals(
                    PauseMenuScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddScene(
                scene.path,
                ordered,
                added);
        }
    }

    private static void AddStages(
        IEnumerable<string> scenePaths,
        List<string> ordered,
        HashSet<string> added)
    {
        List<StageScene> stages =
            new List<StageScene>();

        foreach (string path in scenePaths)
        {
            string sceneName =
                GetSceneName(path);

            Match match =
                StagePattern.Match(
                    sceneName);

            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(
                match.Groups[1].Value,
                out int stageNumber))
            {
                continue;
            }

            stages.Add(
                new StageScene(
                    path,
                    stageNumber));
        }

        stages.Sort(
            (left, right) =>
                left.Number.CompareTo(
                    right.Number));

        foreach (StageScene stage in stages)
        {
            AddScene(
                stage.Path,
                ordered,
                added);
        }
    }

    private static void AddRemainingScenes(
        IEnumerable<string> scenePaths,
        List<string> ordered,
        HashSet<string> added)
    {
        List<string> remaining =
            scenePaths
                .Where(
                    path =>
                        !added.Contains(path))
                .OrderBy(
                    GetSceneName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (string path in remaining)
        {
            AddScene(
                path,
                ordered,
                added);
        }
    }

    private static void AddScene(
        string path,
        List<string> ordered,
        HashSet<string> added)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !added.Add(path))
        {
            return;
        }

        ordered.Add(path);
    }

    #endregion

    #region Apply

    private static void ApplySceneList(
        IReadOnlyList<string> orderedScenes)
    {
        EditorBuildSettingsScene[] buildScenes =
            new EditorBuildSettingsScene[
                orderedScenes.Count];

        for (int index = 0;
            index < orderedScenes.Count;
            index++)
        {
            buildScenes[index] =
                new EditorBuildSettingsScene(
                    orderedScenes[index],
                    true);
        }

        EditorBuildSettings.scenes =
            buildScenes;
    }

    #endregion

    #region Helpers

    private static string GetSceneName(
        string path)
    {
        return Path.GetFileNameWithoutExtension(
            path);
    }

    private static bool IsStage(
        string sceneName)
    {
        return !string.IsNullOrWhiteSpace(
                sceneName) &&
            StagePattern.IsMatch(
                sceneName);
    }

    private static void LogSceneOrder(
        IReadOnlyList<string> scenes)
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        builder.AppendLine(
            $"Configured {scenes.Count} build scenes:");

        for (int index = 0;
            index < scenes.Count;
            index++)
        {
            builder.Append(
                index);

            builder.Append(
                ": ");

            builder.AppendLine(
                GetSceneName(
                    scenes[index]));
        }

        Debug.Log(
            builder.ToString());
    }

    #endregion

    #region Types

    private readonly struct StageScene
    {
        public StageScene(
            string path,
            int number)
        {
            Path = path;
            Number = number;
        }

        public string Path { get; }

        public int Number { get; }
    }

    #endregion
}