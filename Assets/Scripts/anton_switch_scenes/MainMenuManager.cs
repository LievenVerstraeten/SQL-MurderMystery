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

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[MainMenuManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Query buttons by the names you set in UI Builder
        newGameButton = root.Q<Button>("NewGameMain");
        loadGameButton = root.Q<Button>("LoadGameMain");
        exitButton = root.Q<Button>("ExitGameMain");

        // Warn if any button wasn't found — catches typos in UI Builder names
        if (newGameButton == null) Debug.LogError("[MainMenuManager] 'NewGameMain' button not found.");
        if (loadGameButton == null) Debug.LogError("[MainMenuManager] 'LoadGameMain' button not found.");
        if (exitButton == null) Debug.LogError("[MainMenuManager] 'ExitGameMain' button not found.");

        // Wire up click events
        newGameButton?.RegisterCallback<ClickEvent>(OnNewGameClicked);
        loadGameButton?.RegisterCallback<ClickEvent>(OnLoadGameClicked);
        exitButton?.RegisterCallback<ClickEvent>(OnExitClicked);

        // Grey out Load Game if no profiles exist yet
        RefreshLoadGameButton();
    }

    private void OnDisable()
    {
        // Always unregister callbacks to avoid memory leaks
        newGameButton?.UnregisterCallback<ClickEvent>(OnNewGameClicked);
        loadGameButton?.UnregisterCallback<ClickEvent>(OnLoadGameClicked);
        exitButton?.UnregisterCallback<ClickEvent>(OnExitClicked);
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

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Disables the Load Game button if there are no saved profiles yet.
    /// Re-enables it as soon as at least one profile exists.
    /// </summary>
    private void RefreshLoadGameButton()
    {
        if (loadGameButton == null) return;

        var profiles = DatabaseManager.Instance.GetAllProfiles();
        bool hasProfiles = profiles != null && profiles.Count > 0;

        loadGameButton.SetEnabled(hasProfiles);

        // Visual hint — dim the button when disabled
        loadGameButton.style.opacity = hasProfiles ? 1f : 0.4f;
    }
}