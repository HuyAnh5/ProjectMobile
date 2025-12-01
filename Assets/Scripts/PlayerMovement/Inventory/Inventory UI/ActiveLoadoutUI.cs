using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị Active Loadout bên trái dựa trên PlayerItemSlots.
/// </summary>
public class ActiveLoadoutUI : MonoBehaviour
{
    [SerializeField] private PlayerItemSlots playerItemSlots;

    [System.Serializable]
    private class SlotUI
    {
        public Image icon;
        public Text idText;
    }

    [Header("Weapon slots (ví dụ 3 ô)")]
    [SerializeField] private SlotUI[] weaponSlots;

    [Header("Item slots (ví dụ 2 ô)")]
    [SerializeField] private SlotUI[] itemSlots;

    private void Awake()
    {
        if (playerItemSlots == null)
            playerItemSlots = FindAnyObjectByType<PlayerItemSlots>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (playerItemSlots == null) return;

        // Tuỳ bạn map: ở đây mình demo coi slots[] trong PlayerItemSlots là equip chung.
        // Bạn có thể chia: 0–2 = weapon, 3–4 = item, v.v.

        var allSlots = playerItemSlots.DebugGetAllItems();
        // -> bạn có thể tự thêm hàm public ItemData[] DebugGetAllItems() trong PlayerItemSlots để trả về mảng hiện tại.

        // Fill weapon
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            ItemData data = (allSlots != null && i < allSlots.Length) ? allSlots[i] : null;
            SetSlotUI(weaponSlots[i], data);
        }

        // Fill item
        for (int i = 0; i < itemSlots.Length; i++)
        {
            int idx = weaponSlots.Length + i;
            ItemData data = (allSlots != null && idx < allSlots.Length) ? allSlots[idx] : null;
            SetSlotUI(itemSlots[i], data);
        }
    }

    private void SetSlotUI(SlotUI ui, ItemData data)
    {
        if (ui == null) return;

        if (data == null)
        {
            if (ui.icon) { ui.icon.sprite = null; ui.icon.enabled = false; }
            if (ui.idText) ui.idText.text = "";
        }
        else
        {
            if (ui.icon) { ui.icon.sprite = data.icon; ui.icon.enabled = true; }
            if (ui.idText) ui.idText.text = data.id;
        }
    }
}
