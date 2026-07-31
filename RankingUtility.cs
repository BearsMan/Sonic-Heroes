using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class RankingUtility
{
    internal const string RankingFolder =
        "Assets/Resources/Level Rankings";

    internal const string TemplateAssetPath =
        RankingFolder + "/Stage 00.asset";

    internal const string ReportFolder =
        "Assets/Reports";

    internal const string ReportPath =
        ReportFolder + "/Ranking Validation Report.txt";

    internal static bool ValidateRankingFolder()
    {
        if (AssetDatabase.IsValidFolder(RankingFolder))
            return true;

        Debug.LogError(
            $"Ranking folder does not exist: {RankingFolder}");

        return false;
    }

    internal static string GetRankingAssetPath(
        string sceneName)
    {
        return $"{RankingFolder}/{sceneName}.asset";
    }

    internal static StageDatabase FindStageDatabase()
    {
        string[] databaseGuids =
            AssetDatabase.FindAssets("t:StageDatabase");

        if (databaseGuids.Length == 0)
            return null;

        if (databaseGuids.Length > 1)
        {
            Debug.LogWarning(
                "Multiple StageDatabase assets were found. " +
                "Using the first one found.");
        }

        string databasePath =
            AssetDatabase.GUIDToAssetPath(
                databaseGuids[0]);

        return AssetDatabase.LoadAssetAtPath<StageDatabase>(
            databasePath);
    }

    internal static bool TryGetSceneName(
        StageData stageData,
        out string sceneName)
    {
        sceneName = string.Empty;

        if (stageData == null)
        {
            Debug.LogWarning(
                "The StageDatabase contains an empty " +
                "StageData entry.");

            return false;
        }

        sceneName =
            stageData.SceneName?.Trim();

        if (!string.IsNullOrWhiteSpace(sceneName))
            return true;

        Debug.LogWarning(
            $"Stage Data '{stageData.name}' has no Scene Name.",
            stageData);

        return false;
    }

    internal static string FindCaseInsensitiveRankingPath(
        string sceneName)
    {
        string[] rankingGuids =
            AssetDatabase.FindAssets(
                "t:Ranking",
                new[]
                {
                    RankingFolder
                });

        foreach (string rankingGuid in rankingGuids)
        {
            string rankingPath =
                AssetDatabase.GUIDToAssetPath(
                    rankingGuid);

            string assetName =
                Path.GetFileNameWithoutExtension(
                    rankingPath);

            if (string.Equals(
                    assetName,
                    sceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return rankingPath;
            }
        }

        return string.Empty;
    }

    internal static bool TryGetRequiredAssets(
        out StageDatabase stageDatabase,
        bool requireTemplate)
    {
        stageDatabase = null;

        if (!ValidateRankingFolder())
            return false;

        Ranking template =
            AssetDatabase.LoadAssetAtPath<Ranking>(
                TemplateAssetPath);

        if (template == null)
        {
            string message =
                $"Ranking template is missing: " +
                $"{TemplateAssetPath}";

            if (requireTemplate)
            {
                Debug.LogError(message);
                return false;
            }

            Debug.LogWarning(message);
        }

        stageDatabase =
            FindStageDatabase();

        if (stageDatabase != null)
            return true;

        Debug.LogError(
            "No StageDatabase asset was found. " +
            "Create one first.");

        return false;
    }

    internal static void EnsureReportFolderExists()
    {
        if (AssetDatabase.IsValidFolder(ReportFolder))
            return;

        Directory.CreateDirectory(ReportFolder);
        AssetDatabase.Refresh();
    }
}