using UnityEditor;
using UnityEngine;

public static class RankingAssetGenerator
{
    private const string RankingFolder =
        "Assets/Resources/Level Rankings";

    private const string TemplateAssetPath =
        RankingFolder + "/Stage 00.asset";

    [MenuItem(
        "Sonic Heroes/Rankings/Create Missing Ranking Assets")]
    private static void CreateMissingRankingAssets()
    {
        if (!AssetDatabase.IsValidFolder(RankingFolder))
        {
            Debug.LogError(
                $"Ranking folder does not exist: {RankingFolder}");

            return;
        }

        Ranking template =
            AssetDatabase.LoadAssetAtPath<Ranking>(
                TemplateAssetPath);

        if (template == null)
        {
            Debug.LogError(
                $"Ranking template was not found at: {TemplateAssetPath}");

            return;
        }

        StageDatabase stageDatabase =
            FindStageDatabase();

        if (stageDatabase == null)
        {
            Debug.LogError(
                "No StageDatabase asset was found. " +
                "Select a StageDatabase asset or create one first.");

            return;
        }

        int createdCount = 0;
        int skippedCount = 0;
        int invalidCount = 0;

        foreach (StageData stageData in stageDatabase.Stages)
        {
            if (stageData == null)
            {
                invalidCount++;
                continue;
            }

            string sceneName =
                stageData.SceneName?.Trim();

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning(
                    $"Stage Data '{stageData.name}' has no Scene Name.",
                    stageData);

                invalidCount++;
                continue;
            }

            string destinationPath =
                $"{RankingFolder}/{sceneName}.asset";

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
                    $"scene '{sceneName}'.");

                invalidCount++;
                continue;
            }

            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Ranking asset generation complete. " +
            $"Database: {stageDatabase.name}. " +
            $"Created: {createdCount}, " +
            $"Skipped existing: {skippedCount}, " +
            $"Invalid entries: {invalidCount}.");
    }

    private static StageDatabase FindStageDatabase()
    {
        StageDatabase selectedDatabase =
            Selection.activeObject as StageDatabase;

        if (selectedDatabase != null)
            return selectedDatabase;

        string[] databaseGuids =
            AssetDatabase.FindAssets(
                "t:StageDatabase");

        if (databaseGuids.Length == 0)
            return null;

        if (databaseGuids.Length > 1)
        {
            Debug.LogWarning(
                "Multiple StageDatabase assets were found. " +
                "Select the database you want to use in the " +
                "Project window before running the generator.");
        }

        string databasePath =
            AssetDatabase.GUIDToAssetPath(
                databaseGuids[0]);

        return AssetDatabase.LoadAssetAtPath<StageDatabase>(
            databasePath);
    }
}