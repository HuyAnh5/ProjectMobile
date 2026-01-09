using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryTetrisDragDropSystem : MonoBehaviour
{
    public static InventoryTetrisDragDropSystem Instance { get; private set; }

    [SerializeField] private List<InventoryTetris> inventoryTetrisList = new List<InventoryTetris>();

    [Header("Preview Colors")]
    [SerializeField] private Color previewValidColor = new Color(0f, 1f, 0f, 0.25f);
    [SerializeField] private Color previewInvalidColor = new Color(1f, 0f, 0f, 0.25f);

    [Header("Snap Feel")]
    [Tooltip("0 = Floor (kẹt ô), 0.5 = gần Round. Bạn đang dùng 0.4 là OK.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float snapBias = 0.4f;

    [Header("DEBUG")]
    [SerializeField] private bool debugOverlay = false;
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F1;
    [SerializeField] private KeyCode toggleLogsKey = KeyCode.F2;

    private Canvas rootCanvas;
    private RectTransform dragLayer;

    private InventoryTetris draggingInventoryTetris;
    private PlacedObject draggingPlacedObject;
    private ItemTetrisSO draggingItemSO;
    private PlacedObjectTypeSO.Dir dir;
    private RectTransform draggingRect;

    // Hover result
    private InventoryTetris hoverInventory;
    private Vector2Int hoverOrigin;
    private bool hoverCanPlace;
    private string hoverReason;

    // Preview rectangle (under ghost)
    private RectTransform previewRect;
    private Image previewImage;
    private static Sprite whiteSprite;

    // Debug cache
    private string dbgHit = "(none)";
    private string dbgHoverInv = "(none)";
    private string dbgMode = "";
    private float dbgCellSize;
    private Vector2 dbgPivotLocalBL;
    private Vector2 dbgOriginLocalBL;
    private Vector2 dbgOriginFloat;
    private Vector2Int dbgOrigin;
    private bool dbgCanPlace;
    private string dbgReason = "";
    private Vector2Int dbgRotOffset;
    private int dbgBBoxW, dbgBBoxH;
    private Vector2 dbgPreviewAnchored;

    private void Awake()
    {
        Instance = this;
        EnsureInventoryList();
        EnsureRootCanvasAndDragLayer();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleOverlayKey)) debugOverlay = !debugOverlay;
        if (Input.GetKeyDown(toggleLogsKey)) debugLogs = !debugLogs;

        // If destroyed by other handlers (burn/loadout)
        if (draggingPlacedObject == null)
        {
            CleanupPreview();
            ClearDragState();
            return;
        }

        // Rotate while dragging
        if (Input.GetKeyDown(KeyCode.R))
        {
            dir = PlacedObjectTypeSO.GetNextDir(dir);
            ApplyDraggedRotation();
        }

        // Ghost follows mouse freely
        UpdateGhostFollowMouseFree();

        // Update hover + intended origin
        UpdatePlacementTarget();

        // Update preview rectangle (FIXED for rotate)
        UpdatePreviewRectangle();

        if (debugLogs) Debug.Log(BuildDebugLine());
    }

    public void StartedDragging(InventoryTetris inventoryTetris, PlacedObject placedObject)
    {
        EnsureInventoryList();
        EnsureRootCanvasAndDragLayer();

        draggingInventoryTetris = inventoryTetris;
        draggingPlacedObject = placedObject;
        draggingItemSO = placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO;
        draggingRect = placedObject.GetComponent<RectTransform>();

        Cursor.visible = false;

        dir = placedObject.GetDir();
        ApplyDraggedRotation();

        // parent to DragLayer so you can drag across entire UI
        if (draggingRect.parent != dragLayer)
            draggingRect.SetParent(dragLayer, true);

        EnsurePreview();

        UpdateGhostFollowMouseFree();
        UpdatePlacementTarget();
        UpdatePreviewRectangle();
    }

    public void StoppedDragging(InventoryTetris fromInventoryTetris, PlacedObject placedObject)
    {
        Cursor.visible = true;

        ItemTetrisSO itemSO = placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO;
        Vector2Int oldGridPosition = placedObject.GetGridPosition();
        PlacedObjectTypeSO.Dir oldDir = placedObject.GetDir();

        CleanupPreview();

        // Remove destroys GO
        fromInventoryTetris.RemoveItem(placedObject);

        bool placed = false;
        if (hoverInventory != null && hoverCanPlace)
        {
            placed = hoverInventory.TryPlaceItem(itemSO, hoverOrigin, dir);
        }

        if (!placed)
        {
            fromInventoryTetris.TryPlaceItem(itemSO, oldGridPosition, oldDir);
        }

        ClearDragState();
    }

    private void EnsureRootCanvasAndDragLayer()
    {
        if (rootCanvas == null)
        {
            if (inventoryTetrisList.Count > 0 && inventoryTetrisList[0] != null)
                rootCanvas = inventoryTetrisList[0].GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                rootCanvas = FindObjectOfType<Canvas>(true);
        }

        if (dragLayer == null && rootCanvas != null)
        {
            Transform existing = rootCanvas.transform.Find("DragLayer");
            if (existing != null)
            {
                dragLayer = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject go = new GameObject("DragLayer", typeof(RectTransform));
                go.transform.SetParent(rootCanvas.transform, false);
                dragLayer = go.GetComponent<RectTransform>();
                dragLayer.anchorMin = Vector2.zero;
                dragLayer.anchorMax = Vector2.one;
                dragLayer.offsetMin = Vector2.zero;
                dragLayer.offsetMax = Vector2.zero;
                dragLayer.SetAsLastSibling();
            }
        }
    }

    private void ApplyDraggedRotation()
    {
        if (draggingPlacedObject == null) return;
        int angle = draggingPlacedObject.GetPlacedObjectTypeSO().GetRotationAngle(dir);
        draggingPlacedObject.transform.rotation = Quaternion.Euler(0, 0, -angle);
    }

    /// <summary>
    /// Ghost follows mouse freely and keeps CENTER under cursor.
    /// </summary>
    private void UpdateGhostFollowMouseFree()
    {
        if (draggingRect == null || dragLayer == null) return;

        Camera cam = GetCanvasCamera(rootCanvas);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragLayer, Input.mousePosition, cam, out Vector2 mouseLocalInLayer
        );

        Vector3 centerWorld = draggingRect.TransformPoint(draggingRect.rect.center);
        Vector2 centerLocalInLayer = (Vector2)dragLayer.InverseTransformPoint(centerWorld);

        Vector2 delta = mouseLocalInLayer - centerLocalInLayer;
        draggingRect.anchoredPosition += delta;
    }

    private void UpdatePlacementTarget()
    {
        hoverInventory = null;
        hoverOrigin = default;
        hoverCanPlace = false;
        hoverReason = "NotOverGrid";

        if (!TryGetHoveredInventory(out InventoryTetris inv, out RectTransform container, out Camera _, out string hitName))
        {
            dbgHit = hitName;
            dbgHoverInv = "(none)";
            dbgMode = "FreeFollow";
            dbgCanPlace = false;
            dbgReason = "NotOverGrid";
            return;
        }

        dbgHit = hitName;
        dbgHoverInv = inv.name;
        dbgMode = "InGridSnap";

        float cellSize = inv.GetGrid().GetCellSize();

        // rotationOffset still used ONLY for origin derivation (your placement works well already)
        Vector2Int rotOffsetCells = draggingItemSO.GetRotationOffset(dir);

        // ghost pivot -> local BL of container
        Vector2 pivotLocalBL = WorldToContainerLocalBL(container, draggingRect.position);

        Vector2 originLocalBL = pivotLocalBL - (Vector2)rotOffsetCells * cellSize;
        Vector2 originFloat = new Vector2(originLocalBL.x / cellSize, originLocalBL.y / cellSize);

        Vector2Int origin = new Vector2Int(
            Mathf.FloorToInt(originFloat.x + snapBias),
            Mathf.FloorToInt(originFloat.y + snapBias)
        );

        hoverInventory = inv;
        hoverOrigin = origin;

        hoverCanPlace = CanPlaceConsideringDraggedSelf(inv, draggingItemSO, hoverOrigin, dir, out string reason);
        hoverReason = hoverCanPlace ? "OK" : reason;

        // debug pack
        dbgCellSize = cellSize;
        dbgPivotLocalBL = pivotLocalBL;
        dbgOriginLocalBL = originLocalBL;
        dbgOriginFloat = originFloat;
        dbgOrigin = origin;
        dbgCanPlace = hoverCanPlace;
        dbgReason = hoverReason;
        dbgRotOffset = rotOffsetCells;
    }

    private Vector2 WorldToContainerLocalBL(RectTransform container, Vector3 worldPos)
    {
        Vector2 localPivot = (Vector2)container.InverseTransformPoint(worldPos);
        return localPivot + Vector2.Scale(container.rect.size, container.pivot);
    }

    private bool CanPlaceConsideringDraggedSelf(InventoryTetris inv, ItemTetrisSO itemSO, Vector2Int origin, PlacedObjectTypeSO.Dir dir, out string reason)
    {
        reason = "Unknown";
        if (inv == null || itemSO == null) { reason = "NullInvOrItem"; return false; }

        List<Vector2Int> cells = itemSO.GetGridPositionList(origin, dir);
        var grid = inv.GetGrid();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int c = cells[i];

            if (!inv.IsValidGridPosition(c))
            {
                reason = $"InvalidCell({c.x},{c.y})";
                return false;
            }

            InventoryTetris.GridObject g = grid.GetGridObject(c.x, c.y);
            if (g == null)
            {
                reason = $"GridObjectNull({c.x},{c.y})";
                return false;
            }

            if (!g.CanBuild())
            {
                if (inv == draggingInventoryTetris && g.GetPlacedObject() == draggingPlacedObject)
                    continue;

                reason = $"Occupied({c.x},{c.y})";
                return false;
            }
        }

        reason = "OK";
        return true;
    }

    private void UpdatePreviewRectangle()
    {
        EnsurePreview();

        // Bounding box for occupied cells in THIS dir
        GetBoundingBoxCells(draggingItemSO, dir, out int wCells, out int hCells);
        if (wCells <= 0) wCells = 1;
        if (hCells <= 0) hCells = 1;

        dbgBBoxW = wCells;
        dbgBBoxH = hCells;

        float cellSize = 1f;
        if (hoverInventory != null) cellSize = hoverInventory.GetGrid().GetCellSize();
        else if (draggingInventoryTetris != null) cellSize = draggingInventoryTetris.GetGrid().GetCellSize();

        previewRect.sizeDelta = new Vector2(wCells * cellSize, hCells * cellSize);

        if (hoverInventory != null)
        {
            // ===== FIX HERE =====
            // Preview must reflect OCCUPIED CELLS (origin-based), NOT visual rotationOffset.
            // So we anchor to grid.GetWorldPosition(origin) (bottom-left of origin cell),
            // pivot = (0,0), no rotationOffset.
            RectTransform targetContainer = hoverInventory.GetItemContainer();
            if (previewRect.parent != targetContainer)
                previewRect.SetParent(targetContainer, false);

            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.zero;
            previewRect.pivot = Vector2.zero; // bottom-left for cell coverage

            Vector2 snappedBL = (Vector2)hoverInventory.GetGrid().GetWorldPosition(hoverOrigin.x, hoverOrigin.y);
            previewRect.anchoredPosition = snappedBL;

            dbgPreviewAnchored = snappedBL;

            previewImage.color = hoverCanPlace ? previewValidColor : previewInvalidColor;

            // behind items
            previewRect.SetAsFirstSibling();
        }
        else
        {
            // Outside grid: follow ghost center, red
            if (previewRect.parent != dragLayer)
                previewRect.SetParent(dragLayer, false);

            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);

            Vector3 ghostCenterWorld = draggingRect.TransformPoint(draggingRect.rect.center);
            Vector2 ghostCenterLocal = (Vector2)dragLayer.InverseTransformPoint(ghostCenterWorld);

            previewRect.anchoredPosition = ghostCenterLocal;
            dbgPreviewAnchored = ghostCenterLocal;

            previewImage.color = previewInvalidColor;

            previewRect.SetAsFirstSibling();
        }
    }

    private void GetBoundingBoxCells(ItemTetrisSO itemSO, PlacedObjectTypeSO.Dir dir, out int wCells, out int hCells)
    {
        wCells = 1;
        hCells = 1;
        if (itemSO == null) return;

        List<Vector2Int> offs = itemSO.GetGridPositionList(Vector2Int.zero, dir);
        if (offs == null || offs.Count == 0) return;

        int maxX = int.MinValue, maxY = int.MinValue;
        int minX = int.MaxValue, minY = int.MaxValue;

        for (int i = 0; i < offs.Count; i++)
        {
            var v = offs[i];
            if (v.x > maxX) maxX = v.x;
            if (v.y > maxY) maxY = v.y;
            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
        }

        wCells = (maxX - minX + 1);
        hCells = (maxY - minY + 1);
    }

    private void EnsurePreview()
    {
        if (previewRect != null) return;
        if (dragLayer == null) EnsureRootCanvasAndDragLayer();
        if (dragLayer == null) return;

        GameObject go = new GameObject("PlacementPreviewRect", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(dragLayer, false);

        previewRect = go.GetComponent<RectTransform>();
        previewImage = go.GetComponent<Image>();
        previewImage.raycastTarget = false;

        if (whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        previewImage.sprite = whiteSprite;
        previewImage.color = previewInvalidColor;

        previewRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.sizeDelta = new Vector2(64, 64);
        previewRect.anchoredPosition = Vector2.zero;

        previewRect.SetAsFirstSibling();
    }

    private void CleanupPreview()
    {
        if (previewRect != null)
        {
            Destroy(previewRect.gameObject);
            previewRect = null;
            previewImage = null;
        }
    }

    private bool TryGetHoveredInventory(out InventoryTetris inv, out RectTransform container, out Camera cam, out string hitName)
    {
        inv = null;
        container = null;
        cam = null;
        hitName = "(none)";

        if (EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            if (go == null) continue;

            InventoryTetris found = go.GetComponentInParent<InventoryTetris>();
            if (found == null) continue;

            if (inventoryTetrisList.Count > 0 && !inventoryTetrisList.Contains(found))
                continue;

            inv = found;
            container = found.GetItemContainer();
            hitName = go.name;

            Canvas canvas = container != null ? container.GetComponentInParent<Canvas>() : found.GetComponentInParent<Canvas>();
            cam = GetCanvasCamera(canvas);

            return inv != null && container != null;
        }

        return false;
    }

    private Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null) return null;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void EnsureInventoryList()
    {
        if (inventoryTetrisList == null)
            inventoryTetrisList = new List<InventoryTetris>();

        inventoryTetrisList.RemoveAll(i => i == null);

        if (inventoryTetrisList.Count == 0)
            inventoryTetrisList.AddRange(FindObjectsOfType<InventoryTetris>(true));
    }

    private void ClearDragState()
    {
        draggingInventoryTetris = null;
        draggingPlacedObject = null;
        draggingItemSO = null;
        draggingRect = null;

        hoverInventory = null;
        hoverOrigin = default;
        hoverCanPlace = false;
        hoverReason = "";
    }

    private string BuildDebugLine()
    {
        var sb = new StringBuilder();
        sb.Append($"[DragDbg] hit={dbgHit} mode={dbgMode} hoverInv={dbgHoverInv} ");
        sb.Append($"cellSize={dbgCellSize:F2} snapBias={snapBias:F2} dir={dir} rotOffset=({dbgRotOffset.x},{dbgRotOffset.y}) ");
        sb.Append($"pivotBL=({dbgPivotLocalBL.x:F1},{dbgPivotLocalBL.y:F1}) ");
        sb.Append($"originBL=({dbgOriginLocalBL.x:F1},{dbgOriginLocalBL.y:F1}) ");
        sb.Append($"originF=({dbgOriginFloat.x:F2},{dbgOriginFloat.y:F2}) ");
        sb.Append($"origin=({dbgOrigin.x},{dbgOrigin.y}) bbox=({dbgBBoxW}x{dbgBBoxH}) ");
        sb.Append($"previewPos=({dbgPreviewAnchored.x:F1},{dbgPreviewAnchored.y:F1}) ");
        sb.Append($"canPlace={dbgCanPlace} reason={dbgReason}");
        return sb.ToString();
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;
        if (draggingPlacedObject == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 1300, 200), GUI.skin.box);
        GUILayout.Label("InventoryTetrisDragDropSystem DEBUG (F1 overlay / F2 logs)");
        GUILayout.Label(BuildDebugLine());
        GUILayout.EndArea();
    }
}
