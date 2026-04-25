// DynamicDbSeeder.cs
// Seeds the player-writable tables into saves.db at the start of a new case.
//
// These are the tables the player actively modifies during gameplay —
// e.g. the logfile (which starts as Unsolved and gets updated),
// the hounds table (tutorial), or any case-specific table the player creates.
//
// Called ONCE by GameManager.ConfirmNewGame() after StaticDbGenerator.GenerateWorld().
// Safe to call on LoadProfile() too — uses IF NOT EXISTS so it never
// overwrites existing progress if the player is resuming.
//
// SETUP:
//   Attach to the same persistent GameObject as GameManager and DatabaseManager.

using UnityEngine;

public class DynamicDbSeeder : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static DynamicDbSeeder Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================================
    // MAIN ENTRY POINTS
    // =========================================================================

    /// <summary>
    /// Seeds dynamic tables for a brand new game.
    /// Runs all CREATE TABLE and INSERT statements from the CaseDefinition.
    /// Called by GameManager.ConfirmNewGame().
    /// </summary>
    public void SeedNewGame(int profileId)
    {
        CaseDefinition activeCase = CaseManager.Instance.ActiveCase;

        if (activeCase == null)
        {
            Debug.LogError("[DynamicDbSeeder] No active case. Call CaseManager.SetActiveCase() first.");
            return;
        }

        Debug.Log($"[DynamicDbSeeder] Seeding dynamic tables for profile {profileId}, case {activeCase.CaseId}");

        SeedDynamicTables(activeCase, profileId, isNewGame: true);
        CreateCaseProgressEntry(profileId, activeCase.CaseId);

        Debug.Log("[DynamicDbSeeder] Dynamic seeding complete.");
    }

    /// <summary>
    /// Ensures dynamic tables exist when loading a saved profile.
    /// Uses IF NOT EXISTS — never overwrites existing player progress.
    /// Called by GameManager.LoadProfile().
    /// </summary>
    public void EnsureTablesExist(int profileId)
    {
        CaseDefinition activeCase = CaseManager.Instance.ActiveCase;

        if (activeCase == null)
        {
            Debug.LogError("[DynamicDbSeeder] No active case set.");
            return;
        }

        Debug.Log($"[DynamicDbSeeder] Ensuring dynamic tables exist for profile {profileId}");

        // Only creates tables — never re-inserts seed data on load
        SeedDynamicTables(activeCase, profileId, isNewGame: false);
    }

    // =========================================================================
    // INTERNAL LOGIC
    // =========================================================================

    /// <summary>
    /// Iterates over the case's DynamicTables and executes each statement.
    /// On new game: runs CREATE TABLE + INSERT statements.
    /// On load:     runs CREATE TABLE only (inserts skipped to protect progress).
    /// </summary>
    private void SeedDynamicTables(CaseDefinition casedef, int profileId, bool isNewGame)
    {
        if (casedef.DynamicTables == null || casedef.DynamicTables.Count == 0)
        {
            Debug.LogWarning($"[DynamicDbSeeder] Case '{casedef.CaseId}' has no dynamic tables defined.");
            return;
        }

        int tableCount = 0;
        int insertCount = 0;

        foreach (var table in casedef.DynamicTables)
        {
            // Dynamic game tables now live in world.db (per-profile) so that
            // SELECT * FROM sqlite_master shows only game tables to the player.
            if (!string.IsNullOrEmpty(table.CreateSQL))
            {
                string createSql = EnsureIfNotExists(table.CreateSQL);
                DatabaseManager.Instance.RunWorldNonQuery(createSql);
                tableCount++;
            }

            // Only insert seed rows on a fresh new game.
            // On load the world.db already contains the player's progress.
            if (isNewGame && table.InsertStatements != null)
            {
                foreach (var insert in table.InsertStatements)
                {
                    if (!string.IsNullOrEmpty(insert))
                    {
                        string sql = insert.Replace("{PROFILE_ID}", profileId.ToString());
                        DatabaseManager.Instance.RunWorldNonQuery(sql);
                        insertCount++;
                    }
                }
            }
        }

        Debug.Log($"[DynamicDbSeeder] {tableCount} tables checked, {insertCount} rows seeded.");
    }

    /// <summary>
    /// Creates or resets the case_progress entry for this profile and case.
    /// Only called on new game — not on load.
    /// </summary>
    private void CreateCaseProgressEntry(int profileId, string caseId)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // INSERT OR IGNORE — if a progress row already exists, leave it alone
        DatabaseManager.Instance.RunSaveNonQuery(
            @"INSERT OR IGNORE INTO case_progress (profile_id, case_id, current_task, completed)
              VALUES (?, ?, 0, 0)",
            profileId, caseId
        );

        Debug.Log($"[DynamicDbSeeder] Case progress entry ready for profile {profileId}, case {caseId}.");
    }

    /// <summary>
    /// Ensures a CREATE TABLE statement includes IF NOT EXISTS.
    /// Protects against accidentally wiping a table that already has player data.
    /// </summary>
    private string EnsureIfNotExists(string createSQL)
    {
        // Already has it — return as-is
        if (createSQL.Contains("IF NOT EXISTS"))
            return createSQL;

        // Insert IF NOT EXISTS after CREATE TABLE
        return createSQL.Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");
    }
}