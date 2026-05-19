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

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private AudioSource _dialogueAudio;

    private AudioClip _sfxDebbie;
    private AudioClip _sfxPlayer;
    private AudioClip _sfxSystem;

    private static readonly string[] PortraitClasses =
    {
        "portrait-debbie", "portrait-debbie-mad",
        "portrait-detective", "portrait-detective-smile", "portrait-detective-frown",
        "portrait-detective-doubt", "portrait-detective-stubborn", "portrait-detective-proud",
        "portrait-jessica", "portrait-jessica-screaming",
        "portrait-neil", "portrait-neil-speaking",
        "portrait-timehound", "portrait-timehound-speaking", "portrait-timehound-happy",
    };

    private List<DialogueNode> _story;
    private int _nodeIndex = 0;
    private bool _awaitingSQL = false;
    private bool _bgFading = false;
    private bool _bgInitialized = false;

    private VisualElement _sceneBg;
    private VisualElement _sceneFade;

    private static readonly WaitForSeconds WaitFadeSwap = new WaitForSeconds(0.05f);
    private static readonly WaitForSeconds WaitEndCredits = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds WaitIdleCluck = new WaitForSeconds(30f);
    private static readonly WaitForSeconds WaitIdleCluckHold = new WaitForSeconds(7f);

    private static readonly string[] BgClasses =
    {
        "bg-first-scene", "bg-wormhole", "bg-thinking",
        "bg-somerton-beach", "bg-timehound",
        "bg-jessica-outside", "bg-jessica-interior", "bg-after-jessica",
        "bg-neil-interview", "bg-cleland", "bg-escape-room", "bg-coffee",
    };

    //typewriter state here
    private Coroutine _typewriterCoroutine;
    private bool _isTyping;
    private string _fullText;
    private Label _activeTypewriterLabel;
    private bool _typewriterSkipGuard;

    private const float TypewriterCharsPerSec = 40f;

    private VisualElement _dialogueLayer;
    private VisualElement _cutsceneLayer;
    private VisualElement _speakerPortrait;
    private Label _speakerNameLbl;
    private Label _dialogueTextLbl;
    private Button _continueBtn;
    private Label _cutsceneTextLbl;
    private Button _cutsceneContinueBtn;
    private VisualElement _taskBanner;
    private Label _taskLbl;
    private Button _debbieHintBtn;
    private VisualElement _hintPopup;
    private Label _hintPopupText;
    private Button _hintPopupClose;

    private Coroutine _idleCluckCoroutine;

    // ── Dialogue history ──────────────────────────────────────────────────────
    private readonly List<(string speaker, string text)> _history = new();

    private static readonly string[] IdleClucks =
    {
        "Cluck.",
        "Still thinking, are we?",
        "The data will not query itself, {name}.",
        "I have seen faster detectives in 1948.",
        "Cluck cluck cluck.",
        "Perhaps try reading the task label again.",
        "* taps beak impatiently *",
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_dialogueAudio == null)
            _dialogueAudio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _dialogueAudio.loop = true;
        _dialogueAudio.playOnAwake = false;
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
        if (_idleCluckCoroutine != null)
        {
            StopCoroutine(_idleCluckCoroutine);
            _idleCluckCoroutine = null;
        }
    }

    private void BindUI(VisualElement root)
    {
        _sceneBg = root.Q("scene-bg");
        _sceneFade = root.Q("scene-fade");

        _dialogueLayer = root.Q("dialogue-layer");
        _cutsceneLayer = root.Q("cutscene-layer");
        _speakerPortrait = root.Q("speaker-portrait");
        _speakerNameLbl = root.Q<Label>("speaker-name");
        _dialogueTextLbl = root.Q<Label>("dialogue-text");
        _continueBtn = root.Q<Button>("continue-btn");
        _cutsceneTextLbl = root.Q<Label>("cutscene-text");
        _cutsceneContinueBtn = root.Q<Button>("cutscene-continue");
        _taskBanner = root.Q("task-banner");
        _taskLbl = root.Q<Label>("task-label");
        _debbieHintBtn = root.Q<Button>("debbie-hint-btn");

        _hintPopup = root.Q("hint-popup");
        _hintPopupText = root.Q<Label>("hint-popup-text");
        _hintPopupClose = root.Q<Button>("hint-popup-close");

        if (_continueBtn != null) _continueBtn.clicked += OnContinueClicked;
        if (_cutsceneContinueBtn != null) _cutsceneContinueBtn.clicked += OnContinueClicked;
        if (_debbieHintBtn != null) _debbieHintBtn.clicked += OnDebbieHintClicked;
        if (_hintPopupClose != null) _hintPopupClose.clicked += CloseHintPopup;

        // Clicking anywhere on the dialogue layer or cutscene layer advances the story
        _dialogueLayer?.RegisterCallback<ClickEvent>(_ => OnContinueClicked());
        _cutsceneLayer?.RegisterCallback<ClickEvent>(_ => OnContinueClicked());

        // Register at TrickleDown phase on the panel root so keyboard
        // events are captured regardless of which element has focus.
        // This means Space/Enter always works even after clicking buttons or overlays.
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.focusable = true;
        root.Focus();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        // Only Space or Enter advance dialogue
        if (evt.keyCode != KeyCode.Space &&
            evt.keyCode != KeyCode.Return &&
            evt.keyCode != KeyCode.KeypadEnter) return;

        // Never advance while SQL task is active
        if (_awaitingSQL) return;

        // Never advance if a text field has focus
        var focused = uiDocument?.rootVisualElement?.focusController?.focusedElement;
        if (focused is TextField) return;

        // Never advance if any overlay is open
        var root = uiDocument?.rootVisualElement;
        if (root != null)
        {
            // Check all named overlays
            bool tutorialOpen = root.Q("tutorial-overlay")?.resolvedStyle.display == DisplayStyle.Flex;
            bool profileOpen = root.Q("profile-overlay")?.resolvedStyle.display == DisplayStyle.Flex;
            bool erdOpen = root.Q("erd-overlay")?.resolvedStyle.display == DisplayStyle.Flex;
            bool boardOpen = ClueBoardManager.Instance != null && ClueBoardManager.Instance.IsVisible();
            if (tutorialOpen || profileOpen || erdOpen || boardOpen) return;
        }

        OnContinueClicked();
    }

    private void UnbindUI()
    {
        if (_continueBtn != null) _continueBtn.clicked -= OnContinueClicked;
        if (_cutsceneContinueBtn != null) _cutsceneContinueBtn.clicked -= OnContinueClicked;
        if (_debbieHintBtn != null) _debbieHintBtn.clicked -= OnDebbieHintClicked;
        if (_hintPopupClose != null) _hintPopupClose.clicked -= CloseHintPopup;
        // Unregister keyboard so it doesn't fire twice if ConnectToUI is called again
        uiDocument?.rootVisualElement?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
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
        ClearHistory();

        // Pre-populate history with all dialogue nodes up to startIndex
        // so resume shows correct history
        for (int i = 0; i < startIndex && i < story.Count; i++)
            if (story[i].Type == NodeType.Dialogue)
                _history.Add((story[i].Speaker, story[i].Text));

        _story = story;
        _nodeIndex = startIndex;
        _awaitingSQL = false;
        _bgFading = false;
        _bgInitialized = false;

        // Restore the correct background when resuming a saved game mid-story
        if (startIndex > 0)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                var n = story[i];
                string key = n.Type == NodeType.Background ? n.BackgroundKey :
                             n.Type == NodeType.Cutscene ? n.BackgroundKey : null;
                if (key != null)
                {
                    ApplyBackgroundClass(key);
                    _bgInitialized = true;
                    StartCoroutine(FadeSceneIn(0.4f));
                    break;
                }
            }
        }

        ShowNode();
    }

    public int GetNodeIndex() => _nodeIndex;
    public void SetNodeIndex(int index) { _nodeIndex = index; }

    private void ShowNode()
    {
        if (_story == null || _nodeIndex >= _story.Count)
        {
            HideAll();
            StartCoroutine(FadeToMainMenu());
            return;
        }

        var node = _story[_nodeIndex];

        // Ensure scene-fade is faded out for any non-bg node (handles edge cases)
        if (node.Type != NodeType.Background) EnsureFadedIn();

        switch (node.Type)
        {
            case NodeType.Dialogue: ShowDialogue(node); break;
            case NodeType.SQLTask: ShowTask(node); break;
            case NodeType.DemoSQL: RunDemo(node); break;
            case NodeType.Cutscene: ShowCutscene(node); break;
            case NodeType.InventoryAdd: TriggerInventory(node); break;
            case NodeType.Background: StartCoroutine(CrossFadeBackground(node.BackgroundKey)); break;
        }
    }

    private void ShowDialogue(DialogueNode node)
    {
        // Record in history
        _history.Add((node.Speaker, node.Text));

        // Close the terminal in case a Demo node left it open
        UIDatabase.Instance?.CloseTerminal();

        SetLayerVisible(_dialogueLayer, true);
        SetLayerVisible(_cutsceneLayer, false);
        SetLayerVisible(_taskBanner, false);
        SetButtonVisible(_continueBtn, true);

        if (_speakerNameLbl != null) _speakerNameLbl.text = node.Speaker;
        if (_dialogueTextLbl != null) StartTypewriter(_dialogueTextLbl, node.Text, node.Speaker);

        if (_speakerPortrait != null)
        {
            foreach (var cls in PortraitClasses)
                _speakerPortrait.RemoveFromClassList(cls);

            string playerName = GameManager.Instance?.ActiveProfileName ?? "";

            string portraitClass;
            if (node.Portrait != null)
            {
                // Explicit expression set on this node
                portraitClass = node.Portrait;
            }
            else
            {
                // Default portrait per speaker
                portraitClass = node.Speaker switch
                {
                    "Debbie" => "portrait-debbie",
                    "Jessica" => "portrait-jessica",
                    "Neil" => "portrait-neil",
                    "TimeHound" => "portrait-timehound",
                    _ => node.Speaker == playerName ? "portrait-detective" : null,
                };
            }

            if (portraitClass != null)
                _speakerPortrait.AddToClassList(portraitClass);
        }
    }

    private void ShowTask(DialogueNode node)
    {
        SkipTypewriter();
        SetLayerVisible(_taskBanner, true);
        SetButtonVisible(_continueBtn, false);

        if (_taskLbl != null) _taskLbl.text = node.TaskLabel;

        // Keep the dialogue layer and portrait visible during SQL tasks.
        // When resuming from a save, the last dialogue node before this task
        // is looked up so the speaker and portrait are correctly restored.
        SetLayerVisible(_dialogueLayer, true);

        // Find the last Dialogue node before this task to restore speaker/portrait
        for (int i = _nodeIndex - 1; i >= 0; i--)
        {
            var prev = _story[i];
            if (prev.Type == NodeType.Dialogue)
            {
                // Restore speaker name
                if (_speakerNameLbl != null)
                    _speakerNameLbl.text = prev.Speaker;

                // Restore dialogue text
                if (_dialogueTextLbl != null)
                    _dialogueTextLbl.text = prev.Text;

                // Restore portrait
                if (_speakerPortrait != null)
                {
                    foreach (var cls in PortraitClasses)
                        _speakerPortrait.RemoveFromClassList(cls);

                    string playerName = GameManager.Instance?.ActiveProfileName ?? "";
                    string portraitClass = prev.Portrait ?? prev.Speaker switch
                    {
                        "Debbie" => "portrait-debbie",
                        "Jessica" => "portrait-jessica",
                        "Neil" => "portrait-neil",
                        "TimeHound" => "portrait-timehound",
                        _ => prev.Speaker == playerName ? "portrait-detective" : null,
                    };
                    if (portraitClass != null)
                        _speakerPortrait.AddToClassList(portraitClass);
                }

                // Hide the continue button — player must solve the SQL task
                SetButtonVisible(_continueBtn, false);
                break;
            }
        }

        if (UIDatabase.Instance != null) UIDatabase.Instance.OpenTerminal();
        _awaitingSQL = true;

        // Idle cluck — Debbie gets impatient after 30 s of no submission
        if (_idleCluckCoroutine != null) StopCoroutine(_idleCluckCoroutine);
        _idleCluckCoroutine = StartCoroutine(IdleCluckRoutine());
    }

    private void RunDemo(DialogueNode node)
    {
        // Auto-run the SQL and show in terminal; advance immediately after
        UIDatabase.Instance?.RunDemo(node.DemoSQL, node.DemoLabel);
        Advance();
    }

    private string _activeCutsceneImageClass;

    private void ShowCutscene(DialogueNode node)
    {
        // Close the burger menu so it doesn't float above the cutscene
        GameUIManager.Instance?.ForceCloseBurgerMenu();

        SetLayerVisible(_dialogueLayer, false);
        SetLayerVisible(_cutsceneLayer, true);
        SetLayerVisible(_taskBanner, false);

        // Clean up any image classes from a previous image cutscene
        if (_cutsceneLayer != null)
        {
            _cutsceneLayer.RemoveFromClassList("cutscene-layer--image");
            if (_activeCutsceneImageClass != null)
            {
                _cutsceneLayer.RemoveFromClassList(_activeCutsceneImageClass);
                _activeCutsceneImageClass = null;
            }
        }
        _cutsceneTextLbl?.RemoveFromClassList("cutscene-text--subtitle");

        bool hasImage = !string.IsNullOrEmpty(node.CutsceneImageKey);
        if (hasImage)
        {
            _activeCutsceneImageClass = $"cutscene-img-{node.CutsceneImageKey}";
            _cutsceneLayer?.AddToClassList("cutscene-layer--image");
            _cutsceneLayer?.AddToClassList(_activeCutsceneImageClass);
            _cutsceneTextLbl?.AddToClassList("cutscene-text--subtitle");
            SetButtonVisible(_cutsceneContinueBtn, false);
        }
        else
        {
            SetButtonVisible(_cutsceneContinueBtn, true);
        }

        if (_cutsceneTextLbl != null) StartTypewriter(_cutsceneTextLbl, node.CutsceneText);
        if (_cutsceneLayer != null)
            _cutsceneLayer.style.backgroundColor = new StyleColor(node.CutsceneColor);

        if (!string.IsNullOrEmpty(node.BackgroundKey))
            ApplyBackgroundClass(node.BackgroundKey);
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
        SetLayerVisible(_taskBanner, false);
    }

    private void OnContinueClicked()
    {
        // Return focus to root so keyboard advance keeps working
        uiDocument?.rootVisualElement?.Focus();

        if (_awaitingSQL || _bgFading) return;

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

        // Save before ShowNode — ShowNode can call Advance() again via coroutines
        // so saving after would record the wrong index
        if (GameManager.Instance != null && CaseManager.Instance?.ActiveCase != null)
        {
            CaseManager.Instance.SaveDialogueNode(
                GameManager.Instance.ActiveProfileId,
                CaseManager.Instance.ActiveCase.CaseId,
                _nodeIndex);
            GameManager.Instance.AutoSave("story_advance");
        }

        ShowNode();
    }

    private void OnSQLValidated(TaskValidator.ValidationResult result)
    {
        if (!_awaitingSQL) return;

        if (result.Passed)
        {
            _awaitingSQL = false;
            if (_idleCluckCoroutine != null) { StopCoroutine(_idleCluckCoroutine); _idleCluckCoroutine = null; }
            CloseHintPopup();
            SetLayerVisible(_taskBanner, false);
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

        if (_hintPopupText != null) _hintPopupText.text = $"Cluck! {hint}";
        if (_hintPopup != null) _hintPopup.style.display = DisplayStyle.Flex;
    }

    private void CloseHintPopup()
    {
        if (_hintPopup != null) _hintPopup.style.display = DisplayStyle.None;
    }

    /// <summary>Public version — called when clue board opens so hint doesn't float over it.</summary>
    public void CloseHintPopupPublic() => CloseHintPopup();

    /// <summary>Returns a copy of the dialogue history for the history overlay.</summary>
    public List<(string speaker, string text)> GetHistory() => new(_history);

    /// <summary>Clears history — called when a new story starts.</summary>
    private void ClearHistory() => _history.Clear();

    private void StartTypewriter(Label label, string text, string speaker = "")
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _fullText = text;
        _activeTypewriterLabel = label;
        _typewriterSkipGuard = false;

        PlayTypewriterSound(speaker);

        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(label, text));
    }

    private IEnumerator TypewriterRoutine(Label label, string text)
    {
        _isTyping = true;
        label.text = "";
        var delay = new WaitForSeconds(1f / TypewriterCharsPerSec);
        for (int i = 0; i < text.Length; i++)
        {
            label.text = text[..(i + 1)] + "|";
            yield return delay;
        }
        label.text = text;
        _isTyping = false;
        _typewriterCoroutine = null;
        _dialogueAudio?.Stop();
    }

    private void SkipTypewriter()
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = null;
        _isTyping = false;
        if (_activeTypewriterLabel != null)
            _activeTypewriterLabel.text = _fullText;
        _dialogueAudio?.Stop();
    }

    private IEnumerator IdleCluckRoutine()
    {
        // FLOW:
        // 1. Wait 30 seconds (timer runs regardless of terminal open/closed)
        // 2. When 30s elapsed, mark hint as pending
        // 3. As soon as terminal opens (or if already open), show hint immediately
        // 4. Hint stays visible for 7 seconds regardless of terminal open/closed
        // 5. Close hint, reset 30s timer, repeat

        while (_awaitingSQL)
        {
            // ── Step 1: Wait 30 seconds regardless of terminal state ──────────
            yield return WaitIdleCluck;
            if (!_awaitingSQL) break;

            // ── Step 2: Hint is now pending — wait for terminal to be open ────
            // If terminal already open, show immediately.
            // If closed, wait until player opens it.
            while (_awaitingSQL)
            {
                bool terminalOpen = UIDatabase.Instance != null && UIDatabase.Instance.IsTerminalOpen();
                bool clueBoardOpen = ClueBoardManager.Instance != null && ClueBoardManager.Instance.IsVisible();
                if (terminalOpen && !clueBoardOpen) break;
                yield return null;
            }
            if (!_awaitingSQL) break;

            // ── Step 3: Show hint ─────────────────────────────────────────────
            string line = IdleClucks[Random.Range(0, IdleClucks.Length)];
            line = line.Replace("{name}", GameManager.Instance != null
                ? GameManager.Instance.ActiveProfileName : "detective");
            if (_hintPopupText != null) _hintPopupText.text = line;
            if (_hintPopup != null) _hintPopup.style.display = DisplayStyle.Flex;

            // ── Step 4: Keep hint for 7 seconds regardless of terminal state ──
            yield return WaitIdleCluckHold;

            // ── Step 5: Close and reset ───────────────────────────────────────
            CloseHintPopup();
        }
        _idleCluckCoroutine = null;
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

    // =========================================================================
    // BACKGROUND MANAGEMENT
    // =========================================================================

    private void ApplyBackgroundClass(string key)
    {
        if (_sceneBg == null || string.IsNullOrEmpty(key)) return;
        foreach (var cls in BgClasses) _sceneBg.RemoveFromClassList(cls);
        _sceneBg.AddToClassList($"bg-{key}");
    }

    private void EnsureFadedIn()
    {
        if (_bgInitialized) return;
        _bgInitialized = true;
        StartCoroutine(FadeSceneIn(0.4f));
    }

    private IEnumerator FadeSceneIn(float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            SetFadeOpacity(1f - t / dur);
            yield return null;
        }
        SetFadeOpacity(0f);
    }

    private IEnumerator CrossFadeBackground(string key)
    {
        _bgFading = true;

        if (!_bgInitialized)
        {
            // First background: apply instantly then fade in from black
            _bgInitialized = true;
            ApplyBackgroundClass(key);
            yield return StartCoroutine(FadeSceneIn(0.4f));
            _bgFading = false;
            Advance();
            yield break;
        }

        // Subsequent backgrounds: fade to black, swap, fade back
        const float half = 0.28f;
        for (float t = 0f; t < half; t += Time.deltaTime) { SetFadeOpacity(t / half); yield return null; }
        SetFadeOpacity(1f);

        ApplyBackgroundClass(key);
        yield return WaitFadeSwap;

        for (float t = 0f; t < half; t += Time.deltaTime) { SetFadeOpacity(1f - t / half); yield return null; }
        SetFadeOpacity(0f);

        _bgFading = false;
        Advance();
    }

    private void SetFadeOpacity(float v)
    {
        if (_sceneFade != null) _sceneFade.style.opacity = Mathf.Clamp01(v);
    }

    private IEnumerator FadeToMainMenu()
    {
        const float dur = 1.2f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            SetFadeOpacity(t / dur);
            yield return null;
        }
        SetFadeOpacity(1f);
        yield return WaitEndCredits;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}