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

    public CaseDefinition        ActiveCase { get; private set; }
    public List<CaseDefinition>  AllCases   { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCaseList();
    }

    // =========================================================================
    // CASE LIST
    // =========================================================================

    private void BuildCaseList()
    {
        AllCases = new List<CaseDefinition>();

        // ── Case 1 — Murder of the Somerton Man ───────────────────────────────
        // Teaches: SELECT *, INSERT, CREATE TABLE, WHERE, SELECT columns,
        //          JOIN, LIKE, ORDER BY, UPDATE, COUNT/GROUP BY, NULL,
        //          sqlite_master, UNION, computed columns / aliases, hashing concept
        var case1 = new CaseDefinition(
            caseId:         "case_01",
            title:          "Murder of the Somerton Man",
            overview:       "A body was found on Somerton Beach. No ID. No cause of death.",
            timeline:       "1948, 1st of December",
            reward:         200000,
            difficulty:     "Beginner",
            requiredCaseId: "",
            isReady:        true
        );
        case1.ConclusionType = "inconclusive";
        case1.EpilogueText   =
            "The Somerton Man was found on December 1, 1948. He was never officially identified. " +
            "The cause of his death was never confirmed. The cipher in the Rubaiyat was never decoded. " +
            "Jessica Thomson died in 2007 without ever publicly revealing what she knew. " +
            "In 2022, forensic genealogist Derek Abbott identified the man as Carl \"Charles\" Webb " +
            "with 99.9% confidence through DNA analysis. " +
            "South Australia Police have not officially confirmed the identification. Tamam Shud.";

        // ── Static tables (seeded into world.db, player can only SELECT) ──────

        // contacts — witness addresses; static, player never writes to this
        var contactsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS contacts (" +
            "id TEXT PRIMARY KEY, address TEXT, phone TEXT)");
        contactsTable.InsertStatements.Add("INSERT INTO contacts VALUES ('C001','40 Moseley Street, Glenelg','unlisted')");
        contactsTable.InsertStatements.Add("INSERT INTO contacts VALUES ('C002','12 Jetty Road, Glenelg','unlisted')");
        contactsTable.InsertStatements.Add("INSERT INTO contacts VALUES ('C003','90A Moseley Street, Glenelg','X3239')");
        contactsTable.InsertStatements.Add("INSERT INTO contacts VALUES ('C004','University of Adelaide, North Tce','unlisted')");
        case1.StaticTables.Add(contactsTable);

        // cipher_fragments — static evidence, player reads but never modifies
        var cipherTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS cipher_fragments (" +
            "id TEXT PRIMARY KEY, fragment TEXT, origin TEXT, known_language TEXT, decoded INTEGER)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF001','WRGOABABD','Rubaiyat back cover','Unknown',0)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF002','MLIAOI','Rubaiyat back cover','Unknown',0)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF003','WTMTSTMSA','Rubaiyat back cover','Unknown',0)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF004','ITTMTSAS','Rubaiyat back cover','Unknown',0)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF005','AIAQC','Rubaiyat back cover','Unknown',0)");
        cipherTable.InsertStatements.Add("INSERT INTO cipher_fragments VALUES ('CF006','TAMAM SHUD','Torn page, trouser pocket','Persian',1)");
        case1.StaticTables.Add(cipherTable);

        // internet archive tables (accessed via internet.tablename in script)
        var internetSchema = "id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT";
        var bbcTable = new TableDefinition($"CREATE TABLE IF NOT EXISTS bbc_news ({internetSchema})");
        bbcTable.InsertStatements.Add("INSERT INTO bbc_news VALUES (1,'DNA Analysis Identifies Somerton Man as Carl \"Charles\" Webb','2022-07-26','Somerton Webb Tamam Shud','Full article.','bbc_news')");
        bbcTable.InsertStatements.Add("INSERT INTO bbc_news VALUES (2,'Carl Webb: Electrical Engineer, Melbourne, Born 1905','2022-07-27','Webb Somerton','Full article.','bbc_news')");
        case1.StaticTables.Add(bbcTable);

        var abcTable = new TableDefinition($"CREATE TABLE IF NOT EXISTS abc_australia ({internetSchema})");
        abcTable.InsertStatements.Add("INSERT INTO abc_australia VALUES (1,'The Tamam Shud Mystery: Australia''s Most Puzzling Cold Case','1978-08-19','Somerton Tamam Shud Jestyn','Full article.','abc_australia')");
        abcTable.InsertStatements.Add("INSERT INTO abc_australia VALUES (2,'Carl Webb: Electrical Engineer, Melbourne, Born 1905','2022-07-27','Webb Somerton','Full article.','abc_australia')");
        case1.StaticTables.Add(abcTable);

        var saPoliceTable = new TableDefinition($"CREATE TABLE IF NOT EXISTS sa_police_records ({internetSchema})");
        saPoliceTable.InsertStatements.Add("INSERT INTO sa_police_records VALUES (1,'Somerton Inquest Returns Open Verdict','1949-06-17','Somerton Tamam Shud','Full article.','sa_police_records')");
        saPoliceTable.InsertStatements.Add("INSERT INTO sa_police_records VALUES (2,'SA Police Yet to Officially Confirm Somerton Man Identity','2023-04-01','Somerton Webb','Full article.','sa_police_records')");
        case1.StaticTables.Add(saPoliceTable);

        var advertiserTable = new TableDefinition($"CREATE TABLE IF NOT EXISTS adelaide_advertiser ({internetSchema})");
        advertiserTable.InsertStatements.Add("INSERT INTO adelaide_advertiser VALUES (1,'Unidentified Man Found Dead at Somerton Beach','1948-12-02','Somerton','Full article.','adelaide_advertiser')");
        advertiserTable.InsertStatements.Add("INSERT INTO adelaide_advertiser VALUES (2,'Cold Case: The Somerton Man 30 Years On','1978-12-01','Somerton Tamam Shud','Full article.','adelaide_advertiser')");
        case1.StaticTables.Add(advertiserTable);

        var forensicTable = new TableDefinition($"CREATE TABLE IF NOT EXISTS forensic_reports ({internetSchema})");
        forensicTable.InsertStatements.Add("INSERT INTO forensic_reports VALUES (1,'Forensic Genealogy Confirms Webb Identity with 99.9% Confidence','2022-11-14','Webb Somerton','Full article.','forensic_reports')");
        case1.StaticTables.Add(forensicTable);

        // internet_meta — replaces internet.sqlite_master in queries
        var internetMeta = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS internet_meta (type TEXT, name TEXT, sql TEXT)");
        internetMeta.InsertStatements.Add("INSERT INTO internet_meta VALUES ('table','bbc_news','CREATE TABLE bbc_news (id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT)')");
        internetMeta.InsertStatements.Add("INSERT INTO internet_meta VALUES ('table','abc_australia','CREATE TABLE abc_australia (id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT)')");
        internetMeta.InsertStatements.Add("INSERT INTO internet_meta VALUES ('table','sa_police_records','CREATE TABLE sa_police_records (id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT)')");
        internetMeta.InsertStatements.Add("INSERT INTO internet_meta VALUES ('table','adelaide_advertiser','CREATE TABLE adelaide_advertiser (id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT)')");
        internetMeta.InsertStatements.Add("INSERT INTO internet_meta VALUES ('table','forensic_reports','CREATE TABLE forensic_reports (id INTEGER PRIMARY KEY, headline TEXT, date TEXT, keywords TEXT, content TEXT, source TEXT)')");
        case1.StaticTables.Add(internetMeta);

        // ── Dynamic tables (seeded into world.db, player can modify) ──────────

        // hounds — tutorial table; player INSERTs their name
        var houndsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS hounds (id TEXT PRIMARY KEY, name TEXT)");
        houndsTable.InsertStatements.Add("INSERT INTO hounds VALUES ('01','Jerry')");
        case1.DynamicTables.Add(houndsTable);

        // logfile — player queries and updates this throughout the case
        var logfileTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS logfile (" +
            "id TEXT NOT NULL, namecase TEXT, status TEXT, " +
            "details TEXT, investigator TEXT, victim TEXT, murderer TEXT)");
        logfileTable.InsertStatements.Add(
            "INSERT INTO logfile VALUES ('01','Murder of Somerton Man','Unsolved'," +
            "'None, investigate further','N','?','?')");
        case1.DynamicTables.Add(logfileTable);

        // clues — player creates this table as a task, then inserts clues
        var cluesTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS clues " +
            "(id TEXT PRIMARY KEY, name TEXT, type TEXT, details TEXT, found_at TEXT)");
        // No seed rows — player inserts all clues as part of the story
        case1.DynamicTables.Add(cluesTable);

        // witnesses — player UPDATEs interviewed status
        var witnessesTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS witnesses " +
            "(id TEXT PRIMARY KEY, name TEXT, role TEXT, contact_id TEXT, interviewed INTEGER)");
        witnessesTable.InsertStatements.Add("INSERT INTO witnesses VALUES ('W001','John Lyons','Found the body','C001',0)");
        witnessesTable.InsertStatements.Add("INSERT INTO witnesses VALUES ('W002','Neil Hamilton','Saw man alive night before','C002',0)");
        witnessesTable.InsertStatements.Add("INSERT INTO witnesses VALUES ('W003','Jessica Thomson','Linked to the Rubaiyat','C003',0)");
        witnessesTable.InsertStatements.Add("INSERT INTO witnesses VALUES ('W004','John Burton Cleland','Examined the Tamam Shud','C004',0)");
        case1.DynamicTables.Add(witnessesTable);

        // suspects — player reads and joins; The Rival Hound added mid-story
        var suspectsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS suspects " +
            "(id TEXT PRIMARY KEY, name TEXT, motive TEXT, linked_clue TEXT, eliminated INTEGER)");
        suspectsTable.InsertStatements.Add("INSERT INTO suspects VALUES ('S001','Jessica Thomson','Known associate of victim','CL002',0)");
        suspectsTable.InsertStatements.Add("INSERT INTO suspects VALUES ('S002','Unknown Soviet Agent','Cold War espionage theory','CF003',0)");
        suspectsTable.InsertStatements.Add("INSERT INTO suspects VALUES ('S003','Alfred Boxall','Recipient of same Rubaiyat ed.','CL002',0)");
        suspectsTable.InsertStatements.Add("INSERT INTO suspects VALUES ('S004','The Rival Hound','Competing for reward',NULL,0)");
        case1.DynamicTables.Add(suspectsTable);

        // passwords — player reads only; puzzle component for locked room
        var passwordsTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS passwords (id TEXT PRIMARY KEY, label TEXT, hash TEXT)");
        passwordsTable.InsertStatements.Add("INSERT INTO passwords VALUES ('P001','room_exit_code','5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8')");
        passwordsTable.InsertStatements.Add("INSERT INTO passwords VALUES ('P002','agency_master','[REDACTED]')");
        passwordsTable.InsertStatements.Add("INSERT INTO passwords VALUES ('P003','case_completion','[REDACTED]')");
        case1.DynamicTables.Add(passwordsTable);

        // keys — player updates K001.value to solve the locked room
        var keysTable = new TableDefinition(
            "CREATE TABLE IF NOT EXISTS keys (id TEXT PRIMARY KEY, password_id TEXT, hint TEXT, value TEXT)");
        keysTable.InsertStatements.Add("INSERT INTO keys VALUES ('K001','P001','\"It is ended\" - the last words of the Rubaiyat',NULL)");
        keysTable.InsertStatements.Add("INSERT INTO keys VALUES ('K002','P002',NULL,NULL)");
        keysTable.InsertStatements.Add("INSERT INTO keys VALUES ('K003','P003',NULL,NULL)");
        case1.DynamicTables.Add(keysTable);

        // ── SQL Tasks (must align 1-to-1 with SQLTask nodes in Case01Story) ───

        // Task 0 — SELECT * FROM hounds  (result: 1 row, Jerry)
        var t0 = new SQLTask(
            "Show everyone listed in the hounds table.",
            "Use SELECT * FROM hounds; to read every row.",
            "SELECT", anyOrder: true);
        t0.ExpectedRows.Add(new Dictionary<string, string> { {"id","01"}, {"name","Jerry"} });
        case1.Tasks.Add(t0);

        // Task 1 — INSERT yourself into hounds (free write)
        case1.Tasks.Add(new SQLTask(
            "Add your name to the hounds table using INSERT INTO.",
            "INSERT INTO hounds (id, name) VALUES ('03', 'YourName');",
            "INSERT"));

        // Task 2 — SELECT * FROM hounds  (result: 3 rows; partial match on Jerry+Debbie)
        var t2 = new SQLTask(
            "Verify all three hounds are now listed.",
            "SELECT * FROM hounds;",
            "SELECT", anyOrder: true, partialMatch: true);
        t2.ExpectedRows.Add(new Dictionary<string, string> { {"id","01"}, {"name","Jerry"} });
        t2.ExpectedRows.Add(new Dictionary<string, string> { {"id","02"}, {"name","Debbie"} });
        case1.Tasks.Add(t2);

        // Task 3 — SELECT * FROM logfile
        var t3 = new SQLTask(
            "Show all data from the logfile table.",
            "Use the same SELECT command you used for hounds.",
            "SELECT", anyOrder: true, partialMatch: true);
        t3.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","01"}, {"namecase","Murder of Somerton Man"}, {"status","Unsolved"} });
        case1.Tasks.Add(t3);

        // Task 4 — CREATE TABLE clues (free write — any valid CREATE TABLE passes)
        case1.Tasks.Add(new SQLTask(
            "Create a table called clues with columns: id, name, type, details, found_at.",
            "CREATE TABLE clues (id TEXT PRIMARY KEY, name TEXT, type TEXT, details TEXT, found_at TEXT);",
            "CREATE TABLE"));

        // Task 5 — INSERT CL002 Tamam Shud (free write)
        case1.Tasks.Add(new SQLTask(
            "Insert the Tamam Shud clue into the clues table.",
            "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL002', ...);",
            "INSERT"));

        // Task 6 — SELECT * FROM clues (2 rows after CL001 demo + CL002 player insert)
        var t6 = new SQLTask(
            "Verify both clues are in the table.",
            "SELECT * FROM clues;",
            "SELECT", anyOrder: true, partialMatch: true);
        t6.ExpectedRows.Add(new Dictionary<string, string> { {"id","CL001"}, {"name","The Body"} });
        t6.ExpectedRows.Add(new Dictionary<string, string> { {"id","CL002"}, {"name","Tamam Shud"} });
        case1.Tasks.Add(t6);

        // Task 7 — SELECT * FROM witnesses (4 rows)
        var t7 = new SQLTask(
            "Show all data from the witnesses table.",
            "SELECT * FROM witnesses;",
            "SELECT", anyOrder: true);
        t7.ExpectedRows.Add(new Dictionary<string, string> { {"id","W001"}, {"name","John Lyons"} });
        t7.ExpectedRows.Add(new Dictionary<string, string> { {"id","W002"}, {"name","Neil Hamilton"} });
        t7.ExpectedRows.Add(new Dictionary<string, string> { {"id","W003"}, {"name","Jessica Thomson"} });
        t7.ExpectedRows.Add(new Dictionary<string, string> { {"id","W004"}, {"name","John Burton Cleland"} });
        case1.Tasks.Add(t7);

        // Task 8 — JOIN witnesses + contacts
        var t8 = new SQLTask(
            "Join the witnesses table with contacts to get each witness's name and address.",
            "SELECT witnesses.name, contacts.address, contacts.phone FROM witnesses JOIN contacts ON witnesses.contact_id = contacts.id;",
            "JOIN", anyOrder: true);
        t8.ExpectedRows.Add(new Dictionary<string, string> { {"name","John Lyons"},           {"address","40 Moseley Street, Glenelg"},        {"phone","unlisted"} });
        t8.ExpectedRows.Add(new Dictionary<string, string> { {"name","Neil Hamilton"},        {"address","12 Jetty Road, Glenelg"},             {"phone","unlisted"} });
        t8.ExpectedRows.Add(new Dictionary<string, string> { {"name","Jessica Thomson"},      {"address","90A Moseley Street, Glenelg"},        {"phone","X3239"} });
        t8.ExpectedRows.Add(new Dictionary<string, string> { {"name","John Burton Cleland"}, {"address","University of Adelaide, North Tce"}, {"phone","unlisted"} });
        case1.Tasks.Add(t8);

        // Task 9 — SELECT * FROM cipher_fragments (6 rows)
        var t9 = new SQLTask(
            "Show all rows from the cipher_fragments table.",
            "SELECT * FROM cipher_fragments;",
            "SELECT", anyOrder: true);
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF001"}, {"fragment","WRGOABABD"} });
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF002"}, {"fragment","MLIAOI"} });
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF003"}, {"fragment","WTMTSTMSA"} });
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF004"}, {"fragment","ITTMTSAS"} });
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF005"}, {"fragment","AIAQC"} });
        t9.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF006"}, {"fragment","TAMAM SHUD"} });
        case1.Tasks.Add(t9);

        // Task 10 — SELECT WHERE decoded = 0 (5 rows)
        var t10 = new SQLTask(
            "Show only cipher fragments that have not been decoded (decoded = 0).",
            "SELECT * FROM cipher_fragments WHERE decoded = 0;",
            "WHERE", anyOrder: true);
        t10.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF001"} });
        t10.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF002"} });
        t10.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF003"} });
        t10.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF004"} });
        t10.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF005"} });
        case1.Tasks.Add(t10);

        // Task 11 — SELECT WHERE fragment LIKE '%TSM%' (1 row)
        var t11 = new SQLTask(
            "Find fragments that contain the pattern 'TSM' using LIKE.",
            "SELECT * FROM cipher_fragments WHERE fragment LIKE '%TSM%';",
            "LIKE");
        t11.ExpectedRows.Add(new Dictionary<string, string> { {"id","CF003"}, {"fragment","WTMTSTMSA"} });
        case1.Tasks.Add(t11);

        // Task 12 — SELECT witnesses WHERE interviewed = 0 ORDER BY name ASC (all 4)
        var t12 = new SQLTask(
            "Show all witnesses not yet interviewed, ordered alphabetically by name.",
            "SELECT name, role FROM witnesses WHERE interviewed = 0 ORDER BY name ASC;",
            "ORDER BY");
        t12.ExpectedRows.Add(new Dictionary<string, string> { {"name","Jessica Thomson"},      {"role","Linked to the Rubaiyat"} });
        t12.ExpectedRows.Add(new Dictionary<string, string> { {"name","John Burton Cleland"}, {"role","Examined the Tamam Shud"} });
        t12.ExpectedRows.Add(new Dictionary<string, string> { {"name","John Lyons"},           {"role","Found the body"} });
        t12.ExpectedRows.Add(new Dictionary<string, string> { {"name","Neil Hamilton"},        {"role","Saw man alive night before"} });
        case1.Tasks.Add(t12);

        // Task 13 — UPDATE Jessica Thomson interviewed = 1 (free write)
        case1.Tasks.Add(new SQLTask(
            "Update Jessica Thomson's interviewed status to 1.",
            "UPDATE witnesses SET interviewed = 1 WHERE name = 'Jessica Thomson';",
            "UPDATE"));

        // Task 14 — UPDATE Neil Hamilton interviewed = 1 (free write)
        case1.Tasks.Add(new SQLTask(
            "Update Neil Hamilton's interviewed status to 1.",
            "UPDATE witnesses SET interviewed = 1 WHERE name = 'Neil Hamilton';",
            "UPDATE"));

        // Task 15 — INSERT CL003 Neil Hamilton Testimony (free write)
        case1.Tasks.Add(new SQLTask(
            "Insert Neil Hamilton's testimony as a new clue (CL003) into the clues table.",
            "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL003', 'Neil Hamilton Testimony', 'Testimony', '...', '1948-12-02 10:00:00');",
            "INSERT"));

        // Task 16 — UPDATE John Burton Cleland interviewed = 1 (free write)
        case1.Tasks.Add(new SQLTask(
            "Update John Burton Cleland's interviewed status to 1.",
            "UPDATE witnesses SET interviewed = 1 WHERE name = 'John Burton Cleland';",
            "UPDATE"));

        // Task 17 — COUNT + GROUP BY clues type
        var t17 = new SQLTask(
            "Count how many clues you have per type using COUNT and GROUP BY.",
            "SELECT type, COUNT(*) AS total FROM clues GROUP BY type;",
            "GROUP BY", anyOrder: true);
        t17.ExpectedRows.Add(new Dictionary<string, string> { {"type","Physical"},  {"total","2"} });
        t17.ExpectedRows.Add(new Dictionary<string, string> { {"type","Testimony"}, {"total","1"} });
        case1.Tasks.Add(t17);

        // Task 18 — SELECT name FROM sqlite_master WHERE type='table' (partial match)
        var t18 = new SQLTask(
            "List all tables available in the local database using sqlite_master.",
            "SELECT name FROM sqlite_master WHERE type = 'table';",
            "sqlite_master", partialMatch: true);
        t18.ExpectedRows.Add(new Dictionary<string, string> { {"name","hounds"} });
        case1.Tasks.Add(t18);

        // Task 19 — SELECT * FROM suspects (4 rows)
        var t19 = new SQLTask(
            "Show all suspects in the database.",
            "SELECT * FROM suspects;",
            "SELECT", anyOrder: true);
        t19.ExpectedRows.Add(new Dictionary<string, string> { {"id","S001"}, {"name","Jessica Thomson"} });
        t19.ExpectedRows.Add(new Dictionary<string, string> { {"id","S002"}, {"name","Unknown Soviet Agent"} });
        t19.ExpectedRows.Add(new Dictionary<string, string> { {"id","S003"}, {"name","Alfred Boxall"} });
        t19.ExpectedRows.Add(new Dictionary<string, string> { {"id","S004"}, {"name","The Rival Hound"} });
        case1.Tasks.Add(t19);

        // Task 20 — JOIN suspects + clues WHERE linked_clue = 'CL002'
        var t20 = new SQLTask(
            "Join suspects with clues to find who is linked to the Tamam Shud (CL002).",
            "SELECT suspects.name, suspects.motive, clues.name AS clue_name FROM suspects JOIN clues ON suspects.linked_clue = clues.id WHERE suspects.linked_clue = 'CL002';",
            "JOIN", anyOrder: true);
        t20.ExpectedRows.Add(new Dictionary<string, string> { {"name","Jessica Thomson"}, {"clue_name","Tamam Shud"} });
        t20.ExpectedRows.Add(new Dictionary<string, string> { {"name","Alfred Boxall"},   {"clue_name","Tamam Shud"} });
        case1.Tasks.Add(t20);

        // Task 21 — SELECT * FROM passwords (3 rows)
        var t21 = new SQLTask(
            "Show all rows in the passwords table.",
            "SELECT * FROM passwords;",
            "SELECT", anyOrder: true);
        t21.ExpectedRows.Add(new Dictionary<string, string> { {"id","P001"}, {"label","room_exit_code"} });
        t21.ExpectedRows.Add(new Dictionary<string, string> { {"id","P002"}, {"label","agency_master"} });
        t21.ExpectedRows.Add(new Dictionary<string, string> { {"id","P003"}, {"label","case_completion"} });
        case1.Tasks.Add(t21);

        // Task 22 — SELECT * FROM keys (3 rows)
        var t22 = new SQLTask(
            "Show all rows in the keys table.",
            "SELECT * FROM keys;",
            "SELECT IS NULL", anyOrder: true);
        t22.ExpectedRows.Add(new Dictionary<string, string> { {"id","K001"}, {"password_id","P001"} });
        t22.ExpectedRows.Add(new Dictionary<string, string> { {"id","K002"}, {"password_id","P002"} });
        t22.ExpectedRows.Add(new Dictionary<string, string> { {"id","K003"}, {"password_id","P003"} });
        case1.Tasks.Add(t22);

        // Task 23 — UPDATE keys SET value = 'It is ended' WHERE id = 'K001' (free write)
        case1.Tasks.Add(new SQLTask(
            "Update the value in the keys table for K001 to 'It is ended'.",
            "UPDATE keys SET value = 'It is ended' WHERE id = 'K001';",
            "UPDATE"));

        // Task 24 — SELECT * FROM keys WHERE id = 'K001' (verify value)
        var t24 = new SQLTask(
            "Verify the update by selecting K001 from the keys table.",
            "SELECT * FROM keys WHERE id = 'K001';",
            "WHERE");
        t24.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","K001"}, {"password_id","P001"}, {"value","It is ended"} });
        case1.Tasks.Add(t24);

        // Task 25 — internet.sqlite_master → internet_meta (5 rows)
        var t25 = new SQLTask(
            "List all tables available in the internet archive database.",
            "SELECT name FROM internet.sqlite_master WHERE type = 'table';",
            "attached schema", anyOrder: true);
        t25.ExpectedRows.Add(new Dictionary<string, string> { {"name","bbc_news"} });
        t25.ExpectedRows.Add(new Dictionary<string, string> { {"name","abc_australia"} });
        t25.ExpectedRows.Add(new Dictionary<string, string> { {"name","sa_police_records"} });
        t25.ExpectedRows.Add(new Dictionary<string, string> { {"name","adelaide_advertiser"} });
        t25.ExpectedRows.Add(new Dictionary<string, string> { {"name","forensic_reports"} });
        case1.Tasks.Add(t25);

        // Task 26 — SELECT sql FROM internet_meta WHERE name='sa_police_records' (1 row)
        var t26 = new SQLTask(
            "Inspect the structure of the sa_police_records table.",
            "SELECT sql FROM internet.sqlite_master WHERE name = 'sa_police_records';",
            "sqlite_master", partialMatch: true);
        t26.ExpectedRows.Add(new Dictionary<string, string> { {"name","sa_police_records"} });
        case1.Tasks.Add(t26);

        // Task 27 — UNION across all internet tables (8 rows, any order)
        var t27 = new SQLTask(
            "Search all internet archive tables for records mentioning Somerton, Webb, Tamam Shud, or Jestyn using UNION.",
            "SELECT headline, date, source FROM internet.bbc_news WHERE keywords LIKE '%Somerton%' OR keywords LIKE '%Webb%' UNION SELECT headline, date, source FROM internet.abc_australia WHERE keywords LIKE '%Somerton%' OR keywords LIKE '%Webb%' UNION ...",
            "UNION", anyOrder: true, partialMatch: true);
        t27.ExpectedRows.Add(new Dictionary<string, string> { {"headline","DNA Analysis Identifies Somerton Man as Carl \"Charles\" Webb"} });
        t27.ExpectedRows.Add(new Dictionary<string, string> { {"headline","Forensic Genealogy Confirms Webb Identity with 99.9% Confidence"} });
        case1.Tasks.Add(t27);

        // Task 28 — SELECT * FROM clues WHERE name = 'Neil Hamilton Testimony' (1 row)
        var t28 = new SQLTask(
            "Select the Neil Hamilton testimony from the clues table.",
            "SELECT * FROM clues WHERE name = 'Neil Hamilton Testimony';",
            "WHERE");
        t28.ExpectedRows.Add(new Dictionary<string, string> { {"id","CL003"}, {"name","Neil Hamilton Testimony"} });
        case1.Tasks.Add(t28);

        // Task 29 — INSERT CL004 Carl Webb DNA (free write)
        case1.Tasks.Add(new SQLTask(
            "Insert the 2022 DNA identification of Carl Webb as a new clue (CL004).",
            "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL004', 'Carl Webb Identity (2022 DNA)', 'Documentary', '...', '2022-07-26 00:00:00');",
            "INSERT"));

        // Task 30 — INSERT CL005 Dorothy Webb (free write)
        case1.Tasks.Add(new SQLTask(
            "Insert Dorothy Webb's testimony as a new clue (CL005).",
            "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL005', 'Dorothy Webb Testimony', 'Testimony', '...', '2022-08-01 00:00:00');",
            "INSERT"));

        // Task 31 — UPDATE logfile SET victim = '...' WHERE id = '01' (free write)
        case1.Tasks.Add(new SQLTask(
            "Update the victim field in the logfile to reflect the 2022 DNA finding.",
            "UPDATE logfile SET victim = 'Carl \"Charles\" Webb (unconfirmed, DNA 2022)' WHERE id = '01';",
            "UPDATE"));

        // Task 32 — SELECT clues ORDER BY found_at ASC (5 rows)
        var t32 = new SQLTask(
            "Show all clues ordered by found_at ascending.",
            "SELECT name, type, details, found_at FROM clues ORDER BY found_at ASC;",
            "ORDER BY", partialMatch: true);
        t32.ExpectedRows.Add(new Dictionary<string, string> { {"name","The Body"} });
        t32.ExpectedRows.Add(new Dictionary<string, string> { {"name","Tamam Shud"} });
        case1.Tasks.Add(t32);

        // Task 33 — SELECT suspects WHERE eliminated = 0 (4 rows)
        var t33 = new SQLTask(
            "Show all uneliminated suspects.",
            "SELECT name, motive FROM suspects WHERE eliminated = 0;",
            "WHERE", anyOrder: true);
        t33.ExpectedRows.Add(new Dictionary<string, string> { {"name","Jessica Thomson"} });
        t33.ExpectedRows.Add(new Dictionary<string, string> { {"name","Unknown Soviet Agent"} });
        t33.ExpectedRows.Add(new Dictionary<string, string> { {"name","Alfred Boxall"} });
        t33.ExpectedRows.Add(new Dictionary<string, string> { {"name","The Rival Hound"} });
        case1.Tasks.Add(t33);

        // Task 34 — UPDATE logfile final summary (free write)
        case1.Tasks.Add(new SQLTask(
            "Update the logfile — set status to 'Investigated - Inconclusive' and write your final summary in details.",
            "UPDATE logfile SET status = 'Investigated - Inconclusive', details = 'Your summary here.' WHERE id = '01';",
            "UPDATE"));

        // Task 35 — SELECT * FROM logfile WHERE id = '01' (verify final state)
        var t35 = new SQLTask(
            "Verify the final logfile entry.",
            "SELECT * FROM logfile WHERE id = '01';",
            "SELECT", partialMatch: true);
        t35.ExpectedRows.Add(new Dictionary<string, string> {
            {"id","01"}, {"namecase","Murder of Somerton Man"}, {"status","Investigated - Inconclusive"} });
        case1.Tasks.Add(t35);

        AllCases.Add(case1);

        // ── Cases 2–5 (placeholders) ──────────────────────────────────────────
        AllCases.Add(new CaseDefinition("case_02","Case 02 — Coming Soon","Placeholder.","TBD",0,"Easy","case_01"));
        AllCases.Add(new CaseDefinition("case_03","Case 03 — Coming Soon","Placeholder.","TBD",0,"Intermediate","case_02"));
        AllCases.Add(new CaseDefinition("case_04","Case 04 — Coming Soon","Placeholder.","TBD",0,"Hard","case_03"));
        AllCases.Add(new CaseDefinition("case_05","Case 05 — Coming Soon","Placeholder.","TBD",0,"Expert","case_04"));

        Debug.Log($"[CaseManager] {AllCases.Count} cases loaded.");
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public bool SetActiveCase(string caseId)
    {
        var casedef = AllCases.Find(c => c.CaseId == caseId);
        if (casedef == null) { Debug.LogError($"[CaseManager] Case '{caseId}' not found."); return false; }
        if (!casedef.IsReadyToPlay) { Debug.LogWarning($"[CaseManager] '{caseId}' not ready."); return false; }
        ActiveCase = casedef;
        Debug.Log($"[CaseManager] Active case: {casedef.Title}");
        return true;
    }

    public List<CaseDefinition> GetAllCasesForDisplay() => new List<CaseDefinition>(AllCases);

    public List<CaseDefinition> GetUnlockedCases(int profileId)
    {
        var unlocked = new List<CaseDefinition>();
        foreach (var c in AllCases)
            if (IsCaseUnlocked(c.CaseId, profileId)) unlocked.Add(c);
        return unlocked;
    }

    public bool IsCaseUnlocked(string caseId, int profileId)
    {
        var c = AllCases.Find(x => x.CaseId == caseId);
        if (c == null) return false;
        if (string.IsNullOrEmpty(c.RequiredCaseId)) return true;
        return IsCaseCompleted(c.RequiredCaseId, profileId);
    }

    public bool IsCaseCompleted(string caseId, int profileId)
    {
        var result = DatabaseManager.Instance.RunSaveQueryWithResults(
            "SELECT id FROM case_progress WHERE profile_id = ? AND case_id = ? AND completed = 1",
            profileId.ToString(), caseId);
        return result != null && result.Count > 0;
    }

    public void CompleteCase(string caseId, int profileId)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.RunSaveNonQuery(
            @"INSERT INTO case_progress (profile_id, case_id, completed, completed_at)
              VALUES (?, ?, 1, ?)
              ON CONFLICT(profile_id, case_id) DO UPDATE SET completed = 1, completed_at = ?",
            profileId, caseId, now, now);
        Debug.Log($"[CaseManager] Case '{caseId}' completed for profile {profileId}.");
        GameManager.Instance.AutoSave("case_completed");
    }

    public int GetCurrentTaskIndex(int profileId, string caseId)
    {
        var result = DatabaseManager.Instance.RunSaveQueryWithResults(
            "SELECT current_task FROM case_progress WHERE profile_id = ? AND case_id = ?",
            profileId.ToString(), caseId);
        if (result == null || result.Count == 0) return 0;
        return int.TryParse(result[0]["current_task"], out int idx) ? idx : 0;
    }

    public void AdvanceTask(int profileId, string caseId, int nextTaskIndex)
    {
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        DatabaseManager.Instance.RunSaveNonQuery(
            @"INSERT INTO case_progress (profile_id, case_id, current_task, completed, completed_at)
              VALUES (?, ?, ?, 0, NULL)
              ON CONFLICT(profile_id, case_id) DO UPDATE SET current_task = ?",
            profileId, caseId, nextTaskIndex, nextTaskIndex);
        Debug.Log($"[CaseManager] Profile {profileId} → task {nextTaskIndex} in '{caseId}'.");
        GameManager.Instance.AutoSave("task_completed");
    }
}
