using System.Collections.Generic;
using UnityEngine;

public class ExternalLootSession : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventoryTetris externalInventory;      // UI ExternalGrid
    [SerializeField] private LootContainer dropContainerPrefab;      // Prefab hộp rơi
    [SerializeField] private Transform playerTransform;              // Player để spawn dưới chân

    [Header("Merge into nearest LootBox")]
    [SerializeField] private bool mergeIntoNearest = true;
    [SerializeField] private float mergeRadius = 1.0f;
    [SerializeField] private LayerMask mergeMask = ~0; // Khuyên: set chỉ layer LootDrop

    private LootContainer currentSource; // container đang mở (nếu có)

    public void OpenEmpty()
    {
        CommitExternal();
        currentSource = null;
        ClearExternal();
    }

    public void OpenFromSource(LootContainer source)
    {
        CommitExternal();
        currentSource = source;
        ClearExternal();

        if (currentSource != null)
            currentSource.LoadToExternal(externalInventory);
    }

    public void CommitExternal()
    {
        if (externalInventory == null) return;

        if (currentSource != null)
        {
            currentSource.SaveFromExternal(externalInventory);

            if (currentSource.DestroyWhenEmpty && currentSource.IsEmpty())
                Destroy(currentSource.gameObject);
        }
        else
        {
            if (HasAnyItems(externalInventory))
                DropOrMergeExternalLoot();
        }

        ClearExternal();
        currentSource = null;
    }

    private void DropOrMergeExternalLoot()
    {
        if (dropContainerPrefab == null || playerTransform == null) return;

        string addJson = externalInventory.Save();
        if (GetItemCountFromSave(addJson) <= 0) return;

        if (mergeIntoNearest)
        {
            LootContainer nearest = FindNearestLootContainer(playerTransform.position, mergeRadius);
            if (nearest != null)
            {
                if (TryMergeLootInto(nearest, addJson))
                    return; // merge thành công -> không spawn hộp mới
            }
        }

        // Không có hộp gần / merge fail -> spawn hộp mới
        var box = Instantiate(dropContainerPrefab, playerTransform.position, Quaternion.identity);
        box.SetLootJson(addJson);
    }

    private LootContainer FindNearestLootContainer(Vector3 center, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, mergeMask);
        LootContainer best = null;
        float bestSqr = float.MaxValue;

        foreach (var h in hits)
        {
            if (h == null) continue;
            var c = h.GetComponentInParent<LootContainer>();
            if (c == null) continue;

            float sqr = (c.transform.position - center).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = c;
            }
        }

        return best;
    }

    private bool TryMergeLootInto(LootContainer target, string addJson)
    {
        if (target == null) return false;
        if (externalInventory == null) return false;

        string targetJson = target.GetLootJson();
        if (string.IsNullOrEmpty(targetJson))
        {
            target.SetLootJson(addJson);
            return true;
        }

        // Dùng externalInventory làm "scratch" để merge
        ClearExternal();
        externalInventory.Load(targetJson);

        var addList = JsonUtility.FromJson<InventoryTetris.ListAddItemTetris>(addJson);
        if (addList.addItemTetrisList == null || addList.addItemTetrisList.Count == 0)
        {
            target.SetLootJson(externalInventory.Save());
            return true;
        }

        foreach (var entry in addList.addItemTetrisList)
        {
            var so = InventoryTetrisAssets.Instance.GetItemTetrisSOFromName(entry.itemTetrisSOName);
            if (so == null) continue;

            // ưu tiên đặt đúng vị trí cũ
            if (externalInventory.TryPlaceItem(so, entry.gridPosition, entry.dir))
                continue;

            // không đặt được -> auto pack vào chỗ trống bất kỳ (thử nhiều hướng)
            if (!TryAutoPlace(externalInventory, so, entry.dir))
                return false; // hộp đầy -> báo merge fail để spawn hộp mới
        }

        target.SetLootJson(externalInventory.Save());
        return true;
    }

    private bool TryAutoPlace(InventoryTetris inv, ItemTetrisSO so, PlacedObjectTypeSO.Dir preferredDir)
    {
        var g = inv.GetGrid();
        var dirs = BuildDirTryOrder(preferredDir);

        foreach (var dir in dirs)
        {
            for (int y = 0; y < g.GetHeight(); y++)
                for (int x = 0; x < g.GetWidth(); x++)
                {
                    if (inv.TryPlaceItem(so, new Vector2Int(x, y), dir))
                        return true;
                }
        }

        return false;
    }

    private List<PlacedObjectTypeSO.Dir> BuildDirTryOrder(PlacedObjectTypeSO.Dir preferred)
    {
        var list = new List<PlacedObjectTypeSO.Dir>(4) { preferred };
        void AddIfNot(PlacedObjectTypeSO.Dir d) { if (!list.Contains(d)) list.Add(d); }

        AddIfNot(PlacedObjectTypeSO.Dir.Down);
        AddIfNot(PlacedObjectTypeSO.Dir.Right);
        AddIfNot(PlacedObjectTypeSO.Dir.Up);
        AddIfNot(PlacedObjectTypeSO.Dir.Left);

        return list;
    }

    private bool HasAnyItems(InventoryTetris inv)
    {
        var g = inv.GetGrid();
        for (int x = 0; x < g.GetWidth(); x++)
            for (int y = 0; y < g.GetHeight(); y++)
            {
                var obj = g.GetGridObject(x, y);
                if (obj != null && obj.HasPlacedObject()) return true;
            }
        return false;
    }

    private void ClearExternal()
    {
        if (externalInventory == null) return;

        var g = externalInventory.GetGrid();
        for (int x = 0; x < g.GetWidth(); x++)
            for (int y = 0; y < g.GetHeight(); y++)
                externalInventory.RemoveItemAt(new Vector2Int(x, y));
    }

    private int GetItemCountFromSave(string json)
    {
        if (string.IsNullOrEmpty(json)) return 0;
        var list = JsonUtility.FromJson<InventoryTetris.ListAddItemTetris>(json);
        return list.addItemTetrisList != null ? list.addItemTetrisList.Count : 0;
    }
}
