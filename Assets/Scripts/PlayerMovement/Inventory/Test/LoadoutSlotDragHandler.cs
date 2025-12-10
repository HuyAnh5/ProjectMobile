using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(LoadoutSlot))]
public class LoadoutSlotDragHandler :
    MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [SerializeField] private Canvas uiCanvas;          // Kéo Canvas TestInventory vào trong Inspector

    private LoadoutSlot slot;
    private ItemTetrisSO draggedItem;
    private RectTransform ghostRoot;                  // Root chứa grid + visual gốc

    private void Awake()
    {
        slot = GetComponent<LoadoutSlot>();
        if (uiCanvas == null)
            uiCanvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Không cần làm gì, chỉ để nhận drag
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (slot == null || slot.IsEmpty) return;

        draggedItem = slot.CurrentItem;
        if (draggedItem == null || draggedItem.visual == null || uiCanvas == null)
            return;

        // Tạo root ghost dưới Canvas
        ghostRoot = new GameObject("LoadoutDragGhost", typeof(RectTransform)).GetComponent<RectTransform>();
        ghostRoot.SetParent(uiCanvas.transform, worldPositionStays: false);
        ghostRoot.anchorMin = ghostRoot.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRoot.pivot = new Vector2(0.5f, 0.5f);
        ghostRoot.localScale = Vector3.one;
        ghostRoot.SetAsLastSibling();

        // Lấy cellSize từ một InventoryTetris bất kỳ (các grid của bạn đều dùng chung cellSize)
        float cellSize = 50f;
        if (InventoryTetris.Instance != null && InventoryTetris.Instance.GetGrid() != null)
        {
            cellSize = InventoryTetris.Instance.GetGrid().GetCellSize();
        }

        // 1) Tạo background ô nhỏ như trong InventoryTetris
        ItemTetrisSO.CreateVisualGrid(ghostRoot, draggedItem, cellSize);

        // Ép RectTransform của grid về center để khớp với sprite
        GridLayoutGroup gridLayout = ghostRoot.GetComponentInChildren<GridLayoutGroup>(true);
        if (gridLayout != null)
        {
            RectTransform gridRect = gridLayout.GetComponent<RectTransform>();
            if (gridRect != null)
            {
                gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
                gridRect.pivot = new Vector2(0.5f, 0.5f);
                gridRect.anchoredPosition = Vector2.zero;
            }
        }

        // 2) Thêm prefab Visual gốc vào giữa grid
        Transform visualInstance = Instantiate(draggedItem.visual, ghostRoot);
        RectTransform visualRT = visualInstance as RectTransform;
        if (visualRT != null)
        {
            visualRT.anchorMin = visualRT.anchorMax = new Vector2(0.5f, 0.5f);
            visualRT.pivot = new Vector2(0.5f, 0.5f);
            visualRT.anchoredPosition = Vector2.zero;
            visualRT.localScale = Vector3.one;
        }

        // Ghost không được chặn raycast
        foreach (var img in ghostRoot.GetComponentsInChildren<Image>())
            img.raycastTarget = false;
        foreach (var cg in ghostRoot.GetComponentsInChildren<CanvasGroup>())
            cg.blocksRaycasts = false;

        UpdateGhostPosition(eventData);

        // Ẩn icon 1x1 trong slot khi đang kéo
        slot.SetIconVisible(false);
        Cursor.visible = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Cursor.visible = true;

        bool handled = false;

        if (draggedItem != null &&
            InventoryLoadoutAndBurnSystem.Instance != null)
        {
            handled = InventoryLoadoutAndBurnSystem.Instance.TryHandleDropFromLoadout(
                eventData,
                slot,
                draggedItem
            );
        }

        // Xoá ghost
        if (ghostRoot != null)
            Destroy(ghostRoot.gameObject);
        ghostRoot = null;

        if (handled)
        {
            // Đã được drop vào GRID hoặc Burn → slot rỗng
            slot.Clear();
        }
        else
        {
            // Không drop được chỗ nào hợp lệ → hiện lại icon 1x1 trong slot
            slot.SetIconVisible(true);
        }

        draggedItem = null;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (uiCanvas == null || ghostRoot == null) return;

        RectTransform canvasRect = uiCanvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            uiCanvas.worldCamera,
            out Vector2 localPos);

        ghostRoot.anchoredPosition = localPos;
    }
}
