using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Character Definition",
    menuName = "Sonic Heroes/Characters/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    #region Types

    public enum CharacterType
    {
        Speed,
        Fly,
        Power,
        Special
    }

    public enum Team
    {
        TeamSonic,
        TeamDark,
        TeamRose,
        TeamChaotix,
        Special
    }

    #endregion

    #region Inspector

    [Header("Identity")]
    public string characterId;
    public string displayName;
    public CharacterType characterType;
    public Team team;

    [Header("Profiles")]
    public CharacterMovementProfile movementProfile;
    public CharacterAbilityProfile abilityProfile;
    public CharacterAnimatorProfile animatorProfile;
    public CharacterPresentationProfile presentationProfile;

    #endregion

    #region Public API

    public bool IsValid()
    {
        return
            TryGetIdentity(
                characterId,
                out CharacterIdentity identity) &&
            identity.CharacterType == characterType &&
            identity.Team == team &&
            !string.IsNullOrWhiteSpace(displayName) &&
            movementProfile != null &&
            abilityProfile != null &&
            animatorProfile != null &&
            presentationProfile != null;
    }

    public bool ConfigureKnownCharacter(
        string id)
    {
        if (!TryGetIdentity(
                id,
                out CharacterIdentity identity))
        {
            return false;
        }

        ApplyIdentity(
            identity);

        return true;
    }

    public bool RepairRuntimeIdentity()
    {
        if (TryGetIdentity(
                characterId,
                out CharacterIdentity identity))
        {
            ApplyIdentity(
                identity);

            return true;
        }

        if (TryResolveIdentityFromName(
                name,
                out identity))
        {
            ApplyIdentity(
                identity);

            return true;
        }

        return false;
    }

    public static bool TryGetIdentity(
        string id,
        out CharacterIdentity identity)
    {
        string normalizedId =
            NormalizeId(
                id);

        switch (normalizedId)
        {
            case "sonic":
                identity =
                    new CharacterIdentity(
                        "sonic",
                        "Sonic the Hedgehog",
                        "Sonic",
                        CharacterType.Speed,
                        Team.TeamSonic);

                return true;

            case "tails":
            case "miles-tails-prower":
                identity =
                    new CharacterIdentity(
                        "tails",
                        "Miles Tails Prower",
                        "Tails",
                        CharacterType.Fly,
                        Team.TeamSonic);

                return true;

            case "knuckles":
            case "knuckles-the-echidna":
                identity =
                    new CharacterIdentity(
                        "knuckles",
                        "Knuckles the Echidna",
                        "Knuckles",
                        CharacterType.Power,
                        Team.TeamSonic);

                return true;

            case "shadow":
            case "shadow-the-hedgehog":
                identity =
                    new CharacterIdentity(
                        "shadow",
                        "Shadow the Hedgehog",
                        "Shadow",
                        CharacterType.Speed,
                        Team.TeamDark);

                return true;

            case "rouge":
            case "rouge-the-bat":
                identity =
                    new CharacterIdentity(
                        "rouge",
                        "Rouge the Bat",
                        "Rouge",
                        CharacterType.Fly,
                        Team.TeamDark);

                return true;

            case "omega":
            case "e-123-omega":
            case "e123-omega":
                identity =
                    new CharacterIdentity(
                        "omega",
                        "E-123 Omega",
                        "Omega",
                        CharacterType.Power,
                        Team.TeamDark);

                return true;

            case "amy":
            case "amy-rose":
                identity =
                    new CharacterIdentity(
                        "amy",
                        "Amy Rose",
                        "Amy",
                        CharacterType.Speed,
                        Team.TeamRose);

                return true;

            case "cream":
            case "cream-the-rabbit":
                identity =
                    new CharacterIdentity(
                        "cream",
                        "Cream the Rabbit",
                        "Cream",
                        CharacterType.Fly,
                        Team.TeamRose);

                return true;

            case "big":
            case "big-the-cat":
                identity =
                    new CharacterIdentity(
                        "big",
                        "Big the Cat",
                        "Big",
                        CharacterType.Power,
                        Team.TeamRose);

                return true;

            case "espio":
            case "espio-the-chameleon":
                identity =
                    new CharacterIdentity(
                        "espio",
                        "Espio the Chameleon",
                        "Espio",
                        CharacterType.Speed,
                        Team.TeamChaotix);

                return true;

            case "charmy":
            case "charmy-bee":
                identity =
                    new CharacterIdentity(
                        "charmy",
                        "Charmy Bee",
                        "Charmy",
                        CharacterType.Fly,
                        Team.TeamChaotix);

                return true;

            case "vector":
            case "vector-the-crocodile":
                identity =
                    new CharacterIdentity(
                        "vector",
                        "Vector the Crocodile",
                        "Vector",
                        CharacterType.Power,
                        Team.TeamChaotix);

                return true;

            case "super-sonic":
            case "supersonic":
                identity =
                    new CharacterIdentity(
                        "super-sonic",
                        "Super Sonic",
                        "Super Sonic",
                        CharacterType.Special,
                        Team.Special);

                return true;

            default:
                identity = default;
                return false;
        }
    }

    public static bool TryResolveIdentityFromName(
        string sourceName,
        out CharacterIdentity identity)
    {
        string normalized =
            NormalizeSearchText(
                sourceName);

        if (normalized.Contains("supersonic"))
        {
            return TryGetIdentity(
                "super-sonic",
                out identity);
        }

        if (normalized.Contains("knuckles"))
        {
            return TryGetIdentity(
                "knuckles",
                out identity);
        }

        if (normalized.Contains("milestailsprower") ||
            normalized.Contains("tails"))
        {
            return TryGetIdentity(
                "tails",
                out identity);
        }

        if (normalized.Contains("shadow"))
        {
            return TryGetIdentity(
                "shadow",
                out identity);
        }

        if (normalized.Contains("rouge"))
        {
            return TryGetIdentity(
                "rouge",
                out identity);
        }

        if (normalized.Contains("omega") ||
            normalized.Contains("e123"))
        {
            return TryGetIdentity(
                "omega",
                out identity);
        }

        if (normalized.Contains("amy"))
        {
            return TryGetIdentity(
                "amy",
                out identity);
        }

        if (normalized.Contains("cream"))
        {
            return TryGetIdentity(
                "cream",
                out identity);
        }

        if (normalized.Contains("big"))
        {
            return TryGetIdentity(
                "big",
                out identity);
        }

        if (normalized.Contains("espio"))
        {
            return TryGetIdentity(
                "espio",
                out identity);
        }

        if (normalized.Contains("charmy"))
        {
            return TryGetIdentity(
                "charmy",
                out identity);
        }

        if (normalized.Contains("vector"))
        {
            return TryGetIdentity(
                "vector",
                out identity);
        }

        if (normalized.Contains("sonic"))
        {
            return TryGetIdentity(
                "sonic",
                out identity);
        }

        identity = default;
        return false;
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RepairRuntimeIdentity();
    }

    #endregion

    #region Identity

    private void ApplyIdentity(
        CharacterIdentity identity)
    {
        characterId =
            identity.CharacterId;

        displayName =
            identity.DisplayName;

        characterType =
            identity.CharacterType;

        team =
            identity.Team;
    }

    private static string NormalizeId(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "-")
            .Replace(" ", "-");
    }

    private static string NormalizeSearchText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(".", string.Empty);
    }

    #endregion

    #region Runtime Types

    public readonly struct CharacterIdentity
    {
        public readonly string CharacterId;
        public readonly string DisplayName;
        public readonly string ShortName;
        public readonly CharacterType CharacterType;
        public readonly Team Team;

        public CharacterIdentity(
            string characterId,
            string displayName,
            string shortName,
            CharacterType characterType,
            Team team)
        {
            CharacterId =
                characterId;

            DisplayName =
                displayName;

            ShortName =
                shortName;

            CharacterType =
                characterType;

            Team =
                team;
        }
    }

    #endregion
}