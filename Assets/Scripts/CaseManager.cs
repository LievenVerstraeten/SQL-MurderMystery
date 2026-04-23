// CaseManager.cs
// Owns the master list of all cases in the game.
// Handles which cases are unlocked per profile and which case is currently active.
//
// ADDING A NEW CASE:
//   Define it in BuildCaseList() below.
//   Set IsReadyToPlay = false until the content is finished.
//   Set RequiredCaseId to enforce unlock order.
//
// SETUP:
//   Attach to the same persistent GameObject as GameManager and DatabaseManager.

using System.Collections.Generic;
using UnityEngine;

public class CaseManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static CaseManager Instance { get; private set; }

    // The case the current profile is actively playing
    public CaseDefinition ActiveCase { get; private set; }

    // Master list of every case in the game, in curriculum order
    public List<CaseDefinition> AllCases { get; private set; }

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildCaseList();
    }

    // =========================================================================
    // CASE LIST — add all cases here
    // Set IsReadyToPlay = false for cases that are not yet written.
    // =========================================================================

    private void BuildCaseList()
    {
        AllCases = new List<CaseDefinition>();

        // ── Case 1 ────────────────────────────────────────────────────────────
        // PLACEHOLDER — replace with real content when case is ready.
        // Teaches: SELECT, INSERT, CREATE TABLE, WHERE, column selection
        var case1 = new CaseDefinition(
            caseId: "case_01",
            title: "Case 01 — Coming Soon",
            overview: "Placeholder. This case has not been written yet.",
            timeline: "TBD",
            reward: 0,
            difficulty: "Beginner",
            requiredCaseId: "",
            isReady: true   // Set to false when handing to your teammate to fill in
        );
        case1.ConclusionType = "solvable";
        case1.EpilogueText = "The truth was there all along. Hidden in the data.";

        // ── Static tables ─────────────────────────────────────────────────────

        var personsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS persons (" +
            "id INTEGER PRIMARY KEY, first_name TEXT NOT NULL, last_name TEXT NOT NULL, " +
            "age INTEGER, occupation TEXT, address TEXT, " +
            "is_victim INTEGER DEFAULT 0, is_suspect INTEGER DEFAULT 1)");
        personsTable.InsertStatements.Add("INSERT INTO persons VALUES (1,'Victor','Ashworth',58,'Banker','14 Ashgate Lane',0,1)");
        personsTable.InsertStatements.Add("INSERT INTO persons VALUES (2,'Eleanor','Crane',42,'Physician','7 Brimstone Court',0,1)");
        personsTable.InsertStatements.Add("INSERT INTO persons VALUES (3,'Edmund','Fawcett',35,'Solicitor','22 Craven Street',0,1)");
        personsTable.InsertStatements.Add("INSERT INTO persons VALUES (4,'Harriet','Dunmore',29,'Journalist','3 Dullwich Road',0,1)");
        personsTable.InsertStatements.Add("INSERT INTO persons VALUES (5,'Reginald','Harlow',61,'Merchant','19 Elmsworth Avenue',1,0)");
        case1.StaticTables.Add(personsTable);

        var locationsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS locations (" +
            "id INTEGER PRIMARY KEY, name TEXT NOT NULL, type TEXT, address TEXT)");
        locationsTable.InsertStatements.Add("INSERT INTO locations VALUES (1,'The Rusty Anchor Tavern','tavern','8 Harbour Walk')");
        locationsTable.InsertStatements.Add("INSERT INTO locations VALUES (2,'Ashworth Manor','manor','14 Ashgate Lane')");
        locationsTable.InsertStatements.Add("INSERT INTO locations VALUES (3,'Blackwell Chemist','shop','5 Ivory Gate')");
        locationsTable.InsertStatements.Add("INSERT INTO locations VALUES (4,'City Morgue','morgue','31 Greystone Row')");
        locationsTable.InsertStatements.Add("INSERT INTO locations VALUES (5,'The Grand Hotel','hotel','26 Juniper Close')");
        case1.StaticTables.Add(locationsTable);

        var crimeTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS crime_scene (" +
            "id INTEGER PRIMARY KEY, victim_id INTEGER NOT NULL, weapon TEXT NOT NULL, " +
            "location_id INTEGER NOT NULL, time_of_death TEXT NOT NULL)");
        crimeTable.InsertStatements.Add("INSERT INTO crime_scene VALUES (1,5,'Poison Vial',2,'23:15')");
        case1.StaticTables.Add(crimeTable);

        var alibisTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS alibis (" +
            "id INTEGER PRIMARY KEY, person_id INTEGER NOT NULL, location_id INTEGER NOT NULL, " +
            "alibi_time TEXT, statement TEXT, is_true INTEGER NOT NULL)");
        alibisTable.InsertStatements.Add("INSERT INTO alibis VALUES (1,1,5,'22:00','I was dining at The Grand Hotel all evening.',1)");
        alibisTable.InsertStatements.Add("INSERT INTO alibis VALUES (2,2,1,'21:30','I was at the tavern with colleagues.',1)");
        alibisTable.InsertStatements.Add("INSERT INTO alibis VALUES (3,3,2,'23:00','I was never near the manor that night.',0)");
        alibisTable.InsertStatements.Add("INSERT INTO alibis VALUES (4,4,3,'20:00','I was at the chemist picking up a prescription.',1)");
        case1.StaticTables.Add(alibisTable);

        // ── Dynamic tables ────────────────────────────────────────────────────

        var logfileTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS logfile (" +
            "id INTEGER PRIMARY KEY AUTOINCREMENT, profile_id INTEGER NOT NULL, " +
            "namecase TEXT, status TEXT, details TEXT, investigator TEXT, victim TEXT, murderer TEXT)");
        logfileTable.InsertStatements.Add(
            "INSERT INTO logfile (profile_id, namecase, status, details, investigator, victim, murderer) " +
            "VALUES ({PROFILE_ID},'The Ashworth Poisoning','Unsolved','Investigate further.','Detective','?','?')");
        case1.DynamicTables.Add(logfileTable);

        // ── Tasks ─────────────────────────────────────────────────────────────

        var task1 = new SQLTask(
            description: "Show all persons connected to the case.",
            hint: "Use SELECT * FROM to retrieve every column from a table.",
            conceptTag: "SELECT",
            preDialogue: "Let's start with the basics. Who are our persons of interest?",
            postDialogue: "Good. Now we know who we are dealing with.",
            anyOrder: true
        );
        task1.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","1"},{"first_name","Victor"},{"last_name","Ashworth"},{"age","58"},
            {"occupation","Banker"},{"address","14 Ashgate Lane"},{"is_victim","0"},{"is_suspect","1"} });
        task1.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","2"},{"first_name","Eleanor"},{"last_name","Crane"},{"age","42"},
            {"occupation","Physician"},{"address","7 Brimstone Court"},{"is_victim","0"},{"is_suspect","1"} });
        task1.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","3"},{"first_name","Edmund"},{"last_name","Fawcett"},{"age","35"},
            {"occupation","Solicitor"},{"address","22 Craven Street"},{"is_victim","0"},{"is_suspect","1"} });
        task1.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","4"},{"first_name","Harriet"},{"last_name","Dunmore"},{"age","29"},
            {"occupation","Journalist"},{"address","3 Dullwich Road"},{"is_victim","0"},{"is_suspect","1"} });
        task1.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","5"},{"first_name","Reginald"},{"last_name","Harlow"},{"age","61"},
            {"occupation","Merchant"},{"address","19 Elmsworth Avenue"},{"is_victim","1"},{"is_suspect","0"} });
        case1.Tasks.Add(task1);

        var task2 = new SQLTask(
            description: "Show only the first name, last name and occupation of each person.",
            hint: "Instead of *, list the column names separated by commas.",
            conceptTag: "SELECT columns",
            preDialogue: "We don't need all that information. Let's focus on who these people are.",
            postDialogue: "Good. A solicitor at the scene is interesting.",
            anyOrder: true,
            partialMatch: false
        );
        task2.ExpectedRows.Add(new Dictionary<string, string> {
            {"first_name","Victor"},{"last_name","Ashworth"},{"occupation","Banker"} });
        task2.ExpectedRows.Add(new Dictionary<string, string> {
            {"first_name","Eleanor"},{"last_name","Crane"},{"occupation","Physician"} });
        task2.ExpectedRows.Add(new Dictionary<string, string> {
            {"first_name","Edmund"},{"last_name","Fawcett"},{"occupation","Solicitor"} });
        task2.ExpectedRows.Add(new Dictionary<string, string> {
            {"first_name","Harriet"},{"last_name","Dunmore"},{"occupation","Journalist"} });
        task2.ExpectedRows.Add(new Dictionary<string, string> {
            {"first_name","Reginald"},{"last_name","Harlow"},{"occupation","Merchant"} });
        case1.Tasks.Add(task2);

        var task3 = new SQLTask(
            description: "Find the person whose alibi is false. Check the alibis table where is_true = 0.",
            hint: "Use WHERE to filter rows. Example: SELECT * FROM table WHERE column = value",
            conceptTag: "WHERE",
            preDialogue: "Someone is lying. Let's find out who.",
            postDialogue: "Edmund Fawcett lied about his alibi. He was at Ashworth Manor that night.",
            anyOrder: false
        );
        task3.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","3"},{"person_id","3"},{"location_id","2"},
            {"alibi_time","23:00"},{"statement","I was never near the manor that night."},{"is_true","0"} });
        case1.Tasks.Add(task3);

        var task4 = new SQLTask(
            description: "Update the logfile. Set the murderer to 'Edmund Fawcett' where id = 1.",
            hint: "Use UPDATE tablename SET column = value WHERE condition.",
            conceptTag: "UPDATE",
            preDialogue: "We have our man. Let's close the case file.",
            postDialogue: "Case closed. Edmund Fawcett poisoned Reginald Harlow at Ashworth Manor.",
            anyOrder: false
        );
        case1.Tasks.Add(task4);

        AllCases.Add(case1);

        // ── Case 2 ────────────────────────────────────────────────────────────
        // PLACEHOLDER — replace with real content when case is ready.
        // Teaches: JOIN, LIKE, ORDER BY, UPDATE, COUNT, GROUP BY
        var case2 = new CaseDefinition(
            caseId: "case_02",
            title: "Case 02 — Coming Soon",
            overview: "Placeholder. This case has not been written yet.",
            timeline: "TBD",
            reward: 0,
            difficulty: "Easy",
            requiredCaseId: "case_01",
            isReady: false
        );
        AllCases.Add(case2);

        // ── Case 3 ────────────────────────────────────────────────────────────
        // PLACEHOLDER — replace with real content when case is ready.
        // Teaches: IS NULL, UNION, sqlite_master, multi-table joins
        var case3 = new CaseDefinition(
            caseId: "case_03",
            title: "Case 03 — Coming Soon",
            overview: "Placeholder. This case has not been written yet.",
            timeline: "TBD",
            reward: 0,
            difficulty: "Intermediate",
            requiredCaseId: "case_02",
            isReady: false
        );
        AllCases.Add(case3);

        // ── Case 4 ────────────────────────────────────────────────────────────
        // PLACEHOLDER — replace with real content when case is ready.
        // Teaches: Subqueries, HAVING, DISTINCT, computed columns
        var case4 = new CaseDefinition(
            caseId: "case_04",
            title: "Case 04 — Coming Soon",
            overview: "Placeholder. This case has not been written yet.",
            timeline: "TBD",
            reward: 0,
            difficulty: "Hard",
            requiredCaseId: "case_03",
            isReady: false
        );
        AllCases.Add(case4);

        // ── Case 5 ────────────────────────────────────────────────────────────
        // PLACEHOLDER — replace with real content when case is ready.
        // Teaches: Full combined SQL — complex multi-step investigation
        var case5 = new CaseDefinition(
            caseId: "case_05",
            title: "Case 05 — Coming Soon",
            overview: "Placeholder. This case has not been written yet.",
            timeline: "TBD",
            reward: 0,
            difficulty: "Expert",
            requiredCaseId: "case_04",
            isReady: false
        );
        AllCases.Add(case5);

        Debug.Log($"[CaseManager] {AllCases.Count} cases loaded.");
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Sets the active case by ID. Called by GameManager when starting or loading.
    /// Returns false if the case doesn't exist or isn't ready to play.
    /// </summary>
    public bool SetActiveCase(string caseId)
    {
        var casedef = AllCases.Find(c => c.CaseId == caseId);

        if (casedef == null)
        {
            Debug.LogError($"[CaseManager] Case '{caseId}' not found.");
            return false;
        }

        if (!casedef.IsReadyToPlay)
        {
            Debug.LogWarning($"[CaseManager] Case '{caseId}' is not ready to play yet.");
            return false;
        }

        ActiveCase = casedef;
        Debug.Log($"[CaseManager] Active case set: {casedef.Title}");
        return true;
    }

    /// <summary>
    /// Returns all cases a profile is allowed to see on the case select screen.
    /// Includes locked cases so they show as "Coming Soon" cards.
    /// </summary>
    public List<CaseDefinition> GetAllCasesForDisplay()
    {
        return new List<CaseDefinition>(AllCases);
    }

    /// <summary>
    /// Returns only cases the profile has unlocked and can start.
    /// A case is unlocked when the required case is completed by this profile.
    /// </summary>
    public List<CaseDefinition> GetUnlockedCases(int profileId)
    {
        var unlocked = new List<CaseDefinition>();

        foreach (var c in AllCases)
        {
            if (IsCaseUnlocked(c.CaseId, profileId))
                unlocked.Add(c);
        }

        return unlocked;
    }

    /// <summary>
    /// Returns true if the profile has unlocked the given case.
    /// First case is always unlocked. Others require the previous to be completed.
    /// </summary>
    public bool IsCaseUnlocked(string caseId, int profileId)
    {
        var casedef = AllCases.Find(c => c.CaseId == caseId);
        if (casedef == null) return false;

        // No requirement means it's always accessible
        if (string.IsNullOrEmpty(casedef.RequiredCaseId)) return true;

        // Check if the required case is marked complete for this profile
        return IsCaseCompleted(casedef.RequiredCaseId, profileId);
    }

    /// <summary>
    /// Returns true if the profile has completed the given case.
    /// Reads from the case_progress table in saves.db.
    /// </summary>
    public bool IsCaseCompleted(string caseId, int profileId)
    {
        var result = DatabaseManager.Instance.RunSaveQueryWithResults(
            "SELECT id FROM case_progress WHERE profile_id = ? AND case_id = ? AND completed = 1",
            profileId.ToString(), caseId
        );
        return result != null && result.Count > 0;
    }

    /// <summary>
    /// Marks a case as completed for a profile. Called when the player finishes all tasks.
    /// Also triggers GameManager to auto-save.
    /// </summary>
    public void CompleteCase(string caseId, int profileId)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Insert or update the completion record
        DatabaseManager.Instance.RunSaveNonQuery(
            @"INSERT INTO case_progress (profile_id, case_id, completed, completed_at)
              VALUES (?, ?, 1, ?)
              ON CONFLICT(profile_id, case_id) DO UPDATE SET completed = 1, completed_at = ?",
            profileId, caseId, now, now
        );

        Debug.Log($"[CaseManager] Case '{caseId}' completed for profile {profileId}.");
        GameManager.Instance.AutoSave("case_completed");
    }

    /// <summary>
    /// Returns which task index the player is currently on for the active case.
    /// Reads from saves.db so it survives across sessions.
    /// </summary>
    public int GetCurrentTaskIndex(int profileId, string caseId)
    {
        var result = DatabaseManager.Instance.RunSaveQueryWithResults(
            "SELECT current_task FROM case_progress WHERE profile_id = ? AND case_id = ?",
            profileId.ToString(), caseId
        );

        if (result == null || result.Count == 0) return 0;
        return int.TryParse(result[0]["current_task"], out int idx) ? idx : 0;
    }

    /// <summary>
    /// Advances the player to the next task. Called by TaskValidator on a correct answer.
    /// </summary>
    public void AdvanceTask(int profileId, string caseId, int nextTaskIndex)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        DatabaseManager.Instance.RunSaveNonQuery(
            @"INSERT INTO case_progress (profile_id, case_id, current_task, completed, completed_at)
              VALUES (?, ?, ?, 0, NULL)
              ON CONFLICT(profile_id, case_id) DO UPDATE SET current_task = ?",
            profileId, caseId, nextTaskIndex, nextTaskIndex
        );

        Debug.Log($"[CaseManager] Profile {profileId} advanced to task {nextTaskIndex} in '{caseId}'.");
        GameManager.Instance.AutoSave("task_completed");
    }
}