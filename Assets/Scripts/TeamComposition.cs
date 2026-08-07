using System;
using UnityEngine;
using UnityEngine.Video;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "Team Composition",
    menuName = "Teams/Team Composition")]
public sealed class TeamComposition : ScriptableObject
{
    #region Inspector

    [SerializeField] private PlayableTeam playableTeam;

    [Header("Characters")]
    [SerializeField] private GameObject speedCharacterPrefab;
    [SerializeField] private GameObject flyingCharacterPrefab;
    [SerializeField] private GameObject powerCharacterPrefab;
    [SerializeField] private GameObject superCharacterPrefab;

    [Header("Resolved Character Definitions")]
    [SerializeField, HideInInspector]
    private CharacterDefinition speedCharacterDefinition;

    [SerializeField, HideInInspector]
    private CharacterDefinition flyingCharacterDefinition;

    [SerializeField, HideInInspector]
    private CharacterDefinition powerCharacterDefinition;

    [SerializeField, HideInInspector]
    private CharacterDefinition superCharacterDefinition;

    [Header("Character HUD Data")]
    [SerializeField] private Character speedCharacterData;
    [SerializeField] private Character flyingCharacterData;
    [SerializeField] private Character powerCharacterData;
    [SerializeField] private Character superCharacterData;

    [Header("Presentation")]
    [SerializeField] private Sprite teamIcon;
    [SerializeField] private Color accentColor;
    [SerializeField] private VideoClip teamBlast;

    #endregion

    #region Editor State

#if UNITY_EDITOR
    [NonSerialized] private bool resolutionScheduled;
#endif

    #endregion

    #region Public API

    public PlayableTeam PlayableTeam =>
        playableTeam;

    public Character SpeedCharacterData =>
        speedCharacterData;

    public Character FlyingCharacterData =>
        flyingCharacterData;

    public Character PowerCharacterData =>
        powerCharacterData;

    public Character SuperCharacterData =>
        superCharacterData;

    public GameObject SpeedCharacterPrefab =>
        speedCharacterPrefab;

    public GameObject FlyingCharacterPrefab =>
        flyingCharacterPrefab;

    public GameObject PowerCharacterPrefab =>
        powerCharacterPrefab;

    public GameObject SuperCharacterPrefab =>
        superCharacterPrefab;

    public CharacterDefinition SpeedCharacterDefinition =>
        speedCharacterDefinition;

    public CharacterDefinition FlyingCharacterDefinition =>
        flyingCharacterDefinition;

    public CharacterDefinition PowerCharacterDefinition =>
        powerCharacterDefinition;

    public CharacterDefinition SuperCharacterDefinition =>
        superCharacterDefinition;

    public Sprite TeamIcon =>
        teamIcon;

    public Color AccentColor =>
        accentColor;

    public VideoClip TeamBlast =>
        teamBlast;

    public VideoClip TeamBlastVideo =>
        teamBlast;

    #endregion

    #region Unity Lifecycle

    private void OnValidate()
    {
#if UNITY_EDITOR
        ScheduleAutomaticResolution();
#endif
    }

    #endregion

#if UNITY_EDITOR

    #region Automatic Editor Setup

    private static CharacterDefinition GetPrefabCharacterDefinition(
        GameObject characterPrefab)
    {
        if (characterPrefab == null)
            return null;

        UltimatePlayerMovement movement =
            characterPrefab.GetComponent<UltimatePlayerMovement>();

        if (movement == null)
        {
            movement =
                characterPrefab.GetComponentInChildren<UltimatePlayerMovement>(
                    includeInactive: true);
        }

        return
            movement != null
                ? movement.CharacterDefinition
                : null;
    }

    private void ScheduleAutomaticResolution()
    {
        if (resolutionScheduled)
            return;

        resolutionScheduled = true;

        EditorApplication.delayCall +=
            RunScheduledResolution;
    }

    private void RunScheduledResolution()
    {
        resolutionScheduled = false;

        if (this == null)
            return;

        ResolveCharacterDefinitions();
    }

