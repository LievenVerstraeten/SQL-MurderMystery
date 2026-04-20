using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Clue Board component controller.
///
/// Features:
///   - Open / close the board (call SetVisible or use the X button)
///   - Inventory tray slides in from the right (▷ button)
///   - Add blank sticky notes (✎ button)
///   - Tap a card to select it → floating action bar with: delete, edit, rope, rotate L/R
///   - Drag cards freely; they rotate around their pin pivot
///   - Red rope connections drawn as gravity-sagging quadratic bezier curves
///   - Ropes connect from pin-to-pin
///   - Call ClueBoardManager.Instance.SetVisible(true) from the HUD open button
/// </summary>
public class ClueBoardManager : MonoBehaviour
{
    public static ClueBoardManager Instance { get; private set; }

    // ── Inner Types ──────────────────────────────────────────────────────────

    private class BoardCard
    {
        public string Id;
        public string Title;
        public string Body;
        public VisualElement Element;
        public float Rotation;   // degrees, pivots around the top-centre pin
    }

    private class RopeConnection
    {
        public BoardCard From;
        public BoardCard To;
    }

    // ── Placeholder Inventory Data ────────────────────────────────────────────

    private static readonly (string title, string body)[] PlaceholderItems =
    {
        ("Bloody Knife",   "Found near the well\nat 8:00 PM"),
        ("Torn Letter",    "Fragment mentioning\na secret meeting"),
        ("Boot Print",     "Size 10, near\nthe east wall"),
        ("Candle Wax",     "Dripped on floor,\nstill fresh"),
        ("Victim: ?????",  "Body found at dawn.\nIdentity unknown"),
        ("Witness: ?????", "Saw a figure near\nthe gate at night"),
    };

    // ── Cached UI References ─────────────────────────────────────────────────

    private VisualElement _root;
    private VisualElement _cardsLayer;
    private VisualElement _ropeLayer;
    private VisualElement _cardActions;
    private VisualElement _inventoryTray;
    private Button        _inventoryBtn;
    private Button        _actionRopeBtn;

    // ── Runtime State ────────────────────────────────────────────────────────

    private readonly List<BoardCard>      _cards = new();
    private readonly List<RopeConnection> _ropes = new();

    private BoardCard     _selectedCard  = null;
    private BoardCard     _ropeStartCard = null;
    private bool          _isRopeMode    = false;
    private bool          _isTrayOpen    = false;

    // Drag state
    private VisualElement _dragTarget  = null;
    private Vector2       _dragOffset;
    private bool          _didDrag     = false;

    private int _cardCounter = 0;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;

        _cardsLayer    = _root.Q("cards-layer");
        _ropeLayer     = _root.Q("rope-layer");
        _cardActions   = _root.Q("card-actions");
        _inventoryTray = _root.Q("inventory-tray");
        _inventoryBtn  = _root.Q<Button>("inventory-btn");
        _actionRopeBtn = _root.Q<Button>("action-rope");

        _ropeLayer.generateVisualContent += DrawRopes;

        _root.Q<Button>("close-btn").clicked       += () => SetVisible(false);
        _root.Q<Button>("add-note-btn").clicked    += AddNote;
        _inventoryBtn.clicked                       += ToggleTray;
        _root.Q<Button>("tray-close").clicked      += CloseTray;

        _root.Q<Button>("action-delete").clicked   += ActionDelete;
        _root.Q<Button>("action-edit").clicked     += ActionEdit;
        _actionRopeBtn.clicked                      += ActionStartRope;
        _root.Q<Button>("action-rotate-l").clicked += () => ActionRotate(-15f);
        _root.Q<Button>("action-rotate-r").clicked += () => ActionRotate(+15f);

        // Tapping the empty board deselects
        _cardsLayer.RegisterCallback<PointerDownEvent>(OnBoardPointerDown);

