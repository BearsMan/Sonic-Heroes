using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private const string MainMenuRootName = "Main Menu";
    private const string SelectionName = "Main Menu (Selection)";
    private const string OnePlayerMenuName = "1P Play Menu";
    private const string TwoPlayerMenuName = "2P Play Menu";
    private const string LoadMenuName = "LoadSave";
    private const string ExtrasMenuName = "EXTRAS MENU";

    [Header("Back Navigation")]
    public int backmenu;

    [Header("Input")]
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode confirmControllerButton = KeyCode.JoystickButton0;
    [SerializeField] private KeyCode cancelControllerButton = KeyCode.JoystickButton1;
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField, Range(0.1f, 1f)] private float navigationTriggerThreshold = 0.5f;
    [SerializeField, Min(0.01f)] private float navigationResetThreshold = 0.25f;

    private RectTransform mainMenuRoot;
    private RectTransform selectionVisual;

    private readonly List<Button> menuButtons = new List<Button>();
    private readonly List<GameObject> submenuRoots = new List<GameObject>();

    private int currentIndex;
    private bool navigationReady = true;
    private bool initialized;
    private bool transitionInProgress;

    private void Awake()
    {
        InitializeMenu();
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            InitializeMenu();
        }
        else
        {
            RefreshMenuState();
        }
    }

    protected virtual void Update()
    {
        if (!initialized || transitionInProgress)
        {
            return;
        }

        if (CancelPressed())
        {
            HandleCancel();
            return;
        }

        if (AnySubmenuIsOpen())
        {
            return;
        }

        UpdateNavigation();

        if (ConfirmPressed())
        {
            ActivateCurrentSelection();
        }
    }

    private void OnValidate()
    {
        navigationTriggerThreshold = Mathf.Clamp(navigationTriggerThreshold, 0.1f, 1f);
        navigationResetThreshold = Mathf.Max(0.01f, navigationResetThreshold);

        if (navigationResetThreshold > navigationTriggerThreshold)
        {
            navigationResetThreshold = navigationTriggerThreshold;
        }
    }

    private void InitializeMenu()
    {
        CacheMainMenuRoot();
        CacheSelectionVisual();
        CacheMenuButtons();
        CacheSubmenus();

        if (mainMenuRoot == null)
        {
            Debug.LogError($"{nameof(MainMenu)} could not find '{MainMenuRootName}'.", this);
            enabled = false;
            return;
        }

        if (menuButtons.Count == 0)
        {
            Debug.LogError($"{nameof(MainMenu)} could not find menu Buttons under '{MainMenuRootName}'.", this);
            enabled = false;
            return;
        }

        ConfigureButtons();

        currentIndex = 0;
        navigationReady = true;
        transitionInProgress = false;
        initialized = true;

        RefreshMenuState();
    }

    private void CacheMainMenuRoot()
    {
        mainMenuRoot = transform.Find("Main Menu") as RectTransform;

        if (mainMenuRoot != null)
        {
            return;
        }

        Transform current = transform;

        while (current != null)
        {
            Transform found = current.Find("Main Menu");

            if (found != null)
            {
                mainMenuRoot = found as RectTransform;
                return;
            }

            current = current.parent;
        }

        Debug.LogError(
            "MainMenu could not find 'Main Menu'.",
            this);
    }

    private void CacheSelectionVisual()
    {
        if (mainMenuRoot == null)
        {
            return;
        }

        Transform canvasRoot =
            mainMenuRoot.parent;

        if (canvasRoot == null)
        {
            return;
        }

        Transform found =
            canvasRoot.Find(
                SelectionName);

        if (found != null)
        {
            selectionVisual =
                found as RectTransform;

            return;
        }

        for (int index = 0;
            index < canvasRoot.childCount;
            index++)
        {
            Transform child =
                canvasRoot.GetChild(
                    index);

            if (child == null)
            {
                continue;
            }

            if (child.name.Equals(
                SelectionName,
                StringComparison.OrdinalIgnoreCase))
            {
                selectionVisual =
                    child as RectTransform;

                return;
            }
        }

        Debug.LogWarning(
            $"{nameof(MainMenu)} could not find '{SelectionName}'.",
            this);
    }

    private void CacheMenuButtons()
    {
        menuButtons.Clear();

        if (mainMenuRoot == null)
        {
            return;
        }

        Button[] buttons = mainMenuRoot.GetComponentsInChildren<Button>(true);

        Array.Sort(buttons, CompareButtonsByHierarchy);

        foreach (Button button in buttons)
        {
            if (button != null)
            {
                menuButtons.Add(button);
            }
        }
    }

    private void CacheSubmenus()
    {
        submenuRoots.Clear();

        Transform canvasRoot = mainMenuRoot != null ? mainMenuRoot.parent : transform.parent;

        if (canvasRoot == null)
        {
            return;
        }

        for (int index = 0; index < canvasRoot.childCount; index++)
        {
            Transform child = canvasRoot.GetChild(index);

            if (child == null || child == mainMenuRoot)
            {
                continue;
            }

            if (child.name.Equals(SelectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            submenuRoots.Add(child.gameObject);
        }
    }

    private void ConfigureButtons()
    {
        for (int index = 0; index < menuButtons.Count; index++)
        {
            Button button = menuButtons[index];

            if (button == null)
            {
                continue;
            }

            int capturedIndex = index;

            EventTrigger trigger = button.GetComponent<EventTrigger>();

            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers ??= new List<EventTrigger.Entry>();

            AddEventTrigger(
                trigger,
                EventTriggerType.PointerEnter,
                _ => SetSelection(capturedIndex));

            AddEventTrigger(
                trigger,
                EventTriggerType.Select,
                _ => SetSelection(capturedIndex));
        }
    }

    private void RefreshMenuState()
    {
        if (mainMenuRoot == null || menuButtons.Count == 0)
        {
            return;
        }

        CloseAllSubmenus();
        mainMenuRoot.gameObject.SetActive(true);

        SetSelection(
            Mathf.Clamp(
                currentIndex,
                0,
                menuButtons.Count - 1));
    }

    private void UpdateNavigation()
    {
        float vertical = ReadVerticalInput();

        if (Mathf.Abs(vertical) <= navigationResetThreshold)
        {
            navigationReady = true;
            return;
        }

        if (!navigationReady || Mathf.Abs(vertical) < navigationTriggerThreshold)
        {
            return;
        }

        navigationReady = false;

        MoveSelection(vertical > 0f ? -1 : 1);
    }

    private float ReadVerticalInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            return 1f;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            return -1f;
        }

        if (string.IsNullOrWhiteSpace(verticalAxis))
        {
            return 0f;
        }

        try
        {
            return Input.GetAxisRaw(verticalAxis);
        }
        catch (ArgumentException)
        {
            return 0f;
        }
    }

    private void MoveSelection(int direction)
    {
        if (menuButtons.Count == 0)
        {
            return;
        }

        int nextIndex = (currentIndex + direction) % menuButtons.Count;

        if (nextIndex < 0)
        {
            nextIndex += menuButtons.Count;
        }

        SetSelection(nextIndex);
    }

    private void SetSelection(int index)
    {
        if (index < 0 || index >= menuButtons.Count)
        {
            return;
        }

        currentIndex = index;

        Button button = menuButtons[currentIndex];

        if (button == null)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        UpdateSelectionVisual(button);
    }

    private void UpdateSelectionVisual(Button button)
    {
        if (selectionVisual == null || button == null)
        {
            return;
        }

        RectTransform target = button.transform as RectTransform;

        if (target == null)
        {
            return;
        }

        Vector3 worldPosition = target.TransformPoint(target.rect.center);
        Vector3 selectionPosition = selectionVisual.position;
        selectionPosition.y = worldPosition.y;
        selectionVisual.position = selectionPosition;

        if (!selectionVisual.gameObject.activeSelf)
        {
            selectionVisual.gameObject.SetActive(true);
        }
    }

    private void ActivateCurrentSelection()
    {
        if (currentIndex < 0 || currentIndex >= menuButtons.Count)
        {
            return;
        }

        Button button = menuButtons[currentIndex];

        if (button == null || !button.interactable)
        {
            return;
        }

        if (button.onClick.GetPersistentEventCount() > 0)
        {
            button.onClick.Invoke();
            return;
        }

        ExecuteAutomaticAction(button);
    }

    private void ExecuteAutomaticAction(Button button)
    {
        string normalizedName = NormalizeButtonName(button.name);

        if (normalizedName.Contains("1pplay") || normalizedName.Equals("play"))
        {
            OpenSubmenu(OnePlayerMenuName);
            return;
        }

        if (normalizedName.Contains("2pplay"))
        {
            OpenSubmenu(TwoPlayerMenuName);
            return;
        }

        if (normalizedName.Contains("load"))
        {
            OpenSubmenu(LoadMenuName);
            return;
        }

        if (normalizedName.Contains("extra"))
        {
            OpenSubmenu(ExtrasMenuName);
            return;
        }

        if (normalizedName.Contains("option"))
        {
            OpenOptionsFallback();
            return;
        }

        if (normalizedName.Contains("exit") || normalizedName.Contains("quit"))
        {
            Exit();
        }
    }

    private void OpenSubmenu(string submenuName)
    {
        GameObject submenu = FindSubmenu(submenuName);

        if (submenu == null)
        {
            return;
        }

        CloseAllSubmenus();
        mainMenuRoot.gameObject.SetActive(false);

        if (selectionVisual != null)
        {
            selectionVisual.gameObject.SetActive(false);
        }

        submenu.SetActive(true);
    }

    private void OpenOptionsFallback()
    {
        string[] preferredOptionsMenus =
        {
            "Options Menu",
            "Options",
            "Language Setting",
            "Vibration Setting"
        };

        foreach (string menuName in preferredOptionsMenus)
        {
            GameObject submenu = FindSubmenu(menuName);

            if (submenu == null)
            {
                continue;
            }

            CloseAllSubmenus();
            mainMenuRoot.gameObject.SetActive(false);

            if (selectionVisual != null)
            {
                selectionVisual.gameObject.SetActive(false);
            }

            submenu.SetActive(true);
            return;
        }
    }

    private void HandleCancel()
    {
        if (AnySubmenuIsOpen())
        {
            CloseAllSubmenus();
            mainMenuRoot.gameObject.SetActive(true);
            SetSelection(currentIndex);
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (backmenu >= 0 &&
            activeScene.buildIndex != backmenu &&
            backmenu < SceneManager.sceneCountInBuildSettings)
        {
            LoadScene(backmenu);
        }
    }

    private bool AnySubmenuIsOpen()
    {
        foreach (GameObject submenu in submenuRoots)
        {
            if (submenu != null && submenu.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    private void CloseAllSubmenus()
    {
        foreach (GameObject submenu in submenuRoots)
        {
            if (submenu != null)
            {
                submenu.SetActive(false);
            }
        }
    }

    public void LoadScene(int sceneIndex)
    {
        if (transitionInProgress ||
            sceneIndex < 0 ||
            sceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        transitionInProgress = true;
        SceneManager.LoadScene(sceneIndex);
    }

    public void Exit()
    {
#if UNITY_EDITOR
        Debug.Log(
            "Exit requested. Application.Quit() is ignored while running in the Unity Editor.",
            this);
#else
		Application.Quit();
#endif
    }

    private bool ConfirmPressed()
    {
        return
            Input.GetKeyDown(confirmKey) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(confirmControllerButton);
    }

    private bool CancelPressed()
    {
        return
            Input.GetKeyDown(cancelKey) ||
            Input.GetKeyDown(cancelControllerButton);
    }

    private GameObject FindSubmenu(string submenuName)
    {
        if (string.IsNullOrWhiteSpace(submenuName))
        {
            return null;
        }

        foreach (GameObject submenu in submenuRoots)
        {
            if (submenu != null &&
                submenu.name.Equals(submenuName, StringComparison.OrdinalIgnoreCase))
            {
                return submenu;
            }
        }

        return null;
    }

    private static string NormalizeButtonName(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
        {
            return string.Empty;
        }

        return buttonName
            .Replace("Button", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private static int CompareButtonsByHierarchy(Button left, Button right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.transform.GetSiblingIndex()
            .CompareTo(right.transform.GetSiblingIndex());
    }

    private static void AddEventTrigger(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        if (trigger == null || callback == null)
        {
            return;
        }

        EventTrigger.Entry entry =
            new EventTrigger.Entry
            {
                eventID = eventType
            };

        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
}
