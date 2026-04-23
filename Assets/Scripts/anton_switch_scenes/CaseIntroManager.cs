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
    private UIDocument uiDocument;

    private Label caseTitle;
    private TextField playerNameField;
    private Button confirmButton;

    // How long the fade-in takes in seconds
    private const float FADE_DURATION = 2.0f;

    // How long to pause after the fade before showing the input
    private const float PAUSE_AFTER_FADE = 0.5f;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[CaseIntroManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Query elements by the names you set in UI Builder
        caseTitle = root.Q<Label>("CaseTitle");
        playerNameField = root.Q<TextField>("FieldPlayerName");
        confirmButton = root.Q<Button>("PlayerConfirmName");

        if (caseTitle == null) Debug.LogError("[CaseIntroManager] 'CaseTitle' not found.");
        if (playerNameField == null) Debug.LogError("[CaseIntroManager] 'FieldPlayerName' not found.");
        if (confirmButton == null) Debug.LogError("[CaseIntroManager] 'PlayerConfirmName' not found.");

        // Set the case title text from the active case definition
        // Falls back to a placeholder if no case is set yet
        if (caseTitle != null)
        {
            string title = CaseManager.Instance?.ActiveCase?.Title ?? "Case File — Unknown";
            caseTitle.text = title;
        }

        // Hide input elements immediately — they appear after the fade
        SetInputVisible(false, instant: true);

        // Wire confirm button
        confirmButton?.RegisterCallback<ClickEvent>(OnConfirmClicked);

        // Allow pressing Enter to confirm as well
        playerNameField?.RegisterCallback<KeyDownEvent>(OnKeyDown);

        // Start the fade sequence
        StartCoroutine(FadeInSequence());
    }

    private void OnDisable()
    {
        confirmButton?.UnregisterCallback<ClickEvent>(OnConfirmClicked);
        playerNameField?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
    }

    // =========================================================================
    // FADE SEQUENCE
    // =========================================================================

    private IEnumerator FadeInSequence()
    {
        if (caseTitle == null) yield break;

        // Start fully transparent
        caseTitle.style.opacity = 0f;

        // Small delay before starting — lets the scene fully load
        yield return new WaitForSeconds(0.3f);

        // Fade the title in over FADE_DURATION seconds
        float elapsed = 0f;
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FADE_DURATION);
            caseTitle.style.opacity = t;
            yield return null;
        }

        // Ensure it ends fully opaque
        caseTitle.style.opacity = 1f;

        // Brief pause before showing the input
        yield return new WaitForSeconds(PAUSE_AFTER_FADE);

        // Reveal the name input
        SetInputVisible(true, instant: false);

        // Auto-focus the text field so the player can type immediately
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
        if (playerNameField == null || confirmButton == null) return;

        DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        playerNameField.style.display = display;
        confirmButton.style.display = display;

        // If showing, fade them in from transparent unless instant
        if (visible && !instant)
        {
            playerNameField.style.opacity = 0f;
            confirmButton.style.opacity = 0f;
            StartCoroutine(FadeInElement(playerNameField, 0.6f));
            StartCoroutine(FadeInElement(confirmButton, 0.6f));
        }
        else if (instant)
        {
            playerNameField.style.opacity = visible ? 1f : 0f;
            confirmButton.style.opacity = visible ? 1f : 0f;
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