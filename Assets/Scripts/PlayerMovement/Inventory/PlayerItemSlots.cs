using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerItemSlots : MonoBehaviour
{
    [Header("Core refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private OilLamp oilLamp;
    [SerializeField] private DashController dashController;

    [Header("Lights (optional)")]
    [SerializeField] private Light2D mainFovLight;

    [Header("Equipped items (2 slots for now)")]
    [SerializeField] private ItemData[] slots = new ItemData[2];

    // Bản snapshot runtime để biết slot nào đã được apply effect.
    // Dùng để detect khi bạn đổi slots[] trong Inspector lúc đang Play.
    private ItemData[] _appliedSlots;


    // Context cho các Effect s? d?ng
    public PlayerController Player => player;
    public OilLamp OilLamp => oilLamp;
    public DashController Dash => dashController;
    public Light2D MainFovLight => mainFovLight;

    private void Awake()
    {
        AutoWire();

        // Khởi tạo snapshot runtime
        _appliedSlots = new ItemData[slots.Length];

        // Nếu có assign sẵn item trong Inspector thì áp hiệu ứng luôn
        for (int i = 0; i < slots.Length; i++)
        {
            var item = slots[i];
            _appliedSlots[i] = item;

            if (item == null) continue;

            foreach (var eff in item.effects)
                if (eff != null) eff.OnEquip(this);
        }
    }


    private void AutoWire()
    {
        if (!player)
            player = FindAnyObjectByType<PlayerController>();

        if (!oilLamp)
            oilLamp = FindAnyObjectByType<OilLamp>();

        if (!dashController)
            dashController = FindAnyObjectByType<DashController>();

        if (!mainFovLight && oilLamp)
            mainFovLight = oilLamp.fovLight;
    }

    /// <summary>
    /// Equip m?t Item vào slot. Không có switch-case gì c?,
    /// ch? g?i OnUnequip c?a item c? và OnEquip c?a item m?i.
    /// </summary>
    public void Equip(int slotIndex, ItemData newItem)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        // 1) Un-equip item cũ
        var old = slots[slotIndex];
        if (old != null)
        {
            foreach (var eff in old.effects)
                if (eff != null) eff.OnUnequip(this);
        }

        // 2) Gán item mới
        slots[slotIndex] = newItem;

        // Cập nhật snapshot runtime
        if (_appliedSlots == null || _appliedSlots.Length != slots.Length)
            _appliedSlots = new ItemData[slots.Length];
        _appliedSlots[slotIndex] = newItem;

        // 3) Apply hiệu ứng item mới
        if (newItem != null)
        {
            foreach (var eff in newItem.effects)
                if (eff != null) eff.OnEquip(this);
        }
    }


    /// <summary>
    /// Burn item trong slot: g?i OnBurn, sau ?ó OnUnequip và clear slot.
    /// </summary>
    public void Burn(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var item = slots[slotIndex];
        if (item == null) return;

        // Burn
        foreach (var eff in item.effects)
            if (eff != null) eff.OnBurn(this);

        // Un-equip (để trả lại state gốc nếu effect có lưu)
        foreach (var eff in item.effects)
            if (eff != null) eff.OnUnequip(this);

        slots[slotIndex] = null;

        if (_appliedSlots != null && slotIndex >= 0 && slotIndex < _appliedSlots.Length)
            _appliedSlots[slotIndex] = null;
    }


    private void LateUpdate()
    {
        // Nếu chưa khởi tạo (trường hợp nào đó), tạo lại
        if (_appliedSlots == null || _appliedSlots.Length != slots.Length)
            _appliedSlots = new ItemData[slots.Length];

        // Nếu phát hiện slots[i] khác với cái đã apply → gọi Equip để apply effect
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != _appliedSlots[i])
            {
                // Gọi Equip sẽ:
                // - OnUnequip item cũ (nếu có)
                // - Gán item mới
                // - OnEquip effect mới
                Equip(i, slots[i]);
            }
        }
    }

    private readonly Queue<ItemEffect> _pendingBurnEffects = new Queue<ItemEffect>();

    /// <summary>
    /// Kiểm tra còn hiệu ứng Burn chờ hay không (cho UI nếu cần).
    /// </summary>
    public bool HasPendingBurnEffects => _pendingBurnEffects.Count > 0;

    /// <summary>
    /// Xóa toàn bộ queue (dùng khi cancel batch burn hoặc làm mới).
    /// </summary>
    public void ClearPendingBurnEffects()
    {
        _pendingBurnEffects.Clear();
    }

    /// <summary>
    /// Đẩy thêm 1 effect vào queue burn.
    /// Được gọi từ InventoryManager.BurnSelectedSlots().
    /// </summary>
    public void EnqueuePendingBurnEffect(ItemEffect effect)
    {
        if (effect != null)
            _pendingBurnEffects.Enqueue(effect);
    }

    /// <summary>
    /// Coroutine thực thi queue Burn sau khi Unpause:
    /// - Đợi 0.5s cho người chơi aim.
    /// - Mỗi hiệu ứng bắn cách nhau 0.2s (machine-gun pacing).
    /// </summary>
    public IEnumerator ExecuteBurnQueue()
    {
        if (_pendingBurnEffects.Count == 0)
            yield break;

        // Aiming window
        yield return new WaitForSeconds(0.5f);

        while (_pendingBurnEffects.Count > 0)
        {
            var effect = _pendingBurnEffects.Dequeue();
            if (effect != null)
            {
                // OnBurn sẽ đọc hướng từ PlayerItemSlots.Player.Facing
                effect.OnBurn(this);
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>
    /// Trả về toàn bộ ItemData đang được equip trong các slot của Player.
    /// Dùng cho ActiveLoadoutUI để hiển thị loadout bên trái.
    /// </summary>
    public ItemData[] DebugGetAllItems()
    {
        // Nếu không muốn cho code ngoài sửa trực tiếp mảng slots,
        // có thể clone ra mảng mới:
        if (slots == null)
            return System.Array.Empty<ItemData>();

        var copy = new ItemData[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            copy[i] = slots[i];
        return copy;
    }


    /// <summary>
    /// Cho ItemEffect m??n Coroutine mà không c?n bi?t ??n MonoBehaviour.
    /// </summary>
    public void RunCoroutine(System.Collections.IEnumerator routine)
    {
        if (routine != null)
            StartCoroutine(routine);
    }
}
