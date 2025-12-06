using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Root UI cho Inventory:
/// - Storage Grid (bầu đèn)
/// - External Grid (3x9)
/// - Burn Zone (kéo thả để đốt)
/// Quản lý tương tác click cell & drag/drop.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private PlayerItemSlots playerItemSlots;

    [Header("Storage Grid UI")]
    [SerializeField] private RectTransform storageGridRoot;
    [SerializeField] private GameObject storageCellPrefab;
    [SerializeField] private Vector2 storageCellSize = new Vector2(64, 64);
    [SerializeField] private Vector2 storageCellSpacing = new Vector2(4, 4);
    [Header("External Grid UI")]
    [SerializeField] private RectTransform externalGridRoot;
    [SerializeField] private GameObject externalCellPrefab;

    [Header("Buttons / Controls")]
    [SerializeField] private Button closeButton;   // chỉ còn nút Close

    private readonly List<InventoryCellUI> _storageCells = new();
    private readonly List<InventoryCellUI> _externalCells = new();

    // Item đang "cầm trên tay" (giả lập drag & drop bằng 2 click)
    private ItemInstance _pickedItem;
    private bool _pickedFromStorage;
    private GridSlot _pickedStorageSlot;
    private int _pickedExternalX = -1;
    private int _pickedExternalY = -1;

    private void Awake()
    {
        if (inventoryManager == null)
            inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (playerItemSlots == null)
            playerItemSlots = FindAnyObjectByType<PlayerItemSlots>();

        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryOpened += OnInventoryOpened;
            inventoryManager.OnInventoryClosed += OnInventoryClosed;
        }

        if (closeButton)
            closeButton.onClick.AddListener(OnCloseButtonPressed);
    }

    private void Start()
    {
        // Lúc này tất cả Awake khác (kể cả InventoryManager) đã chạy xong
        BuildStorageGrid();
        BuildExternalGrid();
        RefreshAll();
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
    // InventoryUI.cs
    private void BuildStorageGrid()
    {
        if (inventoryManager == null || storageGridRoot == null || storageCellPrefab == null)
        {
            Debug.LogError("InventoryUI: Missing refs for StorageGrid.");
            return;
        }

        foreach (Transform child in storageGridRoot)
            Destroy(child.gameObject);
        _storageCells.Clear();

        var slots = inventoryManager.Slots;
        float cellW = storageCellSize.x + storageCellSpacing.x;
        float cellH = storageCellSize.y + storageCellSpacing.y;

        foreach (var slot in slots)
        {
            var go = Instantiate(storageCellPrefab, storageGridRoot);
            var rect = go.GetComponent<RectTransform>();

            // anchor theo top-left của StorageGridRoot
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = storageCellSize;

            // slot.coordinate.x đã gồm startColumn -> ra đúng pattern bầu đèn
            float x = slot.coordinate.x * cellW;
            float y = -slot.coordinate.y * cellH;
            rect.anchoredPosition = new Vector2(x, y);

            var cell = go.GetComponent<InventoryCellUI>();
            cell.BindStorageSlot(slot, inventoryManager, this);
            _storageCells.Add(cell);
        }

        Debug.Log($"InventoryUI: Built {_storageCells.Count} storage cells.");
    }



    private void BuildExternalGrid()
    {
        if (inventoryManager == null || externalGridRoot == null || externalCellPrefab == null)
            return;

        foreach (Transform child in externalGridRoot)
            Destroy(child.gameObject);
        _externalCells.Clear();

        // Đảm bảo external grid logic đã được khởi tạo
        if (inventoryManager.ExternalGrid == null)
        {
            inventoryManager.ClearExternalGrid();
        }

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
        RefreshAll();
        gameObject.SetActive(true);
    }

    private void OnInventoryClosed(InventoryManager mgr)
    {
        ClearPickedItem();
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

    // click vào ô Storage: nếu đang cầm item -> đặt; nếu không -> nhấc item
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

        // Nếu ô có item: nhấc item lên tay
        var inst = slot.item;
        if (inst != null && inst.Data != null)
        {
            PickItemFromStorage(slot);
        }
    }

    // được BurnZoneUI gọi khi thả cell vào vùng Burn
    public void OnCellDroppedIntoBurnZone(InventoryCellUI cell)
    {
        if (cell == null) return;
        if (!cell.IsStorageCell) return;                 // chỉ burn item từ Storage
        if (inventoryManager == null) return;

        var slot = cell.StorageSlot;
        if (slot == null || !slot.IsOccupied) return;

        // Burn 1 slot, cộng dầu + enqueue effect
        inventoryManager.BurnSingleSlot(slot);
        RefreshAll();
    }

    private void OnExternalCellClicked(InventoryCellUI cell)
    {
        var grid = inventoryManager.ExternalGrid;
        if (grid == null) return;

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

        // NEW: xoá item khỏi tất cả ô mà nó chiếm
        if (inventoryManager != null)
            inventoryManager.ClearItemFromStorage(_pickedItem);

        RefreshAll();
    }



    private void TryPlacePickedItemToStorage(GridSlot targetSlot)
    {
        if (_pickedItem == null || targetSlot == null) return;
        if (inventoryManager == null) return;

        bool placed = inventoryManager.TryPlaceItemAt(_pickedItem, targetSlot.coordinate);
        if (!placed)
            return; // không đủ chỗ / nằm ngoài hình bầu -> bỏ qua

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
    // CLOSE
    // --------------------------------------------------------------------
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
