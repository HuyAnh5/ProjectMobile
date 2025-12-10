using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryLoadoutAndBurnSystem : MonoBehaviour
{
    public static InventoryLoadoutAndBurnSystem Instance { get; private set; }

    [Header("Loadout slots (3 weapon + 2 item)")]
    [SerializeField] private List<LoadoutSlot> loadoutSlots = new List<LoadoutSlot>();

    [Header("Inventories (Storage + External)")]
    [SerializeField] private List<InventoryTetris> inventories = new List<InventoryTetris>();

    [Header("Burn Drop Zone")]
    [SerializeField] private RectTransform burnZoneRect;
    [SerializeField] private Canvas burnZoneCanvasOverride; // optional

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // -------------------------------------------------------------
    // 1) DROP TỪ GRID → Loadout / Burn
    // -------------------------------------------------------------
    public bool TryHandleDropFromGrid(
        PointerEventData eventData,
        InventoryTetris fromInventory,
        PlacedObject placedObject)
    {
        if (!fromInventory || !placedObject) return false;

        Vector2Int oldGridPosition = placedObject.GetGridPosition();
        PlacedObjectTypeSO.Dir oldDir = placedObject.GetDir();
        ItemTetrisSO itemSO = placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO;
        if (itemSO == null) return false;

        Vector2 screenPos = eventData.position;

        // 1) Burn zone
        if (IsPointerOverBurnZone(screenPos))
        {
            fromInventory.RemoveItem(placedObject);
            Debug.Log($"[Burn] Đã đốt item {itemSO.name} từ {fromInventory.name}");
            return true;
        }

        // 2) Loadout slot
        LoadoutSlot targetSlot = GetSlotUnderPointer(screenPos);
        if (targetSlot != null)
        {
            if (!targetSlot.IsEmpty && targetSlot.CurrentItem != null)
            {
                // Slot đã có item → trả về chỗ cũ
                fromInventory.RemoveItem(placedObject);
                fromInventory.TryPlaceItem(itemSO, oldGridPosition, oldDir);
                Debug.Log("[Loadout] Slot đã có item, trả về vị trí cũ");
                return true;
            }

            if (!targetSlot.Accepts(itemSO))
            {
                // Sai loại slot → trả về chỗ cũ
                fromInventory.RemoveItem(placedObject);
                fromInventory.TryPlaceItem(itemSO, oldGridPosition, oldDir);
                Debug.Log("[Loadout] Item không hợp type slot, trả về vị trí cũ");
                return true;
            }

            // Hợp lệ → remove khỏi grid, Equip vào slot
            fromInventory.RemoveItem(placedObject);
            targetSlot.Equip(itemSO);
            Debug.Log($"[Loadout] Equip {itemSO.name} vào slot {targetSlot.name}");
            return true;
        }

        // Không rơi vào Loadout/Burn → để hệ Tetris gốc xử lý
        return false;
    }

    // -------------------------------------------------------------
    // 2) DROP TỪ LOADOUT → Grid / Burn
    // -------------------------------------------------------------
    public bool TryHandleDropFromLoadout(
        PointerEventData eventData,
        LoadoutSlot fromSlot,
        ItemTetrisSO itemSO)
    {
        if (fromSlot == null || itemSO == null) return false;

        Vector2 screenPos = eventData.position;

        // 1) Burn zone?
        if (IsPointerOverBurnZone(screenPos))
        {
            Debug.Log($"[Burn] Đã đốt item {itemSO.name} từ slot {fromSlot.name}");
            return true; // slot sẽ Clear() trong handler
        }

        // 2) Thử đặt vào một InventoryTetris
        foreach (var inventory in inventories)
        {
            if (!inventory) continue;

            RectTransform container = inventory.GetItemContainer();
            if (!container) continue;

            Canvas canvas = inventory.GetComponentInParent<Canvas>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container,
                screenPos,
                canvas ? canvas.worldCamera : null,
                out Vector2 anchoredPosition
            );

            // Cell gần nhất dưới chuột
            Vector2Int mouseGridPos = inventory.GetGridPosition(anchoredPosition);

            // Chuột phải đang nằm trên 1 ô hợp lệ của grid
            if (!inventory.IsValidGridPosition(mouseGridPos))
                continue;

            bool placed = false;
            var dir = PlacedObjectTypeSO.Dir.Down;

            // Quét quanh cell chuột trong vùng kích thước item (width × height)
            // để tìm origin sao cho shape của item bao phủ cell chuột.
            for (int ox = -(itemSO.width - 1); ox <= 0 && !placed; ox++)
            {
                for (int oy = -(itemSO.height - 1); oy <= 0 && !placed; oy++)
                {
                    Vector2Int origin = new Vector2Int(mouseGridPos.x + ox, mouseGridPos.y + oy);

                    // Lấy danh sách cell mà item chiếm với origin này
                    var cells = itemSO.GetGridPositionList(origin, dir);

                    // Nếu cell chuột không nằm trong shape thì bỏ qua origin này
                    bool coversMouseCell = false;
                    foreach (var c in cells)
                    {
                        if (c == mouseGridPos)
                        {
                            coversMouseCell = true;
                            break;
                        }
                    }
                    if (!coversMouseCell)
                        continue;

                    // Thử place; TryPlaceItem tự kiểm tra tràn lưới / va chạm
                    if (inventory.TryPlaceItem(itemSO, origin, dir))
                    {
                        Debug.Log($"[Loadout→Grid] Đặt {itemSO.name} vào {inventory.name} tại {origin}");
                        placed = true;
                    }
                }
            }

            if (placed)
            {
                // Đã drop thành công vào inventory này
                return true; // LoadoutSlotDragHandler sẽ Clear() slot
            }
        }

        // Không inventory nào nhận được → caller giữ item lại trong slot
        return false;
    }


    // -------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------
    private bool IsPointerOverBurnZone(Vector2 screenPos)
    {
        if (!burnZoneRect) return false;

        Canvas canvas = burnZoneCanvasOverride;
        if (!canvas)
            canvas = burnZoneRect.GetComponentInParent<Canvas>();

        return RectTransformUtility.RectangleContainsScreenPoint(
            burnZoneRect,
            screenPos,
            canvas ? canvas.worldCamera : null
        );
    }

    private LoadoutSlot GetSlotUnderPointer(Vector2 screenPos)
    {
        foreach (var slot in loadoutSlots)
        {
            if (!slot) continue;

            RectTransform rt = slot.GetComponent<RectTransform>();
            if (!rt) continue;

            Canvas canvas = rt.GetComponentInParent<Canvas>();
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rt,
                    screenPos,
                    canvas ? canvas.worldCamera : null))
            {
                return slot;
            }
        }
        return null;
    }
}
