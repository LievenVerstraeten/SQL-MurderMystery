// CaseIntroManager.cs
// Handles the case intro scene:
//   1. CaseTitle fades in from transparent to full opacity
//   2. Once the fade is complete, FieldPlayerName and PlayerConfirmName appear
//   3. Player types their detective name and confirms
//   4. Calls GameManager.ConfirmNewGame(playerName) to create the profile
//
// SETUP:
//   Attach to the GameObject that has your UIDocument component in the intro scene.
//   Your UXML must have:
//     - A Label named "CaseTitle"
//     - A TextField named "FieldPlayerName"
//     - A Button named "PlayerConfirmName"

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class CaseIntroManager : MonoBehaviour
{
    private const float FADE_DURATION    = 0.6f;
    private const float PAUSE_AFTER_FADE = 0.2f;

    // Parallax depths (pixels at full mouse offset)
    private const float BgDepth    = 18f;
    private const float TitleDepth = 36f;
    private const float InputDepth = 12f;
    private const float LerpSpeed  = 5f;

    private UIDocument uiDocument;

    // Functional elements
    private TextField  playerNameField;
    private Button     confirmButton;
    private Button     backButton;

    // Parallax layers
    private VisualElement _bgLayer;
    private VisualElement _titleImg;
    private VisualElement _inputArea;

    private Vector2 _parallaxTarget;
    private Vector2 _parallaxCurrent;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[CaseIntroManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        playerNameField = root.Q<TextField>("FieldPlayerName");
        confirmButton   = root.Q<Button>("PlayerConfirmName");
        backButton      = root.Q<Button>("BackButton");

        _bgLayer   = root.Q<VisualElement>("bg-layer");
        _titleImg  = root.Q<VisualElement>("title-img");
        _inputArea = root.Q<VisualElement>("input-area");

        if (playerNameField == null) Debug.LogError("[CaseIntroManager] 'FieldPlayerName' not found.");
        if (confirmButton   == null) Debug.LogError("[CaseIntroManager] 'PlayerConfirmName' not found.");
        if (_titleImg       == null) Debug.LogError("[CaseIntroManager] 'title-img' not found.");

        SetInputVisible(false, instant: true);

        confirmButton?.RegisterCallback<ClickEvent>(OnConfirmClicked);
        playerNameField?.RegisterCallback<KeyDownEvent>(OnKeyDown);
        backButton?.RegisterCallback<ClickEvent>(OnBackClicked);

        root.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        root.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

        StartCoroutine(FadeInSequence());
    }

    private void OnDisable()
    {
        confirmButton?.UnregisterCallback<ClickEvent>(OnConfirmClicked);
        playerNameField?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);

        var root = uiDocument?.rootVisualElement;
        root?.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
        root?.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
    }

    // =========================================================================
    // FADE SEQUENCE
    // =========================================================================

    // ─── Parallax ─────────────────────────────────────────────────────────────

    private void OnMouseMove(MouseMoveEvent e)
    {
        var layout = uiDocument.rootVisualElement.layout;
        float nx = (e.localMousePosition.x / layout.width  - 0.5f) * 2f;
        float ny = (e.localMousePosition.y / layout.height - 0.5f) * 2f;
        _parallaxTarget = new Vector2(nx, ny);
    }

    private void OnMouseLeave(MouseLeaveEvent e) => _parallaxTarget = Vector2.zero;

    private void Update()
    {
        if (_parallaxCurrent == _parallaxTarget && _parallaxCurrent == Vector2.zero) return;

        _parallaxCurrent = Vector2.Lerp(_parallaxCurrent, _parallaxTarget, Time.deltaTime * LerpSpeed);

        ApplyTranslate(_bgLayer,   -_parallaxCurrent.x * BgDepth,    -_parallaxCurrent.y * BgDepth);
        ApplyTranslate(_titleImg,  -_parallaxCurrent.x * TitleDepth, -_parallaxCurrent.y * TitleDepth);
        ApplyTranslate(_inputArea, -_parallaxCurrent.x * InputDepth, -_parallaxCurrent.y * InputDepth);
    }

    private static void ApplyTranslate(VisualElement el, float x, float y)
    {
        if (el == null) return;
        el.style.translate = new StyleTranslate(new Translate(
            new Length(x, LengthUnit.Pixel),
            new Length(y, LengthUnit.Pixel)
        ));
    }

    // ─── Fade sequence ────────────────────────────────────────────────────────

    private IEnumerator FadeInSequence()
    {
        if (_titleImg == null) yield break;

        _titleImg.style.opacity = 0f;

        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            _titleImg.style.opacity = Mathf.Clamp01(elapsed / FADE_DURATION);
            yield return null;
        }
        _titleImg.style.opacity = 1f;

        yield return new WaitForSeconds(PAUSE_AFTER_FADE);

        SetInputVisible(true, instant: false);
        playerNameField?.Focus();
    }

    // =========================================================================
    // INPUT VISIBILITY
    // =========================================================================

    /// <summary>
    /// Shows or hides the name input field and confirm button.
    /// instant = true skips any transition (used for initial hide on load).
    /// </summary>
    private void SetInputVisible(bool visible, bool instant)
    {
        if (_inputArea == null) return;

        _inputArea.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible && !instant)
        {
            _inputArea.style.opacity = 0f;
            StartCoroutine(FadeInElement(_inputArea, 0.7f));
        }
        else
        {
            _inputArea.style.opacity = visible ? 1f : 0f;
        }
    }

    /// <summary>
    /// Fades a single VisualElement from 0 to 1 opacity over duration seconds.
    /// </summary>
    private IEnumerator FadeInElement(VisualElement element, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            element.style.opacity = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        element.style.opacity = 1f;
    }

    // =========================================================================
    // INPUT HANDLERS
    // =========================================================================

    private void OnBackClicked(ClickEvent e)
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    private void OnConfirmClicked(ClickEvent e)
    {
        SubmitName();
    }

    private void OnKeyDown(KeyDownEvent e)
    {
        // Allow Enter or Numpad Enter to confirm
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            SubmitName();
    }

    private void SubmitName()
    {
        string playerName = playerNameField?.value?.Trim() ?? "";

        if (string.IsNullOrEmpty(playerName))
        {
            return;
        }

        Debug.Log($"[CaseIntroManager] Name confirmed: {playerName}");

        // Hand off to GameManager — this creates the profile and loads the game scene
        GameManager.Instance.ConfirmNewGame(playerName);
    }
}