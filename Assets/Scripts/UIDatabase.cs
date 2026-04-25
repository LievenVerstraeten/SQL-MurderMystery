// UIDatabase.cs
// Manages the SQL terminal UI: input field, history, results display.
// Routes all submitted queries through TaskValidator so that story progress
// is tracked correctly.
//
// Public API used by DialogueManager:
//   OpenTerminal()           — shows the terminal panel
//   CloseTerminal()          — hides the terminal panel
//   RunDemo(sql, label)      — auto-executes SQL, displays result, no validation
//
// SETUP:
//   Attach to a persistent GameObject in the game scene alongside DialogueManager.
//   Assign the same UIDocument as GameUIManager (GameUI.uxml).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIDatabase : MonoBehaviour
{
    public static UIDatabase Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── UI references ─────────────────────────────────────────────────────────
    private VisualElement _terminalPanel;
    private TextField     _queryInputField;
    private ListView      _commandHistoryList;
    private Button        _sendQueryButton;
    private Label         _queryOutputText;

    private readonly List<string> _history = new();

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

        var root = uiDocument.rootVisualElement;

        _terminalPanel      = root.Q("sql-terminal-container");
        _queryInputField    = root.Q<TextField>("query-input-field");
        _commandHistoryList = root.Q<ListView>("command-history-list");
        _sendQueryButton    = root.Q<Button>("send-query-button");
        _queryOutputText    = root.Q<Label>("query-output-text");

        if (_sendQueryButton != null)
            _sendQueryButton.clicked += OnSendQuery;

        if (_queryInputField != null)
            _queryInputField.RegisterCallback<KeyDownEvent>(OnQueryKeyDown);

        if (_commandHistoryList != null)
        {
            _commandHistoryList.itemsSource = _history;
            _commandHistoryList.makeItem    = () => new Label();
            _commandHistoryList.bindItem    = (el, i) =>
            {
                var lbl = (Label)el;
                lbl.text = _history[i];
                lbl.style.color       = new StyleColor(Color.white);
                lbl.style.fontSize    = 14;
                lbl.style.paddingLeft = lbl.style.paddingTop =
                    lbl.style.paddingBottom = 5;
            };
            _commandHistoryList.selectionChanged += OnHistoryItemSelected;
        }

        TaskValidator.OnValidationComplete += OnValidationComplete;

        if (_terminalPanel != null)
            _terminalPanel.style.display = DisplayStyle.None;
    }

    void OnDisable()
    {
        if (_sendQueryButton    != null) _sendQueryButton.clicked             -= OnSendQuery;
        if (_queryInputField    != null) _queryInputField.UnregisterCallback<KeyDownEvent>(OnQueryKeyDown);
        if (_commandHistoryList != null) _commandHistoryList.selectionChanged -= OnHistoryItemSelected;
        TaskValidator.OnValidationComplete -= OnValidationComplete;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ConnectToUI(UIDocument doc)
    {
        uiDocument = doc;
        var root = doc.rootVisualElement;

        _terminalPanel      = root.Q("sql-terminal-container");
        _queryInputField    = root.Q<TextField>("query-input-field");
        _commandHistoryList = root.Q<ListView>("command-history-list");
        _sendQueryButton    = root.Q<Button>("send-query-button");
        _queryOutputText    = root.Q<Label>("query-output-text");

        if (_sendQueryButton != null) _sendQueryButton.clicked += OnSendQuery;
        if (_queryInputField != null) _queryInputField.RegisterCallback<KeyDownEvent>(OnQueryKeyDown);

        if (_commandHistoryList != null)
        {
            _commandHistoryList.itemsSource = _history;
            _commandHistoryList.makeItem    = () => new Label();
            _commandHistoryList.bindItem    = (el, i) =>
            {
                var lbl = (Label)el;
                lbl.text = _history[i];
                lbl.style.color       = new StyleColor(Color.white);
                lbl.style.fontSize    = 14;
                lbl.style.paddingLeft = lbl.style.paddingTop =
                    lbl.style.paddingBottom = 5;
            };
            _commandHistoryList.selectionChanged += OnHistoryItemSelected;
        }

        if (_terminalPanel != null) _terminalPanel.style.display = DisplayStyle.None;

        // Ensure event is subscribed (OnEnable skips this when there's no UIDocument)
        TaskValidator.OnValidationComplete -= OnValidationComplete;
        TaskValidator.OnValidationComplete += OnValidationComplete;
    }

    public void OpenTerminal()
    {
        if (_terminalPanel != null)
        {
            _terminalPanel.style.display = DisplayStyle.Flex;
            _terminalPanel.BringToFront();
        }
        ClearInput();
    }

    public void CloseTerminal()
    {
        if (_terminalPanel != null) _terminalPanel.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Runs SQL automatically (Debbie demonstration).
    /// Shows result in the output panel but does NOT call TaskValidator.
    /// </summary>
    public void RunDemo(string sql, string label)
    {
        if (string.IsNullOrEmpty(sql)) return;

        OpenTerminal();

        string header = string.IsNullOrEmpty(label) ? "" : $"-- {label}\n";
        ShowOutput($"{header}> {sql}\n\n{ExecuteAndFormat(sql)}", isError: false);
        AddToHistory(sql);
    }

    // ── Send query ────────────────────────────────────────────────────────────

    private void OnQueryKeyDown(KeyDownEvent e)
    {
        // Ctrl+Enter submits; plain Enter adds a newline (multiline field)
        if (e.keyCode == KeyCode.Return && e.ctrlKey)
        {
            e.StopPropagation();
            OnSendQuery();
        }
    }

    private void OnSendQuery()
    {
        if (_queryInputField == null) return;

        string query = (_queryInputField.value ?? "").Trim();
        if (string.IsNullOrEmpty(query)) return;

        AddToHistory(query);
        ClearInput();

        int profileId = GameManager.Instance != null
            ? GameManager.Instance.ActiveProfileId
            : -1;

        if (profileId < 0)
        {
            // Sandbox / test scene — execute directly without validation
            ShowOutput(ExecuteAndFormat(query), isError: false);
            return;
        }

        // Route through TaskValidator for story gating
        TaskValidator.Instance?.ValidatePlayerQuery(query, profileId);
    }

    // ── Validation feedback ───────────────────────────────────────────────────

    private void OnValidationComplete(TaskValidator.ValidationResult result)
    {
        string output = "";

        if (result.ReturnedRows != null && result.ReturnedRows.Count > 0)
            output = FormatRows(result.ReturnedRows) + "\n\n";

        output += result.Passed
            ? $"[OK] {result.Message}"
            : $"[!!] {result.Message}";

        ShowOutput(output, isError: !result.Passed);
    }

    // ── Output display ────────────────────────────────────────────────────────

    private void ShowOutput(string text, bool isError)
    {
        if (_queryOutputText == null) return;
        _queryOutputText.text = text;
        _queryOutputText.style.color = new StyleColor(isError ? Color.red : Color.white);
    }

    /// <summary>Executes SQL directly against world.db for demos (bypasses validator).</summary>
    private string ExecuteAndFormat(string sql)
    {
        try
        {
            string processed = sql.Replace("internet.sqlite_master", "internet_meta")
                                  .Replace("internet.", "");

            var rows = DatabaseManager.Instance?.RunWorldQueryWithResults(processed);

            if (rows == null || rows.Count == 0)
                return "(query executed — 0 rows returned)";

            return FormatRows(rows);
        }
        catch (System.Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string FormatRows(List<Dictionary<string, string>> rows)
    {
        if (rows == null || rows.Count == 0) return "(0 rows)";

        var headers = new List<string>(rows[0].Keys);
        string header  = string.Join(" | ", headers);
        string divider = new string('-', header.Length);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine(divider);

        foreach (var row in rows)
        {
            var values = new List<string>();
            foreach (var h in headers)
                values.Add(row.ContainsKey(h) ? (row[h] ?? "NULL") : "NULL");
            sb.AppendLine(string.Join(" | ", values));
        }

        return sb.ToString().TrimEnd();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddToHistory(string query)
    {
        if (!_history.Contains(query))
        {
            _history.Add(query);
            _commandHistoryList?.RefreshItems();
            _commandHistoryList?.ScrollToItem(-1);
        }
    }

    private void ClearInput()
    {
        if (_queryInputField != null) _queryInputField.value = "";
    }

    private void OnHistoryItemSelected(IEnumerable<object> selection)
    {
        foreach (var sel in selection)
        {
            if (sel is string s && !string.IsNullOrEmpty(s) && _queryInputField != null)
            {
                _queryInputField.value = s;
                break;
            }
        }
    }
}
