using UnityEngine;
using UnityEngine.UIElements;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }
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
    private Button saveExitButton;

    private bool isMenuOpen = false;
    private bool isHistoryOpen = false;
    private VisualElement _historyOverlay;
    private VisualElement _historyContent;
    private bool isInputMenuOpen = false;
    private bool isTutorialOpen = false;
    private bool isProfileOpen = false;
    // Mirror of DialogueManager._awaitingSQL — true when a SQL task is active
    // Used to prevent closing the terminal when the burger menu closes
    private bool _awaitingSQL => DialogueManager.Instance != null && UIDatabase.Instance != null && UIDatabase.Instance.IsTerminalOpen();

    private VisualElement _tutorialOverlay;
    private VisualElement _profileOverlay;

    // ── Parallax ──────────────────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _sceneBg;
    private VisualElement _speakerPortrait;
    private const float BgDepth = 14f;
    private const float PortDepth = 6f;
    private const float LerpSpeed = 5f;
    private Vector2 _parallaxTarget;
    private Vector2 _parallaxCurrent;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
        cluesButton = root.Q<Button>("clues-button");
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

        _historyOverlay = root.Q("history-overlay");
        if (_historyOverlay != null) _historyOverlay.style.display = DisplayStyle.None;
        _historyContent = root.Q("history-content");
        var historyCloseBtn = root.Q<Button>("history-close-btn");
        if (historyCloseBtn != null) historyCloseBtn.clicked += CloseHistory;
        var historyBtn = root.Q<Button>("history-button");
        if (historyBtn != null) historyBtn.clicked += OnHistoryClicked;

        // Profile overlay
        _profileOverlay = root.Q("profile-overlay");
        if (_profileOverlay != null) _profileOverlay.style.display = DisplayStyle.None;
        var profileCloseBtn = root.Q<Button>("profile-close-btn");
        if (profileCloseBtn != null) profileCloseBtn.clicked += CloseProfile;

        // Burger menu items
        if (tutorialButton != null) tutorialButton.clicked += OnTutorialClicked;
        if (profileButton != null) profileButton.clicked += OnProfileClicked;
        if (cluesButton != null) cluesButton.clicked += () =>
        {
            ClueBoardManager.Instance?.SetVisible(true);
            // Close burger menu but leave SQL terminal open —
            // player may want both clue board and terminal visible
            if (isMenuOpen) OnBurgerMenuClicked();
            // Also close hint popup so it doesn't float over the clue board
            DialogueManager.Instance?.CloseHintPopupPublic();
        };
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

        // Connect clue board
        ClueBoardManager.Instance?.ConnectToUI(uiDocument);

        // Parallax — query layers and start listening to mouse
        _sceneBg = root.Q("scene-bg");
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
        ApplyTranslate(_sceneBg, -_parallaxCurrent.x * BgDepth, -_parallaxCurrent.y * BgDepth);
        ApplyTranslate(_speakerPortrait, -_parallaxCurrent.x * PortDepth, -_parallaxCurrent.y * PortDepth);
    }

    private void OnMouseMove(MouseMoveEvent e)
    {
        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;
        float nx = (e.localMousePosition.x / layout.width - 0.5f) * 2f;
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
        else _root?.Focus();
    }

    private void OnHistoryClicked()
    {
        isHistoryOpen = !isHistoryOpen;
        if (_historyOverlay == null) return;
        _historyOverlay.style.display = isHistoryOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isHistoryOpen)
        {
            PopulateHistory();
            _historyOverlay.BringToFront();
        }
        else _root?.Focus();
    }

    private void CloseHistory()
    {
        isHistoryOpen = false;
        if (_historyOverlay != null) _historyOverlay.style.display = DisplayStyle.None;
        _root?.Focus();
    }

    private void PopulateHistory()
    {
        if (_historyContent == null) return;
        _historyContent.Clear();

        var history = DialogueManager.Instance?.GetHistory();
        if (history == null || history.Count == 0)
        {
            var empty = new UnityEngine.UIElements.Label("No dialogue yet.");
            empty.AddToClassList("history-text");
            empty.style.opacity = 0.5f;
            _historyContent.Add(empty);
            return;
        }

        // Show most recent at top
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var (speaker, text) = history[i];
            var entry = new UnityEngine.UIElements.VisualElement();
            entry.AddToClassList("history-entry");

            var speakerLbl = new UnityEngine.UIElements.Label(speaker);
            speakerLbl.AddToClassList("history-speaker");

            var textLbl = new UnityEngine.UIElements.Label(text);
            textLbl.AddToClassList("history-text");

            entry.Add(speakerLbl);
            entry.Add(textLbl);
            _historyContent.Add(entry);
        }
    }

    private void CloseTutorial()
    {
        isTutorialOpen = false;
        if (_tutorialOverlay != null) _tutorialOverlay.style.display = DisplayStyle.None;
        _root?.Focus();
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
        _root?.Focus();
    }

    // ─── Burger menu ──────────────────────────────────────────────────────────

    /// <summary>Force-closes the burger menu and SQL panel. Called by DialogueManager on cutscene start.</summary>
    public void ForceCloseBurgerMenu()
    {
        if (!isMenuOpen && !isInputMenuOpen) return;
        isMenuOpen = false;
        isInputMenuOpen = false;
        if (burgerMenuDropdown != null)
            burgerMenuDropdown.style.display = DisplayStyle.None;
        if (querieInputMenu != null)
            querieInputMenu.style.display = DisplayStyle.None;
        CloseHistory();
    }

    private void OnBurgerMenuClicked()
    {
        if (burgerMenuDropdown == null) return;
        isMenuOpen = !isMenuOpen;
        burgerMenuDropdown.style.display = isMenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (isMenuOpen) burgerMenuDropdown.BringToFront();

        // Only close SQL panel if it was opened via the burger menu toggle,
        // not if the player has it open for a task
        bool sqlOpenedManually = isInputMenuOpen && !_awaitingSQL;
        if (!isMenuOpen && sqlOpenedManually)
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
        Debug.Log("[GameUIManager] Save & Exit clicked — saving and returning to main menu.");
        GameManager.Instance?.ReturnToMainMenu();
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

        string playerName = GameManager.Instance.ActiveProfileName;
        var nodes = Case01Story.Build(playerName);

        // Use the saved dialogue node index for exact resume position.
        // Falls back to task-based resume if dialogue_node is 0 (older saves).
        int dialogueNodeIndex = CaseManager.Instance.GetDialogueNode(profileId, activeCase.CaseId);

        if (dialogueNodeIndex == 0)
        {
            // Legacy fallback — convert task index to dialogue node
            int savedTaskIndex = CaseManager.Instance.GetCurrentTaskIndex(profileId, activeCase.CaseId);
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
                            dialogueNodeIndex = i + 1;
                            break;
                        }
                    }
                }
            }
        }

        Debug.Log($"[GameUIManager] Resuming at dialogue node {dialogueNodeIndex}");
        DialogueManager.Instance.StartStory(nodes, dialogueNodeIndex);
    }
}