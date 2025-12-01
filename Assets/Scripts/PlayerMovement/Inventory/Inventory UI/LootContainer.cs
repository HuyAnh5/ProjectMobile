using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container ch?a loot (r??ng, xác quái, thùng ??, v.v.).
/// Implement ILootSource ?? InventoryManager có th? load / save contents.
/// 
/// Cách s? d?ng:
/// - G?n script này lên prefab r??ng / xác quái.
/// - Thi?t l?p s?n contents trong Inspector (n?u mu?n).
/// - Khi player t??ng tác, g?i OpenLoot(inventoryController).
///   -> InventoryController.OpenInventoryFromLoot(this);
/// </summary>
public class LootContainer : MonoBehaviour, ILootSource
{
    [Header("Loot contents")]
    [Tooltip("Danh sách ItemInstance ?ang n?m trong container này.")]
    [SerializeField]
    private List<ItemInstance> contents = new List<ItemInstance>();

    [Header("Optional: auto-open khi player b?m phím g?n r??ng")]
    [SerializeField]
    private KeyCode interactKey = KeyCode.E;

    [Tooltip("Kho?ng cách t?i ?a ?? cho phép t??ng tác.")]
    [SerializeField]
    private float interactRadius = 1.5f;

    [Tooltip("Tham chi?u t?i InventoryController trong scene (n?u mu?n auto-open).")]
    [SerializeField]
    private InventoryController inventoryController;

    /// <summary>
    /// Tr? v? danh sách item hi?n có trong container (dùng b?i InventoryManager).
    /// </summary>
    public List<ItemInstance> GetItems()
    {
        return contents;
    }

    /// <summary>
    /// Gán l?i contents sau khi ng??i ch?i loot xong (dùng b?i InventoryManager).
    /// </summary>
    public void SetItems(List<ItemInstance> items)
    {
        contents = items ?? new List<ItemInstance>();
    }

    /// <summary>
    /// G?i hàm này khi player t??ng tác (t? script player, trigger, v.v.).
    /// </summary>
    public void OpenLoot(InventoryController controller)
    {
        if (controller == null) return;

        // M? Inventory ? Looting mode, l?y contents t? container này.
        controller.OpenInventoryFromLoot(this);
    }

    private void Awake()
    {
        if (inventoryController == null)
        {
            inventoryController = FindAnyObjectByType<InventoryController>();
        }
    }

    private void Update()
    {
        // Optional: auto-check player trong radius và b?m phím interact ?? m? r??ng.
        // N?u b?n ?ã có h? th?ng t??ng tác riêng thì có th? xóa toàn b? Update() này.
        if (inventoryController == null) return;
        if (!Input.GetKeyDown(interactKey)) return;

        var player = inventoryController.GetComponentInParent<Transform>();
        if (player == null) return;

        float dist = Vector2.Distance(player.position, transform.position);
        if (dist <= interactRadius)
        {
            OpenLoot(inventoryController);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // V? vòng tròn radius t??ng tác trong editor cho d? ch?nh
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
#endif
}