        PopulateTray();
    }

    private void OnDisable()
    {
        if (_ropeLayer != null)
            _ropeLayer.generateVisualContent -= DrawRopes;
    }

    // ── Board Visibility ─────────────────────────────────────────────────────

    /// <summary>Call from the HUD circle-icon to show the board.</summary>
    public void SetVisible(bool visible)
    {
        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) CloseTray();
    }

    // ── Rope Drawing ─────────────────────────────────────────────────────────

    private void DrawRopes(MeshGenerationContext ctx)
    {
        if (_ropes.Count == 0) return;

        var p = ctx.painter2D;
        p.strokeColor = new Color(0.72f, 0.08f, 0.08f);
        p.lineWidth   = 3.5f;
        p.lineCap     = LineCap.Round;

        foreach (var rope in _ropes)
        {
            Vector2 a = CardPinLocal(rope.From.Element);
            Vector2 b = CardPinLocal(rope.To.Element);

            // Gravity sag: control point below the midpoint, proportional to distance
            float   dist = Vector2.Distance(a, b);
            float   sag  = Mathf.Clamp(dist * 0.22f, 24f, 110f);
            Vector2 cp   = new Vector2((a.x + b.x) * 0.5f, Mathf.Max(a.y, b.y) + sag);

            p.BeginPath();
            p.MoveTo(a);
            p.QuadraticCurveTo(cp, b);
            p.Stroke();
        }
    }

    /// Returns the top-centre pin position of <paramref name="card"/> in rope-layer space.
    private Vector2 CardPinLocal(VisualElement card)
    {
        var wb = card.worldBound;
        return _ropeLayer.WorldToLocal(new Vector2(wb.xMin + wb.width * 0.5f, wb.yMin + 10f));
    }

    // ── Inventory Tray ───────────────────────────────────────────────────────

    private void PopulateTray()
    {
        var scroll = _root.Q<ScrollView>("tray-scroll");
        scroll.Clear();
        foreach (var (title, body) in PlaceholderItems)
            scroll.Add(BuildTrayItem(title, body));
    }

    private VisualElement BuildTrayItem(string title, string body)
    {
        var item = new VisualElement();
        item.AddToClassList("tray-item");

        var t = new Label(title); t.AddToClassList("tray-item-title");
        var b = new Label(body);  b.AddToClassList("tray-item-body");
        item.Add(t);
        item.Add(b);

        item.RegisterCallback<ClickEvent>(_ => SpawnFromInventory(title, body));
        return item;
    }

    private void SpawnFromInventory(string title, string body)
    {
        float cx = _cardsLayer.layout.width  > 10 ? _cardsLayer.layout.width  * 0.38f : 200f;
        float cy = _cardsLayer.layout.height > 10 ? _cardsLayer.layout.height * 0.35f : 140f;

        SpawnCard(new BoardCard
        {
            Id       = "card_" + _cardCounter++,
            Title    = title,
            Body     = body,
            Rotation = Random.Range(-12f, 12f),
        },
        new Vector2(cx + Random.Range(-80, 80), cy + Random.Range(-60, 60)));
    }

    private void ToggleTray()
    {
        if (_isTrayOpen) CloseTray(); else OpenTray();
    }

    private void OpenTray()
    {
        _isTrayOpen = true;
        _inventoryTray.RemoveFromClassList("inventory-tray--closed");
        _inventoryTray.AddToClassList("inventory-tray--open");
        _inventoryBtn.AddToClassList("cb-icon-btn--active");
    }

    private void CloseTray()
    {
        _isTrayOpen = false;
        _inventoryTray.RemoveFromClassList("inventory-tray--open");
        _inventoryTray.AddToClassList("inventory-tray--closed");
        _inventoryBtn.RemoveFromClassList("cb-icon-btn--active");
    }

    // ── Add Note ─────────────────────────────────────────────────────────────

    private void AddNote()
    {
        float cx = _cardsLayer.layout.width  > 10 ? _cardsLayer.layout.width  * 0.45f : 240f;
        float cy = _cardsLayer.layout.height > 10 ? _cardsLayer.layout.height * 0.40f : 160f;

        SpawnCard(new BoardCard
        {
            Id       = "note_" + _cardCounter++,
            Title    = "Note",
            Body     = "...",
            Rotation = Random.Range(-8f, 8f),
        },
        new Vector2(cx + Random.Range(-50, 50), cy + Random.Range(-50, 50)));
    }

    // ── Card Spawning ────────────────────────────────────────────────────────

    private void SpawnCard(BoardCard card, Vector2 position)
    {
        var el = new VisualElement();
        el.AddToClassList("board-card");
        el.style.left = position.x;
        el.style.top  = position.y;
        SetCardRotation(el, card.Rotation);

        // Decorative pin at top-centre
        var pin = new VisualElement();
        pin.AddToClassList("card-pin");
        el.Add(pin);

        var titleLbl = new Label(card.Title);
        titleLbl.AddToClassList("card-title");

        var bodyLbl = new Label(card.Body);
        bodyLbl.AddToClassList("card-body");
        bodyLbl.name = "body_" + card.Id;

        el.Add(titleLbl);
        el.Add(bodyLbl);

        card.Element = el;
        _cards.Add(card);
        _cardsLayer.Add(el);

        el.RegisterCallback<PointerDownEvent>(OnCardPointerDown);
        el.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
        el.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
    }

    private static void SetCardRotation(VisualElement el, float degrees)
    {
        el.style.rotate = new StyleRotate(new Rotate(degrees));
        // Pivot around the pin at top-centre
        el.style.transformOrigin = new StyleTransformOrigin(
            new TransformOrigin(Length.Percent(50), new Length(8f, LengthUnit.Pixel)));
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private void SelectCard(BoardCard card)
    {
        if (_selectedCard != null && _selectedCard != card)
            _selectedCard.Element.RemoveFromClassList("board-card--selected");

        _selectedCard = card;
        card.Element.AddToClassList("board-card--selected");
        card.Element.BringToFront();
        _cardActions.style.display = DisplayStyle.Flex;
        PositionActionBar(card);
    }

    private void DeselectAll()
    {
        if (_selectedCard != null)
            _selectedCard.Element.RemoveFromClassList("board-card--selected");

        _selectedCard = null;
        _cardActions.style.display = DisplayStyle.None;

        if (_isRopeMode)
        {
            _isRopeMode    = false;
            _ropeStartCard = null;
            _actionRopeBtn.RemoveFromClassList("card-action-btn--active");
        }
    }

    private void PositionActionBar(BoardCard card)
    {
        float left  = card.Element.resolvedStyle.left;
        float top   = card.Element.resolvedStyle.top;
        float width = card.Element.resolvedStyle.width;

        const float barWidth = 172f;
        _cardActions.style.left = Mathf.Max(4f, left + width * 0.5f - barWidth * 0.5f);
        _cardActions.style.top  = Mathf.Max(4f, top - 38f);
    }

    private void OnBoardPointerDown(PointerDownEvent evt)
    {
        // Only deselect when the bare board is tapped (cards stop propagation)
        if (evt.target == _cardsLayer)
            DeselectAll();
    }

    // ── Action Bar ───────────────────────────────────────────────────────────

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
        var el   = card.Element;

        var bodyLbl = el.Q<Label>("body_" + card.Id);
        // Guard: don't open a second field if one is already open
        if (bodyLbl == null || el.Q<TextField>("edit_" + card.Id) != null) return;

        var field = new TextField { value = card.Body, multiline = true };
        field.AddToClassList("card-edit-field");
        field.name = "edit_" + card.Id;

        bodyLbl.style.display = DisplayStyle.None;
        el.Add(field);
        field.Focus();

        field.RegisterCallback<FocusOutEvent>(_ =>
        {
            card.Body             = field.value;
            bodyLbl.text          = field.value;
            bodyLbl.style.display = DisplayStyle.Flex;
            if (el.Contains(field)) el.Remove(field);
        });
    }

    private void ActionStartRope()
    {
        if (_selectedCard == null) return;
        _isRopeMode    = true;
        _ropeStartCard = _selectedCard;
        _actionRopeBtn.AddToClassList("card-action-btn--active");
    }

    private void ActionRotate(float delta)
    {
        if (_selectedCard == null) return;
        _selectedCard.Rotation += delta;
        SetCardRotation(_selectedCard.Element, _selectedCard.Rotation);
        _ropeLayer.MarkDirtyRepaint();
    }

    private void RemoveCard(BoardCard card)
    {
        _ropes.RemoveAll(r => r.From == card || r.To == card);
        _cardsLayer.Remove(card.Element);
        _cards.Remove(card);
        _ropeLayer.MarkDirtyRepaint();
    }

    private void TryConnectRope(BoardCard target)
    {
        if (_ropeStartCard == null || _ropeStartCard == target) return;

        bool exists = _ropes.Any(r =>
            (r.From == _ropeStartCard && r.To == target) ||
            (r.From == target         && r.To == _ropeStartCard));

        if (!exists)
        {
            _ropes.Add(new RopeConnection { From = _ropeStartCard, To = target });
            _ropeLayer.MarkDirtyRepaint();
        }

        _isRopeMode    = false;
        _ropeStartCard = null;
        _actionRopeBtn.RemoveFromClassList("card-action-btn--active");
    }

    // ── Drag ─────────────────────────────────────────────────────────────────

    private void OnCardPointerDown(PointerDownEvent evt)
    {
        // Let action-bar button clicks pass through to their handlers
        if (evt.target is Button) return;

        var el = evt.currentTarget as VisualElement;
        if (el == null) return;

        _dragTarget = el;
        _didDrag    = false;

        var local = _cardsLayer.WorldToLocal(evt.position);
        _dragOffset = new Vector2(
            local.x - el.resolvedStyle.left,
            local.y - el.resolvedStyle.top);

        el.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnCardPointerMove(PointerMoveEvent evt)
    {
        if (_dragTarget == null || !_dragTarget.HasPointerCapture(evt.pointerId)) return;

        var pos = _cardsLayer.WorldToLocal(evt.position);
        _dragTarget.style.left = pos.x - _dragOffset.x;
        _dragTarget.style.top  = pos.y - _dragOffset.y;
        _didDrag = true;

        _ropeLayer.MarkDirtyRepaint();

        if (_selectedCard != null && _selectedCard.Element == _dragTarget)
            PositionActionBar(_selectedCard);

        evt.StopPropagation();
    }

    private void OnCardPointerUp(PointerUpEvent evt)
    {
        if (_dragTarget == null) return;

        var  card    = _cards.FirstOrDefault(c => c.Element == _dragTarget);
        bool wasDrag = _didDrag;

        _dragTarget.ReleasePointer(evt.pointerId);
        _dragTarget = null;
        _didDrag    = false;

        if (wasDrag || card == null) return;

        // Treat as a tap
        if (_isRopeMode)
        {
            if (card == _ropeStartCard)
            {
                // Tap same card again → cancel rope mode
                _isRopeMode    = false;
                _ropeStartCard = null;
                _actionRopeBtn.RemoveFromClassList("card-action-btn--active");
            }
            else
            {
                TryConnectRope(card);
            }
        }
        else
        {
            SelectCard(card);
        }
    }
}
