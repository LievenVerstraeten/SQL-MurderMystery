// ERDManager.cs
// Displays an ERD of the WORLD DB tables only (no save/progress tables).
// Styled to match the game's dark theme: black background, yellow headers,
// white column text. Relations drawn as bright yellow bezier lines with
// solid arrowheads.
//
// Controls:
//   Drag   → pan
//   Scroll → zoom in/out
//   ✕      → close
//
// UXML — add to root in GameUI.uxml just before clue-board-instance:
//
//   <ui:VisualElement name="erd-overlay" style="display:none; position:absolute;
//       left:0; right:0; top:0; bottom:0; background-color:rgb(10,10,10);
//       flex-direction:column;">
//       <ui:VisualElement name="erd-header" style="flex-direction:row;
//           align-items:center; padding:12px 20px;
//           background-color:rgb(20,20,20); flex-shrink:0;
//           border-bottom-width:2px; border-bottom-color:rgb(230,200,50);">
//           <ui:Label text="DATABASE STRUCTURE" style="flex-grow:1;
//               font-size:15px; color:rgb(230,200,50); -unity-font-style:bold;" />
//           <ui:Label name="erd-zoom-label" text="100%" style="font-size:12px;
//               color:rgb(160,160,160); margin-right:20px;" />
//           <ui:Button name="erd-close-btn" text="✕" style="font-size:16px;
//               color:white; background-color:rgba(0,0,0,0);
//               border-width:1px; border-color:rgb(100,100,100);
//               width:32px; height:32px;" />
//       </ui:VisualElement>
//       <ui:VisualElement name="erd-canvas-root" style="flex-grow:1; overflow:hidden;">
//           <ui:VisualElement name="erd-canvas" style="position:absolute; left:0; top:0;">
//               <ui:VisualElement name="erd-lines-layer"
//                   style="position:absolute; left:0; top:0; right:0; bottom:0;" />
//               <ui:VisualElement name="erd-tables-layer"
//                   style="position:absolute; left:0; top:0;" />
//           </ui:VisualElement>
//       </ui:VisualElement>
//   </ui:VisualElement>
//
// ERD button in header-right (already added in a previous step):
//   <ui:Button name="erd-button" class="icon-button" tooltip="View Database ERD">
//       <ui:VisualElement class="btn-icon"
//           style="background-image:url('.../UI_ERD.png');"/>
//   </ui:Button>

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

public class ERDManager : MonoBehaviour
{
    public static ERDManager Instance { get; private set; }

    // ─── Palette (dark gold + warm brown aesthetic) ───────────────────────────
    private static readonly Color C_BG = new Color(0.08f, 0.06f, 0.04f);       // warm near-black
    private static readonly Color C_BORDER = new Color(0.55f, 0.40f, 0.11f);   // dark gold border
    private static readonly Color C_HDR_BG = new Color(0.18f, 0.12f, 0.04f);   // dark brown header
    private static readonly Color C_HDR_TEXT = new Color(0.55f, 0.40f, 0.11f); // dark gold table name
    private static readonly Color C_HDR_DB = new Color(0.40f, 0.28f, 0.09f);   // dimmer dark gold
    private static readonly Color C_ROW_DEFAULT = new Color(0.09f, 0.07f, 0.05f); // warm dark row
    private static readonly Color C_ROW_PK = new Color(0.16f, 0.11f, 0.04f);   // warm PK row
    private static readonly Color C_ROW_FK = new Color(0.06f, 0.10f, 0.16f);   // cool FK row
    private static readonly Color C_ROW_DIVIDER = new Color(0.22f, 0.16f, 0.08f); // warm divider
    private static readonly Color C_COL_NAME = new Color(0.82f, 0.70f, 0.54f); // warm cream
    private static readonly Color C_COL_TYPE = new Color(0.50f, 0.38f, 0.24f); // muted warm
    private static readonly Color C_BADGE_PK = new Color(0.55f, 0.40f, 0.11f); // dark gold
    private static readonly Color C_BADGE_FK = new Color(0.35f, 0.70f, 0.95f); // cyan (FK stays distinct)
    private static readonly Color C_ARROW = new Color(0.55f, 0.40f, 0.11f);    // dark gold arrows

    // ─── Layout constants ─────────────────────────────────────────────────────
    private const float CARD_W = 210f;   // card width
    private const float H_GAP = 160f;   // wide horizontal gap
    private const float V_GAP = 120f;   // tall vertical gap
    private const float HDR_H = 56f;    // header block height
    private const float ROW_H = 26f;    // height per column row
    private const int COLS = 4;      // cards per row

