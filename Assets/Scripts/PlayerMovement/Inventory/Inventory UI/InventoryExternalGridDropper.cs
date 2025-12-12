using System.Collections.Generic;
using UnityEngine;

public class InventoryExternalGridDropper : MonoBehaviour
{
    [SerializeField] private InventoryTetris externalInventory;

    public void DropAll()
    {
        if (externalInventory == null) return;

        var grid = externalInventory.GetGrid();
        var placed = new List<PlacedObject>();

        for (int x = 0; x < grid.GetWidth(); x++)
            for (int y = 0; y < grid.GetHeight(); y++)
            {
                var obj = grid.GetGridObject(x, y);
                if (obj != null && obj.HasPlacedObject())
                {
                    var po = obj.GetPlacedObject();
                    if (po != null && !placed.Contains(po))
                        placed.Add(po);
                }
            }

        foreach (var po in placed)
        {
            var itemSO = po.GetPlacedObjectTypeSO() as ItemTetrisSO;
            Debug.Log($"[ExternalDrop] B? l?i: {(itemSO ? itemSO.name : "Unknown")}");
            externalInventory.RemoveItem(po); // t?m th?i destroy
            // TODO: sau này spawn loot ngoài map ? ?ây
        }
    }
}
