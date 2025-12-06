using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryTetrisDragDropSystem : MonoBehaviour
{
    public static InventoryTetrisDragDropSystem Instance { get; private set; }

    [SerializeField] private List<InventoryTetris> inventoryTetrisList;

    private Canvas uiCanvas;

    private InventoryTetris draggingInventoryTetris;
    private PlacedObject draggingPlacedObject;
    private Vector2Int mouseDragGridPositionOffset;
    private Vector2 mouseDragAnchoredPositionOffset;
    private PlacedObjectTypeSO.Dir dir;

    private void Awake()
    {
        Instance = this;

        // Tìm Canvas bên ngoài (giả sử tất cả inventory cùng 1 Canvas)
        if (inventoryTetrisList != null && inventoryTetrisList.Count > 0)
        {
            uiCanvas = inventoryTetrisList[0].GetComponentInParent<Canvas>();
        }
    }

    private void Start()
    {
        foreach (InventoryTetris inventoryTetris in inventoryTetrisList)
        {
            inventoryTetris.OnObjectPlaced += (object sender, PlacedObject placedObject) =>
            {
                // Không cần làm gì thêm ở đây cho drag-drop
            };
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            dir = PlacedObjectTypeSO.GetNextDir(dir);
        }

        if (draggingPlacedObject == null) return;

        if (uiCanvas == null && draggingInventoryTetris != null)
        {
            uiCanvas = draggingInventoryTetris.GetComponentInParent<Canvas>();
        }

        // Lấy vị trí chuột trong toạ độ local của ItemContainer
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            draggingInventoryTetris.GetItemContainer(),
            Input.mousePosition,
            uiCanvas != null ? uiCanvas.worldCamera : null,
            out Vector2 localPos
        );

        // Trừ offset để bám đúng điểm đã click
        localPos -= mouseDragAnchoredPositionOffset;

        // Di chuyển item đúng theo chuột (không snap)
        RectTransform rect = draggingPlacedObject.GetComponent<RectTransform>();
        rect.anchoredPosition = localPos;
    }

    public void StartedDragging(InventoryTetris inventoryTetris, PlacedObject placedObject)
    {
        draggingInventoryTetris = inventoryTetris;
        draggingPlacedObject = placedObject;

        Cursor.visible = false;

        if (uiCanvas == null)
        {
            uiCanvas = draggingInventoryTetris.GetComponentInParent<Canvas>();
        }

        // Vị trí local của chuột lúc bắt đầu kéo
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inventoryTetris.GetItemContainer(),
            Input.mousePosition,
            uiCanvas != null ? uiCanvas.worldCamera : null,
            out Vector2 anchoredPosition
        );

        // Vị trí ô lưới tại điểm chuột
        Vector2Int mouseGridPosition = inventoryTetris.GetGridPosition(anchoredPosition);

        // Offset giữa origin của item và ô mà chuột đang ở
        mouseDragGridPositionOffset = mouseGridPosition - placedObject.GetGridPosition();

        // Offset giữa tâm Rect và điểm click trên sprite
        RectTransform rect = placedObject.GetComponent<RectTransform>();
        mouseDragAnchoredPositionOffset = anchoredPosition - rect.anchoredPosition;

        // Lưu hướng hiện tại của item
        dir = placedObject.GetDir();
    }

    public void StoppedDragging(InventoryTetris fromInventoryTetris, PlacedObject placedObject)
    {
        draggingInventoryTetris = null;
        draggingPlacedObject = null;

        Cursor.visible = true;

        // Lưu lại gridPosition & dir cũ phòng khi phải restore
        Vector2Int oldGridPosition = placedObject.GetGridPosition();
        PlacedObjectTypeSO.Dir oldDir = placedObject.GetDir();

        // Xoá item khỏi inventory cũ
        fromInventoryTetris.RemoveItemAt(oldGridPosition);

        InventoryTetris toInventoryTetris = null;

        // Tìm inventory đang nằm dưới chuột
        foreach (InventoryTetris inventoryTetris in inventoryTetrisList)
        {
            Canvas canvas = inventoryTetris.GetComponentInParent<Canvas>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inventoryTetris.GetItemContainer(),
                Input.mousePosition,
                canvas != null ? canvas.worldCamera : null,
                out Vector2 anchoredPosition
            );

            Vector2Int placedObjectOrigin = inventoryTetris.GetGridPosition(anchoredPosition);
            placedObjectOrigin -= mouseDragGridPositionOffset;

            if (inventoryTetris.IsValidGridPosition(placedObjectOrigin))
            {
                toInventoryTetris = inventoryTetris;
                break;
            }
        }

        // Nếu chuột đang nằm trên 1 inventory nào đó
        if (toInventoryTetris != null)
        {
            Canvas canvas = toInventoryTetris.GetComponentInParent<Canvas>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                toInventoryTetris.GetItemContainer(),
                Input.mousePosition,
                canvas != null ? canvas.worldCamera : null,
                out Vector2 anchoredPosition
            );

            Vector2Int placedObjectOrigin = toInventoryTetris.GetGridPosition(anchoredPosition);
            placedObjectOrigin -= mouseDragGridPositionOffset;

            bool tryPlaceItem = toInventoryTetris.TryPlaceItem(
                placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO,
                placedObjectOrigin,
                dir
            );

            if (!tryPlaceItem)
            {
                // Không đặt được trong inventory → trả về vị trí cũ
                Debug.Log("Cannot Drop Item Here (invalid position inside inventory)!");
                fromInventoryTetris.TryPlaceItem(
                    placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO,
                    oldGridPosition,
                    oldDir
                );
            }
        }
        else
        {
            // Chuột không nằm trên inventory nào → trả về vị trí cũ
            Debug.Log("Cannot Drop Item Here (no inventory under mouse)!");
            fromInventoryTetris.TryPlaceItem(
                placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO,
                oldGridPosition,
                oldDir
            );
        }
    }
}