    // ─── Data model ───────────────────────────────────────────────────────────
    private class ErdColumn
    {
        public string Name, Type, RefTable;
        public bool IsPK, IsFK;
    }

    private class ErdTable
    {
        public string Name, DbLabel;
        public List<ErdColumn> Columns = new();
        public VisualElement Element;
    }

    private class ErdRelation
    {
        public ErdTable From, To;
        public string FromCol;
    }

    // ─── UI refs ──────────────────────────────────────────────────────────────
    private VisualElement _overlay, _canvasRoot, _canvas, _linesLayer, _tablesLayer, _labelsLayer;
    private Label _zoomLabel;

    // ─── Pan / zoom ───────────────────────────────────────────────────────────
    private Vector2 _pan = Vector2.zero, _panStart, _panOrigin;
    private float _zoom = 1f;
    private bool _panning;
    private const float ZOOM_MIN = 0.2f, ZOOM_MAX = 3f;

    // ─── ERD data ─────────────────────────────────────────────────────────────
    private readonly List<ErdTable> _tables = new();
    private readonly List<ErdRelation> _relations = new();

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void ConnectToUI(UIDocument doc)
    {
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _overlay = root.Q("erd-overlay");
        _canvasRoot = root.Q("erd-canvas-root");
        _canvas = root.Q("erd-canvas");
        _linesLayer = root.Q("erd-lines-layer");
        _tablesLayer = root.Q("erd-tables-layer");
        _labelsLayer = root.Q("erd-labels-layer");
        _zoomLabel = root.Q<Label>("erd-zoom-label");

        if (_overlay == null) { Debug.LogError("[ERDManager] 'erd-overlay' missing from UXML."); return; }
        if (_canvas == null) { Debug.LogError("[ERDManager] 'erd-canvas' missing from UXML."); return; }
        if (_linesLayer == null) { Debug.LogError("[ERDManager] 'erd-lines-layer' missing from UXML."); return; }
        if (_tablesLayer == null) { Debug.LogError("[ERDManager] 'erd-tables-layer' missing from UXML."); return; }
        if (_labelsLayer == null) Debug.LogWarning("[ERDManager] 'erd-labels-layer' not found — relation labels will not show.");

        root.Q<Button>("erd-button")?.RegisterCallback<ClickEvent>(_ => SetVisible(true));
        root.Q<Button>("erd-close-btn")?.RegisterCallback<ClickEvent>(_ =>
        {
            SetVisible(false);
            _overlay?.panel?.visualTree?.Q("root")?.Focus();
        });

        _canvasRoot.RegisterCallback<PointerDownEvent>(OnPanStart);
        _canvasRoot.RegisterCallback<PointerMoveEvent>(OnPanMove);
        _canvasRoot.RegisterCallback<PointerUpEvent>(OnPanEnd);
        _canvasRoot.RegisterCallback<WheelEvent>(OnWheel);

        SetVisible(false);
        Debug.Log("[ERDManager] Connected.");
    }

