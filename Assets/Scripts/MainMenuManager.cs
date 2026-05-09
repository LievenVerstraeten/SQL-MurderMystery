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
    // Parallax depth — how many pixels each layer shifts at full mouse offset
    private const float BgDepth     = 18f;
    private const float LogoDepth   = 32f;
    private const float BtnDepth    = 12f;
    private const float LerpSpeed   = 5f;

    private UIDocument uiDocument;

    private Button newGameButton;
    private Button loadGameButton;
    private Button exitButton;
    private Button creditsButton;
    private VisualElement creditsPanel;
    private Button creditsBackButton;

    private VisualElement _bgLayer;
    private VisualElement _logoWrap;
    private VisualElement _buttonContainer;

    private Vector2 _parallaxTarget;
    private Vector2 _parallaxCurrent;
    private bool    _mouseTracking;

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

        // Parallax layers
        _bgLayer         = root.Q<VisualElement>("bg-layer");
        _logoWrap        = root.Q<VisualElement>("logo-wrap");
        _buttonContainer = root.Q<VisualElement>("button-container");

        root.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        root.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

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
        newGameButton?.UnregisterCallback<ClickEvent>(OnNewGameClicked);
        loadGameButton?.UnregisterCallback<ClickEvent>(OnLoadGameClicked);
        exitButton?.UnregisterCallback<ClickEvent>(OnExitClicked);
        creditsButton?.UnregisterCallback<ClickEvent>(OnCreditsClicked);
        creditsBackButton?.UnregisterCallback<ClickEvent>(OnCreditsBackClicked);

        var root = uiDocument?.rootVisualElement;
        root?.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
        root?.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
    }

    // ─── Parallax ─────────────────────────────────────────────────────────────

    private void OnMouseMove(MouseMoveEvent e)
    {
        var layout = uiDocument.rootVisualElement.layout;
        // Normalise to -1..1 from screen centre
        float nx = (e.localMousePosition.x / layout.width  - 0.5f) * 2f;
        float ny = (e.localMousePosition.y / layout.height - 0.5f) * 2f;
        _parallaxTarget = new Vector2(nx, ny);
        _mouseTracking  = true;
    }

    private void OnMouseLeave(MouseLeaveEvent e)
    {
        _parallaxTarget = Vector2.zero;
    }

    private void Update()
    {
        if (!_mouseTracking && _parallaxCurrent == Vector2.zero) return;

        _parallaxCurrent = Vector2.Lerp(_parallaxCurrent, _parallaxTarget, Time.deltaTime * LerpSpeed);

        // Each layer shifts opposite to mouse (background "recedes", logo "floats closer")
        ApplyTranslate(_bgLayer,         -_parallaxCurrent.x * BgDepth,   -_parallaxCurrent.y * BgDepth);
        ApplyTranslate(_logoWrap,        -_parallaxCurrent.x * LogoDepth, -_parallaxCurrent.y * LogoDepth);
        ApplyTranslate(_buttonContainer, -_parallaxCurrent.x * BtnDepth,  -_parallaxCurrent.y * BtnDepth);
    }

    private static void ApplyTranslate(VisualElement el, float x, float y)
    {
        if (el == null) return;
        el.style.translate = new StyleTranslate(new Translate(
            new Length(x, LengthUnit.Pixel),
            new Length(y, LengthUnit.Pixel)
        ));
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