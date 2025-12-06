using UnityEngine;

public class InventoryDebugFill : MonoBehaviour
{
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private ItemData[] initialItems;

    private void Start()
    {
        if (inventoryManager == null) return;

        var slots = inventoryManager.Slots;
        if (slots == null) return;

        int index = 0;
        foreach (var slot in slots)
        {
            if (index >= initialItems.Length) break;
            var data = initialItems[index];
            if (data != null)
                slot.item = new ItemInstance(data);
            index++;
        }

        // Sau khi gán item thì bảo UI vẽ lại
        var ui = FindAnyObjectByType<InventoryUI>();
        if (ui != null)
            ui.RefreshAll();
    }
}
