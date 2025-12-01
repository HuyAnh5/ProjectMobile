using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Root UI cho Inventory:
/// - Storage Grid (bầu đèn)
/// - External Grid (3x9)
/// - Burn button
/// Quản lý tương tác click cell.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private PlayerItemSlots playerItemSlots;

    [Header("Storage Grid UI")]
    [SerializeField] private RectTransform storageGridRoot;
    [SerializeField] private GameObject storageCellPrefab;

    [Header("External Grid UI")]
    [SerializeField] private RectTransform externalGridRoot;
    [SerializeField] private GameObject externalCellPrefab;

    [Header("Buttons")]
    [SerializeField] private Button burnButton;
    [SerializeField] private Button closeButton;

    private readonly List<InventoryCellUI> _storageCells = new();
    private readonly List<InventoryCellUI> _externalCells = new();

    // Item đang "cầm trên tay" (giả lập drag & drop)
    private ItemInstance _pickedItem;
    private bool _pickedFromStorage;
    private GridSlot _pickedStorageSlot;
    private int _pickedExternalX = -1;
    private int _pickedExternalY = -1;

    // Các ô Storage được chọn để Burn
    private readonly HashSet<InventoryCellUI> _selectedForBurn = new();

    private void Awake()
    {
        if (inventoryManager == null)
            inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (playerItemSlots == null)
            playerItemSlots = FindAnyObjectByType<PlayerItemSlots>();

        BuildStorageGrid();
        BuildExternalGrid();

        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryOpened += OnInventoryOpened;
            inventoryManager.OnInventoryClosed += OnInventoryClosed;
        }

        if (burnButton)
            burnButton.onClick.AddListener(OnBurnButtonPressed);

        if (closeButton)
            closeButton.onClick.AddListener(OnCloseButtonPressed);

        gameObject.SetActive(false); // start hidden
    }

    private void OnDestroy()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryOpened -= OnInventoryOpened;
            inventoryManager.OnInventoryClosed -= OnInventoryClosed;
        }
    }

    // --------------------------------------------------------------------
    // BUILD GRIDS
    // --------------------------------------------------------------------
    private void BuildStorageGrid()
    {
        if (inventoryManager == null || storageGridRoot == null || storageCellPrefab == null)
            return;

        foreach (Transform child in storageGridRoot)
            Destroy(child.gameObject);
        _storageCells.Clear();

        var slots = inventoryManager.Slots;
        foreach (var slot in slots)
        {
            var go = Instantiate(storageCellPrefab, storageGridRoot);
            var cell = go.GetComponent<InventoryCellUI>();
            cell.BindStorageSlot(slot, inventoryManager, this);
            _storageCells.Add(cell);
        }
    }

    private void BuildExternalGrid()
    {
        if (inventoryManager == null || externalGridRoot == null || externalCellPrefab == null)
            return;

        foreach (Transform child in externalGridRoot)
            Destroy(child.gameObject);
        _externalCells.Clear();

        var external = inventoryManager.ExternalGrid;
        if (external == null) return;

        int width = inventoryManager.ExternalWidth;
        int height = inventoryManager.ExternalHeight;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var go = Instantiate(externalCellPrefab, externalGridRoot);
                var cell = go.GetComponent<InventoryCellUI>();
                cell.BindExternalSlot(x, y, inventoryManager, this);
                _externalCells.Add(cell);
            }
        }
    }

    // --------------------------------------------------------------------
    // OPEN / CLOSE (hook từ InventoryManager events)
    // --------------------------------------------------------------------
    private void OnInventoryOpened(InventoryManager mgr)
    {
        ClearPickedItem();
        ClearBurnSelection();
        RefreshAll();
        gameObject.SetActive(true);
    }

    private void OnInventoryClosed(InventoryManager mgr)
    {
        ClearPickedItem();
        ClearBurnSelection();
        gameObject.SetActive(false);
    }

    // --------------------------------------------------------------------
    // PUBLIC API CHO CELL GỌI NGƯỢC LẠI
    // --------------------------------------------------------------------
    public void OnCellClicked(InventoryCellUI cell)
    {
        if (cell.IsStorageCell)
            OnStorageCellClicked(cell);
        else
            OnExternalCellClicked(cell);
    }

    private void OnStorageCellClicked(InventoryCellUI cell)
    {
        var slot = cell.StorageSlot;
        if (slot == null) return;

        // Nếu đang "cầm" 1 item trên tay => cố gắng đặt vào ô storage này
        if (_pickedItem != null)
        {
            TryPlacePickedItemToStorage(slot);
            return;
        }

        // Nếu ô có item: 
        var inst = slot.item;
        if (inst != null && inst.Data != null)
        {
            // Mode chọn để Burn:
            ToggleBurnSelection(cell);
        }
    }

    private void OnExternalCellClicked(InventoryCellUI cell)
    {
        var grid = inventoryManager.ExternalGrid;
        var inst = grid[cell.ExternalX, cell.ExternalY];

        // Nếu đang cầm item trên tay -> đặt xuống External
        if (_pickedItem != null)
        {
            TryPlacePickedItemToExternal(cell.ExternalX, cell.ExternalY);
            return;
        }

        // Nếu chưa cầm gì: nhấc item từ External lên tay
        if (inst != null && inst.Data != null)
        {
            PickItemFromExternal(cell.ExternalX, cell.ExternalY);
        }
    }

    // --------------------------------------------------------------------
    // PICK & PLACE LOGIC (GIẢ LẬP DRAG & DROP BẰNG 2 CLICK)
    // --------------------------------------------------------------------
    private void PickItemFromStorage(GridSlot slot)
    {
        if (slot == null || slot.item == null) return;

        _pickedItem = slot.item;
        _pickedFromStorage = true;
        _pickedStorageSlot = slot;

        slot.item = null;
        RefreshAll();
    }

    private void TryPlacePickedItemToStorage(GridSlot targetSlot)
    {
        if (_pickedItem == null || targetSlot == null) return;

        if (targetSlot.item != null)
        {
            // tạm thời: không swap, không đặt nếu không trống
            return;
        }

        targetSlot.item = _pickedItem;
        ClearPickedItem();
        RefreshAll();
    }

    private void PickItemFromExternal(int x, int y)
    {
        var grid = inventoryManager.ExternalGrid;
        if (grid == null) return;

        var inst = grid[x, y];
        if (inst == null || inst.Data == null) return;

        _pickedItem = inst;
        _pickedFromStorage = false;
        _pickedStorageSlot = null;
        _pickedExternalX = x;
        _pickedExternalY = y;

        grid[x, y] = null;
        RefreshAll();
    }

    private void TryPlacePickedItemToExternal(int x, int y)
    {
        if (_pickedItem == null) return;

        var grid = inventoryManager.ExternalGrid;
        if (grid == null) return;

        if (grid[x, y] != null)
        {
            // tạm thời: không swap
            return;
        }

        grid[x, y] = _pickedItem;
        ClearPickedItem();
        RefreshAll();
    }

    private void ClearPickedItem()
    {
        _pickedItem = null;
        _pickedFromStorage = false;
        _pickedStorageSlot = null;
        _pickedExternalX = -1;
        _pickedExternalY = -1;
    }

    // --------------------------------------------------------------------
    // BURN SELECTION
    // --------------------------------------------------------------------
    private void ToggleBurnSelection(InventoryCellUI cell)
    {
        if (_pickedItem == null && cell.StorageSlot != null)
        {
            bool nowSelected = !_selectedForBurn.Contains(cell);

            if (nowSelected)
            {
                _selectedForBurn.Add(cell);
                cell.SetSelected(true);
            }
            else
            {
                _selectedForBurn.Remove(cell);
                cell.SetSelected(false);
            }
        }
    }

    private void ClearBurnSelection()
    {
        foreach (var c in _storageCells)
            c.SetSelected(false);
        _selectedForBurn.Clear();
    }

    private void OnBurnButtonPressed()
    {
        if (inventoryManager == null) return;

        var slotsToBurn = new List<GridSlot>();
        foreach (var cell in _selectedForBurn)
        {
            if (cell.StorageSlot != null && cell.StorageSlot.IsOccupied)
                slotsToBurn.Add(cell.StorageSlot);
        }

        if (slotsToBurn.Count == 0) return;

        inventoryManager.BurnSelectedSlots(slotsToBurn);
        ClearBurnSelection();
        RefreshAll();
        // Lưu ý: hiệu ứng Burn sẽ nổ sau khi CloseInventory + ExecuteBurnQueue()
    }

    private void OnCloseButtonPressed()
    {
        if (inventoryManager != null)
        {
            inventoryManager.CloseInventory();
        }

        // Ở script khác, sau khi Unpause, nhớ check và chạy:
        // if (playerItemSlots.HasPendingBurnEffects)
        //     StartCoroutine(playerItemSlots.ExecuteBurnQueue());
    }

    // --------------------------------------------------------------------
    // REFRESH UI
    // --------------------------------------------------------------------
    public void RefreshAll()
    {
        foreach (var c in _storageCells)
            c.Refresh();

        foreach (var c in _externalCells)
            c.Refresh();
    }
}
