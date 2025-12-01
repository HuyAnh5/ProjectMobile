using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Định nghĩa hình dáng lưới “bầu đèn” theo từng row.
/// Dùng cho InventoryManager để build các GridSlot.
/// </summary>
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
    }

    [SerializeField]
    private List<RowDefinition> rows = new List<RowDefinition>();

    public IReadOnlyList<RowDefinition> Rows => rows;

    private void OnValidate()
    {
        // Nếu chưa cấu hình, tạo mặc định pattern:
        // Row 0: 2 ô
        // Row 1: 4 ô
        // Row 2: 6 ô
        // Row 3: 6 ô
        // Row 4: 6 ô
        // Row 5: 4 ô
        if (rows == null || rows.Count == 0)
        {
            rows = new List<RowDefinition>
            {
                new RowDefinition { rowIndex = 0, slotCount = 2 },
                new RowDefinition { rowIndex = 1, slotCount = 4 },
                new RowDefinition { rowIndex = 2, slotCount = 6 },
                new RowDefinition { rowIndex = 3, slotCount = 6 },
                new RowDefinition { rowIndex = 4, slotCount = 6 },
                new RowDefinition { rowIndex = 5, slotCount = 4 }
            };
        }
    }
}
