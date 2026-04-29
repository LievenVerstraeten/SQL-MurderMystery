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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private UIDocument  uiDocument;
    [SerializeField] private AudioSource _dialogueAudio;

    private AudioClip _sfxDebbie;
    private AudioClip _sfxPlayer;
    private AudioClip _sfxSystem;

    private static readonly string[] PortraitClasses =
    {
        "portrait-debbie", "portrait-detective", "portrait-jessica",
        "portrait-neil",   "portrait-cleland",   "portrait-timehound",
    };

    private List<DialogueNode> _story;
    private int  _nodeIndex   = 0;
    private bool _awaitingSQL = false;

//typewriter state here
    private Coroutine _typewriterCoroutine;
    private bool      _isTyping;
    private string    _fullText;
    private Label     _activeTypewriterLabel;
    private bool      _typewriterSkipGuard;

    private const float TypewriterCharsPerSec = 40f;

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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_dialogueAudio == null)
            _dialogueAudio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _dialogueAudio.loop         = true;
        _dialogueAudio.playOnAwake  = false;
        _dialogueAudio.spatialBlend = 0f; // 2D — not distance-attenuated

        _sfxDebbie = Resources.Load<AudioClip>("Dialogue_Debbie");
        _sfxPlayer = Resources.Load<AudioClip>("Dialogue_Player");
        _sfxSystem = Resources.Load<AudioClip>("Dialogue_System");

        LoadDialogueClip(_sfxDebbie, "Dialogue_Debbie");
        LoadDialogueClip(_sfxPlayer, "Dialogue_Player");
        LoadDialogueClip(_sfxSystem, "Dialogue_System");
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

        if (_speakerNameLbl  != null) _speakerNameLbl.text = node.Speaker;
        if (_dialogueTextLbl != null) StartTypewriter(_dialogueTextLbl, node.Text, node.Speaker);

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
        SkipTypewriter(); // task banner appears over dialogue, stop any running typewriter
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

        if (_cutsceneTextLbl != null) StartTypewriter(_cutsceneTextLbl, node.CutsceneText);
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

    private void OnContinueClicked()
    {
        if (_awaitingSQL) return;

        // Absorb the bubbled ClickEvent that arrives right after a skip click
        if (_typewriterSkipGuard) { _typewriterSkipGuard = false; return; }

        if (_isTyping)
        {
            SkipTypewriter();
            _typewriterSkipGuard = true; // next call in same frame is the layer bubble
            return;
        }

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

    private void StartTypewriter(Label label, string text, string speaker = "")
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _fullText              = text;
        _activeTypewriterLabel = label;
        _typewriterSkipGuard   = false;

        PlayTypewriterSound(speaker);

        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(label, text));
    }

    private IEnumerator TypewriterRoutine(Label label, string text)
    {
        _isTyping  = true;
        label.text = "";
        var delay  = new WaitForSeconds(1f / TypewriterCharsPerSec);
        for (int i = 0; i < text.Length; i++)
        {
            label.text = text[..(i + 1)];
            yield return delay;
        }
        _isTyping            = false;
        _typewriterCoroutine = null;
        _dialogueAudio?.Stop();
    }

    private void SkipTypewriter()
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = null;
        _isTyping            = false;
        if (_activeTypewriterLabel != null)
            _activeTypewriterLabel.text = _fullText;
        _dialogueAudio?.Stop();
    }

    private void PlayTypewriterSound(string speaker)
    {
        if (_dialogueAudio == null) return;
        string playerName = GameManager.Instance?.ActiveProfileName ?? "";
        AudioClip clip = speaker == "Debbie" ? _sfxDebbie
                       : speaker == playerName ? _sfxPlayer
                       : _sfxSystem;
        if (clip == null)
        {
            Debug.LogWarning($"DialogueManager could not find a typewriter sound for speaker '{speaker}'. Make sure the clip is in Assets/Resources.");
            return;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
        {
            Debug.LogWarning($"DialogueManager could not load audio data for '{clip.name}'. Enable Preload Audio Data in the clip import settings, or use a loaded clip.");
            return;
        }

        _dialogueAudio.clip = clip;
        _dialogueAudio.Play();
    }

    private static void LoadDialogueClip(AudioClip clip, string resourceName)
    {
        if (clip == null)
        {
            Debug.LogWarning($"DialogueManager could not load Resources/{resourceName}. Put the audio file under Assets/Resources and omit the file extension in code.");
            return;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

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
