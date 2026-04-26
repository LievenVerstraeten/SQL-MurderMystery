// CaseDefinition.cs
// Data container that defines everything about a single case.
// CaseManager holds a list of these and passes the active one
// to StaticDbGenerator and DynamicDbSeeder when a game starts.
//
// HOW TO ADD A NEW CASE:
//   1. Create a new CaseDefinition instance in CaseManager.AllCases
//   2. Fill in the metadata fields
//   3. Add your StaticTableData (the world the player queries)
//   4. Add your DynamicTableData (the tables the player writes to)
//   5. Add your SQLTasks in order — each one teaches a concept
//   That's it. No other scripts need to change.
//
// PLACEHOLDER CASES:
//   Cases without IsReadyToPlay = true are shown as "Coming Soon"
//   on the case select screen and cannot be started.

using System.Collections.Generic;

// ─── SQL Task ─────────────────────────────────────────────────────────────────
// Represents one puzzle step inside a case.
// The player writes any SQL they like — if the result matches ExpectedRows, they pass.

[System.Serializable]
public class SQLTask
{
    // Shown to the player as the task prompt
    public string TaskDescription;

    // Debbie's hint — explains the SQL concept needed (shown on request)
    public string Hint;

    // The SQL concept this task is teaching — used for the tutorial index
    // e.g. "SELECT", "INSERT", "WHERE", "JOIN"
    public string ConceptTag;

    // The exact result the player's query must return to pass.
    // Each entry is one row. Each row is a column-name → value dictionary.
    // Order of rows matters — results must match top to bottom.
    // Leave empty for free-write tasks (e.g. "write your own conclusion").
    public List<Dictionary<string, string>> ExpectedRows;

    // If true, row order does not need to match — any order of correct rows passes.
    public bool AnyOrder = false;

    // If true, the player can pass with a partial result (subset of expected rows).
    // Useful for tasks where we only care they found *something* relevant.
    public bool PartialMatch = false;

    // Required target table for tasks where ExpectedRows is empty
    public string RequiredTargetTable = "";

    // Narrative dialogue shown BEFORE the task (Debbie / N exchange)
    public string PreTaskDialogue;

    // Narrative dialogue shown AFTER the player completes the task
    public string PostTaskDialogue;

    // Constructor for convenience
    public SQLTask(
        string description,
        string hint,
        string conceptTag,
        string preDialogue = "",
        string postDialogue = "",
        bool anyOrder = false,
        bool partialMatch = false)
    {
        TaskDescription = description;
        Hint = hint;
        ConceptTag = conceptTag;
        PreTaskDialogue = preDialogue;
        PostTaskDialogue = postDialogue;
        AnyOrder = anyOrder;
        PartialMatch = partialMatch;
        ExpectedRows = new List<Dictionary<string, string>>();
    }
}

// ─── Table Definition ─────────────────────────────────────────────────────────
// Defines a table to be created and seeded into the database.
// Used for both static (world.db) and dynamic (saves.db) tables.

[System.Serializable]
public class TableDefinition
{
    // The CREATE TABLE SQL statement for this table
    public string CreateSQL;

    // Each entry is one INSERT statement for a row of seed data
    public List<string> InsertStatements;

    public TableDefinition(string createSQL)
    {
        CreateSQL = createSQL;
        InsertStatements = new List<string>();
    }
}

// ─── Case Definition ──────────────────────────────────────────────────────────
// The full definition of one case. CaseManager holds a list of these.

[System.Serializable]
public class CaseDefinition
{
    // ── Metadata ──────────────────────────────────────────────────────────────

    // Internal unique ID — never changes, used to track unlock state
    public string CaseId;

    // Displayed on the case select screen
    public string Title;

    // One or two sentence hook shown on the case card
    public string Overview;

    // Year/era — flavour text for the UI
    public string Timeline;

    // Cash reward shown on the case card
    public int Reward;

    // SQL difficulty label shown on the card
    // "Beginner" / "Easy" / "Intermediate" / "Hard" / "Expert"
    public string Difficulty;

    // Which case must be completed before this one unlocks.
    // Empty string means available from the start.
    public string RequiredCaseId;

    // If false, shows as "Coming Soon" and cannot be started.
    // Set to true only when the case content is fully written.
    public bool IsReadyToPlay;

    // ── Static World Data (seeded into world.db, player queries these) ─────────
    // These are the tables that exist in the world before the detective arrives.
    // Player can only SELECT from these — they cannot modify them.
    public List<TableDefinition> StaticTables;

    // ── Dynamic Starting State (seeded into saves.db, player writes to these) ──
    // These tables exist at case start and evolve as the player investigates.
    // e.g. the logfile starts with status = 'Unsolved' and the player updates it.
    public List<TableDefinition> DynamicTables;

    // ── Task List ─────────────────────────────────────────────────────────────
    // Ordered list of SQL puzzles the player must complete.
    // Completing all tasks finishes the case.
    public List<SQLTask> Tasks;

    // ── Conclusion ────────────────────────────────────────────────────────────
    // "solvable"     — case has a definitive answer (murderer revealed on completion)
    // "inconclusive" — case is intentionally unresolved (like Somerton)
    public string ConclusionType;

    // Epilogue text shown on the end screen after the case closes
    public string EpilogueText;

    // Constructor
    public CaseDefinition(string caseId, string title, string overview,
                          string timeline, int reward, string difficulty,
                          string requiredCaseId = "", bool isReady = false)
    {
        CaseId = caseId;
        Title = title;
        Overview = overview;
        Timeline = timeline;
        Reward = reward;
        Difficulty = difficulty;
        RequiredCaseId = requiredCaseId;
        IsReadyToPlay = isReady;
        ConclusionType = "solvable";
        EpilogueText = "";
        StaticTables = new List<TableDefinition>();
        DynamicTables = new List<TableDefinition>();
        Tasks = new List<SQLTask>();
    }
}