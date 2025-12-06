using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryTetrisBackground : MonoBehaviour {

    [SerializeField] private InventoryTetris inventoryTetris;

    private void Start()
    {
        Transform template = transform.Find("Template");
        template.gameObject.SetActive(false);

        int width = inventoryTetris.GetGrid().GetWidth();
        int height = inventoryTetris.GetGrid().GetHeight();

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                Transform bg = Instantiate(template, transform);
                bg.gameObject.SetActive(true);

                var img = bg.GetComponent<UnityEngine.UI.Image>();

                // Dùng chính IsValidGridPosition để quyết định ô sống/chết
                bool cellEnabled = inventoryTetris.IsValidGridPosition(new Vector2Int(x, y));

                if (!cellEnabled && img != null)
                {
                    img.enabled = false; // ô chết -> tắt ô xám, chỉ còn nền vàng
                }
            }
        }


        var grid = inventoryTetris.GetGrid();
        GetComponent<GridLayoutGroup>().cellSize =
            new Vector2(grid.GetCellSize(), grid.GetCellSize());
        GetComponent<RectTransform>().sizeDelta =
            new Vector2(grid.GetWidth(), grid.GetHeight()) * grid.GetCellSize();
        GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

    }

}