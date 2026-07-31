using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class RankingAssetGenerator
{
    [MenuItem(
        "Sonic Heroes/Rankings/Create Missing Ranking Assets",
        priority = 0)]
    private static void CreateMissingRankingAssets()
    {
        GenerateMissingRankingAssets(
            "Ranking asset generation");
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Repair Missing Ranking Assets",
        priority = 2)]
    private static void RepairMissingRankingAssets()
    {
        GenerateMissingRankingAssets(
            "Ranking asset repair");
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Normalize Ranking Asset Names",
        priority = 3)]
    private static void NormalizeRankingAssetNames()
    {
        if (!RankingUtility.TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: false))
        {
            return;
        }

        int renamedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        HashSet<string> processedSceneNames =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (StageData stageData
                 in stageDatabase.Stages)
        {
            if (!RankingUtility.TryGetSceneName(
                    stageData,
                    out string sceneName))
            {
                failedCount++;
                continue;
            }

            if (!processedSceneNames.Add(sceneName))
            {
                skippedCount++;
                continue;
            }

            string expectedPath =
                RankingUtility.GetRankingAssetPath(
                    sceneName);

            Ranking correctlyNamedRanking =
                AssetDatabase.LoadAssetAtPath<Ranking>(
                    expectedPath);

            if (correctlyNamedRanking != null)
            {
                skippedCount++;
                continue;
            }

            string matchingPath =
                RankingUtility.FindCaseInsensitiveRankingPath(
                    sceneName);

            if (string.IsNullOrEmpty(matchingPath))
            {
                Debug.LogWarning(
                    $"No Ranking asset was found that could be " +
                    $"normalized for scene '{sceneName}'.",
                    stageData);

                failedCount++;
                continue;
            }

            string renameError =
                AssetDatabase.RenameAsset(
                    matchingPath,
                    sceneName);

            if (!string.IsNullOrEmpty(renameError))
            {
                Debug.LogError(
                    $"Failed to rename Ranking asset " +
                    $"'{matchingPath}' to '{sceneName}': " +
                    $"{renameError}",
                    stageData);

                failedCount++;
                continue;
            }

            renamedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Ranking name normalization complete. " +
            $"Database: {stageDatabase.name}. " +
            $"Renamed: {renamedCount}, " +
            $"Skipped: {skippedCount}, " +
            $"Failed: {failedCount}.");
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Open Ranking Folder",
        priority = 4)]
    private static void OpenRankingFolder()
    {
        if (!RankingUtility.ValidateRankingFolder())
            return;

        UnityEngine.Object folder =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                RankingUtility.RankingFolder);

        if (folder == null)
        {
            Debug.LogError(
                $"Unable to open Ranking folder: " +
                $"{RankingUtility.RankingFolder}");

            return;
        }

        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    private static void GenerateMissingRankingAssets(
        string operationName)
    {
        if (!RankingUtility.TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: true))
        {
            return;
        }

        int createdCount = 0;
        int skippedCount = 0;
        int invalidCount = 0;

        HashSet<string> processedSceneNames =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (StageData stageData
                 in stageDatabase.Stages)
        {
            if (!RankingUtility.TryGetSceneName(
                    stageData,
                    out string sceneName))
            {
                invalidCount++;
                continue;
            }

            if (!processedSceneNames.Add(sceneName))
            {
                Debug.LogWarning(
                    $"Duplicate Scene Name '{sceneName}' was " +
                    $"found in StageDatabase " +
                    $"'{stageDatabase.name}'.",
                    stageData);

                invalidCount++;
                continue;
            }

            string destinationPath =
                RankingUtility.GetRankingAssetPath(
                    sceneName);

            Ranking existingRanking =
                AssetDatabase.LoadAssetAtPath<Ranking>(
                    destinationPath);

            if (existingRanking != null)
            {
                skippedCount++;
                continue;
            }

            bool copied =
                AssetDatabase.CopyAsset(
                    RankingUtility.TemplateAssetPath,
                    destinationPath);

            if (!copied)
            {
                Debug.LogError(
                    $"Failed to create Ranking asset for " +
                    $"scene '{sceneName}'.",
                    stageData);

                invalidCount++;
                continue;
            }

            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"{operationName} complete. " +
            $"Database: {stageDatabase.name}. " +
            $"Created: {createdCount}, " +
            $"Skipped existing: {skippedCount}, " +
            $"Invalid entries: {invalidCount}.");
    }
}