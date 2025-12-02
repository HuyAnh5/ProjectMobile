using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Khu vực đốt đồ: khi thả một InventoryCellUI (storage) vào đây, item sẽ bị burn.
/// </summary>
public class BurnZoneUI : MonoBehaviour, IDropHandler
{
    [SerializeField] private InventoryUI inventoryUI;

    private void Awake()
    {
        if (inventoryUI == null)
            inventoryUI = GetComponentInParent<InventoryUI>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (inventoryUI == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        var cell = draggedGO.GetComponent<InventoryCellUI>();
        if (cell == null) return;

        inventoryUI.OnCellDroppedIntoBurnZone(cell);
    }
}
