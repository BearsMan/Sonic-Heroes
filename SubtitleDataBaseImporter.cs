using System.IO;
using UnityEditor;
using UnityEngine;

public class SubtitleDatabaseImporter : EditorWindow
{
    private SubtitleDatabase database;
    private TextAsset csvFile;
    private bool clearBeforeImport = true;

    [MenuItem("Sonic Heroes/Subtitle Importer")]
    public static void Open()
    {
        GetWindow<SubtitleDatabaseImporter>("Subtitle Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sonic Heroes Subtitle Importer", EditorStyles.boldLabel);

        database = (SubtitleDatabase)EditorGUILayout.ObjectField(
            "Subtitle Database",
            database,
            typeof(SubtitleDatabase),
            false
        );

        csvFile = (TextAsset)EditorGUILayout.ObjectField(
            "CSV File",
            csvFile,
            typeof(TextAsset),
            false
        );

        clearBeforeImport = EditorGUILayout.Toggle(
            "Clear Before Import",
            clearBeforeImport
        );

        if (GUILayout.Button("Import Subtitles"))
        {
            ImportSubtitles();
        }
    }

    private void ImportSubtitles()
    {
        if (database == null)
        {
            Debug.LogError("No SubtitleDatabase selected.");
            return;
        }

        if (csvFile == null)
        {
            Debug.LogError("No CSV file selected.");
            return;
        }

        Undo.RecordObject(database, "Import Subtitles");

        if (clearBeforeImport)
            database.lines.Clear();

        string[] rows = csvFile.text.Split('\n');

        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i].Trim();

            if (string.IsNullOrWhiteSpace(row))
                continue;

            string[] columns = row.Split(',');

            if (columns.Length < 5)
            {
                Debug.LogWarning($"Skipping row {i + 1}: Not enough columns.");
                continue;
            }

            if (!System.Enum.TryParse(columns[0], out SonicHeroesTeam team))
            {
                Debug.LogWarning($"Skipping row {i + 1}: Bad team.");
                continue;
            }

            if (!System.Enum.TryParse(columns[1], out SonicHeroesCharacter character))
            {
                Debug.LogWarning($"Skipping row {i + 1}: Bad character.");
                continue;
            }

            if (!System.Enum.TryParse(columns[2], out StageID stageID))
            {
                Debug.LogWarning($"Skipping row {i + 1}: Bad stage.");
                continue;
            }

            if (!System.Enum.TryParse(columns[3], out SubtitleEventID eventID))
            {
                Debug.LogWarning($"Skipping row {i + 1}: Bad event.");
                continue;
            }

            CharacterSubtitleEntry entry = new CharacterSubtitleEntry
            {
                team = team,
                character = character,
                stageID = stageID,
                eventID = eventID,
                line = columns[4],
                voiceClip = null
            };

            database.lines.Add(entry);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        Debug.Log($"Imported {database.lines.Count} subtitle entries.");
    }
}