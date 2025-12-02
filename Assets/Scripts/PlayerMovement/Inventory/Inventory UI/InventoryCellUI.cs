using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Một ô UI, có thể là Storage Cell hoặc External Cell.
/// Điều khiển hiển thị icon, text, click và drag/drop.
/// </summary>
public class InventoryCellUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("UI Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI idText;
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

    // Drag support
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Canvas _rootCanvas;

    private Transform _originalParent;
    private Vector3 _originalPosition;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();
    }

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

        // Click trái: báo cho InventoryUI xử lý (select / pick, tùy logic bên kia)
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _inventoryUI.OnCellClicked(this);
        }
    }

    // ----------------------------------------------------------------------
    // Drag & Drop – dùng để kéo cell vào BurnZone
    // ----------------------------------------------------------------------

    private bool HasItem()
    {
        if (IsStorageCell && StorageSlot != null)
        {
            var inst = StorageSlot.item;
            return inst != null && inst.Data != null;
        }

        if (IsExternalCell && _inventoryManager != null)
        {
            var grid = _inventoryManager.ExternalGrid;
            if (grid == null) return false;

            if (ExternalX < 0 || ExternalX >= _inventoryManager.ExternalWidth ||
                ExternalY < 0 || ExternalY >= _inventoryManager.ExternalHeight)
                return false;

            var inst = grid[ExternalX, ExternalY];
            return inst != null && inst.Data != null;
        }

        return false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasItem()) return;

        _originalParent = transform.parent;
        _originalPosition = _rectTransform.position;

        if (_rootCanvas != null)
        {
            // Đưa cell lên Canvas root để drag không bị Layout Group đè
            transform.SetParent(_rootCanvas.transform, true);
        }

        // Cho raycast đi xuyên qua cell để BurnZone nhận OnDrop
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!HasItem()) return;
        if (_rootCanvas == null) return;

        _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Dù item có bị burn hay không, trả cell về parent/position cũ
        if (_originalParent != null)
            transform.SetParent(_originalParent, true);

        _rectTransform.position = _originalPosition;
        _canvasGroup.blocksRaycasts = true;

        if (_inventoryUI != null)
            _inventoryUI.RefreshAll();
    }
}
