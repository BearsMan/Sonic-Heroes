using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class RankingValidationResult
{
    internal int ValidCount;
    internal int MissingCount;
    internal int InvalidCount;
    internal int DuplicateCount;
    internal int OrphanedCount;
    internal int IncorrectNameCount;

    internal readonly List<string> Messages = new();

    internal int TotalProblems =>
        MissingCount +
        InvalidCount +
        DuplicateCount +
        OrphanedCount +
        IncorrectNameCount;

    internal bool Passed =>
        TotalProblems == 0;
}

internal static class RankingValidator
{
    [MenuItem(
        "Sonic Heroes/Rankings/Validate Ranking Assets",
        priority = 1)]
    private static void ValidateRankingAssets()
    {
        if (!RankingUtility.TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: false))
        {
            return;
        }

        RankingValidationResult result =
            BuildValidationResult(stageDatabase);

        LogValidationResult(
            stageDatabase,
            result);
    }

    internal static RankingValidationResult BuildValidationResult(
        StageDatabase stageDatabase)
    {
        RankingValidationResult result =
            new();

        HashSet<string> sceneNames =
            new(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> expectedRankingPaths =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (StageData stageData in stageDatabase.Stages)
        {
            if (!RankingUtility.TryGetSceneName(
                    stageData,
                    out string sceneName))
            {
                result.InvalidCount++;

                result.Messages.Add(
                    stageData == null
                        ? "Invalid: Empty StageData entry."
                        : $"Invalid: '{stageData.name}' has no Scene Name.");

                continue;
            }

            if (!sceneNames.Add(sceneName))
            {
                result.DuplicateCount++;

                string message =
                    $"Duplicate Scene Name: '{sceneName}'.";

                result.Messages.Add(message);

                Debug.LogError(
                    message,
                    stageData);

                continue;
            }

            string rankingPath =
                RankingUtility.GetRankingAssetPath(
                    sceneName);

            expectedRankingPaths.Add(rankingPath);

            Ranking ranking =
                AssetDatabase.LoadAssetAtPath<Ranking>(
                    rankingPath);

            if (ranking != null)
            {
                result.ValidCount++;
                continue;
            }

            string differentlyNamedPath =
                RankingUtility.FindCaseInsensitiveRankingPath(
                    sceneName);

            if (!string.IsNullOrEmpty(
                    differentlyNamedPath))
            {
                result.IncorrectNameCount++;

                string message =
                    $"Incorrect name or capitalization for " +
                    $"scene '{sceneName}': " +
                    $"{differentlyNamedPath}";

                result.Messages.Add(message);

                Debug.LogWarning(
                    message,
                    stageData);

                continue;
            }

            result.MissingCount++;

            string missingMessage =
                $"Missing Ranking asset for scene " +
                $"'{sceneName}'. Expected: {rankingPath}";

            result.Messages.Add(missingMessage);

            Debug.LogWarning(
                missingMessage,
                stageData);
        }

        result.OrphanedCount =
            FindOrphanedRankingAssets(
                expectedRankingPaths,
                result.Messages);

        return result;
    }

    internal static void LogValidationResult(
        StageDatabase stageDatabase,
        RankingValidationResult result)
    {
        if (result.Passed)
        {
            Debug.Log(
                $"Ranking validation passed. " +
                $"Database: {stageDatabase.name}. " +
                $"Valid assets: {result.ValidCount}. " +
                "No problems were found.");

            return;
        }

        Debug.LogWarning(
            $"Ranking validation finished with problems. " +
            $"Database: {stageDatabase.name}. " +
            $"Valid: {result.ValidCount}, " +
            $"Missing: {result.MissingCount}, " +
            $"Invalid: {result.InvalidCount}, " +
            $"Duplicates: {result.DuplicateCount}, " +
            $"Incorrect names: {result.IncorrectNameCount}, " +
            $"Orphaned: {result.OrphanedCount}.");
    }

    private static int FindOrphanedRankingAssets(
        HashSet<string> expectedRankingPaths,
        List<string> messages)
    {
        string[] rankingGuids =
            AssetDatabase.FindAssets(
                "t:Ranking",
                new[]
                {
                    RankingUtility.RankingFolder
                });

        int orphanedCount = 0;

        foreach (string rankingGuid in rankingGuids)
        {
            string rankingPath =
                AssetDatabase.GUIDToAssetPath(
                    rankingGuid);

            if (expectedRankingPaths.Contains(
                    rankingPath))
            {
                continue;
            }

            Ranking orphanedRanking =
                AssetDatabase.LoadAssetAtPath<Ranking>(
                    rankingPath);

            string message =
                $"Orphaned Ranking asset: {rankingPath}. " +
                "It does not match a StageData Scene Name.";

            messages.Add(message);

            Debug.LogWarning(
                message,
                orphanedRanking);

            orphanedCount++;
        }

        return orphanedCount;
    }
}