// InventoryManager.cs
// Tracks items the player has collected during the investigation.
// Items can be viewed in the inventory panel and pinned to the Clue Board.
//
// SETUP:
//   Attach to a persistent GameObject in the game scene.
//   Assign the UIDocument containing GameUI.uxml.
//   Call via InventoryManager.Instance.AddItem(...) from DialogueManager.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    // ── Data ──────────────────────────────────────────────────────────────────

    public class InventoryItem
    {
        public string Name;
        public string Description;
        public string ItemType;   // "log" | "clue" | "document"
    }

    private readonly List<InventoryItem> _items = new();

    // ── UI references ─────────────────────────────────────────────────────────

    private VisualElement _inventoryPanel;
    private ScrollView    _inventoryList;
    private Button        _closeInventoryBtn;
    private Button        _addToBoardBtn;
    private Button        _inventoryHudBtn;   // the HUD icon that opens the panel

    private InventoryItem _selectedItem = null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;

        _inventoryPanel    = root.Q("inventory-panel");
        _inventoryList     = root.Q<ScrollView>("inventory-list");
        _closeInventoryBtn = root.Q<Button>("close-inventory-btn");
        _addToBoardBtn     = root.Q<Button>("add-to-board-btn");
        _inventoryHudBtn   = root.Q<Button>("inventory-hud-btn");

        if (_closeInventoryBtn != null) _closeInventoryBtn.clicked += CloseInventory;
        if (_addToBoardBtn     != null) _addToBoardBtn.clicked     += AddSelectedToBoard;
        if (_inventoryHudBtn   != null) _inventoryHudBtn.clicked   += ToggleInventory;

        // Start hidden
        if (_inventoryPanel != null)
            _inventoryPanel.style.display = DisplayStyle.None;
    }

    void OnDisable()
    {
        if (_closeInventoryBtn != null) _closeInventoryBtn.clicked -= CloseInventory;
        if (_addToBoardBtn     != null) _addToBoardBtn.clicked     -= AddSelectedToBoard;
        if (_inventoryHudBtn   != null) _inventoryHudBtn.clicked   -= ToggleInventory;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void AddItem(string name, string description, string itemType = "document")
    {
        // Prevent duplicates
        if (_items.Exists(i => i.Name == name)) return;

        var item = new InventoryItem { Name = name, Description = description, ItemType = itemType };
        _items.Add(item);
        RefreshList();

        Debug.Log($"[Inventory] Added: {name}");
    }

    public bool HasItem(string name) => _items.Exists(i => i.Name == name);

    public List<InventoryItem> GetItems() => new List<InventoryItem>(_items);

    // ── Panel visibility ──────────────────────────────────────────────────────

    public void ToggleInventory()
    {
        if (_inventoryPanel == null) return;
        bool isOpen = _inventoryPanel.style.display == DisplayStyle.Flex;
        _inventoryPanel.style.display = isOpen ? DisplayStyle.None : DisplayStyle.Flex;
        if (!isOpen) RefreshList();
    }

    public void CloseInventory()
    {
        if (_inventoryPanel != null)
            _inventoryPanel.style.display = DisplayStyle.None;
    }

    // ── List rendering ────────────────────────────────────────────────────────

    private void RefreshList()
    {
        if (_inventoryList == null) return;

        _inventoryList.Clear();
        _selectedItem = null;
        UpdateAddToBoardButton();

        foreach (var item in _items)
        {
            var row = BuildItemRow(item);
            _inventoryList.Add(row);
        }
    }

    private VisualElement BuildItemRow(InventoryItem item)
    {
        var row = new VisualElement();
        row.AddToClassList("inventory-row");

        var icon = new Label(ItemTypeIcon(item.ItemType));
        icon.AddToClassList("inventory-icon");

        var name = new Label(item.Name);
        name.AddToClassList("inventory-item-name");

        row.Add(icon);
        row.Add(name);

        row.RegisterCallback<ClickEvent>(_ => SelectItem(item, row));
        return row;
    }

    private void SelectItem(InventoryItem item, VisualElement row)
    {
        // Clear previous selection
        _inventoryList.Query(className: "inventory-row--selected").ForEach(el =>
            el.RemoveFromClassList("inventory-row--selected"));

        _selectedItem = item;
        row.AddToClassList("inventory-row--selected");
        UpdateAddToBoardButton();
    }

    private void UpdateAddToBoardButton()
    {
        if (_addToBoardBtn != null)
            _addToBoardBtn.SetEnabled(_selectedItem != null);
    }

    // ── Board integration ─────────────────────────────────────────────────────

    private void AddSelectedToBoard()
    {
        if (_selectedItem == null) return;

        ClueBoardManager.Instance?.AddClueFromInventory(
            _selectedItem.Name,
            _selectedItem.Description);

        CloseInventory();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ItemTypeIcon(string itemType) => itemType switch
    {
        "log"      => "[L]",
        "clue"     => "[C]",
        "document" => "[D]",
        _          => "[?]",
    };
}
