// DatabaseManager.cs
// Manages two SQLite database connections:
//   world_[id].db or world_shared.db  — the static crime world
//   saves.db                          — all player profiles and their progress
//
// WORLD MODES:
//   "unique" — each profile gets world_[profileId].db
//   "shared" — all profiles use world_shared.db
//
// All other scripts go through this singleton.
//   Static world queries  → RunWorldNonQuery / RunWorldQueryWithResults
//   Save / profile queries → RunSaveNonQuery  / RunSaveQueryWithResults
//
// SQLite-net: https://github.com/praeclarum/sqlite-net/wiki
// SQLite3:    https://www.sqlite.org/c3ref/funclist.html

using UnityEngine;
using SQLite;
using System.Collections.Generic;
using System.IO;

public class DatabaseManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static DatabaseManager Instance { get; private set; }

    // ─── Database Connections ─────────────────────────────────────────────────
    private SQLiteConnection worldDb; // Static crime world
    private SQLiteConnection saveDb;  // All profiles and progress

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Always open saves.db immediately — profiles are listed on the main menu
        OpenSaveDb();
    }

    // =========================================================================
    // DATABASE INITIALISATION
    // =========================================================================

    /// <summary>
    /// Opens saves.db and ensures the full profile schema exists.
    /// Called automatically on Awake.
    /// </summary>
    private void OpenSaveDb()
    {
        saveDb?.Close();
        string path = Application.persistentDataPath + "/saves.db";
        saveDb = new SQLiteConnection(path);
        Debug.Log($"[DatabaseManager] saves.db → {path}");
        InitialiseSaveSchema();
    }

    /// <summary>
    /// Opens the correct world database for a profile.
    /// "unique" mode → world_[profileId].db
    /// "shared" mode → world_shared.db
    /// Called by GameManager on both NewGame and LoadProfile.
    /// </summary>
    public void OpenWorldDb(int profileId, string worldMode)
    {
        worldDb?.Close();

        string filename = worldMode == "shared"
            ? "world_shared.db"
            : $"world_{profileId}.db";

        string path = Application.persistentDataPath + "/" + filename;
        worldDb = new SQLiteConnection(path);
        Debug.Log($"[DatabaseManager] world db → {path}");
    }

    // =========================================================================
    // SAVE SCHEMA
    // =========================================================================

    /// <summary>
    /// Creates all progress tables in saves.db if they don't already exist.
    /// Safe to call multiple times — all statements use IF NOT EXISTS.
    /// </summary>
    private void InitialiseSaveSchema()
    {
        // One row per detective profile
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS save_slots (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_name     TEXT    NOT NULL,
                world_mode       TEXT    NOT NULL DEFAULT 'unique',
                created_at       TEXT,
                last_played      TEXT,
                playtime_seconds INTEGER DEFAULT 0
            )");

        // Clues the player has found so far
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS discovered_clues (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id INTEGER NOT NULL,
                clue_id    INTEGER NOT NULL,
                found_at   TEXT
            )");

        // Every SQL query the player has run — used for hints and demo replay
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS query_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id  INTEGER NOT NULL,
                sql_text    TEXT    NOT NULL,
                executed_at TEXT,
                row_count   INTEGER DEFAULT 0
            )");

        // NPCs the player has interrogated
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS interrogated_persons (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id INTEGER NOT NULL,
                person_id  INTEGER NOT NULL,
                talked_at  TEXT
            )");

        // Free-text notes pinned to suspects, locations, or clues
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS player_notes (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id  INTEGER NOT NULL,
                target_type TEXT,
                target_id   INTEGER,
                note_text   TEXT,
                created_at  TEXT
            )");

        // Which static tables the player is currently allowed to query
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS unlocked_tables (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id  INTEGER NOT NULL,
                table_name  TEXT    NOT NULL,
                unlocked_at TEXT
            )");

        // Accusations made — both failed and the final correct one
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS accusations (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id  INTEGER NOT NULL,
                person_id   INTEGER,
                weapon_id   INTEGER,
                location_id INTEGER,
                is_correct  INTEGER DEFAULT 0,
                made_at     TEXT
            )");

        // Tracks which task each profile is on per case, and whether it's completed
        RunSaveNonQuery(@"
            CREATE TABLE IF NOT EXISTS case_progress (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id   INTEGER NOT NULL,
                case_id      TEXT    NOT NULL,
                current_task INTEGER DEFAULT 0,
                completed    INTEGER DEFAULT 0,
                completed_at TEXT,
                UNIQUE(profile_id, case_id)
            )");

        Debug.Log("[DatabaseManager] Save schema ready.");
    }

    // =========================================================================
    // PROFILE MANAGEMENT
    // =========================================================================

    /// <summary>
    /// Creates a new detective profile in saves.db.
    /// Returns the new profile's auto-generated ID.
    /// </summary>
    public int CreateProfile(string profileName, string worldMode)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RunSaveNonQuery(
            "INSERT INTO save_slots (profile_name, world_mode, created_at, last_played) VALUES (?, ?, ?, ?)",
            profileName, worldMode, now, now
        );

        // Retrieve the ID that was just auto-generated
        var result = RunSaveQueryWithResults(
            "SELECT id FROM save_slots WHERE profile_name = ? ORDER BY id DESC LIMIT 1",
            profileName
        );

        int profileId = int.Parse(result[0]["id"]);
        Debug.Log($"[DatabaseManager] Profile created: '{profileName}' (id {profileId})");
        return profileId;
    }

    /// <summary>
    /// Returns all profiles as a list for displaying on the load screen.
    /// Each row contains: id, profile_name, created_at, last_played, playtime_seconds.
    /// </summary>
    public List<Dictionary<string, string>> GetAllProfiles()
    {
        return RunSaveQueryWithResults(
            "SELECT id, profile_name, created_at, last_played, playtime_seconds FROM save_slots ORDER BY last_played DESC"
        );
    }

    /// <summary>
    /// Returns a single profile's detective name. Returns empty string if not found.
    /// Used by GameManager.LoadProfile() to verify the profile exists.
    /// </summary>
    public string GetProfileName(int profileId)
    {
        var result = RunSaveQueryWithResults(
            "SELECT profile_name FROM save_slots WHERE id = ?",
            profileId.ToString()
        );
        return (result != null && result.Count > 0) ? result[0]["profile_name"] : "";
    }

    /// <summary>
    /// Returns the case_id the profile was most recently playing.
    /// Finds the incomplete case with the highest task progress,
    /// or the last completed case if everything is done.
    /// Returns empty string if the profile has no case history yet.
    /// </summary>
    public string GetActiveCaseId(int profileId)
    {
        // First try to find an incomplete case (completed = 0)
        var result = RunSaveQueryWithResults(
            @"SELECT case_id FROM case_progress 
              WHERE profile_id = ? AND completed = 0 
              ORDER BY id DESC LIMIT 1",
            profileId.ToString()
        );

        if (result != null && result.Count > 0)
            return result[0]["case_id"];

        // Fall back to the most recently completed case
        result = RunSaveQueryWithResults(
            @"SELECT case_id FROM case_progress 
              WHERE profile_id = ? 
              ORDER BY id DESC LIMIT 1",
            profileId.ToString()
        );

        return (result != null && result.Count > 0) ? result[0]["case_id"] : "";
    }

    /// <summary>
    /// Permanently deletes a profile and all its progress data.
    /// Also deletes the world database file if in unique mode.
    /// </summary>
    public void DeleteProfile(int profileId)
    {
        // Get world mode before deleting the profile row
        var result = RunSaveQueryWithResults(
            "SELECT world_mode FROM save_slots WHERE id = ?",
            profileId.ToString()
        );

        if (result != null && result.Count > 0 && result[0]["world_mode"] == "unique")
        {
            string worldPath = Application.persistentDataPath + $"/world_{profileId}.db";
            if (File.Exists(worldPath))
            {
                File.Delete(worldPath);
                Debug.Log($"[DatabaseManager] Deleted world file: {worldPath}");
            }
        }

        // Delete all progress rows for this profile
        RunSaveNonQuery("DELETE FROM save_slots            WHERE id         = ?", profileId);
        RunSaveNonQuery("DELETE FROM discovered_clues      WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM query_log             WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM interrogated_persons  WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM player_notes          WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM unlocked_tables       WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM accusations           WHERE profile_id = ?", profileId);
        RunSaveNonQuery("DELETE FROM case_progress         WHERE profile_id = ?", profileId);

        Debug.Log($"[DatabaseManager] Profile {profileId} fully deleted.");
    }

    // =========================================================================
    // PROGRESSION HELPERS
    // =========================================================================

    /// <summary>
    /// Unlocks a static table for a profile, granting them permission to query it.
    /// Call this as the player progresses through the investigation.
    /// </summary>
    public void UnlockTable(int profileId, string tableName)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RunSaveNonQuery(
            "INSERT INTO unlocked_tables (profile_id, table_name, unlocked_at) VALUES (?, ?, ?)",
            profileId, tableName, now
        );
        Debug.Log($"[DatabaseManager] '{tableName}' unlocked for profile {profileId}.");
    }

    /// <summary>
    /// Returns true if the player is allowed to query the given static table.
    /// Use this to validate player SQL queries before running them.
    /// </summary>
    public bool IsTableUnlocked(int profileId, string tableName)
    {
        var result = RunSaveQueryWithResults(
            "SELECT id FROM unlocked_tables WHERE profile_id = ? AND table_name = ?",
            profileId.ToString(), tableName
        );
        return result != null && result.Count > 0;
    }

    /// <summary>
    /// Logs a SQL query the player ran. Called by the SQL input system.
    /// </summary>
    public void LogPlayerQuery(int profileId, string sql, int rowCount = 0)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RunSaveNonQuery(
            "INSERT INTO query_log (profile_id, sql_text, executed_at, row_count) VALUES (?, ?, ?, ?)",
            profileId, sql, now, rowCount
        );
    }

    /// <summary>
    /// Records that the player has discovered a clue.
    /// </summary>
    public void DiscoverClue(int profileId, int clueId)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RunSaveNonQuery(
            "INSERT INTO discovered_clues (profile_id, clue_id, found_at) VALUES (?, ?, ?)",
            profileId, clueId, now
        );
    }

    /// <summary>
    /// Records that the player has interrogated an NPC.
    /// </summary>
    public void RecordInterrogation(int profileId, int personId)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RunSaveNonQuery(
            "INSERT INTO interrogated_persons (profile_id, person_id, talked_at) VALUES (?, ?, ?)",
            profileId, personId, now
        );
    }

    // =========================================================================
    // WORLD DB QUERIES  (static crime world — read during play, written on new game)
    // =========================================================================

    /// <summary>Write to world.db — used only during world generation.</summary>
    public void RunWorldNonQuery(string sql, params object[] args)
    {
        worldDb.Execute(sql, args);
    }

    /// <summary>Read from world.db. Returns rows as a list of string dictionaries.</summary>
    public List<Dictionary<string, string>> RunWorldQueryWithResults(string sql, params string[] args)
    {
        return ExecuteQueryWithResults(worldDb, sql, args);
    }

    /// <summary>Debug only — prints world.db query results to the console.</summary>
    public void RunWorldQuery(string sql)
    {
        ExecuteQueryToLog(worldDb, sql);
    }

    // =========================================================================
    // SAVE DB QUERIES  (player profiles and progress)
    // =========================================================================

    /// <summary>Write to saves.db.</summary>
    public void RunSaveNonQuery(string sql, params object[] args)
    {
        saveDb.Execute(sql, args);
    }

    /// <summary>Read from saves.db. Returns rows as a list of string dictionaries.</summary>
    public List<Dictionary<string, string>> RunSaveQueryWithResults(string sql, params string[] args)
    {
        return ExecuteQueryWithResults(saveDb, sql, args);
    }

    // =========================================================================
    // LEGACY API  (keeps existing test scripts working)
    // =========================================================================

    /// <summary>Deprecated — routes to world.db. Use RunWorldNonQuery instead.</summary>
    public void RunNonQuery(string sql, params object[] args)
    {
        worldDb.Execute(sql, args);
    }

    /// <summary>Deprecated — routes to world.db. Use RunWorldQueryWithResults instead.</summary>
    public List<Dictionary<string, string>> RunQueryWithResults(string sql)
    {
        return ExecuteQueryWithResults(worldDb, sql);
    }

    // =========================================================================
    // INTERNAL HELPERS
    // =========================================================================

    private List<Dictionary<string, string>> ExecuteQueryWithResults(
        SQLiteConnection connection, string sql, params string[] args)
    {
        var results = new List<Dictionary<string, string>>();
        var stmt = SQLite3.Prepare2(connection.Handle, sql);

        try
        {
            for (int a = 0; a < args.Length; a++)
                SQLite3.BindText(stmt, a + 1, args[a], -1, new System.IntPtr(-1));

            int cols = SQLite3.ColumnCount(stmt);

            while (SQLite3.Step(stmt) == SQLite3.Result.Row)
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < cols; i++)
                {
                    row[SQLite3.ColumnName16(stmt, i)] = SQLite3.ColumnString(stmt, i);
                }
                results.Add(row);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DatabaseManager] Query failed: {ex.Message}\nSQL: {sql}");
        }
        finally
        {
            SQLite3.Finalize(stmt);
        }

        return results;
    }

    private void ExecuteQueryToLog(SQLiteConnection connection, string sql)
    {
        var stmt = SQLite3.Prepare2(connection.Handle, sql);
        int cols = SQLite3.ColumnCount(stmt);

        while (SQLite3.Step(stmt) == SQLite3.Result.Row)
        {
            string line = "";
            for (int i = 0; i < cols; i++)
                line += $"{SQLite3.ColumnName16(stmt, i)}: {SQLite3.ColumnString(stmt, i)} | ";
            Debug.Log(line);
        }
        SQLite3.Finalize(stmt);
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        worldDb?.Close();
        saveDb?.Close();
    }
}