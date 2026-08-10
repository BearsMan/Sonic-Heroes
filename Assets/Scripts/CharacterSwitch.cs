using System.Collections.Generic;
using UnityEngine;

public class CharacterSwitch : MonoBehaviour
{
    public CHARACTERTYPES currentCharacterType = CHARACTERTYPES.Speed;
    public CHARACTERTYPES CurrentLeaderType => currentCharacterType;
    public GameObject sonic;
    public GameObject superSonic;
    public List<GameObject> TeamMembers = new List<GameObject>();
    public Transform player, leftSlot, rightSlot;
    public Transform speedCharacter, powerCharacter, flyingCharacter;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            LeftCharacter();
        }
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            RightCharacter();
        }

        if (Input.GetKeyDown(KeyCode.Home))
        {
            ChangeSuper();

        }
    }

    public void LeftCharacter()
    {
        switch (currentCharacterType)
        {
            case CHARACTERTYPES.Speed:
                currentCharacterType = CHARACTERTYPES.Fly;
                flyingCharacter.parent = player;
                powerCharacter.parent = leftSlot;
                speedCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Fly:
                currentCharacterType = CHARACTERTYPES.Power;
                powerCharacter.parent = player;
                speedCharacter.parent = leftSlot;
                flyingCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Power:
                currentCharacterType = CHARACTERTYPES.Speed;
                speedCharacter.parent = player;
                flyingCharacter.parent = leftSlot;
                powerCharacter.parent = rightSlot;
                break;
        }
        var _hud0 = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Exclude);
        if (_hud0 != null) _hud0.SetCharacter(-1);
        ResetCharacters();
    }

    public void RightCharacter()
    {
        switch (currentCharacterType)
        {
            case CHARACTERTYPES.Power:
                currentCharacterType = CHARACTERTYPES.Fly;
                flyingCharacter.parent = player;
                powerCharacter.parent = leftSlot;
                speedCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Speed:
                currentCharacterType = CHARACTERTYPES.Power;
                powerCharacter.parent = player;
                speedCharacter.parent = leftSlot;
                flyingCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Fly:
                currentCharacterType = CHARACTERTYPES.Speed;
                speedCharacter.parent = player;
                flyingCharacter.parent = leftSlot;
                powerCharacter.parent = rightSlot;
                break;
        }
        var _hud1 = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Exclude);
        if (_hud1 != null) _hud1.SetCharacter(1);
        ResetCharacters();
    }

    public void SetCharacter(CHARACTERTYPES newType)
    {
        currentCharacterType = newType;
        switch (currentCharacterType)
        {
            case CHARACTERTYPES.Fly:
                currentCharacterType = CHARACTERTYPES.Fly;
                flyingCharacter.parent = player;
                powerCharacter.parent = leftSlot;
                speedCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Power:
                currentCharacterType = CHARACTERTYPES.Power;
                powerCharacter.parent = player;
                speedCharacter.parent = leftSlot;
                flyingCharacter.parent = rightSlot;
                break;
            case CHARACTERTYPES.Speed:
                currentCharacterType = CHARACTERTYPES.Speed;
                speedCharacter.parent = player;
                flyingCharacter.parent = leftSlot;
                powerCharacter.parent = rightSlot;
                break;
        }
        var _hud2 = Object.FindAnyObjectByType<HUD>(FindObjectsInactive.Exclude);
        if (_hud2 != null) _hud2.SetCharacter(currentCharacterType);
        ResetCharacters();
    }

    public void ResetCharacters()
    {
        if (speedCharacter != null)
        {
            speedCharacter.localPosition = Vector3.zero;

            speedCharacter.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        if (flyingCharacter != null)
        {
            flyingCharacter.localPosition = Vector3.zero;

            flyingCharacter.localRotation =Quaternion.Euler(0f, 180f, 0f);
        }

        if (powerCharacter != null)
        {
            powerCharacter.localPosition = Vector3.zero;

            powerCharacter.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        if (player != null && player.TryGetComponent<UltimatePlayerMovement>(out var movement))
        {
            Transform leader =
                currentCharacterType switch
                {
                    CHARACTERTYPES.Speed =>
                        speedCharacter,

                    CHARACTERTYPES.Fly =>
                        flyingCharacter,

                    CHARACTERTYPES.Power =>
                        powerCharacter,

                    _ =>
                        null
                };

            if (leader != null)
            {
                movement.SetCurrentCharacter(leader.gameObject);
            }
        }

        if (leftSlot != null && leftSlot.TryGetComponent<FollowerNavigation>(out var leftNavigation))
        {
            leftNavigation.Setup();
        }

        if (rightSlot != null && rightSlot.TryGetComponent<FollowerNavigation>(out var rightNavigation))
        {
            rightNavigation.Setup();
        }
    }



    public void ChangeSuper()
    {
        if (speedCharacter.name == "Sonic the Hedgehog(Clone)")
        {
            Destroy(speedCharacter.gameObject);
            speedCharacter = Instantiate(superSonic, player).transform;
            GetComponent<TeamSetup>().SwapForSuper();
        }

        else if (speedCharacter.name == "Super Sonic(Clone)")
        {
            Destroy(speedCharacter.gameObject);
            speedCharacter = Instantiate(sonic, player).transform;
            GetComponent<TeamSetup>().SwapForSonic();
        }
    }
}
