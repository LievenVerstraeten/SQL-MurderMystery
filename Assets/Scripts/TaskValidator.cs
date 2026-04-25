// TaskValidator.cs
// Validates the player's SQL query results against the current task's expected output.
//
// ALIAS FLEXIBILITY:
//   Validation is result-based, not query-based.
//   The player can write any valid SQL — SELECT *, aliases, different column order —
//   as long as the returned data matches the expected rows, they pass.
//
// WRITE OPERATION HANDLING:
//   INSERT, UPDATE, CREATE TABLE, DELETE — these don't return rows.
//   For write tasks, ExpectedRows is left empty in the CaseDefinition
//   and the validator calls a post-write check query to confirm the change landed.
//
// STATIC TABLE PROTECTION:
//   The validator blocks any write operation (INSERT/UPDATE/DELETE/DROP)
//   targeting a static table. Players can only write to dynamic tables.
//
// SETUP:
//   Attach to the same persistent GameObject as GameManager and DatabaseManager.
//   Call ValidatePlayerQuery() from your SQL input UI script.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TaskValidator : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static TaskValidator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Events ───────────────────────────────────────────────────────────────
    // Subscribe in your UI script to show pass/fail feedback
    public static event System.Action<ValidationResult> OnValidationComplete;

    // =========================================================================
    // RESULT TYPE
    // =========================================================================

    public class ValidationResult
    {
        public bool Passed;
        public string Message;
        public string FailReason;

        // The actual rows returned by the player's query — shown in the results panel
        public List<Dictionary<string, string>> ReturnedRows;

        public ValidationResult(bool passed, string message, string failReason = "",
            List<Dictionary<string, string>> rows = null)
        {
            Passed = passed;
            Message = message;
            FailReason = failReason;
            ReturnedRows = rows ?? new List<Dictionary<string, string>>();
        }
    }

    // =========================================================================
    // STATIC TABLE REGISTRY
    // =========================================================================
    // Tables the player can never write to.
    // Built from the active CaseDefinition's StaticTables when a case starts.

    private HashSet<string> staticTableNames = new HashSet<string>();

    /// <summary>
    /// Registers which table names are static for the current case.
    /// Called by GameManager after StaticDbGenerator.GenerateWorld().
    /// </summary>
    public void RegisterStaticTables(CaseDefinition casedef)
    {
        staticTableNames.Clear();

        if (casedef?.StaticTables == null) return;

        foreach (var table in casedef.StaticTables)
        {
            // Extract table name from the CREATE TABLE statement
            string name = ExtractTableName(table.CreateSQL);
            if (!string.IsNullOrEmpty(name))
                staticTableNames.Add(name.ToLower());
        }

        Debug.Log($"[TaskValidator] Registered {staticTableNames.Count} static tables: " +
                  string.Join(", ", staticTableNames));
    }

    // =========================================================================
    // MAIN ENTRY POINT
    // =========================================================================

    /// <summary>
    /// Called by the SQL input UI when the player submits a query.
    /// Runs the query, checks the result, fires OnValidationComplete.
    /// </summary>
    /// <param name="playerSQL">The raw SQL the player typed.</param>
    /// <param name="profileId">The active profile ID.</param>
    public void ValidatePlayerQuery(string playerSQL, int profileId)
    {
        playerSQL = playerSQL.Trim();

        if (string.IsNullOrEmpty(playerSQL))
        {
            FireResult(new ValidationResult(false, "Nothing to run.", "Empty query."));
            return;
        }

        // Preprocess for static-table check (strip internet. prefix)
        string processedSQL = PreprocessSQL(playerSQL);

        // ── Static table protection ───────────────────────────────────────────
        if (IsWriteOperation(processedSQL))
        {
            string targetTable = ExtractTargetTable(processedSQL);
            if (!string.IsNullOrEmpty(targetTable) &&
                staticTableNames.Contains(targetTable.ToLower()))
            {
                string msg = $"You cannot modify the '{targetTable}' table — it is part of the case record.";
                FireResult(new ValidationResult(false, msg, "Write to static table blocked."));
                return;
            }
        }

        // ── Get current task ──────────────────────────────────────────────────
        CaseDefinition activeCase = CaseManager.Instance.ActiveCase;
        if (activeCase == null)
        {
            FireResult(new ValidationResult(false, "No active case.", "CaseManager has no active case."));
            return;
        }

        int taskIndex = CaseManager.Instance.GetCurrentTaskIndex(profileId, activeCase.CaseId);
        if (taskIndex >= activeCase.Tasks.Count)
        {
            FireResult(new ValidationResult(true, "All tasks complete!", ""));
            return;
        }

        SQLTask currentTask = activeCase.Tasks[taskIndex];

        // ── Execute the player's query ────────────────────────────────────────
        List<Dictionary<string, string>> returnedRows = ExecutePlayerQuery(processedSQL, profileId);

        // Log every query the player runs
        DatabaseManager.Instance.LogPlayerQuery(profileId, playerSQL,
            returnedRows?.Count ?? 0);

        // ── Free write task (no expected rows) ────────────────────────────────
        // Some tasks are open-ended (e.g. "write your own conclusion")
        // These always pass as long as the query executes without error.
        if (currentTask.ExpectedRows == null || currentTask.ExpectedRows.Count == 0)
        {
            AdvanceAndNotify(profileId, activeCase, taskIndex, returnedRows,
                "Good work. Moving on.");
            return;
        }

        // ── Result comparison ─────────────────────────────────────────────────
        bool passed = CompareResults(currentTask, returnedRows);

        if (passed)
        {
            AdvanceAndNotify(profileId, activeCase, taskIndex, returnedRows,
                "Correct! Well done, detective.");
        }
        else
        {
            string hint = string.IsNullOrEmpty(currentTask.Hint)
                ? "Check your query and try again."
                : $"Hint: {currentTask.Hint}";

            FireResult(new ValidationResult(
                false,
                $"Not quite. {hint}",
                "Result mismatch.",
                returnedRows
            ));
        }
    }

    // =========================================================================
    // RESULT COMPARISON
    // =========================================================================

    /// <summary>
    /// Compares the player's returned rows against the task's expected rows.
    /// Respects AnyOrder and PartialMatch flags on the task.
    /// Column names are compared case-insensitively.
    /// Column values are compared as trimmed strings.
    /// </summary>
    private bool CompareResults(SQLTask task,
        List<Dictionary<string, string>> actual)
    {
        var expected = task.ExpectedRows;

        if (actual == null) actual = new List<Dictionary<string, string>>();

        // Partial match — player just needs to return a subset of expected rows
        if (task.PartialMatch)
            return expected.Any(expRow => actual.Any(actRow => RowMatches(expRow, actRow)));

        // Row count must match for a full comparison
        if (actual.Count != expected.Count)
            return false;

        if (task.AnyOrder)
        {
            // Every expected row must appear somewhere in actual
            return expected.All(expRow =>
                actual.Any(actRow => RowMatches(expRow, actRow)));
        }
        else
        {
            // Rows must match in exact order
            for (int i = 0; i < expected.Count; i++)
            {
                if (!RowMatches(expected[i], actual[i]))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Returns true if every key-value pair in the expected row
    /// is present and matching in the actual row.
    /// Column names are case-insensitive. Values are trimmed.
    /// Extra columns in the actual row are ignored — the player
    /// can SELECT more columns than required and still pass.
    /// </summary>
    private bool RowMatches(
        Dictionary<string, string> expected,
        Dictionary<string, string> actual)
    {
        foreach (var kvp in expected)
        {
            // Find the matching column in actual, case-insensitive
            string matchKey = actual.Keys.FirstOrDefault(
                k => k.Equals(kvp.Key, System.StringComparison.OrdinalIgnoreCase));

            if (matchKey == null) return false;

            string expectedVal = kvp.Value?.Trim() ?? "";
            string actualVal = actual[matchKey]?.Trim() ?? "";

            if (!expectedVal.Equals(actualVal, System.StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    // =========================================================================
    // QUERY EXECUTION
    // =========================================================================

    /// <summary>
    /// Preprocesses SQL before execution.
    /// - Rewrites "internet.sqlite_master" → "internet_meta"
    /// - Strips the "internet." schema prefix from table names so queries
    ///   like "SELECT * FROM internet.bbc_news" hit world.db directly.
    /// </summary>
    private string PreprocessSQL(string sql)
    {
        // Must rewrite sqlite_master reference first (more specific)
        sql = sql.Replace("internet.sqlite_master", "internet_meta");
        // Strip any remaining "internet." prefix
        sql = sql.Replace("internet.", "");
        return sql;
    }

    /// <summary>
    /// Runs the player's SQL against world.db.
    /// All player-visible tables (both static and dynamic) live in world.db
    /// so that sqlite_master shows only game tables.
    /// Writes to static tables are blocked before this method is called.
    /// </summary>
    private List<Dictionary<string, string>> ExecutePlayerQuery(
        string sql, int profileId)
    {
        // Preprocess: handle internet.* schema prefix
        sql = PreprocessSQL(sql);

        try
        {
            if (IsWriteOperation(sql))
            {
                // All player writes go to world.db (static tables already blocked above)
                DatabaseManager.Instance.RunWorldNonQuery(sql);
                return new List<Dictionary<string, string>>();
            }
            else
            {
                // All player reads from world.db
                return DatabaseManager.Instance.RunWorldQueryWithResults(sql);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TaskValidator] Query execution failed: {ex.Message}\nSQL: {sql}");
            FireResult(new ValidationResult(false,
                $"SQL Error: {ex.Message}", "Exception during execution."));
            return null;
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void AdvanceAndNotify(int profileId, CaseDefinition activeCase,
        int taskIndex, List<Dictionary<string, string>> rows, string message)
    {
        int nextIndex = taskIndex + 1;

        if (nextIndex >= activeCase.Tasks.Count)
        {
            // All tasks done — case complete
            CaseManager.Instance.CompleteCase(activeCase.CaseId, profileId);
            FireResult(new ValidationResult(true,
                "Case complete! Well done, detective.", "", rows));
        }
        else
        {
            CaseManager.Instance.AdvanceTask(profileId, activeCase.CaseId, nextIndex);
            FireResult(new ValidationResult(true, message, "", rows));
        }
    }

    private void FireResult(ValidationResult result)
    {
        OnValidationComplete?.Invoke(result);
    }

    /// <summary>
    /// Returns true if the SQL is a write operation.
    /// </summary>
    private bool IsWriteOperation(string sql)
    {
        string upper = sql.TrimStart().ToUpper();
        return upper.StartsWith("INSERT")
            || upper.StartsWith("UPDATE")
            || upper.StartsWith("DELETE")
            || upper.StartsWith("DROP")
            || upper.StartsWith("CREATE")
            || upper.StartsWith("ALTER");
    }

    /// <summary>
    /// Extracts the table name from common SQL statements.
    /// Handles SELECT FROM, INSERT INTO, UPDATE, DELETE FROM, CREATE TABLE.
    /// Returns empty string if it cannot parse the table name.
    /// </summary>
    private string ExtractTargetTable(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return "";

        string[] tokens = sql.Trim().Split(
            new char[] { ' ', '\t', '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 2) return "";

        string verb = tokens[0].ToUpper();

        try
        {
            switch (verb)
            {
                case "SELECT":
                    // SELECT ... FROM tablename
                    int fromIdx = System.Array.FindIndex(
                        tokens, t => t.ToUpper() == "FROM");
                    return fromIdx >= 0 && fromIdx + 1 < tokens.Length
                        ? StripAlias(tokens[fromIdx + 1])
                        : "";

                case "INSERT":
                    // INSERT INTO tablename
                    return tokens.Length > 2 ? StripAlias(tokens[2]) : "";

                case "UPDATE":
                    // UPDATE tablename SET ...
                    return StripAlias(tokens[1]);

                case "DELETE":
                    // DELETE FROM tablename
                    return tokens.Length > 2 ? StripAlias(tokens[2]) : "";

                case "CREATE":
                    // CREATE TABLE [IF NOT EXISTS] tablename
                    int tblIdx = System.Array.FindIndex(
                        tokens, t => t.ToUpper() == "TABLE");
                    if (tblIdx < 0) return "";
                    // Skip IF NOT EXISTS if present
                    int nameIdx = tblIdx + 1;
                    if (nameIdx < tokens.Length &&
                        tokens[nameIdx].ToUpper() == "IF") nameIdx += 3;
                    return nameIdx < tokens.Length ? StripAlias(tokens[nameIdx]) : "";

                case "DROP":
                    // DROP TABLE tablename
                    return tokens.Length > 2 ? StripAlias(tokens[2]) : "";

                default:
                    return "";
            }
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Extracts the table name from a CREATE TABLE statement.
    /// Used when registering static tables from CaseDefinition.
    /// </summary>
    private string ExtractTableName(string createSQL)
    {
        return ExtractTargetTable(createSQL);
    }

    /// <summary>
    /// Strips a trailing alias or punctuation from a token.
    /// e.g. "persons," → "persons", "p.id" → "persons" is NOT handled here
    /// (aliases on columns don't affect table name extraction).
    /// </summary>
    private string StripAlias(string token)
    {
        return token.Trim('(', ')', ',', ';', '`', '"', '\'', '[', ']');
    }
}