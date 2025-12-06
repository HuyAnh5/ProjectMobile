using UnityEngine;
using System.Collections.Generic;

public class InventorySetup : MonoBehaviour
{

    [SerializeField] private InventoryTetris inventory; // Kéo PlayerInventory vào
    [SerializeField] private ItemTetrisSO swordData;    // Kéo SwordData vào

    void Start()
    {
        // Đợi 0.1s cho các script khác khởi tạo xong
        Invoke("AddTestItem", 0.1f);
    }

    void AddTestItem()
    {
        // Thêm kiếm vào vị trí (0,0)
        inventory.TryPlaceItem(swordData, new Vector2Int(0, 0), PlacedObjectTypeSO.Dir.Down);
        Debug.Log("Đã thêm kiếm vào kho!");
    }
}