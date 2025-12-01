using System;
using UnityEngine;

/// <summary>
/// Một ô trong lưới inventory.
/// </summary>
[Serializable]
public class GridSlot
{
    public Vector2Int coordinate;    // (x,y) trong grid logic
    public bool isMasked;            // ô bị khóa – nếu sau này muốn tạo hình bầu đèn phức tạp
    public ItemInstance item;        // null nếu trống

    public bool IsOccupied => item != null && !item.IsEmpty;

    public GridSlot(Vector2Int coord)
    {
        coordinate = coord;
        isMasked = false;
    }

    public GridSlot(Vector2Int coord, bool masked)
    {
        coordinate = coord;
        isMasked = masked;
    }
}
