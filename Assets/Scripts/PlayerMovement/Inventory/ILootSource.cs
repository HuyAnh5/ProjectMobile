using System.Collections.Generic;

/// <summary>
/// Nguồn loot bên ngoài (rương, xác quái, v.v.).
/// Dùng cho InventoryManager.OpenInventory(...) ở chế độ Looting.
/// </summary>
public interface ILootSource
{
    /// <summary>Danh sách item hiện đang nằm trong container.</summary>
    List<ItemInstance> GetItems();

    /// <summary>Cập nhật lại contents của container sau khi người chơi loot xong.</summary>
    void SetItems(List<ItemInstance> items);
}