    [ContextMenu("Resolve Character Definitions")]
    private void ResolveCharacterDefinitions()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        resolutionScheduled = false;

        bool changed = false;

        CharacterDefinition newSpeedDefinition =
            ResolveDefinitionAsset(
                speedCharacterPrefab,
                speedCharacterDefinition);

        if (speedCharacterDefinition != newSpeedDefinition)
        {
            speedCharacterDefinition = newSpeedDefinition;
            changed = true;
        }

        CharacterDefinition newFlyingDefinition =
            ResolveDefinitionAsset(
                flyingCharacterPrefab,
                flyingCharacterDefinition);

        if (flyingCharacterDefinition != newFlyingDefinition)
        {
            flyingCharacterDefinition = newFlyingDefinition;
            changed = true;
        }

        CharacterDefinition newPowerDefinition =
            ResolveDefinitionAsset(
                powerCharacterPrefab,
                powerCharacterDefinition);

        if (powerCharacterDefinition != newPowerDefinition)
        {
            powerCharacterDefinition = newPowerDefinition;
            changed = true;
        }

        CharacterDefinition newSuperDefinition =
            ResolveDefinitionAsset(
                superCharacterPrefab,
                superCharacterDefinition);

        if (superCharacterDefinition != newSuperDefinition)
        {
            superCharacterDefinition = newSuperDefinition;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static CharacterDefinition ResolveDefinitionAsset(
        GameObject characterPrefab,
        CharacterDefinition cachedDefinition)
    {
        if (characterPrefab == null)
            return null;

        CharacterDefinition prefabDefinition =
            GetPrefabCharacterDefinition(
                characterPrefab);

        if (prefabDefinition != null)
        {
            return prefabDefinition;
        }

        CharacterDefinition matchedDefinition =
            FindMatchingDefinition(
                characterPrefab);

        return
            matchedDefinition != null
                ? matchedDefinition
                : cachedDefinition;
    }

    private static CharacterDefinition FindMatchingDefinition(
        GameObject characterPrefab)
    {
        if (characterPrefab == null)
            return null;

        string prefabName =
            NormalizeName(
                characterPrefab.name);

        string[] definitionGuids =
            AssetDatabase.FindAssets(
                "t:CharacterDefinition");

        foreach (string guid in definitionGuids)
        {
            string definitionPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            CharacterDefinition definition =
                AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    definitionPath);

            if (definition == null)
                continue;

            if (IsDefinitionMatch(
                    definition,
                    definitionPath,
                    prefabName))
            {
                return definition;
            }
        }

        Debug.LogWarning(
            $"TeamComposition could not automatically find a " +
            $"CharacterDefinition for '{characterPrefab.name}'.",
            characterPrefab);

        return null;
    }

    private static bool IsDefinitionMatch(
        CharacterDefinition definition,
        string definitionPath,
        string prefabName)
    {
        string displayName =
            NormalizeName(
                definition.displayName);

        string characterId =
            NormalizeName(
                definition.characterId);

        string assetName =
            NormalizeName(
                Path.GetFileNameWithoutExtension(
                    definitionPath));

        return
    (!string.IsNullOrWhiteSpace(displayName) &&
     displayName == prefabName) ||

    (!string.IsNullOrWhiteSpace(characterId) &&
     characterId == prefabName) ||

    (!string.IsNullOrWhiteSpace(assetName) &&
     assetName == prefabName) ||

    (!string.IsNullOrWhiteSpace(assetName) &&
     !string.IsNullOrWhiteSpace(prefabName) &&
     assetName.Contains(prefabName)) ||

    (!string.IsNullOrWhiteSpace(displayName) &&
     !string.IsNullOrWhiteSpace(prefabName) &&
     prefabName.Contains(displayName));
    }

    private static string NormalizeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] characters =
            value.ToLowerInvariant()
                .ToCharArray();

        System.Text.StringBuilder builder =
            new();

        foreach (char character in characters)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Replace(
                "characterdefinition",
                string.Empty)
            .Replace(
                "clone",
                string.Empty);
    }

    #endregion

#endif
}