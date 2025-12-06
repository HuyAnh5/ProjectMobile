using UnityEngine;

public class InventoryTetrisTestSpawner : MonoBehaviour
{
    [SerializeField] private InventoryTetris inventoryA;
    [SerializeField] private InventoryTetris inventoryB;

    private void Start()
    {
        var assets = InventoryTetrisAssets.Instance;

        // Spawn trên b?ng A
        inventoryA.TryPlaceItem(assets.ammo, new Vector2Int(1, 3), PlacedObjectTypeSO.Dir.Down);
        inventoryA.TryPlaceItem(assets.medkit, new Vector2Int(1, 0), PlacedObjectTypeSO.Dir.Down);

        // Spawn trên b?ng B
        inventoryB.TryPlaceItem(assets.pistol, new Vector2Int(0, 0), PlacedObjectTypeSO.Dir.Down);
        inventoryB.TryPlaceItem(assets.shotgun, new Vector2Int(2, 0), PlacedObjectTypeSO.Dir.Down);
    }
}
