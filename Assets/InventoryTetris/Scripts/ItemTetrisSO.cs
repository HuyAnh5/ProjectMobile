using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu()]
public class ItemTetrisSO : PlacedObjectTypeSO
{

    [Header("Custom Shape (base = Dir.Down)")]
    [Tooltip("Khai báo shape cho hướng Down. (0,0) là ô dưới trái.")]
    public List<Vector2Int> localCells = new List<Vector2Int>();

    [Header("Category (for Active Loadout)")]
    [Tooltip("Tick nếu đây là vũ khí. Không tick = item thường.")]
    public bool isWeapon = false;

    [Tooltip("Sprite 1x1 hiển thị khi item ở trong Active Loadout slot.")]
    public Sprite loadoutSprite;

    public static void CreateVisualGrid(Transform visualParentTransform, ItemTetrisSO itemTetrisSO, float cellSize)
    {
        Transform visualTransform = Object.Instantiate(
            InventoryTetrisAssets.Instance.gridVisual,
            visualParentTransform
        );

        // Create background
        Transform template = visualTransform.Find("Template");
        template.gameObject.SetActive(false);

        for (int x = 0; x < itemTetrisSO.width; x++)
        {
            for (int y = 0; y < itemTetrisSO.height; y++)
            {
                Transform backgroundSingleTransform = Object.Instantiate(template, visualTransform);
                backgroundSingleTransform.gameObject.SetActive(true);
            }
        }

        GridLayoutGroup gridLayoutGroup = visualTransform.GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup != null)
        {
            gridLayoutGroup.cellSize = Vector2.one * cellSize;
        }

        RectTransform rt = visualTransform.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(itemTetrisSO.width, itemTetrisSO.height) * cellSize;
            rt.anchoredPosition = Vector2.zero;
        }

        visualTransform.SetAsFirstSibling();
    }

    //public static void CreateVisualGrid(Transform visualParentTransform, ItemTetrisSO itemTetrisSO, float cellSize)
    //{
    //    Transform visualTransform = Object.Instantiate(
    //        InventoryTetrisAssets.Instance.gridVisual,
    //        visualParentTransform
    //    );

    //    // Create background
    //    Transform template = visualTransform.Find("Template");
    //    template.gameObject.SetActive(false);

    //    for (int x = 0; x < itemTetrisSO.width; x++)
    //    {
    //        for (int y = 0; y < itemTetrisSO.height; y++)
    //        {
    //            Transform backgroundSingleTransform = Object.Instantiate(template, visualTransform);
    //            backgroundSingleTransform.gameObject.SetActive(true);

    //            // Nếu có gridSprite riêng cho item → gán vào ô
    //            var img = backgroundSingleTransform.GetComponent<UnityEngine.UI.Image>();
    //            if (img != null && itemTetrisSO.gridSprite != null)
    //            {
    //                img.sprite = itemTetrisSO.gridSprite;
    //                img.preserveAspect = true;
    //            }
    //        }
    //    }

    //    GridLayoutGroup gridLayoutGroup = visualTransform.GetComponent<GridLayoutGroup>();
    //    if (gridLayoutGroup != null)
    //    {
    //        gridLayoutGroup.cellSize = Vector2.one * cellSize;
    //    }

    //    RectTransform rt = visualTransform.GetComponent<RectTransform>();
    //    if (rt != null)
    //    {
    //        rt.sizeDelta = new Vector2(itemTetrisSO.width, itemTetrisSO.height) * cellSize;
    //        rt.anchoredPosition = Vector2.zero;
    //    }

    //    visualTransform.SetAsFirstSibling();
    //}


    private Vector2Int RotateLocalCell(Vector2Int cell, Dir dir)
    {
        switch (dir)
        {
            default:
            case Dir.Down:
                // Không xoay
                return cell;

            case Dir.Left:
                // Xoay 90° CCW (Down -> Left)
                return new Vector2Int(cell.y, -cell.x);

            case Dir.Up:
                // Xoay 180°
                return new Vector2Int(-cell.x, -cell.y);

            case Dir.Right:
                // Xoay 270° CCW (hoặc 90° CW)
                return new Vector2Int(-cell.y, cell.x);
        }
    }

    public override List<Vector2Int> GetGridPositionList(Vector2Int offset, Dir dir)
    {
        // Nếu không set custom shape thì fallback về logic rectangle cũ
        if (localCells == null || localCells.Count == 0)
        {
            return base.GetGridPositionList(offset, dir);
        }

        // 1) Xoay toàn bộ cell quanh gốc (0,0)
        List<Vector2Int> rotated = new List<Vector2Int>();
        foreach (var cell in localCells)
        {
            rotated.Add(RotateLocalCell(cell, dir));
        }

        // 2) Chuẩn hóa: dịch sao cho minX = 0, minY = 0 (tránh tọa độ âm)
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (var c in rotated)
        {
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
        }

        List<Vector2Int> result = new List<Vector2Int>();
        foreach (var c in rotated)
        {
            Vector2Int normalized = new Vector2Int(c.x - minX, c.y - minY);
            result.Add(offset + normalized);
        }

        return result;
    }

}