    public void SetVisible(bool visible)
    {
        if (_overlay == null) return;
        _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible) { _overlay.BringToFront(); BuildERD(); }
    }

    // ─── Build ────────────────────────────────────────────────────────────────

    private void BuildERD()
    {
        _tables.Clear();
        _relations.Clear();
        _tablesLayer.Clear();
        _labelsLayer?.Clear();
        _linesLayer.generateVisualContent -= DrawLines;

        // ── World DB tables ONLY (static + dynamic) ───────────────────────────
        var activeCase = CaseManager.Instance?.ActiveCase;
        if (activeCase == null)
        {
            Debug.LogWarning("[ERDManager] No active case — no tables to show.");
            return;
        }

        // Static tables (read-only world data the player queries)
        foreach (var td in activeCase.StaticTables)
        {
            var t = ParseCreateSQL(td.CreateSQL, "Static");
            if (t != null) _tables.Add(t);
        }

        // Dynamic tables (player-writable tables that live in world.db)
        foreach (var td in activeCase.DynamicTables)
        {
            var t = ParseCreateSQL(td.CreateSQL, "Dynamic");
            if (t != null) _tables.Add(t);
        }

        Debug.Log($"[ERDManager] {_tables.Count} game tables parsed.");

        // ── Hardcoded logical relationships (no REFERENCES in SQL) ──────────
        // These are the actual FK-style relationships defined by column naming
        // and game logic, extracted from CaseManager table definitions.
        AddRelation("witnesses", "contacts", "contact_id");
        AddRelation("clues", "logfile", "found_at");
        AddRelation("suspects", "logfile", "eliminated");
        AddRelation("witnesses", "logfile", "interviewed");
        AddRelation("bbc_news", "internet_meta", "source");
        AddRelation("abc_australia", "internet_meta", "source");
        AddRelation("sa_police_records", "internet_meta", "source");
        AddRelation("adelaide_advertiser", "internet_meta", "source");
        AddRelation("forensic_reports", "internet_meta", "source");
        AddRelation("cipher_fragments", "clues", "id");

        LayoutTables();

        _linesLayer.generateVisualContent += DrawLines;

        // Wait for the tables layer to finish its layout pass before drawing lines.
        // GeometryChangedEvent fires after UI Toolkit resolves all element bounds,
        // so layout values are guaranteed to be non-zero when DrawLines runs.
        _tablesLayer.RegisterCallback<GeometryChangedEvent>(OnTablesLayerGeometryChanged);

        _zoom = 1f;
        _pan = Vector2.zero;
        ApplyTransform();
    }

    // ─── Relation helper ─────────────────────────────────────────────────────

    private void AddRelation(string fromTable, string toTable, string fromCol)
    {
        var from = _tables.Find(t => t.Name == fromTable);
        var to = _tables.Find(t => t.Name == toTable);
        if (from != null && to != null)
            _relations.Add(new ErdRelation { From = from, To = to, FromCol = fromCol });
        // Silently skip if one of the tables isn't in the active case — future cases
        // may not have all the same tables.
    }

    // ─── CREATE SQL parser ────────────────────────────────────────────────────

    private static ErdTable ParseCreateSQL(string sql, string dbLabel)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;

        var nm = Regex.Match(sql,
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[`""\[]?(\w+)[`""\]]?",
            RegexOptions.IgnoreCase);
        if (!nm.Success) return null;

        var table = new ErdTable { Name = nm.Groups[1].Value, DbLabel = dbLabel };

        int s = sql.IndexOf('('), e = sql.LastIndexOf(')');
        if (s < 0 || e <= s) return table;

        foreach (var raw in SplitDefs(sql.Substring(s + 1, e - s - 1)))
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (Regex.IsMatch(line,
                @"^\s*(UNIQUE|CHECK|FOREIGN\s+KEY|PRIMARY\s+KEY\s*\()",
                RegexOptions.IgnoreCase)) continue;

            var cm = Regex.Match(line,
                @"^[`""\[]?(\w+)[`""\]]?\s+(\w+)", RegexOptions.IgnoreCase);
            if (!cm.Success) continue;

            var col = new ErdColumn
            {
                Name = cm.Groups[1].Value,
                Type = cm.Groups[2].Value.ToUpper(),
                IsPK = Regex.IsMatch(line, @"PRIMARY\s+KEY", RegexOptions.IgnoreCase),
            };

            var rm = Regex.Match(line,
                @"REFERENCES\s+[`""\[]?(\w+)[`""\]]?", RegexOptions.IgnoreCase);
            if (rm.Success) { col.IsFK = true; col.RefTable = rm.Groups[1].Value; }

            table.Columns.Add(col);
        }

        return table;
    }

    private static List<string> SplitDefs(string body)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == '(') depth++;
            else if (body[i] == ')') depth--;
            else if (body[i] == ',' && depth == 0)
            { parts.Add(body.Substring(start, i - start)); start = i + 1; }
        }
        if (start < body.Length) parts.Add(body.Substring(start));
        return parts;
    }

    // ─── Layout ───────────────────────────────────────────────────────────────

    private void LayoutTables()
    {
        // ── Manual grouped layout — related tables placed near each other ──────
        // Group 1 (row 0): internet archive siblings all next to internet_meta
        // Group 2 (row 1): news/archive tables continued
        // Group 3 (row 2): core investigation tables
        // Group 4 (row 3): dynamic case tables
        //
        // Tables are positioned using explicit (col, row) slots so
        // relationships draw as short lines instead of crossing the diagram.

        var slots = new Dictionary<string, (int col, int row)>
        {
            // Row 0 — internet archive group
            { "internet_meta",       (0, 0) },
            { "bbc_news",            (1, 0) },
            { "abc_australia",       (2, 0) },
            { "sa_police_records",   (3, 0) },

            // Row 1 — archive continued + cipher
            { "adelaide_advertiser", (0, 1) },
            { "forensic_reports",    (1, 1) },
            { "cipher_fragments",    (2, 1) },
            { "contacts",            (3, 1) },

            // Row 2 — core investigation (static)
            { "clues",               (0, 2) },
            { "witnesses",           (1, 2) },
            { "suspects",            (2, 2) },
            { "hounds",              (3, 2) },

            // Row 3 — dynamic case tables
            { "logfile",             (0, 3) },
            { "passwords",           (1, 3) },
            { "keys",                (2, 3) },
        };

        // First pass: calculate the tallest card per row
        var rowMaxH = new Dictionary<int, float>();
        foreach (var table in _tables)
        {
            int row = slots.TryGetValue(table.Name, out var slot) ? slot.row : 99;
            float h = HDR_H + table.Columns.Count * ROW_H + 8f;
            if (!rowMaxH.ContainsKey(row) || h > rowMaxH[row])
                rowMaxH[row] = h;
        }

        // Build cumulative Y per row
        var rowY = new Dictionary<int, float>();
        var sortedRows = new List<int>(rowMaxH.Keys);
        sortedRows.Sort();
        float yAccum = 60f;
        foreach (int r in sortedRows)
        {
            rowY[r] = yAccum;
            yAccum += rowMaxH[r] + V_GAP;
        }

        // Second pass: place every table
        int overflowCol = 0;
        foreach (var table in _tables)
        {
            int col, row;
            if (slots.TryGetValue(table.Name, out var s))
            {
                col = s.col; row = s.row;
            }
            else
            {
                // Unknown table — append below existing rows
                col = overflowCol % COLS;
                row = 99;
                if (!rowY.ContainsKey(99)) rowY[99] = yAccum;
                if (!rowMaxH.ContainsKey(99)) rowMaxH[99] = HDR_H + table.Columns.Count * ROW_H + 8f;
                overflowCol++;
            }

            float x = col * (CARD_W + H_GAP) + 60f;
            float y = rowY.ContainsKey(row) ? rowY[row] : yAccum;

            var card = BuildCard(table);
            card.style.left = x;
            card.style.top = y;
            card.style.width = CARD_W;

            table.Element = card;
            _tablesLayer.Add(card);
        }
    }

    // ─── Card builder ─────────────────────────────────────────────────────────

    private VisualElement BuildCard(ErdTable table)
    {
        bool isDynamic = table.DbLabel == "Dynamic";

        // Shell
        var card = new VisualElement();
        card.style.position = Position.Absolute;
        card.style.overflow = Overflow.Hidden;
        SetBorder(card, C_BORDER, 2f, 6f);
        card.style.backgroundColor = new StyleColor(C_BG);

        // Header — yellow text on near-black gold-tinted bg
        var hdr = new VisualElement();
        hdr.style.backgroundColor = new StyleColor(C_HDR_BG);
        hdr.style.paddingTop = hdr.style.paddingBottom = 8f;
        hdr.style.paddingLeft = hdr.style.paddingRight = 12f;
        hdr.style.borderBottomWidth = 2f;
        hdr.style.borderBottomColor = new StyleColor(C_BORDER);

        var nameL = new Label(table.Name);
        nameL.style.color = new StyleColor(C_HDR_TEXT);
        nameL.style.fontSize = 13f;
        nameL.style.unityFontStyleAndWeight = FontStyle.Bold;

        // Sub-label: "Static" shown as "WORLD DB", "Dynamic" as "CASE DB"
        var subL = new Label(isDynamic ? "CASE DB" : "WORLD DB");
        subL.style.color = new StyleColor(C_HDR_DB);
        subL.style.fontSize = 9f;
        subL.style.marginTop = 1f;

        hdr.Add(nameL);
        hdr.Add(subL);
        card.Add(hdr);

        // Column rows
        foreach (var col in table.Columns)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = row.style.paddingRight = 10f;
            row.style.paddingTop = row.style.paddingBottom = 5f;
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = new StyleColor(C_ROW_DIVIDER);
            row.style.backgroundColor = new StyleColor(
                col.IsPK ? C_ROW_PK : col.IsFK ? C_ROW_FK : C_ROW_DEFAULT);

            // Badge
            if (col.IsPK || col.IsFK)
            {
                var badge = new Label(col.IsPK ? "PK" : "FK");
                badge.style.fontSize = 8f;
                badge.style.color = new StyleColor(col.IsPK ? C_BADGE_PK : C_BADGE_FK);
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.width = 18f;
                badge.style.marginRight = 4f;
                row.Add(badge);
            }
            else
            {
                var spacer = new VisualElement(); spacer.style.width = 22f; row.Add(spacer);
            }

            var colName = new Label(col.Name);
            colName.style.flexGrow = 1f;
            colName.style.fontSize = 11f;
            colName.style.color = new StyleColor(C_COL_NAME);
            colName.style.unityFontStyleAndWeight =
                col.IsPK ? FontStyle.Bold : FontStyle.Normal;

            var colType = new Label(col.Type);
            colType.style.fontSize = 9f;
            colType.style.color = new StyleColor(C_COL_TYPE);
            colType.style.marginLeft = 6f;

            row.Add(colName);
            row.Add(colType);
            card.Add(row);
        }

        return card;
    }

    private static void SetBorder(VisualElement el, Color c, float w, float r)
    {
        el.style.borderTopWidth = el.style.borderBottomWidth =
        el.style.borderLeftWidth = el.style.borderRightWidth = w;
        el.style.borderTopColor = el.style.borderBottomColor =
        el.style.borderLeftColor = el.style.borderRightColor = new StyleColor(c);
        el.style.borderTopLeftRadius = el.style.borderTopRightRadius =
        el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = r;
    }

    // ─── Geometry callback — fires once layout is resolved ───────────────────

    private void OnTablesLayerGeometryChanged(GeometryChangedEvent e)
    {
        // Check that at least the first card has a resolved layout before drawing.
        // layout.width == 0 means UI Toolkit hasn't measured it yet — keep waiting.
        if (_tables.Count > 0 && _tables[0].Element != null
            && _tables[0].Element.layout.width < 1f) return;

        // Unregister — we only need this once per BuildERD call
        _tablesLayer.UnregisterCallback<GeometryChangedEvent>(OnTablesLayerGeometryChanged);
        _linesLayer.MarkDirtyRepaint();
        BuildRelationLabels();
    }


    // DrawLines draws geometry only — no label creation here.
    // Labels live on _labelsLayer, built once in BuildRelationLabels().
    // Keeping them separate prevents the label->layout->repaint loop
    // that caused arrows to vanish on zoom/pan.

    private void DrawLines(MeshGenerationContext ctx)
    {
        if (_relations.Count == 0) return;

        var p = ctx.painter2D;
        p.lineCap = LineCap.Round;

        foreach (var rel in _relations)
        {
            if (rel.From?.Element == null || rel.To?.Element == null) continue;

            var fb = rel.From.Element.layout;
            var tb = rel.To.Element.layout;
            if (fb.width < 1f || tb.width < 1f) continue;

            Vector2 a, b;
            if (Mathf.Abs(fb.center.x - tb.center.x) > 10f)
            {
                if (fb.center.x < tb.center.x)
                { a = new Vector2(fb.xMax, fb.center.y); b = new Vector2(tb.xMin, tb.center.y); }
                else
                { a = new Vector2(fb.xMin, fb.center.y); b = new Vector2(tb.xMax, tb.center.y); }
            }
            else
            {
                if (fb.center.y < tb.center.y)
                { a = new Vector2(fb.center.x, fb.yMax); b = new Vector2(tb.center.x, tb.yMin); }
                else
                { a = new Vector2(fb.center.x, fb.yMin); b = new Vector2(tb.center.x, tb.yMax); }
            }

            float cx = (a.x + b.x) * 0.5f;

            // Shadow
            p.strokeColor = new Color(0f, 0f, 0f, 0.55f);
            p.lineWidth = 5f;
            p.BeginPath();
            p.MoveTo(a);
            p.BezierCurveTo(new Vector2(cx, a.y), new Vector2(cx, b.y), b);
            p.Stroke();

            // Line
            p.strokeColor = C_ARROW;
            p.lineWidth = 2.5f;
            p.BeginPath();
            p.MoveTo(a);
            p.BezierCurveTo(new Vector2(cx, a.y), new Vector2(cx, b.y), b);
            p.Stroke();

            // Small filled circle at origin end
            p.fillColor = C_ARROW;
            p.BeginPath();
            p.Arc(a, 4f, 0f, 360f);
            p.Fill();

            // Small filled circle at destination end — clean, no messy arrowhead
            p.fillColor = C_ARROW;
            p.BeginPath();
            p.Arc(b, 4f, 0f, 360f);
            p.Fill();
        }
    }

    /// <summary>
    /// Builds relation labels once after layout resolves.
    /// _labelsLayer is inside _canvas so labels zoom/pan automatically
    /// with everything else — no coordinate recalculation ever needed.
    /// Shows "TableA -> TableB" so it is unambiguous even when multiple
    /// arrows share the same column name (e.g. all archive tables use "source").
    /// </summary>
    private void BuildRelationLabels()
    {
        if (_labelsLayer == null) return;
        _labelsLayer.Clear();

        // Group all relations by their From table so tables with multiple
        // relations get a single combined pill instead of stacked overlapping ones.
        var groups = new Dictionary<string, (ErdTable table, List<string> targets)>();
        foreach (var rel in _relations)
        {
            if (rel.From?.Element == null || rel.To?.Element == null) continue;
            if (!groups.ContainsKey(rel.From.Name))
                groups[rel.From.Name] = (rel.From, new List<string>());
            groups[rel.From.Name].targets.Add(rel.To.Name);
        }

        foreach (var kv in groups)
        {
            var fromTable = kv.Value.table;
            var fb = fromTable.Element.layout;
            if (fb.width < 1f) continue;

            // Build combined text: "witnesses -> contacts, logfile"
            string fromName = fromTable.Name;
            string toNames = string.Join(", ", kv.Value.targets);
            string text = fromName + "  ->  " + toNames;

            // Place the pill flush with the card left edge, 4px above its top border
            float x = fb.xMin;
            float y = fb.yMin - 22f;

            var pill = new VisualElement();
            pill.style.position = Position.Absolute;
            pill.style.left = x;
            pill.style.top = y;
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.backgroundColor = new StyleColor(C_ARROW);
            pill.style.paddingLeft = pill.style.paddingRight = 7f;
            pill.style.paddingTop = pill.style.paddingBottom = 3f;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius = 4f;
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 0f;

            // From name in bold black
            var fromLbl = new Label(fromName);
            fromLbl.style.fontSize = 8f;
            fromLbl.style.color = new StyleColor(new Color(0.05f, 0.05f, 0.05f));
            fromLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            fromLbl.style.whiteSpace = WhiteSpace.NoWrap;

            // Arrow separator
            var arrow = new Label("  ->  ");
            arrow.style.fontSize = 8f;
            arrow.style.color = new StyleColor(new Color(0.2f, 0.1f, 0f));
            arrow.style.whiteSpace = WhiteSpace.NoWrap;

            // Target names
            var toLbl = new Label(toNames);
            toLbl.style.fontSize = 8f;
            toLbl.style.color = new StyleColor(new Color(0.05f, 0.05f, 0.05f));
            toLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            toLbl.style.whiteSpace = WhiteSpace.NoWrap;

            pill.Add(fromLbl);
            pill.Add(arrow);
            pill.Add(toLbl);

            _labelsLayer.Add(pill);
        }
    }


    // ─── Pan & zoom ───────────────────────────────────────────────────────────

    private void OnPanStart(PointerDownEvent e)
    {
        if (e.button != 0) return;
        _panning = true;
        _panStart = e.position;
        _panOrigin = _pan;
        _canvasRoot.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnPanMove(PointerMoveEvent e)
    {
        if (!_panning || !_canvasRoot.HasPointerCapture(e.pointerId)) return;
        _pan = _panOrigin + (Vector2)e.position - _panStart;
        ApplyTransform();
        _linesLayer.MarkDirtyRepaint();
        e.StopPropagation();
    }

    private void OnPanEnd(PointerUpEvent e)
    {
        if (!_panning) return;
        _panning = false;
        _canvasRoot.ReleasePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnWheel(WheelEvent e)
    {
        _zoom = Mathf.Clamp(_zoom + (e.delta.y > 0 ? -0.1f : 0.1f), ZOOM_MIN, ZOOM_MAX);
        ApplyTransform();
        _linesLayer.MarkDirtyRepaint();
        e.StopPropagation();
    }

    private void ApplyTransform()
    {
        if (_canvas == null) return;
        _canvas.style.left = _pan.x;
        _canvas.style.top = _pan.y;
        _canvas.style.scale = new StyleScale(new Scale(new Vector3(_zoom, _zoom, 1f)));
        _canvas.style.transformOrigin =
            new StyleTransformOrigin(new TransformOrigin(0, 0, 0));
        if (_zoomLabel != null)
            _zoomLabel.text = $"{Mathf.RoundToInt(_zoom * 100f)}%";
    }
}