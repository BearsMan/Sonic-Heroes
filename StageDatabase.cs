using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Stage Database", menuName = "Sonic Heroes/Loading Screen/Stage Database")]
public class StageDatabase : ScriptableObject
{
    [SerializeField] private List<StageData> stages = new();

    private Dictionary<string, StageData> stageLookup;

    public IReadOnlyList<StageData> Stages => stages;
    public int Count => stages.Count;

    public StageData GetStage(string stageID)
    {
        if (string.IsNullOrWhiteSpace(stageID))
            return null;

        BuildLookup();
        stageLookup.TryGetValue(stageID, out StageData stageData);

        return stageData;
    }

    public StageData GetStage(int index)
    {
        if (index < 0 || index >= stages.Count)
            return null;

        return stages[index];
    }

    public bool Contains(StageData stageData)
    {
        return stageData != null && stages.Contains(stageData);
    }

    private void BuildLookup()
    {
        if (stageLookup != null)
            return;

        stageLookup = new Dictionary<string, StageData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (StageData stageData in stages)
        {
            if (stageData == null)
                continue;

            if (string.IsNullOrWhiteSpace(stageData.StageID))
            {
                Debug.LogWarning(
                    $"Stage Data '{stageData.name}' has no Stage ID.",
                    stageData);

                continue;
            }

            if (stageLookup.ContainsKey(stageData.StageID))
            {
                Debug.LogWarning(
                    $"Duplicate Stage ID '{stageData.StageID}' in '{name}'.",
                    this);

                continue;
            }

            stageLookup.Add(stageData.StageID, stageData);
        }
    }

    private void OnEnable()
    {
        stageLookup = null;
    }

    private void OnValidate()
    {
        stageLookup = null;
    }
}