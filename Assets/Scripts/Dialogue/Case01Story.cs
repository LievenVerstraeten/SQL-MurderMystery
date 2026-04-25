// Case01Story.cs
// Complete dialogue + story node sequence for Case 01 — Murder of the Somerton Man.
//
// Call Build(playerName) to get the full List<DialogueNode> to pass to DialogueManager.
// Each SQLTask node index MUST align with the matching task index in CaseManager.case_01.Tasks.
//
// NODE TYPES:
//   Say(speaker, text)                 — VN dialogue line
//   Task(label, hint)                  — SQL task gate (player must solve to advance)
//   Demo(sql, label)                   — auto-run SQL shown in terminal, no gate
//   Scene(text, color)                 — fullscreen coloured cutscene
//   AddItem(name, description, type)   — silent inventory add

using System.Collections.Generic;
using UnityEngine;

public static class Case01Story
{
    // Cutscene palette
    private static readonly Color ColWormhole  = new Color(0.08f, 0.04f, 0.18f);
    private static readonly Color ColBeach     = new Color(0.04f, 0.12f, 0.14f);
    private static readonly Color ColInterior  = new Color(0.08f, 0.06f, 0.06f);
    private static readonly Color ColRoom      = new Color(0.04f, 0.04f, 0.04f);
    private static readonly Color ColTimeskip  = new Color(0.06f, 0.06f, 0.10f);
    private static readonly Color ColEpilogue  = new Color(0.02f, 0.02f, 0.02f);

