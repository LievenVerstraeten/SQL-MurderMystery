// MainMenuManager.cs
// Wires the three main menu buttons to GameManager.
//
// SETUP:
//   1. Attach this script to the GameObject that has your UIDocument component.
//   2. Make sure the UIDocument is using your main menu UXML file.
//   3. Ensure GameManager, DatabaseManager, and CaseManager are in the scene
//      (on a persistent GameObject — if you haven't added them yet, create an
//      empty GameObject called "GameSystems" and attach all three).

using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuManager : MonoBehaviour
{
    private UIDocument uiDocument;

    private Button newGameButton;
    private Button loadGameButton;
    private Button exitButton;
    private Button creditsButton;
    private VisualElement creditsPanel;
    private Button creditsBackButton;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[MainMenuManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Query buttons by the names in MainMenu.uxml
        newGameButton  = root.Q<Button>("play-button");
        loadGameButton = root.Q<Button>("settings-button"); // 'settings-button' is used for Load Game
        exitButton     = root.Q<Button>("exit-button");
        creditsButton  = root.Q<Button>("credits-button");
        creditsPanel   = root.Q<VisualElement>("credits-panel");
        creditsBackButton = root.Q<Button>("credits-back-button");

        // Warn if any button wasn't found — catches typos in UI Builder names
        if (newGameButton  == null) Debug.LogError("[MainMenuManager] 'play-button' not found.");
        if (loadGameButton == null) Debug.LogError("[MainMenuManager] 'settings-button' not found.");
        if (exitButton     == null) Debug.LogError("[MainMenuManager] 'exit-button' not found.");
        if (creditsButton  == null) Debug.LogError("[MainMenuManager] 'credits-button' not found.");

        // Wire up click events
        newGameButton?.RegisterCallback<ClickEvent>(OnNewGameClicked);
        loadGameButton?.RegisterCallback<ClickEvent>(OnLoadGameClicked);
        exitButton?.RegisterCallback<ClickEvent>(OnExitClicked);
        creditsButton?.RegisterCallback<ClickEvent>(OnCreditsClicked);
        creditsBackButton?.RegisterCallback<ClickEvent>(OnCreditsBackClicked);

        // Grey out Load Game if no profiles exist yet
        RefreshLoadGameButton();
    }

    private void OnDisable()
    {
        // Always unregister callbacks to avoid memory leaks
        newGameButton?.UnregisterCallback<ClickEvent>(OnNewGameClicked);
        loadGameButton?.UnregisterCallback<ClickEvent>(OnLoadGameClicked);
        exitButton?.UnregisterCallback<ClickEvent>(OnExitClicked);
        creditsButton?.UnregisterCallback<ClickEvent>(OnCreditsClicked);
        creditsBackButton?.UnregisterCallback<ClickEvent>(OnCreditsBackClicked);
    }

    // ─── Button Handlers ──────────────────────────────────────────────────────

    private void OnNewGameClicked(ClickEvent e)
    {
        Debug.Log("[MainMenuManager] New Game clicked.");
        GameManager.Instance.StartNewGameFlow();
    }

    private void OnLoadGameClicked(ClickEvent e)
    {
        Debug.Log("[MainMenuManager] Load Game clicked.");
        GameManager.Instance.OpenLoadGameScreen();
    }

    private void OnExitClicked(ClickEvent e)
    {
        Debug.Log("[MainMenuManager] Exit clicked.");

#if UNITY_EDITOR
        // Stops play mode in the editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnCreditsClicked(ClickEvent e)
    {
        if (creditsPanel != null)
        {
            creditsPanel.style.display = DisplayStyle.Flex;
        }
    }

    private void OnCreditsBackClicked(ClickEvent e)
    {
        if (creditsPanel != null)
        {
            creditsPanel.style.display = DisplayStyle.None;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Disables the Load Game button if there are no saved profiles yet.
    /// Re-enables it as soon as at least one profile exists.
    /// </summary>
    private void RefreshLoadGameButton()
    {
        if (loadGameButton == null) return;

        if (DatabaseManager.Instance == null) return;
        var profiles = DatabaseManager.Instance.GetAllProfiles();
        bool hasProfiles = profiles != null && profiles.Count > 0;

        loadGameButton.SetEnabled(hasProfiles);

        // Visual hint — dim the button when disabled
        loadGameButton.style.opacity = hasProfiles ? 1f : 0.4f;
    }
}