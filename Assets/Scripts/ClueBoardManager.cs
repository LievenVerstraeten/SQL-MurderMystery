// ClueBoardManager.cs
// DEFINITIVE VERSION
// ASSETS — all in Assets/Textures/actual assets/clue board/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

public class ClueBoardManager : MonoBehaviour
{
    public static ClueBoardManager Instance { get; private set; }

    private const string A = "Assets/Textures/actual assets/clue board/";

    private class BoardCard
    {
        public string Id, Title, Body;
        public VisualElement Element;
        public float Rotation;
    }

    private class RopeConnection
    {
        public BoardCard From, To;
    }

    // ── UI refs ───────────────────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _cardsLayer;
    private VisualElement _ropeLayer;
    private VisualElement _cardActions;
    private VisualElement _inventoryTray;
    private Button _inventoryBtn;
    private Button _actionRopeBtn;
    private Button _actionDeleteBtn;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly List<BoardCard> _cards = new();
    private readonly List<RopeConnection> _ropes = new();
    private BoardCard _selectedCard = null;
    private BoardCard _ropeStartCard = null;
    private bool _isRopeMode = false;
    private bool _isTrayOpen = false;
    private VisualElement _dragTarget = null;
    private Vector2 _dragOffset;
    private bool _didDrag = false;
    private bool _ropeDirty = false;
    private int _cardCounter = 0;
    private bool _isLoading = false; // suppress SaveBoard during LoadBoard

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void ConnectToUI(UIDocument uiDocument)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[ClueBoardManager] UIDocument null in ConnectToUI.");
            return;
        }
        var rve = uiDocument.rootVisualElement;
        var instance = rve.Q("clue-board-instance");
        if (instance != null)
            _root = instance.Q("cb-root") ?? instance;
        else
            _root = rve.Q("cb-root");

        if (_root == null)
        {
            Debug.LogError("[ClueBoardManager] cb-root not found.");
            return;
        }
        Debug.Log($"[ClueBoardManager] ConnectToUI — root: {_root.name}");
        InitialiseBoard();
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var rve = doc.rootVisualElement;
        _root = rve.Q("clue-board-instance") ?? rve.Q("cb-root") ?? rve;
        InitialiseBoard();
    }

    private void InitialiseBoard()
    {
        if (_root == null) { Debug.LogError("[ClueBoardManager] _root null."); return; }

        _cardsLayer = _root.Q("cards-layer");
        _ropeLayer = _root.Q("rope-layer");
        _cardActions = _root.Q("card-actions");
        _inventoryTray = _root.Q("inventory-tray");
        _inventoryBtn = _root.Q<Button>("inventory-btn");
        _actionRopeBtn = _root.Q<Button>("action-rope");
        _actionDeleteBtn = _root.Q<Button>("action-delete");

        if (_cardsLayer == null) { Debug.LogError("[ClueBoardManager] cards-layer not found."); return; }
        if (_ropeLayer == null) { Debug.LogError("[ClueBoardManager] rope-layer not found."); return; }

        var boardBg = _root.Q("board-bg");
        if (boardBg != null)
        {
            boardBg.style.backgroundColor = new StyleColor(new Color(0.14f, 0.11f, 0.08f));
            ApplyTexture(boardBg, "clueboard bg (1).png");
        }

        _ropeLayer.generateVisualContent -= DrawRopes;

        _root.Q<Button>("close-btn")?.RegisterCallback<ClickEvent>(_ =>
        {
            SetVisible(false);
            // Re-focus root so keyboard dialogue advance works again
            _root.panel?.visualTree?.Q("root")?.Focus();
        });
        _root.Q<Button>("add-note-btn")?.RegisterCallback<ClickEvent>(_ => AddNote());
        _inventoryBtn?.RegisterCallback<ClickEvent>(_ => ToggleTray());
        _root.Q<Button>("tray-close")?.RegisterCallback<ClickEvent>(_ => CloseTray());
        _actionDeleteBtn?.RegisterCallback<ClickEvent>(_ => ActionDelete());
        _root.Q<Button>("action-edit")?.RegisterCallback<ClickEvent>(_ => ActionEdit());
        _actionRopeBtn?.RegisterCallback<ClickEvent>(_ => ActionStartRope());
        _root.Q<Button>("action-rotate-l")?.RegisterCallback<ClickEvent>(_ => ActionRotate(-15f));
        _root.Q<Button>("action-rotate-r")?.RegisterCallback<ClickEvent>(_ => ActionRotate(+15f));

        ApplyTexture(_root.Q("action-bar-bg"), "Untitled39_0000s_0006_Panel-For-Close_Open_Add-options.png");
        ApplyButtonIcon(_root.Q<Button>("close-btn"), "Untitled39_0000s_0000_Button-Close.png");
        ApplyButtonIcon(_root.Q<Button>("add-note-btn"), "Untitled39_0000s_0005_Button-Circle-Empty.png");
        ApplyButtonIcon(_inventoryBtn, "Untitled39_0000s_0001_Button-Arrow.png");
        ApplyButtonIcon(_actionDeleteBtn, "Untitled39_0000s_0000_Button-Close.png");
        ApplyButtonIcon(_actionRopeBtn, "Untitled39_0000s_0004_Button-Pin.png");
        ApplyButtonIcon(_root.Q<Button>("action-rotate-l"), "Untitled39_0000s_0002_Button-Left.png");
        ApplyButtonIcon(_root.Q<Button>("action-rotate-r"), "Untitled39_0000s_0003_Button-Right.png");

        var editBtn = _root.Q<Button>("action-edit");
        if (editBtn != null)
        {
            var tex = LoadAsset("Untitled39_0000s_0005_Button-Circle-Empty.png");
            if (tex != null)
            {
                editBtn.text = "✎";
                editBtn.style.backgroundImage = new StyleBackground(tex);
                editBtn.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                editBtn.style.backgroundColor = new StyleColor(Color.clear);
                editBtn.style.borderTopWidth = editBtn.style.borderBottomWidth =
                editBtn.style.borderLeftWidth = editBtn.style.borderRightWidth = 0f;
                editBtn.style.fontSize = 16f;
                editBtn.style.color = new StyleColor(new Color(0.15f, 0.12f, 0.08f));
                editBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                editBtn.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                editBtn.style.width = editBtn.style.height = 44f;
            }
        }

        _ropeLayer.generateVisualContent += DrawRopes;
        _cardsLayer.RegisterCallback<PointerDownEvent>(OnBoardPointerDown);

        SetVisible(false);
        PopulateTray();

        // Clear any existing cards/ropes from a previous ConnectToUI call
        ClearBoard();
        LoadBoard();
    }

    private void ClearBoard()
    {
        foreach (var card in _cards)
            if (_cardsLayer.Contains(card.Element))
                _cardsLayer.Remove(card.Element);
        _cards.Clear();
        _ropes.Clear();
        _selectedCard = null;
        _ropeStartCard = null;
        _isRopeMode = false;
        _cardCounter = 0;
        _ropeDirty = true;
    }

    private void OnDisable()
    {
        if (_ropeLayer != null)
            _ropeLayer.generateVisualContent -= DrawRopes;
    }

    private void Update()
    {
        if (_ropeDirty)
        {
            _ropeDirty = false;
            _ropeLayer?.MarkDirtyRepaint();
        }
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetVisible(bool visible)
    {
        if (_root == null) { Debug.LogError("[ClueBoardManager] SetVisible — _root null."); return; }

        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        var wrapper = _root.parent;
        if (wrapper != null)
        {
            wrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            wrapper.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        if (visible)
            (wrapper ?? _root).BringToFront();
        else
            CloseTray();
    }

    public bool IsVisible()
    {
        return _root != null && _root.resolvedStyle.display == DisplayStyle.Flex;
    }

    public void AddClueFromInventory(string title, string body)
    {
        SetVisible(true);
        float cx = _cardsLayer.layout.width > 10 ? _cardsLayer.layout.width * 0.38f : 200f;
        float cy = _cardsLayer.layout.height > 10 ? _cardsLayer.layout.height * 0.35f : 140f;
        SpawnCard(new BoardCard
        {
            Id = "card_" + _cardCounter++,
            Title = title,
            Body = body,
            Rotation = UnityEngine.Random.Range(-12f, 12f)
        },
            new Vector2(cx + UnityEngine.Random.Range(-80, 80), cy + UnityEngine.Random.Range(-60, 60)));
    }

    // ── Rope drawing ──────────────────────────────────────────────────────────

    private void DrawRopes(MeshGenerationContext ctx)
    {
        if (_ropes.Count == 0) return;
        var p = ctx.painter2D;
        p.strokeColor = new Color(0.65f, 0.08f, 0.08f);
        p.lineWidth = 4f;
        p.lineCap = LineCap.Round;
        foreach (var rope in _ropes)
        {
            if (rope.From?.Element == null || rope.To?.Element == null) continue;
            Vector2 a = CardPinLocal(rope.From.Element);
            Vector2 b = CardPinLocal(rope.To.Element);
            float dist = Vector2.Distance(a, b);
            float sag = Mathf.Clamp(dist * 0.22f, 20f, 100f);
            Vector2 cp = new Vector2((a.x + b.x) * 0.5f, Mathf.Max(a.y, b.y) + sag);
            p.BeginPath(); p.MoveTo(a); p.QuadraticCurveTo(cp, b); p.Stroke();
        }
    }

    private Vector2 CardPinLocal(VisualElement card)
    {
        var wb = card.worldBound;
        return _ropeLayer.WorldToLocal(new Vector2(wb.xMin + wb.width * 0.5f, wb.yMin + 14f));
    }

    // ── Inventory tray ────────────────────────────────────────────────────────

    private void PopulateTray()
    {
        var scroll = _root.Q<ScrollView>("tray-scroll");
        if (scroll == null) return;
        scroll.Clear();
    }

    private void ToggleTray() { if (_isTrayOpen) CloseTray(); else OpenTray(); }

    private void OpenTray()
    {
        _isTrayOpen = true;
        _inventoryTray?.RemoveFromClassList("inventory-tray--closed");
        _inventoryTray?.AddToClassList("inventory-tray--open");
        _inventoryBtn?.AddToClassList("cb-icon-btn--active");
    }

    private void CloseTray()
    {
        _isTrayOpen = false;
        _inventoryTray?.RemoveFromClassList("inventory-tray--open");
        _inventoryTray?.AddToClassList("inventory-tray--closed");
        _inventoryBtn?.RemoveFromClassList("cb-icon-btn--active");
    }

    // ── Card spawning ─────────────────────────────────────────────────────────

    private void AddNote()
    {
        float cx = _cardsLayer.layout.width > 10 ? _cardsLayer.layout.width * 0.5f : 220f;
        float cy = _cardsLayer.layout.height > 10 ? _cardsLayer.layout.height * 0.4f : 160f;
        SpawnCard(new BoardCard
        {
            Id = "card_" + _cardCounter++,
            Title = "Note",
            Body = "...",
            Rotation = UnityEngine.Random.Range(-8f, 8f)
        },
            new Vector2(cx + UnityEngine.Random.Range(-60, 60), cy + UnityEngine.Random.Range(-40, 40)));
    }

    private void SpawnCard(BoardCard card, Vector2 position)
    {
        var el = new VisualElement();
        el.name = card.Id;
        el.style.position = Position.Absolute;
        el.style.left = position.x;
        el.style.top = position.y;
        el.style.width = 180f;
        el.style.height = 200f;

        var noteTex = LoadAsset("note.png");
        if (noteTex != null)
            el.style.backgroundImage = new StyleBackground(noteTex);
        else
            el.style.backgroundColor = new StyleColor(new Color(0.78f, 0.72f, 0.54f));

        el.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
        SetCardRotation(el, card.Rotation);

        var textArea = new VisualElement();
        textArea.style.position = Position.Absolute;
        textArea.style.left = 16f;
        textArea.style.right = 16f;
        textArea.style.top = 44f;
        textArea.style.bottom = 12f;
        textArea.style.flexDirection = FlexDirection.Column;
        textArea.style.overflow = Overflow.Hidden;

        var titleLbl = new Label(card.Title);
        titleLbl.style.fontSize = 14f;
        titleLbl.style.color = new StyleColor(new Color(0.15f, 0.10f, 0.05f));
        titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLbl.style.whiteSpace = WhiteSpace.Normal;
        titleLbl.style.marginBottom = 4f;

        var bodyLbl = new Label(card.Body);
        bodyLbl.name = "body_" + card.Id;
        bodyLbl.style.fontSize = 13f;
        bodyLbl.style.color = new StyleColor(new Color(0.20f, 0.14f, 0.06f));
        bodyLbl.style.whiteSpace = WhiteSpace.Normal;
        bodyLbl.style.flexGrow = 1f;

        textArea.Add(titleLbl);
        textArea.Add(bodyLbl);
        el.Add(textArea);

        card.Element = el;
        _cards.Add(card);
        _cardsLayer.Add(el);

        el.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
        el.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
        el.RegisterCallback<PointerUpEvent>(OnCardPointerUp);

        if (!_isLoading) SaveBoard();
    }

    private static void SetCardRotation(VisualElement el, float degrees)
    {
        el.style.rotate = new StyleRotate(new Rotate(degrees));
        el.style.transformOrigin = new StyleTransformOrigin(
            new TransformOrigin(Length.Percent(50), new Length(10f, LengthUnit.Pixel)));
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private void SelectCard(BoardCard card)
    {
        if (_selectedCard != null && _selectedCard != card)
            _selectedCard.Element.style.opacity = 1f;
        _selectedCard = card;
        card.Element.style.opacity = 0.85f;
        card.Element.BringToFront();
        if (_cardActions != null) _cardActions.style.display = DisplayStyle.Flex;
        PositionActionBar(card);
    }

    private void DeselectAll()
    {
        if (_selectedCard != null) _selectedCard.Element.style.opacity = 1f;
        _selectedCard = null;
        if (_cardActions != null) _cardActions.style.display = DisplayStyle.None;
        if (_isRopeMode)
        {
            _isRopeMode = false;
            _ropeStartCard = null;
            _actionRopeBtn?.RemoveFromClassList("cb-active");
        }
    }

    private void PositionActionBar(BoardCard card)
    {
        if (_cardActions == null) return;
        float left = card.Element.resolvedStyle.left;
        float top = card.Element.resolvedStyle.top;
        float width = card.Element.resolvedStyle.width;
        _cardActions.style.left = Mathf.Max(4f, left + width * 0.5f - 90f);
        _cardActions.style.top = Mathf.Max(4f, top - 44f);
    }

    private void OnBoardPointerDown(PointerDownEvent evt)
    {
        if (evt.target == _cardsLayer) DeselectAll();
    }

    // ── Action bar ────────────────────────────────────────────────────────────

    private void ActionDelete()
    {
        if (_selectedCard == null) return;
        var card = _selectedCard;
        DeselectAll();
        RemoveCard(card);
    }

    private void ActionEdit()
    {
        if (_selectedCard == null) return;
        var card = _selectedCard;
        var el = card.Element;
        var bodyLbl = el.Q<Label>("body_" + card.Id);
        if (bodyLbl == null || el.Q<TextField>("edit_" + card.Id) != null) return;

        var field = new TextField { value = card.Body, multiline = true };
        field.name = "edit_" + card.Id;
        field.style.position = Position.Absolute;
        field.style.left = 16f;
        field.style.right = 16f;
        field.style.top = 44f;
        field.style.bottom = 12f;
        field.style.fontSize = 13f;
        field.AddToClassList("card-edit-field");

        var titleLbl = el.Q<Label>(null);
        if (titleLbl != null) titleLbl.style.display = DisplayStyle.None;
        bodyLbl.style.display = DisplayStyle.None;
        el.Add(field);
        field.Focus();

        field.RegisterCallback<FocusOutEvent>(_ =>
        {
            card.Body = field.value;
            bodyLbl.text = field.value;
            bodyLbl.style.display = DisplayStyle.Flex;
            if (titleLbl != null) titleLbl.style.display = DisplayStyle.Flex;
            if (el.Contains(field)) el.Remove(field);
            SaveBoard();
        });
    }

    private void ActionStartRope()
    {
        if (_selectedCard == null) return;
        if (_isRopeMode && _ropeStartCard == _selectedCard)
        {
            _isRopeMode = false;
            _ropeStartCard = null;
            _actionRopeBtn?.RemoveFromClassList("cb-active");
        }
        else
        {
            _isRopeMode = true;
            _ropeStartCard = _selectedCard;
            _actionRopeBtn?.AddToClassList("cb-active");
            Debug.Log($"[ClueBoardManager] Rope mode ON from '{_ropeStartCard.Title}' — tap another card.");
        }
    }

    private void ActionRotate(float delta)
    {
        if (_selectedCard == null) return;
        _selectedCard.Rotation += delta;
        SetCardRotation(_selectedCard.Element, _selectedCard.Rotation);
        _ropeDirty = true;
    }

    private void RemoveCard(BoardCard card)
    {
        _ropes.RemoveAll(r => r.From == card || r.To == card);
        if (_cardsLayer.Contains(card.Element)) _cardsLayer.Remove(card.Element);
        _cards.Remove(card);
        _ropeDirty = true;
        SaveBoard();
    }

    private void TryConnectRope(BoardCard target)
    {
        if (_ropeStartCard == null || _ropeStartCard == target) return;
        bool exists = _ropes.Any(r =>
            (r.From == _ropeStartCard && r.To == target) ||
            (r.From == target && r.To == _ropeStartCard));
        if (!exists)
        {
            _ropes.Add(new RopeConnection { From = _ropeStartCard, To = target });
            _ropeDirty = true;
        }
        _isRopeMode = false;
        _ropeStartCard = null;
        _actionRopeBtn?.RemoveFromClassList("cb-active");
    }

    // ── Drag ──────────────────────────────────────────────────────────────────

    private void OnCardPointerDown(PointerDownEvent evt)
    {
        if (evt.target is Button) return;
        var el = evt.currentTarget as VisualElement;
        if (el == null) return;
        _dragTarget = el;
        _didDrag = false;
        var local = _cardsLayer.WorldToLocal(evt.position);
        _dragOffset = new Vector2(local.x - el.resolvedStyle.left, local.y - el.resolvedStyle.top);
        el.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnCardPointerMove(PointerMoveEvent evt)
    {
        if (_dragTarget == null || !_dragTarget.HasPointerCapture(evt.pointerId)) return;
        var pos = _cardsLayer.WorldToLocal(evt.position);

        float cardW = _dragTarget.resolvedStyle.width;
        float cardH = _dragTarget.resolvedStyle.height;
        float layerW = _cardsLayer.resolvedStyle.width;
        float layerH = _cardsLayer.resolvedStyle.height;

        // Clamp so at least half the card stays visible inside the board
        float minX = -cardW * 0.5f;
        float maxX = layerW > 0 ? layerW - cardW * 0.5f : float.MaxValue;
        float minY = -cardH * 0.5f;
        float maxY = layerH > 0 ? layerH - cardH * 0.5f : float.MaxValue;

        float newX = Mathf.Clamp(pos.x - _dragOffset.x, minX, maxX);
        float newY = Mathf.Clamp(pos.y - _dragOffset.y, minY, maxY);

        _dragTarget.style.left = newX;
        _dragTarget.style.top = newY;
        _didDrag = true;
        _ropeDirty = true;
        if (_selectedCard?.Element == _dragTarget) PositionActionBar(_selectedCard);
        evt.StopPropagation();
    }

    private void OnCardPointerUp(PointerUpEvent evt)
    {
        if (_dragTarget == null) return;
        var card = _cards.FirstOrDefault(c => c.Element == _dragTarget);
        bool wasDrag = _didDrag;
        _dragTarget.ReleasePointer(evt.pointerId);
        _dragTarget = null;
        _didDrag = false;
        if (wasDrag) SaveBoard();
        if (wasDrag || card == null) return;
        if (_isRopeMode)
        {
            if (card == _ropeStartCard)
            {
                _isRopeMode = false;
                _ropeStartCard = null;
                _actionRopeBtn?.RemoveFromClassList("cb-active");
            }
            else TryConnectRope(card);
        }
        else SelectCard(card);
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    private void SaveBoard()
    {
        if (_isLoading) return;
        int profileId = GameManager.Instance?.ActiveProfileId ?? -1;
        if (profileId < 0) return;

        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < _cards.Count; i++)
        {
            var c = _cards[i];
            // Use style.left/top (the values we set) not resolvedStyle
            // which may not be calculated yet if layout hasn't run
            float x = c.Element.style.left.value.value;
            float y = c.Element.style.top.value.value;
            string t = c.Title.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string b = c.Body.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string xi = x.ToString(CultureInfo.InvariantCulture);
            string yi = y.ToString(CultureInfo.InvariantCulture);
            string ri = c.Rotation.ToString(CultureInfo.InvariantCulture);
            sb.Append("{");
            sb.Append("\"id\":\"").Append(c.Id).Append("\",");
            sb.Append("\"title\":\"").Append(t).Append("\",");
            sb.Append("\"body\":\"").Append(b).Append("\",");
            sb.Append("\"x\":").Append(xi).Append(",");
            sb.Append("\"y\":").Append(yi).Append(",");
            sb.Append("\"rot\":").Append(ri);
            sb.Append("}");
            if (i < _cards.Count - 1) sb.Append(",");
        }
        sb.Append("]");

        // Append ropes as a separate array at the end
        // Format: main JSON object wrapping cards and ropes arrays
        string cardsJson = sb.ToString();

        var ropesSb = new StringBuilder();
        ropesSb.Append("[");
        bool firstRope = true;
        foreach (var r in _ropes)
        {
            if (r.From == null || r.To == null) continue;
            if (!firstRope) ropesSb.Append(",");
            firstRope = false;
            ropesSb.Append("{");
            ropesSb.Append("\"from\":\"").Append(r.From.Id).Append("\",");
            ropesSb.Append("\"to\":\"").Append(r.To.Id).Append("\"");
            ropesSb.Append("}");
        }
        ropesSb.Append("]");

        string finalJson = "{\"cards\":" + cardsJson + ",\"ropes\":" + ropesSb.ToString() + "}";
        DatabaseManager.Instance?.SaveBoardCards(profileId, finalJson);
    }

    private void LoadBoard()
    {
        int profileId = GameManager.Instance?.ActiveProfileId ?? -1;
        if (profileId < 0) return;

        string json = DatabaseManager.Instance?.LoadBoardCards(profileId);
        if (string.IsNullOrEmpty(json)) return;

        json = json.Trim();

        // New format: {"cards":[...],"ropes":[...]}
        // Legacy format: [...] (cards only array)
        string cardsJson = json;
        string ropesJson = null;

        if (json.StartsWith("{"))
        {
            // Extract cards array
            int cardsIdx = json.IndexOf("\"cards\":", StringComparison.Ordinal);
            int ropesIdx = json.IndexOf("\"ropes\":", StringComparison.Ordinal);
            if (cardsIdx >= 0)
            {
                int start = json.IndexOf('[', cardsIdx);
                int end = FindMatchingBracket(json, start);
                if (start >= 0 && end > start)
                    cardsJson = json.Substring(start, end - start + 1);
            }
            if (ropesIdx >= 0)
            {
                int start = json.IndexOf('[', ropesIdx);
                int end = FindMatchingBracket(json, start);
                if (start >= 0 && end > start)
                    ropesJson = json.Substring(start, end - start + 1);
            }
        }

        if (!cardsJson.StartsWith("[") || !cardsJson.EndsWith("]")) return;
        string inner = cardsJson.Substring(1, cardsJson.Length - 2);

        _isLoading = true;
        try
        {
            var cardJsons = SplitJsonObjects(inner);
            foreach (var cardJson in cardJsons)
            {
                string id = JsonGetString(cardJson, "id");
                string t = JsonGetString(cardJson, "title");
                string b = JsonGetString(cardJson, "body");
                float x = JsonGetFloat(cardJson, "x");
                float y = JsonGetFloat(cardJson, "y");
                float rot = JsonGetFloat(cardJson, "rot");
                if (string.IsNullOrEmpty(id)) continue;
                SpawnCard(new BoardCard { Id = id, Title = t, Body = b, Rotation = rot },
                    new Vector2(x, y));
            }
            _cardCounter = _cards.Count + 1;
            Debug.Log($"[ClueBoardManager] Loaded {_cards.Count} cards.");
        }
        finally
        {
            _isLoading = false;
        }

        // Restore ropes after layout is done so worldBound is valid
        // GeometryChangedEvent fires once layout has been calculated
        if (!string.IsNullOrEmpty(ropesJson) && ropesJson.StartsWith("["))
        {
            string ropesJsonCopy = ropesJson;
            void RestoreRopes(GeometryChangedEvent _)
            {
                _cardsLayer.UnregisterCallback<GeometryChangedEvent>(RestoreRopes);
                string ropesInner = ropesJsonCopy.Substring(1, ropesJsonCopy.Length - 2);
                var ropeJsons = SplitJsonObjects(ropesInner);
                foreach (var ropeJson in ropeJsons)
                {
                    string fromId = JsonGetString(ropeJson, "from");
                    string toId = JsonGetString(ropeJson, "to");
                    var from = _cards.FirstOrDefault(c => c.Id == fromId);
                    var to = _cards.FirstOrDefault(c => c.Id == toId);
                    if (from != null && to != null)
                        _ropes.Add(new RopeConnection { From = from, To = to });
                }
                _ropeDirty = true;
                Debug.Log($"[ClueBoardManager] Restored {_ropes.Count} ropes after layout.");
            }
            _cardsLayer.RegisterCallback<GeometryChangedEvent>(RestoreRopes);
        }
    }

    /// <summary>Finds the index of the closing bracket matching the opening bracket at startIdx.</summary>
    private static int FindMatchingBracket(string s, int startIdx)
    {
        if (startIdx < 0 || startIdx >= s.Length) return -1;
        char open = s[startIdx];
        char close = open == '[' ? ']' : '}';
        int depth = 0;
        for (int i = startIdx; i < s.Length; i++)
        {
            if (s[i] == open) depth++;
            else if (s[i] == close) { if (--depth == 0) return i; }
        }
        return -1;
    }

    // ── Minimal JSON helpers ──────────────────────────────────────────────────

    private static List<string> SplitJsonObjects(string json)
    {
        var result = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '{') { if (depth++ == 0) start = i; }
            else if (json[i] == '}') { if (--depth == 0) result.Add(json.Substring(start, i - start + 1)); }
        }
        return result;
    }

    private static string JsonGetString(string json, string key)
    {
        // Find "key":"value"
        string search = "\"" + key + "\":\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return "";
        idx += search.Length;
        var sb = new StringBuilder();
        while (idx < json.Length)
        {
            char c = json[idx];
            if (c == '\\' && idx + 1 < json.Length)
            {
                char next = json[idx + 1];
                if (next == '"') { sb.Append('"'); idx += 2; continue; }
                if (next == '\\') { sb.Append('\\'); idx += 2; continue; }
            }
            if (c == '"') break;
            sb.Append(c);
            idx++;
        }
        return sb.ToString();
    }

    private static float JsonGetFloat(string json, string key)
    {
        string search = "\"" + key + "\":";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return 0f;
        idx += search.Length;
        // Skip leading whitespace
        while (idx < json.Length && json[idx] == ' ') idx++;
        int end = idx;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == 'E' || json[end] == 'e' || json[end] == '+'))
            end++;
        if (end == idx) return 0f;
        return float.TryParse(json.Substring(idx, end - idx), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    // ── Asset helpers ─────────────────────────────────────────────────────────

    private static Texture2D LoadAsset(string filename)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(A + filename);
#else
        return Resources.Load<Texture2D>("ClueBoardAssets/" + System.IO.Path.GetFileNameWithoutExtension(filename));
#endif
    }

    private static void ApplyTexture(VisualElement el, string filename)
    {
        if (el == null) return;
        var tex = LoadAsset(filename);
        if (tex != null) el.style.backgroundImage = new StyleBackground(tex);
    }

    private static void ApplyButtonIcon(Button btn, string filename)
    {
        if (btn == null) return;
        var tex = LoadAsset(filename);
        if (tex == null) return;
        btn.text = "";
        btn.style.backgroundImage = new StyleBackground(tex);
        btn.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        btn.style.backgroundColor = new StyleColor(Color.clear);
        btn.style.borderTopWidth = btn.style.borderBottomWidth =
        btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
    }
}