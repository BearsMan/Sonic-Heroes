using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RankingAssetGenerator
{
    private const string RankingFolder =
        "Assets/Resources/Level Rankings";

    private const string TemplateAssetPath =
        RankingFolder + "/Stage 00.asset";

    private const string ReportFolder =
        "Assets/Reports";

    private const string ReportPath =
        ReportFolder + "/Ranking Validation Report.txt";

    private sealed class ValidationResult
    {
        public int ValidCount;
        public int MissingCount;
        public int InvalidCount;
        public int DuplicateCount;
        public int OrphanedCount;
        public int IncorrectNameCount;

        public readonly List<string> Messages = new();

        public int TotalProblems =>
            MissingCount +
            InvalidCount +
            DuplicateCount +
            OrphanedCount +
            IncorrectNameCount;

        public bool Passed =>
            TotalProblems == 0;
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Create Missing Ranking Assets",
        priority = 0)]
    private static void CreateMissingRankingAssets()
    {
        GenerateMissingRankingAssets(
            "Ranking asset generation");
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Validate Ranking Assets",
        priority = 1)]
    private static void ValidateRankingAssets()
    {
        if (!TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: false))
        {
            return;
        }

        ValidationResult result =
            BuildValidationResult(stageDatabase);

        LogValidationResult(
            stageDatabase,
            result);
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
        if (!TryGetRequiredAssets(
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
            if (!TryGetSceneName(
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
                GetRankingAssetPath(sceneName);

            Ranking correctlyNamedRanking =
                AssetDatabase.LoadAssetAtPath<Ranking>(
                    expectedPath);

            if (correctlyNamedRanking != null)
            {
                skippedCount++;
                continue;
            }

            string matchingPath =
                FindCaseInsensitiveRankingPath(
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
        if (!ValidateRankingFolder())
            return;

        UnityEngine.Object folder =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                RankingFolder);

        if (folder == null)
        {
            Debug.LogError(
                $"Unable to open Ranking folder: " +
                $"{RankingFolder}");

            return;
        }

        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    [MenuItem(
        "Sonic Heroes/Rankings/Export Validation Report",
        priority = 5)]
    private static void ExportValidationReport()
    {
        if (!TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: false))
        {
            return;
        }

        ValidationResult result =
            BuildValidationResult(stageDatabase);

        EnsureReportFolderExists();

        string report =
            BuildReportText(
                stageDatabase,
                result);

        File.WriteAllText(
            ReportPath,
            report,
            Encoding.UTF8);

        AssetDatabase.Refresh();

        TextAsset reportAsset =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                ReportPath);

        if (reportAsset != null)
        {
            Selection.activeObject = reportAsset;
            EditorGUIUtility.PingObject(reportAsset);
        }

        Debug.Log(
            $"Ranking validation report exported to: " +
            $"{ReportPath}");
    }

    private static void GenerateMissingRankingAssets(
        string operationName)
    {
        if (!TryGetRequiredAssets(
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
            if (!TryGetSceneName(
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
                GetRankingAssetPath(sceneName);

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
                    TemplateAssetPath,
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

    private static ValidationResult BuildValidationResult(
        StageDatabase stageDatabase)
    {
        ValidationResult result =
            new();

        HashSet<string> sceneNames =
            new(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> expectedRankingPaths =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (StageData stageData
                 in stageDatabase.Stages)
        {
            if (!TryGetSceneName(
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
                GetRankingAssetPath(sceneName);

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
                FindCaseInsensitiveRankingPath(
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

    private static int FindOrphanedRankingAssets(
        HashSet<string> expectedRankingPaths,
        List<string> messages)
    {
        string[] rankingGuids =
            AssetDatabase.FindAssets(
                "t:Ranking",
                new[]
                {
                    RankingFolder
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

    private static void LogValidationResult(
        StageDatabase stageDatabase,
        ValidationResult result)
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
            $"Incorrect names: " +
            $"{result.IncorrectNameCount}, " +
            $"Orphaned: {result.OrphanedCount}.");
    }

    private static string BuildReportText(
        StageDatabase stageDatabase,
        ValidationResult result)
    {
        StringBuilder report =
            new();

        report.AppendLine(
            "SONIC HEROES — RANKING VALIDATION REPORT");

        report.AppendLine(
            "========================================");

        report.AppendLine(
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        report.AppendLine(
            $"Stage Database: {stageDatabase.name}");

        report.AppendLine(
            $"Ranking Folder: {RankingFolder}");

        report.AppendLine();

        report.AppendLine(
            $"Status: {(result.Passed ? "PASSED" : "PROBLEMS FOUND")}");

        report.AppendLine(
            $"Valid Assets: {result.ValidCount}");

        report.AppendLine(
            $"Missing Assets: {result.MissingCount}");

        report.AppendLine(
            $"Invalid Entries: {result.InvalidCount}");

        report.AppendLine(
            $"Duplicate Scene Names: {result.DuplicateCount}");

        report.AppendLine(
            $"Incorrect Names: {result.IncorrectNameCount}");

        report.AppendLine(
            $"Orphaned Assets: {result.OrphanedCount}");

        report.AppendLine();

        report.AppendLine("DETAILS");
        report.AppendLine("-------");

        if (result.Messages.Count == 0)
        {
            report.AppendLine(
                "No validation problems were found.");
        }
        else
        {
            foreach (string message in result.Messages)
            {
                report.AppendLine(
                    $"- {message}");
            }
        }

        return report.ToString();
    }

    private static bool TryGetRequiredAssets(
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

    private static bool TryGetSceneName(
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
            $"Stage Data '{stageData.name}' has no " +
            "Scene Name.",
            stageData);

        return false;
    }

    private static string FindCaseInsensitiveRankingPath(
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

    private static string GetRankingAssetPath(
        string sceneName)
    {
        return
            $"{RankingFolder}/{sceneName}.asset";
    }

    private static bool ValidateRankingFolder()
    {
        if (AssetDatabase.IsValidFolder(
                RankingFolder))
        {
            return true;
        }

        Debug.LogError(
            $"Ranking folder does not exist: " +
            $"{RankingFolder}");

        return false;
    }

    private static StageDatabase FindStageDatabase()
    {
        string[] databaseGuids =
            AssetDatabase.FindAssets(
                "t:StageDatabase");

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

        return
            AssetDatabase.LoadAssetAtPath<StageDatabase>(
                databasePath);
    }

    private static void EnsureReportFolderExists()
    {
        if (AssetDatabase.IsValidFolder(
                ReportFolder))
        {
            return;
        }

        Directory.CreateDirectory(
            ReportFolder);

        AssetDatabase.Refresh();
    }
}