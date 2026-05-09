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
    private Button notesButton;
    private Button sqlMenuButton;
    private Button saveExitButton;

    private bool isMenuOpen = false;
    private bool isInputMenuOpen = false;
    private bool isTutorialOpen = false;
    private bool isProfileOpen = false;

    private VisualElement _tutorialOverlay;
    private VisualElement _profileOverlay;

    // ── Parallax ──────────────────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _sceneBg;
    private VisualElement _speakerPortrait;
    private const float BgDepth   = 14f;
    private const float PortDepth =  6f;
    private const float LerpSpeed =  5f;
    private Vector2 _parallaxTarget;
    private Vector2 _parallaxCurrent;

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("GameUIManager: UIDocument is missing or root is null!");
            return;
        }

        _root = uiDocument.rootVisualElement;
        var root = _root;

        // Querying elements
        burgerMenuButton = root.Q<Button>("burger-menu-button");
        burgerMenuDropdown = root.Q<VisualElement>("burger-menu-dropdown");
        querieInputMenu = root.Q<VisualElement>("sql-terminal-container");

        // Initialize panels as hidden
        if (burgerMenuDropdown != null)
            burgerMenuDropdown.style.display = DisplayStyle.None;
        if (querieInputMenu != null)
            querieInputMenu.style.display = DisplayStyle.None;

        tutorialButton = root.Q<Button>("tutorial-button");
        profileButton = root.Q<Button>("profile-button");
        notesButton = root.Q<Button>("notes-button");
        sqlMenuButton = root.Q<Button>("sql-menu-button");
        saveExitButton = root.Q<Button>("save-exit-button");

        // Burger menu
        if (burgerMenuButton != null)
            burgerMenuButton.clicked += OnBurgerMenuClicked;

        // Tutorial overlay
        _tutorialOverlay = root.Q("tutorial-overlay");
        if (_tutorialOverlay != null) _tutorialOverlay.style.display = DisplayStyle.None;
        var tutorialCloseBtn = root.Q<Button>("tutorial-close-btn");
        if (tutorialCloseBtn != null) tutorialCloseBtn.clicked += CloseTutorial;

        // Profile overlay
        _profileOverlay = root.Q("profile-overlay");
        if (_profileOverlay != null) _profileOverlay.style.display = DisplayStyle.None;
        var profileCloseBtn = root.Q<Button>("profile-close-btn");
        if (profileCloseBtn != null) profileCloseBtn.clicked += CloseProfile;

        // Burger menu items
        if (tutorialButton != null) tutorialButton.clicked += OnTutorialClicked;
        if (profileButton != null) profileButton.clicked += OnProfileClicked;
        if (notesButton != null) notesButton.clicked += () => Debug.Log("Notes clicked");
        if (sqlMenuButton != null) sqlMenuButton.clicked += OnSqlQuerieMenuClicked;
        if (saveExitButton != null) saveExitButton.clicked += OnSaveExitClicked;

        // Connect persistent managers to this scene's UIDocument
        DialogueManager.Instance?.ConnectToUI(uiDocument);
        UIDatabase.Instance?.ConnectToUI(uiDocument);

        // Connect mute button
        MuteManager.Instance?.ConnectToUI(uiDocument);

        // Connect text size button — cycles Normal → Large → XL
        TextSizeManager.Instance?.ConnectToUI(uiDocument);

        // Connect ERD overlay — shows database diagram on button click
        ERDManager.Instance?.ConnectToUI(uiDocument);

        // Parallax — query layers and start listening to mouse
        _sceneBg         = root.Q("scene-bg");
        _speakerPortrait = root.Q("speaker-portrait");
        root.RegisterCallback<MouseMoveEvent>(OnMouseMove);

        StartStoryIfReady();
    }

    private void OnDisable()
    {
        if (burgerMenuButton != null) burgerMenuButton.clicked -= OnBurgerMenuClicked;
        if (sqlMenuButton != null) sqlMenuButton.clicked -= OnSqlQuerieMenuClicked;
        if (saveExitButton != null) saveExitButton.clicked -= OnSaveExitClicked;
        if (tutorialButton != null) tutorialButton.clicked -= OnTutorialClicked;
        if (profileButton != null) profileButton.clicked -= OnProfileClicked;
        if (_root != null) _root.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
    }

    // ── Parallax ──────────────────────────────────────────────────────────────

    private void Update()
    {
        _parallaxCurrent = Vector2.Lerp(_parallaxCurrent, _parallaxTarget, Time.deltaTime * LerpSpeed);
        ApplyTranslate(_sceneBg,         -_parallaxCurrent.x * BgDepth,   -_parallaxCurrent.y * BgDepth);
        ApplyTranslate(_speakerPortrait,  -_parallaxCurrent.x * PortDepth, -_parallaxCurrent.y * PortDepth);
    }

    private void OnMouseMove(MouseMoveEvent e)
    {
        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;
        float nx = (e.localMousePosition.x / layout.width  - 0.5f) * 2f;
        float ny = (e.localMousePosition.y / layout.height - 0.5f) * 2f;
        _parallaxTarget = new Vector2(nx, ny);
    }

    private static void ApplyTranslate(VisualElement el, float x, float y)
    {
        if (el == null) return;
        el.style.translate = new StyleTranslate(new Translate(
            new Length(x, LengthUnit.Pixel),
            new Length(y, LengthUnit.Pixel)));
    }

    // ─── Tutorial overlay ─────────────────────────────────────────────────────

    private void OnTutorialClicked()
    {
        isTutorialOpen = !isTutorialOpen;
        if (_tutorialOverlay == null) return;
        _tutorialOverlay.style.display = isTutorialOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isTutorialOpen) _tutorialOverlay.BringToFront();
    }

    private void CloseTutorial()
    {
        isTutorialOpen = false;
        if (_tutorialOverlay != null) _tutorialOverlay.style.display = DisplayStyle.None;
    }

    private void OnProfileClicked()
    {
        isProfileOpen = !isProfileOpen;
        if (_profileOverlay == null) return;
        _profileOverlay.style.display = isProfileOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isProfileOpen)
        {
            var nameLabel = _root.Q<Label>("profile-n-name");
            if (nameLabel != null)
            {
                string pName = GameManager.Instance != null ? GameManager.Instance.ActiveProfileName : "DETECTIVE";
                nameLabel.text = pName.ToUpper();
            }
            _profileOverlay.BringToFront();
        }
    }

    private void CloseProfile()
    {
        isProfileOpen = false;
        if (_profileOverlay != null) _profileOverlay.style.display = DisplayStyle.None;
    }

    // ─── Burger menu ──────────────────────────────────────────────────────────

    private void OnBurgerMenuClicked()
    {
        if (burgerMenuDropdown == null) return;
        isMenuOpen = !isMenuOpen;
        burgerMenuDropdown.style.display = isMenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isMenuOpen) burgerMenuDropdown.BringToFront();

        // Closing the burger menu also closes the SQL panel
        if (!isMenuOpen && isInputMenuOpen)
        {
            isInputMenuOpen = false;
            if (querieInputMenu != null)
                querieInputMenu.style.display = DisplayStyle.None;
        }
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
        if (GameManager.Instance == null) { Debug.LogError("[GameUIManager] GameManager.Instance is null"); return; }

        int profileId = GameManager.Instance.ActiveProfileId;
        if (profileId < 0) { Debug.LogError($"[GameUIManager] No active profile (id={profileId})"); return; }

        CaseDefinition activeCase = CaseManager.Instance?.ActiveCase;
        if (activeCase == null) { Debug.LogError("[GameUIManager] CaseManager.ActiveCase is null"); return; }

        Debug.Log($"[GameUIManager] Starting story for profile {profileId}, case {activeCase.CaseId}");

        // Retrieve saved task index so we resume mid-story on load
        int savedTaskIndex = CaseManager.Instance.GetCurrentTaskIndex(profileId, activeCase.CaseId);

        string playerName = GameManager.Instance.ActiveProfileName;
        var nodes = Case01Story.Build(playerName);

        // Convert the SQL task index to the matching dialogue node index
        int dialogueNodeIndex = 0;
        if (savedTaskIndex > 0)
        {
            int seenTasks = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type == NodeType.SQLTask)
                {
                    seenTasks++;
                    if (seenTasks == savedTaskIndex)
                    {
                        // Found the last completed task. Start at the node right after it.
                        dialogueNodeIndex = i + 1;
                        break;
                    }
                }
            }
        }

        DialogueManager.Instance.StartStory(nodes, dialogueNodeIndex);
    }
}