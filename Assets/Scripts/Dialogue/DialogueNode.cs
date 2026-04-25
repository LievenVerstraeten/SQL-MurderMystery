// DialogueNode.cs
// Data model for every beat in the visual novel story.
//
// Types:
//   Dialogue    — A character says something. Shows in the VN speech box.
//   SQLTask     — Player must run a correct SQL query to advance.
//   DemoSQL     — System runs SQL automatically and shows result (Debbie demonstration).
//   Cutscene    — Coloured fullscreen overlay with text.
//   InventoryAdd — Silently adds an item to the player's inventory.
//
// Use the static factory helpers (Say, Task, Demo, Scene, AddItem) to build the
// story list concisely. See Case01Story.cs for usage.

using UnityEngine;

public enum NodeType
{
    Dialogue,
    SQLTask,
    DemoSQL,
    Cutscene,
    InventoryAdd,
}

public class DialogueNode
{
    public NodeType Type;

    // ── Dialogue ─────────────────────────────────────────────────────────────
    // Speaker keys: "N", "Debbie", "Jessica", "Neil", "Cleland",
    //               "TimeHound", "System", "Narration"
    public string Speaker;
    public string Text;

    // ── SQL Task ──────────────────────────────────────────────────────────────
    // TaskLabel is shown above the terminal when the task opens.
    // HintText overrides the CaseManager hint if non-empty.
    public string TaskLabel;
    public string HintText;

    // ── Demo SQL ──────────────────────────────────────────────────────────────
    // DemoSQL is executed automatically; result is displayed in the terminal.
    // DemoLabel appears as a caption above the query (e.g. "Debbie types:").
    public string DemoSQL;
    public string DemoLabel;

    // ── Cutscene ──────────────────────────────────────────────────────────────
    public string CutsceneText;
    public Color  CutsceneColor;

    // ── Inventory ─────────────────────────────────────────────────────────────
    public string ItemName;
    public string ItemDescription;
    // "log" | "clue" | "document" — used to pick the icon in the inventory panel
    public string ItemType;

    // ── Factory helpers ───────────────────────────────────────────────────────

    public static DialogueNode Say(string speaker, string text) =>
        new DialogueNode { Type = NodeType.Dialogue, Speaker = speaker, Text = text };

    public static DialogueNode Task(string label, string hint = "") =>
        new DialogueNode { Type = NodeType.SQLTask, TaskLabel = label, HintText = hint };

    public static DialogueNode Demo(string sql, string label = "Debbie types:") =>
        new DialogueNode { Type = NodeType.DemoSQL, DemoSQL = sql, DemoLabel = label };

    public static DialogueNode Scene(string text, Color color) =>
        new DialogueNode { Type = NodeType.Cutscene, CutsceneText = text, CutsceneColor = color };

    public static DialogueNode AddItem(string name, string description, string itemType = "document") =>
        new DialogueNode
        {
            Type            = NodeType.InventoryAdd,
            ItemName        = name,
            ItemDescription = description,
            ItemType        = itemType,
        };
}
