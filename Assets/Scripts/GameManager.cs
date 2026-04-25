// GameManager.cs
// Central state machine for the Murder Mystery SQL Game.
//
// PROFILE SYSTEM:
//   Each player creates a named detective profile (e.g. "Detective Harris").
//   Every profile has exactly one save state — no slots, no duplicates.
//   Auto-saves at key gameplay moments. Manual save available from the pause menu.
//
// WORLD MODES:
//   "unique" — each profile gets its own freshly generated murder world (default)
//   "shared" — all profiles investigate the same case
//   Switch by changing WORLD_MODE constant. No other code needs to change.
//
// SETUP:
//   Attach to a persistent GameObject in your MainMenu scene.
//   Fill in the four scene name fields in the Unity Inspector.
//   Wire your canvas buttons to the public methods below.

using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Scene Names (fill these in via the Unity Inspector) ─────────────────
    [Header("Scene Names — match your Unity Build Settings exactly")]
    public string mainMenuScene = "MainMenu";
    public string newGameScene = "NewGameSetup";  // Name input screen
    public string loadGameScene = "LoadGame";       // Profile select screen
    public string gameScene = "MurderMystery";   // The actual game

    // ─── World Mode ───────────────────────────────────────────────────────────
    // "unique" = every profile gets its own generated murder world
    // "shared" = all profiles share one world (decide with your team later)
    public const string WORLD_MODE = "unique";

    // ─── Game State ───────────────────────────────────────────────────────────
    public enum GameState { MainMenu, CreatingProfile, Playing, Paused, LoadGame }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // The active profile ID. -1 means no profile is loaded.
    public int ActiveProfileId { get; private set; } = -1;
    public string ActiveProfileName { get; private set; } = "";

    // ─── Events ───────────────────────────────────────────────────────────────
    // Subscribe to these in other scripts to react to state changes.
    // Example in another script: GameManager.OnGameSaved += () => ShowSaveIcon();
    public static event Action<GameState> OnGameStateChanged;
    public static event Action OnGameSaved;
    public static event Action OnGameLoaded;

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
    }

    // ─── State Transition ─────────────────────────────────────────────────────
    private void SetState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }

    // =========================================================================
    // PUBLIC API — wire your canvas buttons to these methods
    // =========================================================================

    /// <summary>
    /// Called by the "New Game" button on the main menu.
    /// Just transitions to the name input screen.
    /// The profile is not created until the player confirms their name.
    /// </summary>
    public void StartNewGameFlow()
    {
        SetState(GameState.CreatingProfile);

        // Set the first available ready case as active so the intro
        // scene can display its title. For now this is always case_01.
        // Later this can be driven by a case select screen.
        var firstCase = CaseManager.Instance.AllCases.Find(c => c.IsReadyToPlay);
        if (firstCase != null)
            CaseManager.Instance.SetActiveCase(firstCase.CaseId);
        else
            Debug.LogWarning("[GameManager] No ready cases found. Title will show placeholder.");

        SceneManager.LoadScene(newGameScene);
    }

    /// <summary>
    /// Called by the Confirm button on the NewGameSetup screen.
    /// This is where the profile and world are actually created.
    /// Wire the name input field's text to this method's parameter.
    /// </summary>
    public void ConfirmNewGame(string detectiveName)
    {
        // Fall back to a default if the player left the name blank
        detectiveName = string.IsNullOrWhiteSpace(detectiveName)
            ? "Detective"
            : detectiveName.Trim();

        Debug.Log($"[GameManager] Creating new profile: {detectiveName}");

        // 1. Clean up any orphaned world databases from previous test runs
        //    Finds all profiles and deletes their world files before creating a new one
        CleanUpOldWorldFiles();

        // 2. Create the profile row in saves.db — returns the new profile's ID
        int profileId = DatabaseManager.Instance.CreateProfile(detectiveName, WORLD_MODE);
        ActiveProfileId = profileId;
        ActiveProfileName = detectiveName;

        // 2. Open (or create) the world database for this profile
        DatabaseManager.Instance.OpenWorldDb(profileId, WORLD_MODE);

        // 3. Generate the full static world — only ever called here, never on load
        StaticDbGenerator.Instance.GenerateWorld();

        // 4. Tell the validator which tables are static so it can protect them
        TaskValidator.Instance.RegisterStaticTables(CaseManager.Instance.ActiveCase);

        // 5. Seed the player-writable dynamic tables (logfile, hounds, etc.)
        DynamicDbSeeder.Instance.SeedNewGame(profileId);

        // 6. Unlock the two starting tables every new detective gets access to
        DatabaseManager.Instance.UnlockTable(profileId, "persons");
        DatabaseManager.Instance.UnlockTable(profileId, "crime_scene");

        // 7. Auto-save so the profile immediately appears on the load screen
        AutoSave("new_game");

        // 8. Load the game scene
        SetState(GameState.Playing);
        SceneManager.LoadScene(gameScene);
    }

    /// <summary>
    /// Called by the "Load Game" button on the main menu.
    /// Transitions to the profile select screen.
    /// </summary>
    public void OpenLoadGameScreen()
    {
        SetState(GameState.LoadGame);
        SceneManager.LoadScene(loadGameScene);
    }

    /// <summary>
    /// Called when the player taps a profile card on the LoadGame screen.
    /// Resumes that detective's investigation exactly where they left off.
    /// </summary>
    public void LoadProfile(int profileId)
    {
        Debug.Log($"[GameManager] Loading profile {profileId}...");

        // Verify the profile actually exists before doing anything
        string name = DatabaseManager.Instance.GetProfileName(profileId);
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError($"[GameManager] Profile {profileId} not found.");
            SetState(GameState.MainMenu);
            SceneManager.LoadScene(mainMenuScene);
            return;
        }

        ActiveProfileId = profileId;
        ActiveProfileName = name;

        // Open the correct world database for this profile
        DatabaseManager.Instance.OpenWorldDb(profileId, WORLD_MODE);

        // Resolve which case this profile is currently on and set it as active
        string activeCaseId = DatabaseManager.Instance.GetActiveCaseId(profileId);
        if (!string.IsNullOrEmpty(activeCaseId))
        {
            CaseManager.Instance.SetActiveCase(activeCaseId);

            // Re-register static tables so the validator knows what to protect
            TaskValidator.Instance.RegisterStaticTables(CaseManager.Instance.ActiveCase);

            // Ensure dynamic tables exist (safe — never overwrites existing data)
            DynamicDbSeeder.Instance.EnsureTablesExist(profileId);
        }

        Debug.Log($"[GameManager] Resumed: {name} (id {profileId})");

        SetState(GameState.Playing);
        SceneManager.LoadScene(gameScene);
        OnGameLoaded?.Invoke();
    }

    /// <summary>
    /// Manual save — called by the Save button in the pause menu.
    /// </summary>
    public void SaveGame()
    {
        AutoSave("manual");
    }

    /// <summary>
    /// Auto-save triggered at key gameplay moments.
    /// Call this after: a SQL query runs, a suspect is interrogated, a table unlocks.
    /// </summary>
    public void AutoSave(string reason = "auto")
    {
        if (ActiveProfileId < 0)
        {
            Debug.LogWarning("[GameManager] Save attempted with no active profile.");
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int playtime = Mathf.FloorToInt(Time.realtimeSinceStartup);

        DatabaseManager.Instance.RunSaveNonQuery(
            "UPDATE save_slots SET last_played = ?, playtime_seconds = playtime_seconds + ? WHERE id = ?",
            timestamp, playtime, ActiveProfileId
        );

        Debug.Log($"[GameManager] Saved profile {ActiveProfileId} ({reason}).");
        OnGameSaved?.Invoke();
    }

    /// <summary>
    /// Saves progress and returns to the main menu.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (CurrentState == GameState.Playing)
            AutoSave("return_to_menu");

        ActiveProfileId = -1;
        ActiveProfileName = "";
        SetState(GameState.MainMenu);
        SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>
    /// Pauses or unpauses the game.
    /// </summary>
    public void TogglePause()
    {
        if (CurrentState == GameState.Playing)
        {
            SetState(GameState.Paused);
            Time.timeScale = 0f;
        }
        else if (CurrentState == GameState.Paused)
        {
            SetState(GameState.Playing);
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Permanently deletes a profile and its world database if in unique mode.
    /// Wire to a Delete button on the profile select screen.
    /// </summary>
    public void DeleteProfile(int profileId)
    {
        DatabaseManager.Instance.DeleteProfile(profileId);
        Debug.Log($"[GameManager] Profile {profileId} deleted.");
    }

    // ─── Application Quit ─────────────────────────────────────────────────────
    void OnApplicationQuit()
    {
        if (CurrentState == GameState.Playing && ActiveProfileId >= 0)
        {
            Debug.Log("[GameManager] Auto-saving on quit...");
            AutoSave("app_quit");
        }
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes any world database files that no longer have a matching profile
    /// in saves.db. Prevents world_1.db, world_2.db... accumulating on disk.
    /// Called before creating a new profile.
    /// </summary>
    private void CleanUpOldWorldFiles()
    {
        try
        {
            // Get all profile IDs that actually exist in saves.db
            var profiles = DatabaseManager.Instance.GetAllProfiles();
            var validIds = new System.Collections.Generic.HashSet<string>();
            foreach (var p in profiles)
                if (p.ContainsKey("id")) validIds.Add(p["id"]);

            // Find all world_*.db files on disk
            string[] files = System.IO.Directory.GetFiles(
                Application.persistentDataPath, "world_*.db");

            foreach (string file in files)
            {
                // Extract the ID number from the filename e.g. world_42.db → 42
                string filename = System.IO.Path.GetFileNameWithoutExtension(file);
                string idPart = filename.Replace("world_", "");

                // Delete if no matching profile exists
                if (!validIds.Contains(idPart))
                {
                    System.IO.File.Delete(file);
                    Debug.Log($"[GameManager] Cleaned up orphaned world file: {filename}.db");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameManager] CleanUpOldWorldFiles failed: {ex.Message}");
        }
    }
}