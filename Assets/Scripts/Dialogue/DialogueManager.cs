// DialogueManager.cs
// Drives the visual novel story loop.
//
// FLOW:
//   StartStory(nodes, startIndex) → walks through DialogueNode list in order.
//   Dialogue  → shows speaker + text in the VN box. Player clicks Continue.
//   SQLTask   → opens terminal, hides Continue, waits for TaskValidator pass.
//   DemoSQL   → auto-runs the SQL via UIDatabase, shows result, auto-advances.
//   Cutscene  → coloured fullscreen overlay. Player clicks Continue.
//   InventoryAdd → calls InventoryManager.AddItem, immediately advances.
//
// SETUP:
//   Attach to a persistent GameObject in the game scene.
//   Assign the UIDocument that contains GameUI.uxml.
//   Call DialogueManager.Instance.StartStory(Case01Story.Build(playerName))
//   from GameUIManager after the game scene loads.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // Portrait CSS classes applied to speaker-portrait element at runtime
    private static readonly string[] PortraitClasses =
    {
        "portrait-debbie", "portrait-detective", "portrait-jessica",
        "portrait-neil",   "portrait-cleland",   "portrait-timehound",
    };

    // ── Story state ───────────────────────────────────────────────────────────
    private List<DialogueNode> _story;
    private int  _nodeIndex   = 0;
    private bool _awaitingSQL = false;

    // ── UI references ─────────────────────────────────────────────────────────
    private VisualElement _dialogueLayer;
    private VisualElement _cutsceneLayer;
    private VisualElement _speakerPortrait;
    private Label         _speakerNameLbl;
    private Label         _dialogueTextLbl;
    private Button        _continueBtn;
    private Label         _cutsceneTextLbl;
    private Button        _cutsceneContinueBtn;
    private VisualElement _taskBanner;
    private Label         _taskLbl;
    private Button        _debbieHintBtn;
    private VisualElement _hintPopup;
    private Label         _hintPopupText;
    private Button        _hintPopupClose;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        BindUI(uiDocument.rootVisualElement);
        TaskValidator.OnValidationComplete += OnSQLValidated;
    }

    void OnDisable()
    {
        UnbindUI();
        TaskValidator.OnValidationComplete -= OnSQLValidated;
    }

    private void BindUI(VisualElement root)
    {
        _dialogueLayer       = root.Q("dialogue-layer");
        _cutsceneLayer       = root.Q("cutscene-layer");
        _speakerPortrait     = root.Q("speaker-portrait");
        _speakerNameLbl      = root.Q<Label>("speaker-name");
        _dialogueTextLbl     = root.Q<Label>("dialogue-text");
        _continueBtn         = root.Q<Button>("continue-btn");
        _cutsceneTextLbl     = root.Q<Label>("cutscene-text");
        _cutsceneContinueBtn = root.Q<Button>("cutscene-continue");
        _taskBanner          = root.Q("task-banner");
        _taskLbl             = root.Q<Label>("task-label");
        _debbieHintBtn       = root.Q<Button>("debbie-hint-btn");

        _hintPopup      = root.Q("hint-popup");
        _hintPopupText  = root.Q<Label>("hint-popup-text");
        _hintPopupClose = root.Q<Button>("hint-popup-close");

        if (_continueBtn         != null) _continueBtn.clicked         += OnContinueClicked;
        if (_cutsceneContinueBtn != null) _cutsceneContinueBtn.clicked += OnContinueClicked;
        if (_debbieHintBtn       != null) _debbieHintBtn.clicked       += OnDebbieHintClicked;
        if (_hintPopupClose      != null) _hintPopupClose.clicked      += CloseHintPopup;

        // Clicking anywhere on the dialogue layer or cutscene layer advances the story
        _dialogueLayer?.RegisterCallback<ClickEvent>(_ => OnContinueClicked());
        _cutsceneLayer?.RegisterCallback<ClickEvent>(_ => OnContinueClicked());
    }

    private void UnbindUI()
    {
        if (_continueBtn         != null) _continueBtn.clicked         -= OnContinueClicked;
        if (_cutsceneContinueBtn != null) _cutsceneContinueBtn.clicked -= OnContinueClicked;
        if (_debbieHintBtn       != null) _debbieHintBtn.clicked       -= OnDebbieHintClicked;
        if (_hintPopupClose      != null) _hintPopupClose.clicked      -= CloseHintPopup;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GameUIManager when the game scene loads so DialogueManager
    /// can bind to the correct UIDocument (it lives on the persistent GameSystems
    /// object and has no UIDocument of its own).
    /// </summary>
    public void ConnectToUI(UIDocument doc)
    {
        UnbindUI();
        uiDocument = doc;
        BindUI(doc.rootVisualElement);
        TaskValidator.OnValidationComplete -= OnSQLValidated;
        TaskValidator.OnValidationComplete += OnSQLValidated;
    }

    public void StartStory(List<DialogueNode> story, int startIndex = 0)
    {
        _story      = story;
        _nodeIndex  = startIndex;
        _awaitingSQL = false;
        ShowNode();
    }

    public int  GetNodeIndex() => _nodeIndex;
    public void SetNodeIndex(int index) { _nodeIndex = index; }

    // ── Story engine ──────────────────────────────────────────────────────────

    private void ShowNode()
    {
        if (_story == null || _nodeIndex >= _story.Count)
        {
            HideAll();
            return;
        }

        var node = _story[_nodeIndex];

        switch (node.Type)
        {
            case NodeType.Dialogue:    ShowDialogue(node);    break;
            case NodeType.SQLTask:     ShowTask(node);        break;
            case NodeType.DemoSQL:     RunDemo(node);         break;
            case NodeType.Cutscene:    ShowCutscene(node);    break;
            case NodeType.InventoryAdd: TriggerInventory(node); break;
        }
    }

    private void ShowDialogue(DialogueNode node)
    {
        // Close the terminal in case a Demo node left it open
        UIDatabase.Instance?.CloseTerminal();

        SetLayerVisible(_dialogueLayer,  true);
        SetLayerVisible(_cutsceneLayer,  false);
        SetLayerVisible(_taskBanner,     false);
        SetButtonVisible(_continueBtn,   true);

        if (_speakerNameLbl  != null) _speakerNameLbl.text  = node.Speaker;
        if (_dialogueTextLbl != null) _dialogueTextLbl.text = node.Text;

        // Portrait: apply CSS class matching the speaker; fallback to base dark bg
        if (_speakerPortrait != null)
        {
            foreach (var cls in PortraitClasses)
                _speakerPortrait.RemoveFromClassList(cls);

            string playerName = GameManager.Instance?.ActiveProfileName ?? "";
            string portraitClass = node.Speaker switch
            {
                "Debbie"    => "portrait-debbie",
                "Jessica"   => "portrait-jessica",
                "Neil"      => "portrait-neil",
                "Cleland"   => "portrait-cleland",
                "TimeHound" => "portrait-timehound",
                _           => node.Speaker == playerName ? "portrait-detective" : null,
            };

            if (portraitClass != null)
                _speakerPortrait.AddToClassList(portraitClass);
        }
    }

    private void ShowTask(DialogueNode node)
    {
        // Keep dialogue layer visible for context; add task banner on top
        SetLayerVisible(_taskBanner,   true);
        SetButtonVisible(_continueBtn, false);

        if (_taskLbl != null) _taskLbl.text = node.TaskLabel;

        // Open the terminal
        UIDatabase.Instance?.OpenTerminal();

        _awaitingSQL = true;
    }

    private void RunDemo(DialogueNode node)
    {
        // Auto-run the SQL and show in terminal; advance immediately after
        UIDatabase.Instance?.RunDemo(node.DemoSQL, node.DemoLabel);
        Advance();
    }

    private void ShowCutscene(DialogueNode node)
    {
        SetLayerVisible(_dialogueLayer,  false);
        SetLayerVisible(_cutsceneLayer,  true);
        SetLayerVisible(_taskBanner,     false);

        if (_cutsceneTextLbl != null) _cutsceneTextLbl.text = node.CutsceneText;
        if (_cutsceneLayer   != null)
            _cutsceneLayer.style.backgroundColor = new StyleColor(node.CutsceneColor);
    }

    private void TriggerInventory(DialogueNode node)
    {
        InventoryManager.Instance?.AddItem(node.ItemName, node.ItemDescription, node.ItemType);
        Advance();
    }

    private void HideAll()
    {
        SetLayerVisible(_dialogueLayer, false);
        SetLayerVisible(_cutsceneLayer, false);
        SetLayerVisible(_taskBanner,    false);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnContinueClicked()
    {
        if (_awaitingSQL) return;
        Advance();
    }

    private void Advance()
    {
        _nodeIndex++;
        ShowNode();

        // Auto-save story progress
        if (GameManager.Instance != null)
            GameManager.Instance.AutoSave("story_advance");
    }

    private void OnSQLValidated(TaskValidator.ValidationResult result)
    {
        if (!_awaitingSQL) return;

        if (result.Passed)
        {
            _awaitingSQL = false;
            SetLayerVisible(_taskBanner,   false);
            SetButtonVisible(_continueBtn, true);
            UIDatabase.Instance?.CloseTerminal();
            Advance();
        }
        // On failure the terminal stays open; player retries
    }

    private void OnDebbieHintClicked()
    {
        if (CaseManager.Instance?.ActiveCase == null) return;

        int taskIdx = CaseManager.Instance.GetCurrentTaskIndex(
            GameManager.Instance.ActiveProfileId,
            CaseManager.Instance.ActiveCase.CaseId);

        var tasks = CaseManager.Instance.ActiveCase.Tasks;
        if (taskIdx >= tasks.Count) return;

        string hint = tasks[taskIdx].Hint;
        if (string.IsNullOrEmpty(hint)) hint = "Trust the data. Read the task label carefully.";

        if (_hintPopupText  != null) _hintPopupText.text = $"Cluck! {hint}";
        if (_hintPopup      != null) _hintPopup.style.display = DisplayStyle.Flex;
    }

    private void CloseHintPopup()
    {
        if (_hintPopup != null) _hintPopup.style.display = DisplayStyle.None;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetLayerVisible(VisualElement el, bool visible)
    {
        if (el != null)
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void SetButtonVisible(Button btn, bool visible)
    {
        if (btn != null)
            btn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