    public static List<DialogueNode> Build(string playerName = "N")
    {
        string N = string.IsNullOrWhiteSpace(playerName) ? "N" : playerName;

        var story = new List<DialogueNode>
        {
            // ═══════════════════════════════════════════════════════════════
            // PHASE 1 — INTRO
            // ═══════════════════════════════════════════════════════════════

            Say(N, "Ughh... I need that paycheck. I haven't eaten in days..."),

            // Query Screen card
            Say("System",
                "NEW CASE: Murder of the Somerton Man\n" +
                "Reward: $200,000\n" +
                "Timeline: 1948, 1st of December\n" +
                "Requirement: A Rank Detective with Query Abilities\n" +
                "Overview: A body was found on Somerton Beach. No ID. No cause of death.\n" +
                "Signed: Anonymous"),

            Say(N, "Talking about the devil, this looks interesting."),
            Say(N, "The reward looks generous enough, but I don't have the abilities of a Query Hound..."),
            Say(N, "I might pass on this."),

            Scene("* Debbie poof *", ColBeach),

            Say(N, "Eh? What the heck?"),
            Say("Debbie", "Cluck, I'm Debbie, your assistant for this case."),
            Say(N, "What? A talking chicken? I'm fine, I'm fine. I'm already hallucinating due to dire hunger."),
            Say("Debbie", "Hey, cluck! I'm not food."),
            Say(N, "Okay, so I'm actually going insane."),
            Say("Debbie", "Would you actually listen?"),
            Say(N, "Fine, fine. What do you want?"),
            Say("Debbie", "What if we take this case together? I can help you handle the Query part while you put on the effort?"),
            Say(N, "This feels fishy. Why don't you take the case yourself? What's in it for you?"),
            Say("Debbie", "As you can see, I have no wings that work like fingers, so typing on a Timecase is out of the question."),
            Say("Debbie", "I can read the screen just fine and tell you exactly what to type — but the actual typing? That's on you. Plus, I am also hungry, so we can share the money."),
            Say(N, "*sigh*\nFine. But so you know, I'll take 70% of the money."),
            Say("Debbie", "Fair. Let's go!"),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 2 — WORMHOLE / QUALIFICATION
            // ═══════════════════════════════════════════════════════════════

            Scene(
                "Chicken and N jump into a wormhole and are transported to 1948.\n" +
                "Debbie panics and jumps on N's head mid-wormhole.",
                ColWormhole),

            Say(N, "We are stuck in a wormhole???"),
            Say("Debbie", "The system only allows A Rank detectives with Query Hound abilities."),
            Say(N, "Of course they do. They don't want leechers jumping on the case."),
            Say("Debbie", "It's okay. This is easy. I'll teach you.\nOpen your Timecase (Terminal). Now!"),

            // ── Task 0: SELECT * FROM hounds ─────────────────────────────
            Say("Debbie",
                "We are going to use Queries to pull data from the Time Travel Agency's Database.\n" +
                "Try this first:\n\n  SELECT * FROM hounds;\n\n" +
                "SELECT reads data. The * means \"give me everything.\"\n" +
                "FROM tells it which table. A table is like a spreadsheet — rows are records, columns are fields."),

            Task("Task: Search for your name in the list of qualified members.\nFirst, run:  SELECT * FROM hounds;",
                 "SELECT * FROM hounds;"),

            Say(N, "My name is not here... I feel offended."),
            Say("Debbie", "It's okay! We can add yours. But first, watch — I'll show you how."),
            Say("Debbie", "INSERT INTO is how you add a new row to a table.\nYou list which columns you're filling, then provide the values in the same order."),

            // ── Demo: Debbie inserts herself ──────────────────────────────
            Demo("INSERT INTO hounds (id, name) VALUES ('02', 'Debbie');", "Debbie types:"),

            Say(N, "Alright, alright. My turn."),

            // ── Task 1: INSERT yourself ───────────────────────────────────
            Task("Task: Add your own name to the hounds table using INSERT INTO.",
                 "INSERT INTO hounds (id, name) VALUES ('03', 'YourName');"),

            Say(N, "Oh, that's easy. Why did they even make this a requirement?"),
            Say("Debbie", "Because most detectives do not have a talking chicken to guide them.\nLucky you, cluck."),

            // ── Task 2: SELECT all hounds (verify) ────────────────────────
            Task("Task: Verify all three hounds are listed.\nRun:  SELECT * FROM hounds;",
                 "SELECT * FROM hounds;"),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 3 — BEACH ARRIVAL
            // ═══════════════════════════════════════════════════════════════

            Scene(
                "Debbie and N tumble out of the wormhole and land at Somerton Beach, Adelaide.\n" +
                "December 1, 1948. The air smells of salt. The beach is cordoned off.",
                ColBeach),

            Say(N, "Do you have the details of the case, Debbie?"),
            Say("Debbie", "There you go!"),
            AddItem("Log File", "The Agency case file for the Murder of the Somerton Man.", "log"),
            Say("Debbie", "Click the backpack icon to see your readables."),
            Say(N, "This feels heavy for a folder."),
            Say("Debbie", "This is essential so you can have the details of the case."),

            Scene("Police crime tape. Officers mill about in the time-frozen preservation bubble.", ColBeach),

            Say(N, "Oh, this place looks like it is frozen in time."),
            Say("Debbie", "It's a measure of the Time Travel Agency — a preservation bubble to prevent anyone from disturbing the evidence."),
            Say(N, "Smart. I'll take that.\nOkay, first. What do we know about the victim?"),
            Say("Debbie", "Use your Timecase terminal to retrieve the data from the Agency's database."),
            Say(N, "More queries..."),

            // ── Task 3: SELECT * FROM logfile ─────────────────────────────
            Say("Debbie",
                "The logfile holds our case record. Use the same SELECT command you used for hounds:\n\n" +
                "  SELECT * FROM logfile;"),

            Task("Task: Show all data from the logfile table.\nHint: Use the command you used to see all hounds.",
                 "SELECT * FROM logfile;"),

            Say(N, "Okay, so this log file will update the more we advance?"),
            Say("Debbie",
                "Yep, yep, cluck! That's to make sure we keep the Agency posted on our progress.\n\n" +
                "Debbie's Suggestion: To view only specific columns, replace * with column names separated by commas.\n" +
                "Example:  SELECT namecase, id FROM logfile;"),

            // ── Phase 3b: Clues table ────────────────────────────────────

            Say(N, "Okay. Now we have to actually build a case.\nSince we can use queries to read information, can we create our own tables?"),
            Say("Debbie", "Exactly, N. My only concern is that you cannot even create one."),
            Say(N, "...\nTeach me, Magical Talking Chicken. We will need one for clues."),
            Say("Debbie",
                "Tables are created like this:\n\n" +
                "  CREATE TABLE table_name (\n" +
                "    column1 DATATYPE PRIMARY KEY,\n" +
                "    column2 DATATYPE\n" +
                "  );\n\n" +
                "SQL data types: INTEGER (whole numbers), REAL (decimals), TEXT (strings), BLOB (binary), NUMERIC (flexible).\n" +
                "In SQLite, dates are stored as TEXT in format 'YYYY-MM-DD HH:MM:SS'.\n" +
                "PRIMARY KEY marks the unique identifier column — no two rows can share it."),
            Say(N, "Right. If two victims were both named John, we'd pull the wrong one without a proper ID."),
            Say("Debbie", "Exactly. Now — create the clues table."),

            // ── Task 4: CREATE TABLE clues ────────────────────────────────
            Task(
                "Task: Create a table called clues with columns: id (TEXT, PRIMARY KEY), name, type, details, found_at.",
                "CREATE TABLE clues (id TEXT PRIMARY KEY, name TEXT, type TEXT, details TEXT, found_at TEXT);"),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 4 — CLUE SHEET
            // ═══════════════════════════════════════════════════════════════

            Say("Debbie", "Hey! I see a Time Hound over there. Maybe he can fill us in and save some legwork."),
            Say(N, "Wow. You are lazy as heck, Debs."),

            Say(N, "Hello, Mister... We are Time Hounds N and Debbie, on case."),
            Say("TimeHound", "Oh, you are also on this one..."),
            Say(N, "What do you mean also? I thought it was first come, first serve."),
            Say("TimeHound", "Hah, no kiddo. It's a race against the clock.\nPlenty of hounds are already ahead of you."),
            Say(N, "...\nBro. We are not getting that prize money."),
            Say("Debbie", "Thank you for the heads up! Let's hurry, N!"),

            Scene("N and Debbie inspect the crime scene.", ColBeach),

            Say(N, "I'm pretty sure everyone already grabbed the obvious clues."),
            Say("Debbie", "I have the initial list right here!"),
            Say(N, "How did you — you know what, forget it. Gimme that."),
            Say("Debbie", "Geez, impatient as ever. Here:"),
            AddItem("Clue Sheet — Somerton Beach, December 1, 1948",
                "1. The Body — Male, 40-45. Found at seawall. No ID, no cause of death.\n" +
                "2. No Identification — No wallet, no ID. All clothing labels removed.\n" +
                "3. Cause of Death Unknown — Autopsy inconclusive. Signs of poisoning.\n" +
                "4. The Suitcase — Brown suitcase at Adelaide Railway Station. All labels removed.\n" +
                "5. Tamam Shud — Scrap of paper in secret pocket. 'It is ended' in Persian.\n" +
                "6. The Book — Rubaiyat copy found in car. Unlisted phone number + cipher in back.\n" +
                "7. The Phone Number — Traced to a woman known only as 'Jestyn.'", "clue"),

            // Debbie demonstrates CL001 insert
            Say("Debbie", "Let's get these into the database. I'll walk you through the first one.\nThe body itself is our anchor clue."),
            Demo(
                "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL001','The Body','Physical','Male, approx. 40-45 years old. Found at Somerton Beach seawall. No ID, no cause of death confirmed.','1948-12-01 06:30:00');",
                "Debbie inserts CL001:"),

            Say(N, "Okay. So the values go in the same order as the column names I listed."),
            Say("Debbie", "Exactly. The order must match. Now you try — add the Tamam Shud clue."),

            // ── Task 5: INSERT CL002 ──────────────────────────────────────
            Task(
                "Task: Insert the Tamam Shud clue (CL002) into the clues table.",
                "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL002','Tamam Shud','Physical','Scrap reading \"Tamam Shud\" in secret trouser pocket. Torn from a rare Rubaiyat edition.','1948-12-01 09:00:00');"),

            Say(N, "Done. Let's verify it actually went in."),

            // ── Task 6: SELECT * FROM clues ───────────────────────────────
            Task("Task: Select all rows from the clues table.", "SELECT * FROM clues;"),

            Say(N, "Good. We have a starting point."),
            Say("Debbie",
                "Now here is the interesting part — we do not just want to see all the clues every time.\n\n" +
                "WHERE lets you filter rows based on a condition — just like an if statement in code.\n\n" +
                "  SELECT * FROM clues WHERE type = 'Physical';\n\n" +
                "You can use =, !=, >, < — and LIKE for partial text matches."),
            Say(N, "So if I wanted only document-type clues later, I would just swap out the value."),
            Say("Debbie", "Now you are thinking like a detective. Cluck."),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 5 — WITNESSES INVESTIGATION
            // ═══════════════════════════════════════════════════════════════

            Say(N, "Alright. Five more clues to log, a cipher nobody has cracked in seventy years, and a mystery woman who won't talk."),
            Say(N, "Easy."),
            Say("Debbie", "And we are behind every other hound on the case."),
            Say(N, "I said easy, Debs. Don't ruin it."),

            Scene("N and Debbie walk along the beachfront. The time bubble holds everything still.", ColBeach),

            Say(N, "Okay. We have clues in the database but they don't tell us anything we couldn't read off a newspaper."),
            Say("Debbie", "That is because clues alone are not enough. We need people, N. Witnesses. Suspects."),
            Say(N, "And I assume the Agency has a table for that too."),
            Say("Debbie", "Cluck. Obviously."),

            // ── Task 7: SELECT * FROM witnesses ───────────────────────────
            Say("Debbie",
                "In SQLite, there is no native BOOLEAN type.\n" +
                "Booleans are stored as INTEGER — 0 means false, 1 means true.\n" +
                "So WHERE interviewed = 0 checks for false."),

            Task("Task: Select everything from the witnesses table.", "SELECT * FROM witnesses;"),

            Say(N, "Jessica Thomson. That must be 'Jestyn.'"),
            Say("Debbie", "The very same. Her phone number was written in the back of the Rubaiyat found near the scene."),
            Say(N, "And there is a contact_id column. That links somewhere else?"),
            Say("Debbie",
                "Sharp. There is a separate contacts table. That is where addresses and phone numbers live.\n\n" +
                "Splitting data across linked tables is called normalization. If Jessica's phone number was in the witnesses table and she changed it, you'd update every row that mentioned her.\n" +
                "By keeping contact details in contacts and linking with an ID, you only update one place.\n" +
                "The link is called a foreign key. To see data from both tables together, you need a JOIN."),
            Say(N, "So the contact_id in witnesses points to a row in contacts."),
            Say("Debbie",
                "A JOIN combines rows from two tables based on a matching column:\n\n" +
                "  SELECT t1.col, t2.col\n" +
                "  FROM t1\n" +
                "  JOIN t2 ON t1.shared_id = t2.shared_id;\n\n" +
                "Think of it like zip() in Python — pairing rows that share a common value."),

            // ── Task 8: JOIN witnesses + contacts ─────────────────────────
            Task(
                "Task: Join witnesses with contacts to get each witness's name, address and phone.",
                "SELECT witnesses.name, contacts.address, contacts.phone FROM witnesses JOIN contacts ON witnesses.contact_id = contacts.id;"),

            Say(N, "She lives three minutes from where the body was found."),
            Say("Debbie", "And her number was in his book."),
            Say(N, "...This woman knew him."),
            Say("Debbie", "Let's go pay her a visit. Politely."),
            Say(N, "Obviously."),

            // ── Jessica Thomson scene ─────────────────────────────────────
            Scene(
                "N knocks on the door of 90A Moseley Street.\n" +
                "A woman in her late 20s opens it. Her expression flickers.",
                ColInterior),

            Say("Jessica", "I already told the police. I don't know that man."),
            Say(N, "Right. We are not the police, Miss Thomson.\nWe are... workers of the Police's Emotional Support Department. Slightly different jurisdiction..."),
            Say("Jessica", "* starts closing door *"),
            Say("Debbie", "* wedges wing in door * Cluck."),
            Say(N, "Wait, this is Debbie. The best emotional support chicken the department has to offer."),
            Say("Jessica", "* pause * ...You have five minutes."),

            Scene("Interior of the house. Sparse. Jessica sits across from N, arms crossed.", ColInterior),

            Say(N, "You gave a copy of the Rubaiyat of Omar Khayyam to someone once. As a gift. Do you remember?"),
            Say("Jessica", "* quietly * I may have."),
            Say(N, "There was writing in the back. Letters. Like a code."),
            Say("Jessica", "I don't know anything about that."),
            Say(N, "The man on the beach had a page torn from that exact book in a hidden pocket in his trousers."),
            Say("Jessica", "* stands * I think you should leave."),
            Say("Debbie", "* whispers * She is hiding something."),
            Say(N, "Yeah. But we can't force it. Let's work with what we have."),

            AddItem("Interview Log — Jessica Thomson",
                "Jessica Thomson, 90A Moseley Street, Glenelg. Phone X3239.\n" +
                "Denied knowing the victim. Visibly shaken. Gave a copy of the Rubaiyat to 'someone' once.\n" +
                "Refused to elaborate. Interview terminated.", "log"),
            AddItem("Cipher Fragment — Rubaiyat back cover",
                "Five rows of capital letters found in the back cover of the Rubaiyat.\n" +
                "Origin: book thrown into a car near Somerton Beach.\n" +
                "Status: Undecoded.", "clue"),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 5b — CIPHER ANALYSIS
            // ═══════════════════════════════════════════════════════════════

            Scene("Outside. Evening. N and Debbie assess the cipher.", ColBeach),

            Say(N, "Alright. We have a cipher and a witness who won't talk. Classic."),
            Say("Debbie", "Let's focus on the cipher. The Agency may have a cipher fragments database we can cross-reference."),
            Say(N, "Of course it does."),

            // ── Task 9: SELECT * FROM cipher_fragments ────────────────────
            Task("Task: Select all from the cipher_fragments table.", "SELECT * FROM cipher_fragments;"),

            Say(N, "TAMAM SHUD is the only decoded one."),
            Say("Debbie", "The rest have stumped cryptographers for decades. But we can filter smartly."),
            Say(N, "Show me only the undecoded ones."),

            // ── Task 10: WHERE decoded = 0 ────────────────────────────────
            Task(
                "Task: Select only cipher fragments where decoded is false (decoded = 0).",
                "SELECT * FROM cipher_fragments WHERE decoded = 0;"),

            Say("Debbie",
                "Now try searching for fragments that contain the pattern 'TSM'.\n\n" +
                "LIKE is used for partial text matching. % is a wildcard:\n" +
                "  LIKE 'A%'   — starts with A\n" +
                "  LIKE '%A'   — ends with A\n" +
                "  LIKE '%TSM%' — contains TSM anywhere"),

            // ── Task 11: LIKE '%TSM%' ─────────────────────────────────────
            Task(
                "Task: Select fragments where the fragment text contains 'TSM' using LIKE.",
                "SELECT * FROM cipher_fragments WHERE fragment LIKE '%TSM%';"),

            Say(N, "WTMTSTMSA. One hit."),
            Say("Debbie", "Some researchers believe the cipher is acrostic — first letters of words in a message. But no confirmed translation exists."),
            Say(N, "Dead end for now. What else do we have?"),

            // ── Task 12: ORDER BY witnesses ───────────────────────────────
            Say("Debbie",
                "ORDER BY sorts your results. By default it sorts ascending (A-Z, 0-9). Add DESC to reverse it.\n\n" +
                "  SELECT * FROM table WHERE condition ORDER BY column ASC;"),

            Task(
                "Task: Select all witnesses not yet interviewed, ordered alphabetically by name.",
                "SELECT name, role FROM witnesses WHERE interviewed = 0 ORDER BY name ASC;"),

            Say(N, "We spoke to Jessica. Sort of. Let's update that."),
            Say("Debbie",
                "UPDATE modifies existing rows. ALWAYS use a WHERE clause — without one, you update every row.\n\n" +
                "  UPDATE table SET column = value WHERE condition;"),

            // ── Task 13: UPDATE Jessica Thomson ──────────────────────────
            Task(
                "Task: Update Jessica Thomson's interviewed status to 1 (true).",
                "UPDATE witnesses SET interviewed = 1 WHERE name = 'Jessica Thomson';"),

            // ── Neil Hamilton scene ───────────────────────────────────────
            Scene("A small guesthouse on Jetty Road. Neil Hamilton opens the door.", ColInterior),

            Say("Neil", "Detectives? Sure, come in. I already told the police but I'll say it again."),
            Say(N, "We appreciate it, Mr. Hamilton. Walk us through what you saw."),
            Say("Neil", "It was the evening of November 30th. Around half past seven. I saw a man lying on the beach, propped up against the seawall."),
            Say("Neil", "I thought he was drunk or sleeping. He was very still. Didn't move the whole time I watched."),
            Say(N, "Did you see his face?"),
            Say("Neil", "Briefly. He seemed... peaceful. Like he wasn't troubled by anything."),
            Say("Debbie", "* quietly * Or already gone."),
            Say("Neil", "His arm raised a few times. Odd angle. Like he couldn't quite control it. Then I left. Didn't think much of it until I heard about the body the next morning."),
            Say(N, "Thank you, Mr. Hamilton. That's very helpful."),
            AddItem("Interview Log — Neil Hamilton",
                "Neil Hamilton, 12 Jetty Road, Glenelg.\n" +
                "Saw victim at 19:30, Nov 30, 1948 — motionless at seawall.\n" +
                "Arm movements suggested loss of motor control — consistent with poisoning.", "log"),

            // ── Task 14: UPDATE Neil Hamilton ─────────────────────────────
            Task(
                "Task: Update Neil Hamilton's interviewed status to 1 (true).",
                "UPDATE witnesses SET interviewed = 1 WHERE name = 'Neil Hamilton';"),

            // ── Task 15: INSERT CL003 testimony ───────────────────────────
            Task(
                "Task: Insert a new clue based on the interview into the clues table (CL003).",
                "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL003','Neil Hamilton Testimony','Testimony','Witness saw man alive and motionless at seawall approx 19:30 on Nov 30, 1948. Arm movements suggest loss of motor control — consistent with poisoning.','1948-12-02 10:00:00');"),

            Say(N, "Motor control loss. So he was poisoned before he even got to the wall."),
            Say("Debbie", "Or he walked there himself and collapsed. The poison could have been slow-acting."),
            Say(N, "Either way, someone gave it to him."),

            // ── Dr. Cleland scene ─────────────────────────────────────────
            Scene("University of Adelaide. A cluttered office full of books and specimen jars.\nDr. Cleland peers at them over his glasses.", ColInterior),

            Say("Cleland", "Ah. More investigators. Sit, sit."),
            Say(N, "Doctor. You examined the scrap of paper found on the body."),
            Say("Cleland", "Indeed. Remarkable thing. The paper — very thin, almost like India paper. The typeface was unusual. We traced it to a first edition of the Rubaiyat, published by a small New Zealand press. Only a handful of copies known to exist."),
            Say("Debbie", "And the book itself — the one found in the car?"),
            Say("Cleland", "Written in the back cover. Five rows of capital letters. I've looked at them for months. No pattern I can identify with certainty."),
            Say("Cleland", "Could be a book cipher, could be initials, could be nothing at all. The mind sees patterns where there are none."),
            Say(N, "Or where the author didn't want them seen."),
            Say("Cleland", "* nods slowly * Quite."),

            AddItem("Interview Log — John Burton Cleland",
                "Dr. John Burton Cleland, University of Adelaide.\n" +
                "Confirmed Tamam Shud scrap is from a rare New Zealand Rubaiyat first edition.\n" +
                "Five rows of cipher letters in back cover. No confirmed translation.", "log"),
            AddItem("Rubaiyat Analysis Report",
                "The Rubaiyat of Omar Khayyam, rare New Zealand edition.\n" +
                "Found thrown into a car near Somerton Beach.\n" +
                "Back cover: unlisted phone number (X3239) and five rows of capital letters.\n" +
                "Cipher status: undecoded.", "document"),

            // ── Task 16: UPDATE Cleland ───────────────────────────────────
            Task(
                "Task: Update John Burton Cleland's interviewed status to 1 (true).",
                "UPDATE witnesses SET interviewed = 1 WHERE name = 'John Burton Cleland';"),

            // ── Task 17: COUNT + GROUP BY ─────────────────────────────────
            Say("Debbie",
                "Before we move on — let's take stock of what we have.\n\n" +
                "COUNT() counts rows. Combined with GROUP BY, it counts rows per category:\n\n" +
                "  SELECT column, COUNT(*) FROM table GROUP BY column;\n\n" +
                "Similar to a frequency counter in Python: {type: count for type in list}."),

            Task(
                "Task: Count how many clues you have per type using COUNT and GROUP BY.",
                "SELECT type, COUNT(*) AS total FROM clues GROUP BY type;"),

            Say(N, "Three clues. Not a lot."),
            Say("Debbie",
                "Quality over quantity, N. We know he was poisoned, he had a link to Jessica Thomson, " +
                "and the cipher is likely a message — possibly to her, possibly from her."),
            Say(N, "We still don't know who he is."),
            Say("Debbie", "No. And that might be deliberate. Someone went to a lot of trouble to make sure of that."),
            Say(N, "Espionage?"),
            Say("Debbie", "It was 1948. The Cold War was just starting. It would not be the strangest theory."),
            Say(N, "...\nI need to sit with this. Let's go back to the beach and think."),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 6 — LOCKED ROOM
            // ═══════════════════════════════════════════════════════════════

            Scene("Dusk at Somerton Beach. The frozen waves glow orange.\nDebbie is perched on N's shoulder.", ColBeach),

            Say("Debbie", "N. We are being followed."),
            Say(N, "...Since when?"),
            Say("Debbie", "Since we left Jessica Thomson's house. There is a figure behind us. Trenchcoat. Nice hat."),
            Say(N, "Another Time Hound?"),
            Say("Debbie", "Or something worse. Cluck."),
            Say(N, "Walk faster."),

            Scene("A sharp sound. The world goes dark.", ColRoom),

            Scene(
                "N wakes up in a small locked room. Bare concrete walls. A single light.\n" +
                "The Timecase is still in hand. Debbie sits on N's chest, staring.",
                ColRoom),

            Say("Debbie", "You are awake. Finally."),
            Say(N, "...Where are we?"),
            Say("Debbie", "A locked room somewhere inside the time bubble. I can't get a signal out."),
            Say(N, "Who did this?"),
            Say("Debbie", "* points wing at a note slipped under the door *"),

            Say("System",
                "Note reads:\n\n" +
                "\"Back off the case, Hound. You're closer than you should be.\n" +
                "If you're clever enough to get out, maybe you deserve the answer.\n" +
                "The door has a passcode. Good luck.\n" +
                "— A fellow Hound\""),

            Say(N, "A rival locked us in a room and left us a puzzle."),
            Say("Debbie", "How irritating."),
            Say(N, "How do we get out?"),
            Say("Debbie", "The Timecase still has a connection to the local Agency database. Whoever locked us in didn't cut it."),
            Say(N, "Or maybe that's part of the puzzle."),
            Say("Debbie", "* pause * ...I like the way you think sometimes, N."),

            // ── Task 18: SELECT FROM sqlite_master ────────────────────────
            Say("Debbie",
                "In SQLite, you can see all tables in the current database with a special query.\n" +
                "sqlite_master is the database's own index — it stores metadata about every table.\n\n" +
                "  SELECT name FROM sqlite_master WHERE type = 'table';"),

            Task(
                "Task: List all tables available in the local database.",
                "SELECT name FROM sqlite_master WHERE type = 'table';"),

            Say(N, "Suspects table. We haven't touched that."),
            Say("Debbie", "And passwords. And keys."),
            Say(N, "The note said the door has a passcode. I'm guessing it's in passwords."),
            Say("Debbie", "Let's look at suspects first."),

            // ── Task 19: SELECT * FROM suspects ───────────────────────────
            Task("Task: Select all from the suspects table.", "SELECT * FROM suspects;"),

            Say(N, "The Rival Hound is in our own suspect table."),
            Say("Debbie", "I may have added them while you were unconscious."),
            Say(N, "...Good thinking, Debs."),
            Say("Debbie", "I have my moments. Cluck."),
            Say(N, "Alfred Boxall — who is that?"),
            Say("Debbie",
                "He was a man Jessica Thomson gave a different copy of the Rubaiyat to. Also inscribed.\n" +
                "When police first found the book they thought Boxall was the victim — but he turned up alive in 1949."),
            Say(N, "So she gave the same book to two different men."),
            Say("Debbie", "She denied knowing the Somerton Man. But she gave him the same book she gave Boxall. With her phone number in it."),
            Say(N, "She knew him. She just won't say so."),

            // ── Task 20: JOIN suspects + clues WHERE CL002 ────────────────
            Task(
                "Task: Select all suspects linked to clue CL002 — the Tamam Shud.",
                "SELECT suspects.name, suspects.motive, clues.name AS clue_name FROM suspects JOIN clues ON suspects.linked_clue = clues.id WHERE suspects.linked_clue = 'CL002';"),

            Say(N, "Both tied to the same scrap of paper."),
            Say("Debbie", "And neither of them talking."),
            Say(N, "What about the Soviet agent theory?"),

            // Demo: Soviet agent JOIN (auto-run, no gate)
            Demo(
                "SELECT suspects.name, suspects.motive, cipher_fragments.fragment FROM suspects JOIN cipher_fragments ON suspects.linked_clue = cipher_fragments.id WHERE suspects.name = 'Unknown Soviet Agent';",
                "Debbie runs the Soviet agent query:"),

            Say(N, "WTMTSTMSA. The one with the repeating TSM pattern."),
            Say("Debbie",
                "Some analysts believe the cipher was a one-time pad — a Soviet encryption method.\n" +
                "Nearly unbreakable without the matching key sheet."),
            Say(N, "And the key sheet would have been destroyed after reading."),
            Say("Debbie", "If he was a spy, he would have made sure of it."),
            Say(N, "Which is why the cipher was never decoded."),
            Say("Debbie", "* quietly * It is ended. Tamam Shud."),
            Say(N, "He knew he was going to die."),
            Say("Debbie", "And he made sure no one would ever know who sent him."),
            Say(N, "...\nOkay. We need to get out of this room. Back to the passwords table."),

            // ── Task 21: SELECT * FROM passwords ──────────────────────────
            Task("Task: Select all from the passwords table.", "SELECT * FROM passwords;"),

            Say(N, "A hash. That's not a password, that's a locked safe with no keyhole."),
            Say("Debbie",
                "A hash is what you get when you run a value through a hash function — a one-way operation.\n" +
                "The same input always produces the same output, but you cannot reverse it.\n\n" +
                "For example, the word 'password' run through SHA-256 always produces:\n" +
                "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8\n\n" +
                "Websites store hashes, not passwords. When you log in, they hash what you typed and compare it.\n" +
                "To crack it, you guess the input, hash your guess, and compare."),
            Say(N, "So the hash in the table is the hashed exit code. To open the door I need to find the original value and hash it myself."),
            Say("Debbie", "Or find a keys table that has the original value stored somewhere."),
            Say(N, "Why would anyone store the original if the whole point is to hash it?"),
            Say("Debbie", "Because whoever set this up wanted us to be able to get out. It is a puzzle, not a prison."),

            // ── Task 22: SELECT * FROM keys ───────────────────────────────
            Say("Debbie",
                "NULL in SQL means the absence of a value — not zero, not empty string, just nothing.\n" +
                "You can filter for it specifically:\n\n" +
                "  SELECT * FROM keys WHERE value IS NULL;\n" +
                "  SELECT * FROM keys WHERE value IS NOT NULL;\n\n" +
                "Note: you cannot use = NULL. Always use IS NULL or IS NOT NULL."),

            Task("Task: Select all from the keys table.", "SELECT * FROM keys;"),

            Say(N, "The value is NULL. Of course, nobody stores hashes and passwords in the same place..."),
            Say("Debbie", "But the hint is not NULL."),
            Say(N, "\"It is ended.\" That's what Tamam Shud means. In English."),
            Say("Debbie", "In Persian, yes. But the English translation is what we need."),

            // ── Task 23: UPDATE keys SET value ────────────────────────────
            Task(
                "Task: Update the value in the keys table for K001 to 'It is ended'.",
                "UPDATE keys SET value = 'It is ended' WHERE id = 'K001';"),

            // ── Task 24: SELECT * FROM keys WHERE id = 'K001' ─────────────
            Task(
                "Task: Verify the update by selecting K001 from the keys table.",
                "SELECT * FROM keys WHERE id = 'K001';"),

            Say(N, "Now we need to verify that hashing 'It is ended' matches the hash stored in passwords."),
            Say("Debbie",
                "The Timecase can run a hash check using:\n\n" +
                "  SELECT lower(hex(sha256('your text')));\n\n" +
                "lower() converts to lowercase. hex() gives a readable string. sha256() does the hashing.\n\n" +
                "Result: 5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8\n\n" +
                "It matches the hash stored in passwords. The code is \"It is ended.\""),

            Scene("N walks to the door. A small keypad on the wall.\nTypes in the phrase. A beat. The lock disengages.", ColRoom),

            Say(N, "...We are never speaking of this to anyone."),
            Say("Debbie", "That you got locked in a room and rescued by a chicken?"),
            Say(N, "Specifically that part, yes. Please don't."),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 7 — INTERNET RESEARCH
            // ═══════════════════════════════════════════════════════════════

            Scene(
                "They step out into the Somerton Beach night.\n" +
                "A figure in a trenchcoat watches from the pier — then turns and walks away.",
                ColBeach),

            Say(N, "There's our rival."),
            Say("Debbie", "Should we follow?"),
            Say(N, "No. They let us out. That means they respect the game."),
            Say("Debbie", "...Or they already finished it."),
            Say(N, "Then we have no time to waste."),

            Scene(
                "N and Debbie find a small coffee shop on the outskirts of Somerton Beach.\n" +
                "The time bubble still holds.",
                ColInterior),

            Say(N, "Okay. We have suspects, clues, testimony, a cipher nobody can crack, and a victim with no name. Let's put it together."),
            Say("Debbie", "Do I look like I care, N?"),
            Say(N, "I can't really tell. Chickens aren't that expressive."),
            Say("Debbie", "...I will pluck out your eyes with my bare beak."),
            Say(N, "Alright, alright! Calm down!\nLook — what if we're investigating in the wrong timeline? What if this case received updates decades later?"),
            Say("Debbie", "Are you implying there are clues further ahead in time?"),
            Say(N, "Still worth a try. We would need to cross-reference the modern internet and filter through billions of results."),
            Say("Debbie", "Already found it. We can pull information through the Timecase. It has access to indexed web archives.\nCome on — you are a Query Hound now. Use what you know."),

            // ── Task 25: internet.sqlite_master (→ internet_meta) ─────────
            Say("Debbie",
                "In SQLite, you can attach external databases as schemas and query them with:\n\n" +
                "  SELECT name FROM internet.sqlite_master WHERE type = 'table';\n\n" +
                "The Timecase auto-attaches the internet archive. Let's see what tables are available."),

            Task(
                "Task: List all tables available in the internet archive database.",
                "SELECT name FROM internet.sqlite_master WHERE type = 'table';"),

            Say(N, "Five sources. Let's see what each one holds."),

            // ── Task 26: inspect sa_police_records structure ───────────────
            Say("Debbie",
                "To inspect a table's structure in SQLite without selecting all its data:\n\n" +
                "  SELECT sql FROM internet.sqlite_master WHERE name = 'sa_police_records';"),

            Task(
                "Task: Check what columns the sa_police_records table has.",
                "SELECT sql FROM internet.sqlite_master WHERE name = 'sa_police_records';"),

            Say(N, "All the news tables probably have the same structure. Smart."),
            Say("Debbie",
                "UNION stacks results of multiple SELECT statements into one output, removing duplicates.\n" +
                "All SELECT statements in a UNION must have the same number of columns.\n\n" +
                "  SELECT col1, col2 FROM table1\n" +
                "  UNION\n" +
                "  SELECT col1, col2 FROM table2;\n\n" +
                "Use UNION ALL if you want to keep duplicates — it's also faster."),

            // ── Task 27: UNION across all internet tables ──────────────────
            Task(
                "Task: Search all internet archive tables for records mentioning 'Somerton', 'Webb', 'Tamam Shud', or 'Jestyn'. Combine using UNION.\n\n" +
                "SELECT headline, date, source FROM internet.bbc_news\n" +
                "WHERE keywords LIKE '%Somerton%' OR keywords LIKE '%Webb%'\n" +
                "   OR keywords LIKE '%Tamam Shud%' OR keywords LIKE '%Jestyn%'\n" +
                "UNION\n" +
                "SELECT headline, date, source FROM internet.abc_australia ...\n" +
                "(repeat for all 5 tables)\n" +
                "ORDER BY date ASC;",
                "Use UNION to combine SELECT statements from all 5 internet tables. Filter with LIKE."),

            Say("Debbie", "Look — they found the victim's identity in 2022! A man named Carl \"Charles\" Webb!"),
            Say(N, "An electrical engineer from Melbourne. Born 1905."),
            Say("Debbie", "This is incredible. Shall we jump ahead and ask about him?"),
            Say(N, "Yeah. That's actually a good idea for a chicken."),
            Say("Debbie", "Moron."),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 8 — TIME SKIP / EVIDENCE GATHERING
            // ═══════════════════════════════════════════════════════════════

            Scene(
                "N and Debbie spend the rest of the week collecting evidence about Carl \"Charles\" Webb,\n" +
                "crossing timelines between 1948 and the 2020s. It is exhausting.",
                ColTimeskip),

            AddItem("Dorothy Webb Testimony",
                "Wife of Carl Webb. Confirmed estrangement.\n" +
                "Webb had history of depression and suicide attempts.\n" +
                "Disappeared from public record April 1947.", "log"),
            AddItem("War Friend Testimony (1943)",
                "Unnamed war friend confirms Carl Webb's identity and service history.\n" +
                "Webb was known as quiet, private, and meticulous.", "log"),
            AddItem("Mental Health Record",
                "Carl Webb — documented history of depression and suicide attempts.\n" +
                "Last known address: Melbourne. Disappeared April 1947.", "document"),

            Say("Debbie", "Based on Dorothy Webb's testimony and his mental health records, it seems this man was not in a good place.\nWait — this reminds me of Neil Hamilton's testimony."),

            // ── Task 28: SELECT clue WHERE name = 'Neil Hamilton Testimony' ──
            Task(
                "Task: Select the Neil Hamilton Testimony from the clues table.",
                "SELECT * FROM clues WHERE name = 'Neil Hamilton Testimony';"),

            Say(N, "Loss of motor control. This matches the records of him having manic episodes and depressive surges."),
            Say("Debbie", "It could be a case of self-poisoning or suicide. But it doesn't explain the missing labels, the cipher, the secret pocket, or why two of our suspects refuse to speak."),
            Say(N, "True. But we now have a plausible cause of death, at least."),
            Say("Debbie", "A plausible one. Not a confirmed one. There is a difference, N."),
            Say(N, "Yeah. I know."),

            // ── Task 29: INSERT CL004 ─────────────────────────────────────
            Task(
                "Task: Insert the Carl Webb DNA identification as a new clue (CL004).",
                "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL004','Carl Webb Identity (2022 DNA)','Documentary','DNA analysis by Prof. Derek Abbott identifies victim as Carl \"Charles\" Webb, electrical engineer, Melbourne, born 1905. 99.9% confidence. Not officially confirmed by SA Police.','2022-07-26 00:00:00');"),

            // ── Task 30: INSERT CL005 ─────────────────────────────────────
            Task(
                "Task: Insert Dorothy Webb's testimony as a new clue (CL005).",
                "INSERT INTO clues (id, name, type, details, found_at) VALUES ('CL005','Dorothy Webb Testimony','Testimony','Wife of Carl Webb. Confirmed estrangement. Webb had history of depression and suicide attempts. Disappeared from public record April 1947.','2022-08-01 00:00:00');"),

            // ── Task 31: UPDATE logfile victim ────────────────────────────
            Task(
                "Task: Update the victim field in the logfile to reflect the new finding.",
                "UPDATE logfile SET victim = 'Carl \"Charles\" Webb (unconfirmed, DNA 2022)' WHERE id = '01';"),

            Say(N, "One question mark down. One identity — probably."),
            Say("Debbie", "The murderer column is still empty."),
            Say(N, "Yeah."),

            // ═══════════════════════════════════════════════════════════════
            // PHASE 9 — CONCLUSION
            // ═══════════════════════════════════════════════════════════════

            Say(N, "Do you think anyone actually killed him, Debs? Or did he walk to that beach himself?"),
            Say("Debbie", "...The evidence points both ways. That is what makes this case what it is."),
            Say(N, "A man who made sure no one would ever know who he was. No labels. No ID. A torn page that says 'it is ended.'"),
            Say("Debbie", "And a cipher that has never been decoded. A message to someone who may have never replied."),
            Say(N, "Jessica Thomson nearly fainted when she saw his face."),
            Say("Debbie", "She knew. She just took it with her."),
            Say(N, "She dies in 2007."),
            Say("Debbie", "* quietly * Yes."),

            // ── Task 32: SELECT clues ORDER BY found_at ASC ───────────────
            Say(N, "...Alright. Let's close what we can."),

            Task(
                "Task: Select all clues ordered by found_at ascending.",
                "SELECT name, type, details, found_at FROM clues ORDER BY found_at ASC;"),

            // ── Task 33: SELECT suspects WHERE eliminated = 0 ─────────────
            Task(
                "Task: Select all uneliminated suspects.",
                "SELECT name, motive FROM suspects WHERE eliminated = 0;"),

            Say(N, "We cannot eliminate anyone yet. Not without knowing who the victim actually is — for certain."),
            Say("Debbie", "And that is the wall every investigator before us hit."),
            Say(N, "What do we actually know for certain?"),
            Say("Debbie", "Summarize it. Update the logfile."),

            // ── Task 34: UPDATE logfile (free write — player writes summary) ──
            Task(
                "Task: Update the logfile — set status to 'Investigated - Inconclusive' and write your own final summary in details.\n\n" +
                "UPDATE logfile SET status = 'Investigated - Inconclusive', details = 'Your summary here.' WHERE id = '01';",
                "UPDATE logfile SET status = 'Investigated - Inconclusive', details = '...' WHERE id = '01';"),

            // ── Task 35: SELECT * FROM logfile WHERE id = '01' (verify) ───
            Task(
                "Task: Verify the final logfile entry.",
                "SELECT * FROM logfile WHERE id = '01';"),

            Say(N, "Status still unsolved."),
            Say("Debbie", "Well, it seems those $200,000 aren't going anywhere fast."),
            Say("Debbie", "By the way — is that a decaf coffee from the kids' menu?"),
            Say(N, "It was the cheapest option. Did you forget I'm broke?"),
            Say("Debbie", "Cluck."),

            // ═══════════════════════════════════════════════════════════════
            // EPILOGUE
            // ═══════════════════════════════════════════════════════════════

            Scene("The time bubble begins to dissolve.", ColBeach),

            Say(N, "We are done here."),
            Say("Debbie", "The wormhole back is open. Whenever you're ready."),
            Say(N, "Do you think we did enough?"),
            Say("Debbie", "We did what every detective who came before us did. We asked the right questions. We just couldn't get all the answers."),
            Say(N, "Because some answers were buried with the people who had them."),
            Say("Debbie", "That is the job sometimes, N."),
            Say(N, "Let's go home."),

            Scene(
                "Wormhole opens. N and Debbie step through.\n" +
                "Behind them, Somerton Beach 1948 closes like a door.",
                ColWormhole),

            Scene(
                "Back at the Agency. The reward notification appears on the Timecase screen.\n\n" +
                "CASE CLOSED — INVESTIGATED\n" +
                "Status: Inconclusive\n" +
                "Reward disbursed: $200,000\n" +
                $"Hound {N}     — $140,000 (70%)\n" +
                "Assistant Debbie — $60,000 (30%)",
                ColTimeskip),

            Say(N, "Finally. Food."),
            Say("Debbie", "* cluck * I want corn."),
            Say(N, "You can have the entire corn aisle, Debs."),
            Say("Debbie", "...You are not so bad, N."),
            Say(N, "Don't push it."),

            // Post-credits text
            Scene(
                "The Somerton Man was found on December 1, 1948.\n" +
                "He was never officially identified.\n" +
                "The cause of his death was never confirmed.\n" +
                "The cipher in the Rubaiyat was never decoded.\n" +
                "Jessica Thomson died in 2007 without ever publicly revealing what she knew.\n" +
                "In 2022, forensic genealogist Derek Abbott identified the man as Carl \"Charles\" Webb\n" +
                "with 99.9% confidence through DNA analysis.\n" +
                "South Australia Police have not officially confirmed the identification.\n\n" +
                "Tamam Shud.",
                ColEpilogue),
        };

        return story;
    }

    // ── Shorthand aliases ─────────────────────────────────────────────────────

    private static DialogueNode Say(string speaker, string text) =>
        DialogueNode.Say(speaker, text);

    private static DialogueNode Task(string label, string hint = "") =>
        DialogueNode.Task(label, hint);

    private static DialogueNode Demo(string sql, string label = "Debbie types:") =>
        DialogueNode.Demo(sql, label);

    private static DialogueNode Scene(string text, Color color) =>
        DialogueNode.Scene(text, color);

    private static DialogueNode AddItem(string name, string desc, string type = "document") =>
        DialogueNode.AddItem(name, desc, type);
}
