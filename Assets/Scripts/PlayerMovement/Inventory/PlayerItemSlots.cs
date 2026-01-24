using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hub trung tâm cho tất cả ItemEffect:
/// - Giữ danh sách slot item đang equip (Active Loadout bên trái).
/// - Cung cấp truy cập tới Player / OilLamp / Dash / Ranged / Melee / Health / MainFovLight
///   để ItemEffect có thể chỉnh stat.
/// - Quản lý hàng chờ BurnEffect (pendingBurnEffects) để nổ sau khi đóng Inventory.
/// </summary>
public class PlayerItemSlots : MonoBehaviour
{
    #region Nested types

    [System.Serializable]
    public class ItemSlot
    {
        [Tooltip("Item đang được equip ở slot này (ScriptableObject).")]
        public ItemData item;
    }

    #endregion

    #region Inspector

    [Header("Core refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private OilLamp oilLamp;
    [SerializeField] private DashController dashController;
    [SerializeField] private AutoAttackRunner autoAttackRunner;
    [SerializeField] private MeleeAutoRunner meleeAutoRunner;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Lights (optional)")]
    [SerializeField] private Light2D mainFovLight;

    [Header("Equipped items (Active Loadout slots)")]
    [SerializeField] private ItemSlot[] slots = new ItemSlot[2];

    #endregion

    #region Public accessors (cho ItemEffect dùng)

    public PlayerController Player => player;
    public OilLamp OilLamp => oilLamp;
    public DashController Dash => dashController;
    public AutoAttackRunner RangedRunner => autoAttackRunner;
    public MeleeAutoRunner MeleeRunner => meleeAutoRunner;
    public PlayerHealth Health => playerHealth;
    public Light2D MainFovLight => mainFovLight;

    public IReadOnlyList<ItemSlot> Slots => slots;

    #endregion

    #region Burn queue

    /// <summary>
    /// Hàng chờ các ItemEffect sẽ OnBurn sau khi đóng Inventory.
    /// </summary>
    private readonly Queue<ItemEffect> _pendingBurnEffects = new Queue<ItemEffect>();

    [Header("Burn queue timing")]
    [Tooltip("Delay after closing inventory before the first burn effect fires.")]
    [Min(0f)][SerializeField] private float burnStartDelay = 0.5f;

    [Tooltip("Delay between burn effects. Spec: ~1.5s (effects are sequential, not stacked).")]
    [Min(0f)][SerializeField] private float burnBetweenEffectsDelay = 1.5f;

    public bool HasPendingBurnEffects => _pendingBurnEffects.Count > 0;

    /// <summary>
    /// Được InventoryManager gọi khi đốt đồ (Batch Burn).
    /// Chỉ enqueue, chưa thực thi ngay.
    /// </summary>
    public void EnqueuePendingBurnEffect(ItemEffect effect)
    {
        if (effect == null) return;
        _pendingBurnEffects.Enqueue(effect);
    }

    /// <summary>
    /// Xoá sạch hàng chờ Burn (nếu cần).
    /// </summary>
    public void ClearPendingBurnEffects()
    {
        _pendingBurnEffects.Clear();
    }

    /// <summary>
    /// Coroutine thực thi lần lượt các hiệu ứng Burn sau khi đóng Inventory.
    /// - Delay đầu (burnStartDelay) cho player chỉnh hướng.
    /// - Mỗi hiệu ứng chạy tuần tự (không chồng), cách nhau burnBetweenEffectsDelay.
    /// </summary>
    public IEnumerator ExecuteBurnQueue()
    {
        if (_pendingBurnEffects.Count == 0)
            yield break;

        // Aiming window (after inventory closes)
        if (burnStartDelay > 0f)
            yield return new WaitForSeconds(burnStartDelay);

        while (_pendingBurnEffects.Count > 0)
        {
            var eff = _pendingBurnEffects.Dequeue();
            if (eff != null)
            {
                eff.OnBurn(this);
            }

            if (_pendingBurnEffects.Count > 0 && burnBetweenEffectsDelay > 0f)
                yield return new WaitForSeconds(burnBetweenEffectsDelay);
        }
    }

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        // Auto-wire một phần nếu quên gán trong Inspector
        if (player == null) player = GetComponent<PlayerController>();
        if (oilLamp == null) oilLamp = FindAnyObjectByType<OilLamp>();
        if (dashController == null) dashController = GetComponent<DashController>();
        if (autoAttackRunner == null) autoAttackRunner = GetComponentInChildren<AutoAttackRunner>();
        if (meleeAutoRunner == null) meleeAutoRunner = GetComponentInChildren<MeleeAutoRunner>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        // Khi scene load, áp dụng lại tất cả item đã gán sẵn trong slot.
        ReapplyAllEquipped();
    }

    private void OnDisable()
    {
        // Khi object disable, trả lại toàn bộ stat gốc.
        UnequipAllSilently();
    }

    #endregion

    #region Equip / Unequip API

    /// <summary>
    /// Dùng khi bạn muốn equip runtime (nhặt đồ, đổi loadout).
    /// Gọi đúng OnUnequip của item cũ và OnEquip của item mới.
    /// </summary>
    public void EquipRuntime(int slotIndex, ItemData newItem)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return;

        var slot = slots[slotIndex];
        if (slot == null)
        {
            slot = new ItemSlot();
            slots[slotIndex] = slot;
        }

        var oldItem = slot.item;
        if (oldItem == newItem)
            return;

        // 1) OnUnequip item cũ
        if (oldItem != null && oldItem.effects != null)
        {
            foreach (var eff in oldItem.effects)
            {
                if (eff != null) eff.OnUnequip(this);
            }
        }

        // 2) Gán item mới
        slot.item = newItem;

        // 3) OnEquip item mới
        if (newItem != null && newItem.effects != null)
        {
            foreach (var eff in newItem.effects)
            {
                if (eff != null) eff.OnEquip(this);
            }
        }
    }

