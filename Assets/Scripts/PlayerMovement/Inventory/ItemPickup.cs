using UnityEngine;

/// <summary>
/// Đối tượng item rơi ngoài world.
/// Hiện tại chỉ giữ ItemData; logic nhặt sẽ thêm sau.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemData;

    /// <summary>Gọi khi spawn từ InventoryManager.DropItemsInExternalGrid.</summary>
    public void Initialize(ItemData data)
    {
        itemData = data;
        // TODO: cập nhật sprite, scale, v.v. nếu cần.
    }

    // Sau này bạn có thể thêm:
    // private void OnTriggerEnter2D(Collider2D other)
    // {
    //     // Nếu other là Player -> add item vào InventoryManager/PlayerItemSlots rồi Destroy(gameObject)
    // }
}
