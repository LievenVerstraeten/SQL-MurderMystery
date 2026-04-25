using UnityEngine;
using UnityEngine.UIElements;

public class GameUIManager : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;

    // UI Elements
    private Button burgerMenuButton;
    private VisualElement burgerMenuDropdown;
    private VisualElement querieInputMenu;

    private Button tutorialButton;
    private Button profileButton;
    private Button cluesButton;
    private Button notesButton;
    private Button sqlMenuButton;
    private Button inventoryHudBtn;
    private Button saveExitButton;

    private bool isMenuOpen = false;
    private bool isInputMenuOpen = false;

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("GameUIManager: UIDocument is missing or root is null!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Querying elements
        burgerMenuButton   = root.Q<Button>("burger-menu-button");
        burgerMenuDropdown = root.Q<VisualElement>("burger-menu-dropdown");
        querieInputMenu    = root.Q<VisualElement>("sql-terminal-container");

        // Initialize panels as hidden
        if (burgerMenuDropdown != null)
            burgerMenuDropdown.style.display = DisplayStyle.None;
        if (querieInputMenu != null)
            querieInputMenu.style.display = DisplayStyle.None;

        tutorialButton   = root.Q<Button>("tutorial-button");
        profileButton    = root.Q<Button>("profile-button");
        cluesButton      = root.Q<Button>("clues-button");
        notesButton      = root.Q<Button>("notes-button");
        sqlMenuButton    = root.Q<Button>("sql-menu-button");
        inventoryHudBtn  = root.Q<Button>("inventory-hud-btn");
        saveExitButton   = root.Q<Button>("save-exit-button");

        // Burger menu
        if (burgerMenuButton != null)
            burgerMenuButton.clicked += OnBurgerMenuClicked;

        // Burger menu items
        if (tutorialButton != null)  tutorialButton.clicked  += () => Debug.Log("Tutorial clicked");
        if (profileButton != null)   profileButton.clicked   += () => Debug.Log("Profile clicked");
        if (cluesButton != null)     cluesButton.clicked     += () => Debug.Log("Clues clicked");
        if (notesButton != null)     notesButton.clicked     += () => Debug.Log("Notes clicked");
        if (sqlMenuButton != null)   sqlMenuButton.clicked   += OnSqlQuerieMenuClicked;
        if (saveExitButton != null)  saveExitButton.clicked  += OnSaveExitClicked;

        // Inventory HUD button
        if (inventoryHudBtn != null)
            inventoryHudBtn.clicked += () => InventoryManager.Instance?.ToggleInventory();

        // Connect persistent managers to this scene's UIDocument
        DialogueManager.Instance?.ConnectToUI(uiDocument);
        UIDatabase.Instance?.ConnectToUI(uiDocument);
        StartStoryIfReady();
    }

    private void OnDisable()
    {
        if (burgerMenuButton != null) burgerMenuButton.clicked -= OnBurgerMenuClicked;
        if (sqlMenuButton != null)    sqlMenuButton.clicked   -= OnSqlQuerieMenuClicked;
        if (saveExitButton != null)   saveExitButton.clicked  -= OnSaveExitClicked;
        if (inventoryHudBtn != null)  inventoryHudBtn.clicked -= () => InventoryManager.Instance?.ToggleInventory();
    }

    // ─── Burger menu ──────────────────────────────────────────────────────────

    private void OnBurgerMenuClicked()
    {
        if (burgerMenuDropdown == null) return;
        isMenuOpen = !isMenuOpen;
        burgerMenuDropdown.style.display = isMenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isMenuOpen) burgerMenuDropdown.BringToFront();
    }

    // ─── SQL terminal toggle ──────────────────────────────────────────────────

    private void OnSqlQuerieMenuClicked()
    {
        if (querieInputMenu == null) return;
        isInputMenuOpen = !isInputMenuOpen;
        querieInputMenu.style.display = isInputMenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isInputMenuOpen) querieInputMenu.BringToFront();
    }

    // ─── Save & Exit ──────────────────────────────────────────────────────────

    private void OnSaveExitClicked()
    {
        // Auto-save is handled by DialogueManager on every node advance.
        // Here we just return to the main menu scene.
        Debug.Log("[GameUIManager] Save & Exit clicked — returning to main menu.");
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    // ─── Story kickoff ────────────────────────────────────────────────────────

    /// <summary>
    /// Starts Case 01 story via DialogueManager if a profile is active.
    /// Resumes from the saved task index so continuing a save picks up mid-story.
    /// </summary>
    private void StartStoryIfReady()
    {
        if (DialogueManager.Instance == null) { Debug.LogError("[GameUIManager] DialogueManager.Instance is null"); return; }
        if (GameManager.Instance == null)     { Debug.LogError("[GameUIManager] GameManager.Instance is null"); return; }

        int profileId = GameManager.Instance.ActiveProfileId;
        if (profileId < 0) { Debug.LogError($"[GameUIManager] No active profile (id={profileId})"); return; }

        CaseDefinition activeCase = CaseManager.Instance?.ActiveCase;
        if (activeCase == null) { Debug.LogError("[GameUIManager] CaseManager.ActiveCase is null"); return; }

        Debug.Log($"[GameUIManager] Starting story for profile {profileId}, case {activeCase.CaseId}");

        // Retrieve saved task index so we resume mid-story on load
        int savedTaskIndex = CaseManager.Instance.GetCurrentTaskIndex(profileId, activeCase.CaseId);

        string playerName = GameManager.Instance.ActiveProfileName;
        var nodes = Case01Story.Build(playerName);

        DialogueManager.Instance.StartStory(nodes, savedTaskIndex);
    }
}