    /// <summary>
    /// Tháo item khỏi slot, gọi OnUnequip nếu có.
    /// </summary>
    public void UnequipSlot(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return;

        var slot = slots[slotIndex];
        if (slot == null || slot.item == null)
            return;

        var oldItem = slot.item;
        if (oldItem.effects != null)
        {
            foreach (var eff in oldItem.effects)
            {
                if (eff != null) eff.OnUnequip(this);
            }
        }

        slot.item = null;
    }

    /// <summary>
    /// Equip lại tất cả item đang gán sẵn trong Inspector (dùng khi start scene).
    /// </summary>
    private void ReapplyAllEquipped()
    {
        if (slots == null) return;

        foreach (var slot in slots)
        {
            if (slot == null || slot.item == null) continue;

            var item = slot.item;
            if (item.effects == null) continue;

            foreach (var eff in item.effects)
            {
                if (eff != null) eff.OnEquip(this);
            }
        }
    }

    /// <summary>
    /// UnEquip tất cả item nhưng không xoá khỏi slot (dùng khi OnDisable).
    /// </summary>
    private void UnequipAllSilently()
    {
        if (slots == null) return;

        foreach (var slot in slots)
        {
            if (slot == null || slot.item == null) continue;

            var item = slot.item;
            if (item.effects == null) continue;

            foreach (var eff in item.effects)
            {
                if (eff != null) eff.OnUnequip(this);
            }
        }
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Cho phép ItemEffect chạy coroutine thông qua PlayerItemSlots.
    /// Ví dụ: target.RunCoroutine(...).
    /// </summary>
    public Coroutine RunCoroutine(IEnumerator routine)
    {
        if (routine == null) return null;
        return StartCoroutine(routine);
    }

    /// <summary>
    /// Lấy ItemData đang được equip ở slot (có thể null).
    /// </summary>
    public ItemData GetEquippedItem(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return null;

        return slots[slotIndex]?.item;
    }

    #endregion
}
