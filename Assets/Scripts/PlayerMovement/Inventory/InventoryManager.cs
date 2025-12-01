using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý:
/// - Lưới inventory chính (bầu đèn) -> List<GridSlot>.
/// - Darkness / Decay dựa trên OilLamp.
/// - Batch Burn (tính dầu + đẩy effect vào PlayerItemSlots).
/// - External / Ground Grid (3x9) cho Looting & Dropping.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // --------------------------------------------------------------------
    // STORAGE GRID (bầu đèn)
    // --------------------------------------------------------------------
    [Header("Storage grid (lantern shape)")]
    [SerializeField] private LanternGridDefinition gridDefinition;

    [Tooltip("ItemData dùng làm 'Than Củi' sau khi item bị decay hỏng.")]
    [SerializeField] private ItemData charcoalItemData;

    [Header("Runtime references")]
    [SerializeField] private OilLamp oilLamp;
    [SerializeField] private PlayerItemSlots playerItemSlots;

    [Header("Decay config")]
    [Tooltip("Thời gian để item trong Dark Row bị biến thành than (giây).")]
    [SerializeField] private float decayThresholdSeconds = 60f;

    [Tooltip("Khi vượt ngưỡng này (giây) thì bắn cảnh báo warning.")]
    [SerializeField] private float decayWarningThresholdSeconds = 45f;

    [Tooltip("Curve: input = OilRatio [0..1], output = số row bị Dark (từ trên xuống).")]
    [SerializeField] private AnimationCurve darknessCurve;

    /// <summary>
    /// Event cho UI – gọi khi item bắt đầu bước vào vùng warning (sắp hỏng).
    /// </summary>
    public event Action<ItemInstance> OnItemDecayWarning;

    private readonly List<GridSlot> _slots = new();
    private readonly HashSet<int> _darkRows = new();

    /// <summary>Danh sách slot của Storage Grid.</summary>
    public IReadOnlyList<GridSlot> Slots => _slots;

    // --------------------------------------------------------------------
    // EXTERNAL / GROUND GRID (3x9)
    // --------------------------------------------------------------------
    [Header("External / Ground grid (always visible)")]
    [SerializeField] private int externalWidth = 9;
    [SerializeField] private int externalHeight = 3;

    [Tooltip("Vị trí spawn item khi Drop ra thế giới (thường là chân Player).")]
    [SerializeField] private Transform dropOrigin;

    [Tooltip("Prefab dùng để spawn item rơi ra thế giới.")]
    [SerializeField] private GameObject itemPickupPrefab;

    private ItemInstance[,] _externalGrid;

    /// <summary>External grid 2D (x,y) – cho UI hiển thị.</summary>
    public ItemInstance[,] ExternalGrid => _externalGrid;
    public int ExternalWidth => externalWidth;
    public int ExternalHeight => externalHeight;

    // Trạng thái Inventory đang mở theo mode nào
    private ILootSource _currentLootSource;
    private bool _isLootMode; // true = Looting mode, false = Dropping mode

    public bool IsLootMode => _isLootMode;

    // Event để UI hook nếu muốn
    public event Action<InventoryManager> OnInventoryOpened;
    public event Action<InventoryManager> OnInventoryClosed;

    // --------------------------------------------------------------------
    // Unity lifecycle
    // --------------------------------------------------------------------
    private void Awake()
    {
        BuildGridFromDefinition();
        EnsureDarknessCurveDefault();
        InitExternalGrid();
    }

    private void Update()
    {
        if (!oilLamp) return;

        UpdateRowState(oilLamp.Current);
        UpdateDecay(Time.deltaTime);
    }

    // --------------------------------------------------------------------
    // STORAGE GRID BUILD
    // --------------------------------------------------------------------
    private void BuildGridFromDefinition()
    {
        _slots.Clear();
        if (gridDefinition == null)
        {
            Debug.LogError("InventoryManager: Missing LanternGridDefinition.");
            return;
        }

        foreach (var rowDef in gridDefinition.Rows)
        {
            for (int x = 0; x < rowDef.slotCount; x++)
            {
                var coord = new Vector2Int(x, rowDef.rowIndex);
                _slots.Add(new GridSlot(coord));
            }
        }
    }

    private void EnsureDarknessCurveDefault()
    {
        if (darknessCurve == null || darknessCurve.length == 0)
        {
            // OilRatio 1.0 -> 0 dark row
            // 0.75 -> 1; 0.5 -> 2; 0.25 -> 3; 0.1 -> 4; 0.0 -> 5
            darknessCurve = new AnimationCurve(
                new Keyframe(1f, 0),
                new Keyframe(0.75f, 1),
                new Keyframe(0.5f, 2),
                new Keyframe(0.25f, 3),
                new Keyframe(0.1f, 4),
                new Keyframe(0f, 5)
            );
            darknessCurve.preWrapMode = WrapMode.Clamp;
            darknessCurve.postWrapMode = WrapMode.Clamp;
        }
    }

    // --------------------------------------------------------------------
    // DARKNESS & DECAY
    // --------------------------------------------------------------------
    /// <summary>
    /// Cập nhật Dark Row dựa trên lượng dầu hiện tại.
    /// Bóng tối ăn từ row 0 xuống, row đáy luôn an toàn.
    /// </summary>
    public void UpdateRowState(float currentOil)
    {
        _darkRows.Clear();

        if (oilLamp == null || oilLamp.Capacity <= 0f)
            return;

        float ratio = Mathf.Clamp01(currentOil / oilLamp.Capacity);
        int darkRowCount = Mathf.RoundToInt(darknessCurve.Evaluate(ratio));

        int maxRow = GetMaxRowIndex();

        // Row index nhỏ = trên cùng; thêm dần xuống, nhưng không đè row đáy.
        for (int row = 0; row < darkRowCount && row <= maxRow; row++)
        {
            _darkRows.Add(row);
        }

        _darkRows.Remove(maxRow); // đảm bảo row đáy an toàn
    }

    private int GetMaxRowIndex()
    {
        int max = int.MinValue;
        foreach (var slot in _slots)
        {
            if (slot.coordinate.y > max)
                max = slot.coordinate.y;
        }
        return max;
    }

    /// <summary>
    /// Gọi mỗi frame: tăng decay cho item nằm trong Dark Row, bắn warning và transform.
    /// </summary>
    public void UpdateDecay(float deltaTime)
    {
        if (_slots.Count == 0) return;

        foreach (var slot in _slots)
        {
            if (!slot.IsOccupied)
                continue;

            int row = slot.coordinate.y;
            if (!_darkRows.Contains(row))
            {
                // Ở row an toàn: reset decay (hoặc có thể giảm dần tùy design)
                slot.item.ResetDecay();
                continue;
            }

            var inst = slot.item;
            inst.DecayTimer += deltaTime;

            // Warning
            if (!inst.HasDecayWarning &&
                inst.DecayTimer >= decayWarningThresholdSeconds &&
                inst.DecayTimer < decayThresholdSeconds)
            {
                inst.HasDecayWarning = true;
                OnItemDecayWarning?.Invoke(inst);
            }

            // Biến thành than
            if (inst.DecayTimer >= decayThresholdSeconds)
            {
                TransformToCharcoal(slot);
            }
        }
    }

    private void TransformToCharcoal(GridSlot slot)
    {
        if (charcoalItemData == null)
        {
            Debug.LogWarning("InventoryManager: Charcoal ItemData not set.");
            slot.item = null;
            return;
        }

        slot.item = new ItemInstance(charcoalItemData);
    }

    // --------------------------------------------------------------------
    // BATCH BURN – ECONOMY + PUSH EFFECTS
    // --------------------------------------------------------------------
    /// <summary>
    /// Tính lượng dầu nhận được khi đốt 1 batch item.
    /// </summary>
    public float CalculateBurnOil(IList<ItemInstance> itemsToBurn)
    {
        if (itemsToBurn == null || itemsToBurn.Count == 0) return 0f;

        float baseOilTotal = 0f;
        float totalPoints = 0f;

        foreach (var inst in itemsToBurn)
        {
            if (inst == null || inst.Data == null) continue;
            var data = inst.Data;

            baseOilTotal += data.baseOilOnBurn;

            // Equipment/Weapon = 1, Material = 0.2
            if (data.isMaterial)
            {
                totalPoints += 0.2f;
            }
            else
            {
                totalPoints += 1f;
            }
        }

        float comboMultiplier = 1f + totalPoints * 0.1f;
        float totalOil = baseOilTotal * comboMultiplier;

        return totalOil;
    }

    /// <summary>
    /// Được gọi khi người chơi chọn nhiều ô Storage và ấn nút Burn trong Inventory.
    /// </summary>
    public void BurnSelectedSlots(IList<GridSlot> slotsToBurn)
    {
        if (slotsToBurn == null || slotsToBurn.Count == 0) return;

        var instances = new List<ItemInstance>();
        foreach (var slot in slotsToBurn)
        {
            if (slot != null && slot.IsOccupied)
                instances.Add(slot.item);
        }

        if (instances.Count == 0) return;

        // 1) Tính dầu và cộng ngay lập tức
        float oilGain = CalculateBurnOil(instances);
        if (oilLamp != null && oilGain > 0f)
        {
            oilLamp.AddOil(oilGain);
        }

        // 2) Đưa ItemEffect vào queue ở PlayerItemSlots
        if (playerItemSlots != null)
        {
            playerItemSlots.ClearPendingBurnEffects();

            foreach (var inst in instances)
            {
                var data = inst.Data;
                if (data == null || data.effects == null) continue;

                foreach (var eff in data.effects)
                {
                    if (eff != null)
                        playerItemSlots.EnqueuePendingBurnEffect(eff);
                }
            }

            // UI có thể hiển thị "Effects Primed" ở đây nếu muốn
        }

        // 3) Xóa item khỏi storage grid
        foreach (var slot in slotsToBurn)
        {
            if (slot == null) continue;
            slot.item = null;
        }
    }

    // --------------------------------------------------------------------
    // EXTERNAL / GROUND GRID – INIT & CLEAR
    // --------------------------------------------------------------------
    private void InitExternalGrid()
    {
        _externalGrid = new ItemInstance[externalWidth, externalHeight];
    }

    public void ClearExternalGrid()
    {
        if (_externalGrid == null)
            InitExternalGrid();

        for (int y = 0; y < externalHeight; y++)
        {
            for (int x = 0; x < externalWidth; x++)
            {
                _externalGrid[x, y] = null;
            }
        }
    }

    // --------------------------------------------------------------------
    // OPEN / CLOSE INVENTORY (LOOTING vs DROPPING)
    // --------------------------------------------------------------------
    /// <summary>
    /// Mở Inventory.
    /// - source != null  => Looting mode (đang tương tác container).
    /// - source == null  => Dropping mode (mở túi bình thường).
    /// </summary>
    public void OpenInventory(ILootSource source = null)
    {
        if (_externalGrid == null)
            InitExternalGrid();

        _currentLootSource = source;
        _isLootMode = source != null;

        if (_isLootMode)
        {
            LoadFromLootSource(source);
        }
        else
        {
            ClearExternalGrid();
        }

        OnInventoryOpened?.Invoke(this);
        // Script khác sẽ lo Pause game + bật Canvas
    }

    /// <summary>
    /// Đóng Inventory.
    /// - Nếu Looting: trả đồ thừa về LootSource.
    /// - Nếu Dropping: drop tất cả đồ ở External Grid ra thế giới.
    /// </summary>
    public void CloseInventory()
    {
        if (_isLootMode)
        {
            SaveBackToLootSource();
        }
        else
        {
            DropItemsInExternalGrid();
        }

        ClearExternalGrid();
        _currentLootSource = null;
        _isLootMode = false;

        OnInventoryClosed?.Invoke(this);
        // Script khác sẽ lo Unpause game + tắt Canvas
    }

    private void LoadFromLootSource(ILootSource source)
    {
        ClearExternalGrid();

        if (source == null) return;
        var items = source.GetItems();
        if (items == null) return;

        int index = 0;
        foreach (var inst in items)
        {
            if (inst == null || inst.Data == null) continue;

            int x = index % externalWidth;
            int y = index / externalWidth;
            if (y >= externalHeight) break;

            _externalGrid[x, y] = inst;
            index++;
        }
    }

    private void SaveBackToLootSource()
    {
        if (_currentLootSource == null) return;

        var list = new List<ItemInstance>();

        foreach (var inst in EnumerateExternalGridItems())
        {
            if (inst != null && inst.Data != null)
                list.Add(inst);
        }

        _currentLootSource.SetItems(list);
    }

    private IEnumerable<ItemInstance> EnumerateExternalGridItems()
    {
        if (_externalGrid == null) yield break;

        for (int y = 0; y < externalHeight; y++)
        {
            for (int x = 0; x < externalWidth; x++)
            {
                var inst = _externalGrid[x, y];
                if (inst != null)
                    yield return inst;
            }
        }
    }

    private void DropItemsInExternalGrid()
    {
        if (_externalGrid == null) return;
        if (!itemPickupPrefab || !dropOrigin) return;

        foreach (var inst in EnumerateExternalGridItems())
        {
            if (inst == null || inst.Data == null) continue;

            var go = GameObject.Instantiate(
                itemPickupPrefab,
                dropOrigin.position,
                Quaternion.identity);

            // Giả sử prefab có script ItemPickup để nhận ItemData
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                pickup.Initialize(inst.Data);
            }
        }
    }
}
