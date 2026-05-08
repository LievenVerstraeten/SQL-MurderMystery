// TextSizeManager.cs
// Cycles through three text size presets (Normal → Large → Extra Large → Normal).
// Applies the size by toggling a USS class on the root visual element so all
// text inherits the change via the USS cascade.
// Persists the choice across sessions with PlayerPrefs.
//
// SETUP:
//   1. Attach to the same persistent GameObject as GameManager.
//
//   2. Add these classes to GameUI.uss — adjust the selectors to match
//      whichever text elements you want to scale:
//
//        .text-size-large .dialogue-text  { font-size: 18px; }
//        .text-size-large .task-label     { font-size: 18px; }
//        .text-size-large .sql-output-text{ font-size: 18px; }
//        .text-size-large .card-body      { font-size: 18px; }
//        .text-size-large .speaker-name   { font-size: 18px; }
//
//        .text-size-xl .dialogue-text     { font-size: 22px; }
//        .text-size-xl .task-label        { font-size: 22px; }
//        .text-size-xl .sql-output-text   { font-size: 22px; }
//        .text-size-xl .card-body         { font-size: 22px; }
//        .text-size-xl .speaker-name      { font-size: 22px; }
//
//   3. Button in header-right in GameUI.uxml (icon version, no text):
//
//        <ui:Button name="text-size-button" class="icon-button" tooltip="Text Size">
//            <ui:VisualElement class="btn-icon"
//                style="background-image: url('project://database/Assets/Textures/Graphics/icons/UI_TextSize.png');"/>
//        </ui:Button>

using UnityEngine;
using UnityEngine.UIElements;

public class TextSizeManager : MonoBehaviour
{
    public static TextSizeManager Instance { get; private set; }

    private const string PREFS_KEY = "text_size_index";
    private const string CLASS_LARGE = "text-size-large";
    private const string CLASS_XL = "text-size-xl";

    private enum TextSize { Normal = 0, Large = 1, ExtraLarge = 2 }

    private TextSize _current;
    private Button _button;
    private VisualElement _root;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _current = (TextSize)PlayerPrefs.GetInt(PREFS_KEY, 0);
    }

    public void ConnectToUI(UIDocument uiDocument)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        if (_button != null)
            _button.clicked -= CycleSize;

        _root = uiDocument.rootVisualElement;
        _button = _root.Q<Button>("text-size-button");

        if (_button == null)
        {
            Debug.LogWarning("[TextSizeManager] 'text-size-button' not found.");
            return;
        }

        _button.clicked += CycleSize;
        ApplySize();
        Debug.Log($"[TextSizeManager] Connected. Size = {_current}");
    }

    public void CycleSize()
    {
        _current = (TextSize)(((int)_current + 1) % 3);
        PlayerPrefs.SetInt(PREFS_KEY, (int)_current);
        PlayerPrefs.Save();
        ApplySize();
        Debug.Log($"[TextSizeManager] Cycled to {_current}. " +
                  $"Root classes: {string.Join(", ", _root.GetClasses())}");
    }

    private void ApplySize()
    {
        if (_root == null) return;

        _root.RemoveFromClassList(CLASS_LARGE);
        _root.RemoveFromClassList(CLASS_XL);

        switch (_current)
        {
            case TextSize.Large: _root.AddToClassList(CLASS_LARGE); break;
            case TextSize.ExtraLarge: _root.AddToClassList(CLASS_XL); break;
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.clicked -= CycleSize;
    }
}