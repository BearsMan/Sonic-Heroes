using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class RankingReportExporter
{
    [MenuItem(
        "Sonic Heroes/Rankings/Export Validation Report",
        priority = 5)]
    private static void ExportValidationReport()
    {
        if (!RankingUtility.TryGetRequiredAssets(
                out StageDatabase stageDatabase,
                requireTemplate: false))
        {
            return;
        }

        RankingValidationResult result =
            RankingValidator.BuildValidationResult(
                stageDatabase);

        RankingUtility.EnsureReportFolderExists();

        string report =
            BuildReportText(
                stageDatabase,
                result);

        File.WriteAllText(
            RankingUtility.ReportPath,
            report,
            Encoding.UTF8);

        AssetDatabase.Refresh();

        TextAsset reportAsset =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                RankingUtility.ReportPath);

        if (reportAsset != null)
        {
            Selection.activeObject = reportAsset;
            EditorGUIUtility.PingObject(reportAsset);
        }

        Debug.Log(
            $"Ranking validation report exported to: " +
            $"{RankingUtility.ReportPath}");
    }

    private static string BuildReportText(
        StageDatabase stageDatabase,
        RankingValidationResult result)
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
            $"Ranking Folder: {RankingUtility.RankingFolder}");

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
}