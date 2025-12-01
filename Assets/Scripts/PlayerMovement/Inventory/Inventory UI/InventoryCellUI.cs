using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Một ô UI, có thể là Storage Cell hoặc External Cell.
/// Điều khiển hiển thị icon, text và click event.
/// </summary>
public class InventoryCellUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text idText;
    [SerializeField] private Image selectionHighlight;

    // Loại cell
    public bool IsStorageCell { get; private set; }
    public bool IsExternalCell => !IsStorageCell;

    // Tham chiếu logic
    public GridSlot StorageSlot { get; private set; }   // nếu là storage
    public int ExternalX { get; private set; }          // nếu là external
    public int ExternalY { get; private set; }

    private InventoryManager _inventoryManager;
    private InventoryUI _inventoryUI;

    public bool IsSelected { get; private set; }

    public void BindStorageSlot(GridSlot slot, InventoryManager manager, InventoryUI ui)
    {
        IsStorageCell = true;
        StorageSlot = slot;
        ExternalX = -1;
        ExternalY = -1;

        _inventoryManager = manager;
        _inventoryUI = ui;

        Refresh();
    }

    public void BindExternalSlot(int x, int y, InventoryManager manager, InventoryUI ui)
    {
        IsStorageCell = false;
        StorageSlot = null;
        ExternalX = x;
        ExternalY = y;

        _inventoryManager = manager;
        _inventoryUI = ui;

        Refresh();
    }

    public void Refresh()
    {
        ItemInstance inst = null;

        if (IsStorageCell)
        {
            if (StorageSlot != null)
                inst = StorageSlot.item;
        }
        else
        {
            var grid = _inventoryManager.ExternalGrid;
            if (grid != null &&
                ExternalX >= 0 && ExternalX < _inventoryManager.ExternalWidth &&
                ExternalY >= 0 && ExternalY < _inventoryManager.ExternalHeight)
            {
                inst = grid[ExternalX, ExternalY];
            }
        }

        if (inst == null || inst.Data == null)
        {
            if (iconImage) { iconImage.sprite = null; iconImage.enabled = false; }
            if (idText) idText.text = "";
        }
        else
        {
            if (iconImage)
            {
                iconImage.sprite = inst.Data.icon;
                iconImage.enabled = true;
            }

            if (idText)
            {
                idText.text = inst.Data.id; // hoặc displayName
            }
        }

        UpdateSelectionVisual();
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateSelectionVisual();
    }

    private void UpdateSelectionVisual()
    {
        if (selectionHighlight)
            selectionHighlight.enabled = IsSelected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_inventoryUI == null) return;

        // Click trái: báo cho InventoryUI xử lý
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _inventoryUI.OnCellClicked(this);
        }
    }
}
