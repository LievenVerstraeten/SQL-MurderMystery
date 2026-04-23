// StaticDbGenerator.cs
// Seeds the static world database (world_[profileId].db) from a CaseDefinition.
//
// Called ONCE by GameManager.ConfirmNewGame() — never on scene load.
// Reads the CaseDefinition's StaticTables list and executes each
// CREATE TABLE and INSERT statement in order.
//
// The player can only SELECT from these tables during gameplay.
// They cannot INSERT, UPDATE, or DELETE from static tables.
// That restriction is enforced by TaskValidator.
//
// SETUP:
//   Attach to the same persistent GameObject as GameManager and DatabaseManager.

using UnityEngine;

public class StaticDbGenerator : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static StaticDbGenerator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================================
    // MAIN ENTRY POINT
    // =========================================================================

    /// <summary>
    /// Seeds world.db from the active CaseDefinition's StaticTables.
    /// Called once by GameManager.ConfirmNewGame() after OpenWorldDb().
    /// </summary>
    public void GenerateWorld()
    {
        CaseDefinition activeCase = CaseManager.Instance.ActiveCase;

        if (activeCase == null)
        {
            Debug.LogError("[StaticDbGenerator] No active case set. Call CaseManager.SetActiveCase() first.");
            return;
        }

        if (activeCase.StaticTables == null || activeCase.StaticTables.Count == 0)
        {
            Debug.LogWarning($"[StaticDbGenerator] Case '{activeCase.CaseId}' has no static tables defined.");
            return;
        }

        Debug.Log($"[StaticDbGenerator] Seeding world for case: {activeCase.Title}");

        int tableCount = 0;
        int insertCount = 0;

        foreach (var table in activeCase.StaticTables)
        {
            // Create the table
            if (!string.IsNullOrEmpty(table.CreateSQL))
            {
                DatabaseManager.Instance.RunWorldNonQuery(table.CreateSQL);
                tableCount++;
            }

            // Insert all seed rows
            if (table.InsertStatements != null)
            {
                foreach (var insert in table.InsertStatements)
                {
                    if (!string.IsNullOrEmpty(insert))
                    {
                        DatabaseManager.Instance.RunWorldNonQuery(insert);
                        insertCount++;
                    }
                }
            }
        }

        Debug.Log($"[StaticDbGenerator] World seeded — {tableCount} tables, {insertCount} rows inserted.");
    }
}