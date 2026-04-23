// GameSceneManager.cs
// Manages the main game scene (GameStart).
//
// UI STRUCTURE:
//   content
//   ├── TopBar
//   │   ├── CaseLabel
//   │   ├── SuspectsBtn
//   │   └── MenuBtn
//   ├── QueryOutput
//   │   └── OutputText
//   ├── QueryText
//   ├── ExecuteQuery
//   └── ProfileName
//
//   SuspectsOverlay          (outside content — covers everything)
//   └── SuspectsOverview
//       └── SuspectsOverlayQuit
//
// SETUP:
//   Attach to the GameObject with the UIDocument component in GameStart scene.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameSceneManager : MonoBehaviour
{
    private UIDocument uiDocument;

    // ── Top bar ───────────────────────────────────────────────────────────────
    private Label caseLabel;
    private Button suspectsBtn;
    private Button menuBtn;

    // ── Query area ────────────────────────────────────────────────────────────
    private ScrollView queryOutput;
    private Label outputText;
    private TextField queryText;
    private Button executeQuery;

    // ── Bottom bar ────────────────────────────────────────────────────────────
    private Label profileName;

    // ── Suspects overlay ──────────────────────────────────────────────────────
    private VisualElement suspectsOverlay;
    private VisualElement suspectsOverview;
    private Button suspectsOverlayQuit;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[GameSceneManager] UIDocument missing or root is null.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // ── Query elements ────────────────────────────────────────────────────
        caseLabel = root.Q<Label>("CaseLabel");
        suspectsBtn = root.Q<Button>("SuspectsBtn");
        menuBtn = root.Q<Button>("MenuBtn");
        queryOutput = root.Q<ScrollView>("QueryOutput");
        outputText = root.Q<Label>("OutputText");
        queryText = root.Q<TextField>("QueryText");
        executeQuery = root.Q<Button>("ExecuteQuery");
        profileName = root.Q<Label>("ProfileName");

        // ── Overlay elements ──────────────────────────────────────────────────
        suspectsOverlay = root.Q<VisualElement>("SuspectsOverlay");
        suspectsOverview = root.Q<VisualElement>("SuspectsOverview");
        suspectsOverlayQuit = root.Q<Button>("SuspectsOverlayQuit");

        // Warn on any missing elements
        if (caseLabel == null) Debug.LogError("[GameSceneManager] 'CaseLabel' not found.");
        if (suspectsBtn == null) Debug.LogError("[GameSceneManager] 'SuspectsBtn' not found.");
        if (menuBtn == null) Debug.LogError("[GameSceneManager] 'MenuBtn' not found.");
        if (queryOutput == null) Debug.LogError("[GameSceneManager] 'QueryOutput' not found.");
        if (outputText == null) Debug.LogError("[GameSceneManager] 'OutputText' not found.");
        if (queryText == null) Debug.LogError("[GameSceneManager] 'QueryText' not found.");
        if (executeQuery == null) Debug.LogError("[GameSceneManager] 'ExecuteQuery' not found.");
        if (profileName == null) Debug.LogError("[GameSceneManager] 'ProfileName' not found.");
        if (suspectsOverlay == null) Debug.LogError("[GameSceneManager] 'SuspectsOverlay' not found.");
        if (suspectsOverview == null) Debug.LogError("[GameSceneManager] 'SuspectsOverview' not found.");
        if (suspectsOverlayQuit == null) Debug.LogError("[GameSceneManager] 'SuspectsOverlayQuit' not found.");

        // ── Initial state ─────────────────────────────────────────────────────
        SetOverlayVisible(false);
        SetInitialText();

        // ── Subscribe to TaskValidator so output updates on query result ───────
        TaskValidator.OnValidationComplete += OnValidationComplete;

        // ── Wire buttons ──────────────────────────────────────────────────────
        executeQuery?.RegisterCallback<ClickEvent>(OnExecuteClicked);
        suspectsBtn?.RegisterCallback<ClickEvent>(OnSuspectsBtnClicked);
        suspectsOverlayQuit?.RegisterCallback<ClickEvent>(OnOverlayQuitClicked);
        menuBtn?.RegisterCallback<ClickEvent>(OnMenuClicked);

        // Allow Ctrl+Enter to run query
        queryText?.RegisterCallback<KeyDownEvent>(OnQueryKeyDown);

        // Auto focus the query input
        queryText?.Focus();
    }

    private void OnDisable()
    {
        TaskValidator.OnValidationComplete -= OnValidationComplete;

        executeQuery?.UnregisterCallback<ClickEvent>(OnExecuteClicked);
        suspectsBtn?.UnregisterCallback<ClickEvent>(OnSuspectsBtnClicked);
        suspectsOverlayQuit?.UnregisterCallback<ClickEvent>(OnOverlayQuitClicked);
        menuBtn?.UnregisterCallback<ClickEvent>(OnMenuClicked);
        queryText?.UnregisterCallback<KeyDownEvent>(OnQueryKeyDown);
    }

    // =========================================================================
    // INITIAL STATE
    // =========================================================================

    private void SetInitialText()
    {
        // Case label from active case
        if (caseLabel != null)
            caseLabel.text = CaseManager.Instance?.ActiveCase?.Title ?? "Case File";

        // Profile name at the bottom
        if (profileName != null)
            profileName.text = $"Currently playing as {GameManager.Instance?.ActiveProfileName ?? "Detective"}";

        // Welcome message in the output panel
        if (outputText != null)
        {
            string caseName = CaseManager.Instance?.ActiveCase?.Title ?? "the case";
            outputText.text = $"Welcome, Detective {GameManager.Instance?.ActiveProfileName}.\n\n" +
                              $"You are investigating: {caseName}\n\n" +
                              "Type a SQL query below and press Execute to begin your investigation.\n\n" +
                              "Tip: Try   SELECT * FROM persons;   to see who is involved.";
        }
    }

    // =========================================================================
    // QUERY EXECUTION
    // =========================================================================

    private void OnExecuteClicked(ClickEvent e)
    {
        RunQuery();
    }

    private void OnQueryKeyDown(KeyDownEvent e)
    {
        // Ctrl + Enter runs the query
        if (e.keyCode == KeyCode.Return && e.ctrlKey)
            RunQuery();
    }

    private void RunQuery()
    {
        string sql = queryText?.value?.Trim() ?? "";

        if (string.IsNullOrEmpty(sql))
        {
            AppendOutput("Please enter a SQL query first.");
            return;
        }

        // Pass to TaskValidator — it runs the query, checks the result,
        // and fires OnValidationComplete which we handle below
        TaskValidator.Instance.ValidatePlayerQuery(sql, GameManager.Instance.ActiveProfileId);
    }

    // =========================================================================
    // VALIDATION RESULT HANDLER
    // =========================================================================

    private void OnValidationComplete(TaskValidator.ValidationResult result)
    {
        // Clear the input field after running
        if (queryText != null)
            queryText.value = "";

        // Show the returned rows if any
        if (result.ReturnedRows != null && result.ReturnedRows.Count > 0)
            AppendOutput(FormatRows(result.ReturnedRows));
        else if (!result.Passed)
            AppendOutput(result.Message);
        else
            AppendOutput(result.Message);

        // Auto save after every query
        GameManager.Instance.AutoSave("query_executed");

        // Scroll output to bottom so latest result is visible
        queryOutput?.ScrollTo(outputText);
    }

    // =========================================================================
    // OUTPUT FORMATTING
    // =========================================================================

    /// <summary>
    /// Formats a list of rows into a readable table string for the output panel.
    /// </summary>
    private string FormatRows(List<Dictionary<string, string>> rows)
    {
        if (rows == null || rows.Count == 0)
            return "Query returned no results.";

        // Build header from column names
        var columns = new List<string>(rows[0].Keys);
        var lines = new System.Text.StringBuilder();

        // Header row
        lines.AppendLine(string.Join(" | ", columns));
        lines.AppendLine(new string('-', columns.Count * 20));

        // Data rows
        foreach (var row in rows)
        {
            var values = new List<string>();
            foreach (var col in columns)
                values.Add(row.ContainsKey(col) ? row[col] : "");
            lines.AppendLine(string.Join(" | ", values));
        }

        lines.AppendLine($"\n{rows.Count} row(s) returned.");
        return lines.ToString();
    }

    /// <summary>
    /// Appends text to the output panel with a separator between entries.
    /// </summary>
    private void AppendOutput(string text)
    {
        if (outputText == null) return;

        string separator = "\n─────────────────────────────\n";
        outputText.text = string.IsNullOrEmpty(outputText.text)
            ? text
            : outputText.text + separator + text;
    }

    // =========================================================================
    // SUSPECTS OVERLAY
    // =========================================================================

    private void OnSuspectsBtnClicked(ClickEvent e)
    {
        SetOverlayVisible(true);
        PopulateSuspects();
    }

    private void OnOverlayQuitClicked(ClickEvent e)
    {
        SetOverlayVisible(false);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (suspectsOverlay == null) return;
        suspectsOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Reads persons from world.db and populates the suspects overlay.
    /// Only shows persons the player has unlocked access to.
    /// </summary>
    private void PopulateSuspects()
    {
        if (suspectsOverview == null) return;

        // Clear existing cards
        suspectsOverview.Clear();

        var persons = DatabaseManager.Instance.RunWorldQueryWithResults(
            "SELECT id, first_name, last_name, age, occupation, address FROM persons WHERE is_victim = 0"
        );

        if (persons == null || persons.Count == 0)
        {
            var empty = new Label { text = "No suspects identified yet." };
            empty.AddToClassList("suspect-empty");
            suspectsOverview.Add(empty);
            return;
        }

        foreach (var person in persons)
        {
            var card = BuildSuspectCard(person);
            suspectsOverview.Add(card);
        }
    }

    /// <summary>
    /// Builds a single suspect card VisualElement from a person row.
    /// </summary>
    private VisualElement BuildSuspectCard(Dictionary<string, string> person)
    {
        var card = new VisualElement();
        card.AddToClassList("suspect-card");

        string fullName = $"{person.GetValueOrDefault("first_name", "?")} {person.GetValueOrDefault("last_name", "?")}";
        string age = person.GetValueOrDefault("age", "?");
        string job = person.GetValueOrDefault("occupation", "Unknown");
        string address = person.GetValueOrDefault("address", "Unknown");

        var nameLabel = new Label { text = fullName };
        nameLabel.AddToClassList("suspect-name");

        var detailLabel = new Label { text = $"Age {age}  ·  {job}\n{address}" };
        detailLabel.AddToClassList("suspect-detail");

        card.Add(nameLabel);
        card.Add(detailLabel);

        return card;
    }

    // =========================================================================
    // MENU BUTTON
    // =========================================================================

    private void OnMenuClicked(ClickEvent e)
    {
        // Save and return to main menu
        // Later this can open a pause overlay instead
        GameManager.Instance.ReturnToMainMenu();
    }
}