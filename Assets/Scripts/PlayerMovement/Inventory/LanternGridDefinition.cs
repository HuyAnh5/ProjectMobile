using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Định nghĩa hình dáng lưới “bầu đèn” theo từng row.
/// Dùng cho InventoryManager để build các GridSlot.
/// </summary>
/// 
[CreateAssetMenu(
    fileName = "LanternGridDefinition",
    menuName = "Inventory/Lantern Grid Definition")]
public class LanternGridDefinition : ScriptableObject
{
    [Serializable]
    public class RowDefinition
    {
        [Tooltip("Chỉ số row (y) trong lưới, 0 = trên cùng.")]
        public int rowIndex;

        [Tooltip("Số ô sử dụng trong row này.")]
        public int slotCount;

        [Tooltip("Cột bắt đầu của row trong lưới tổng (0 = cột ngoài cùng bên trái).")]
        public int startColumn;
    }

    [SerializeField]
    private List<RowDefinition> rows = new List<RowDefinition>();

    public IReadOnlyList<RowDefinition> Rows => rows;

    /// <summary>Tổng số cột của lưới = max(startColumn + slotCount).</summary>
    public int GridWidth
    {
        get
        {
            int max = 0;
            foreach (var r in rows)
            {
                int right = r.startColumn + r.slotCount;
                if (right > max) max = right;
            }
            return max;
        }
    }

    /// <summary>Tổng số hàng (max(rowIndex)+1).</summary>
    public int GridHeight
    {
        get
        {
            int maxRow = -1;
            foreach (var r in rows)
            {
                if (r.rowIndex > maxRow)
                    maxRow = r.rowIndex;
            }
            return maxRow + 1;
        }
    }

    private void OnValidate()
    {
        // Nếu chưa cấu hình, tạo mặc định pattern bầu đèn 6 hàng:
        // Row 0:       [ ][ ]
        // Row 1:    [ ][ ][ ][ ]
        // Row 2: [ ][ ][ ][ ][ ][ ]
        // Row 3: [ ][ ][ ][ ][ ][ ]
        // Row 4: [ ][ ][ ][ ][ ][ ]
        // Row 5:    [ ][ ][ ][ ]
        if (rows == null || rows.Count == 0)
        {
            rows = new List<RowDefinition>
            {
                new RowDefinition { rowIndex = 0, slotCount = 2, startColumn = 2 },
                new RowDefinition { rowIndex = 1, slotCount = 4, startColumn = 1 },
                new RowDefinition { rowIndex = 2, slotCount = 6, startColumn = 0 },
                new RowDefinition { rowIndex = 3, slotCount = 6, startColumn = 0 },
                new RowDefinition { rowIndex = 4, slotCount = 6, startColumn = 0 },
                new RowDefinition { rowIndex = 5, slotCount = 4, startColumn = 1 },
            };
        }
    }
}
