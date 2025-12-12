using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

public class InventoryTetris : MonoBehaviour {

    public static InventoryTetris Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float cellSize = 50f;


    public event EventHandler<PlacedObject> OnObjectPlaced;

    [System.Serializable]
    public struct RowShape
    {
        [Tooltip("Cột bắt đầu (từ 0) của vùng còn dùng được trên hàng này")]
        public int startColumn;
        [Tooltip("Số ô liên tiếp còn dùng được trên hàng này")]
        public int slotCount;
    }

    [Header("Custom Shape")]
    [SerializeField] private bool useCustomShape = false;
    [SerializeField] private RowShape[] rowShapes;


    private Grid<GridObject> grid;
    private RectTransform itemContainer;


    private void Awake() {
        Instance = this;

        grid = new Grid<GridObject>(gridWidth, gridHeight, cellSize, new Vector3(0, 0, 0), (Grid<GridObject> g, int x, int y) => new GridObject(g, x, y));

        itemContainer = transform.Find("ItemContainer").GetComponent<RectTransform>();

        //transform.Find("BackgroundTempVisual").gameObject.SetActive(false);
    }

    public class GridObject {

        private Grid<GridObject> grid;
        private int x;
        private int y;
        public PlacedObject placedObject;

        public GridObject(Grid<GridObject> grid, int x, int y) {
            this.grid = grid;
            this.x = x;
            this.y = y;
            placedObject = null;
        }

        public override string ToString() {
            return x + ", " + y + "\n" + placedObject;
        }

        public void SetPlacedObject(PlacedObject placedObject) {
            this.placedObject = placedObject;
            grid.TriggerGridObjectChanged(x, y);
        }

        public void ClearPlacedObject() {
            placedObject = null;
            grid.TriggerGridObjectChanged(x, y);
        }

        public PlacedObject GetPlacedObject() {
            return placedObject;
        }

        public bool CanBuild() {
            return placedObject == null;
        }

        public bool HasPlacedObject() {
            return placedObject != null;
        }

    }

    public Grid<GridObject> GetGrid() {
        return grid;
    }

    public Vector2Int GetGridPosition(Vector3 worldPosition) {
        grid.GetXY(worldPosition, out int x, out int z);
        return new Vector2Int(x, z);
    }

    public bool IsValidGridPosition(Vector2Int gridPosition)
    {
        if (!grid.IsValidGridPosition(gridPosition)) return false;
        if (!IsCellEnabled(gridPosition)) return false;
        return true;
    }



    public bool TryPlaceItem(ItemTetrisSO itemTetrisSO, Vector2Int placedObjectOrigin, PlacedObjectTypeSO.Dir dir) {
        // Test Can Build
        List<Vector2Int> gridPositionList = itemTetrisSO.GetGridPositionList(placedObjectOrigin, dir);
        bool canPlace = true;
        foreach (Vector2Int gridPosition in gridPositionList) {
            bool isValidPosition = IsValidGridPosition(gridPosition);

            if (!isValidPosition) {
                // Not valid
                canPlace = false;
                break;
            }
            if (!grid.GetGridObject(gridPosition.x, gridPosition.y).CanBuild()) {
                canPlace = false;
                break;
            }
        }

        if (canPlace) {
            foreach (Vector2Int gridPosition in gridPositionList) {
                if (!grid.GetGridObject(gridPosition.x, gridPosition.y).CanBuild()) {
                    canPlace = false;
                    break;
                }
            }
        }

        if (canPlace) {
            Vector2Int rotationOffset = itemTetrisSO.GetRotationOffset(dir);
            Vector3 placedObjectWorldPosition = grid.GetWorldPosition(placedObjectOrigin.x, placedObjectOrigin.y) + new Vector3(rotationOffset.x, rotationOffset.y) * grid.GetCellSize();

            PlacedObject placedObject = PlacedObject.CreateCanvas(itemContainer, placedObjectWorldPosition, placedObjectOrigin, dir, itemTetrisSO);
            placedObject.transform.rotation = Quaternion.Euler(0, 0, -itemTetrisSO.GetRotationAngle(dir));

            placedObject.GetComponent<InventoryTetrisDragDrop>().Setup(this);

            foreach (Vector2Int gridPosition in gridPositionList) {
                grid.GetGridObject(gridPosition.x, gridPosition.y).SetPlacedObject(placedObject);
            }

            OnObjectPlaced?.Invoke(this, placedObject);

            // Object Placed!
            return true;
        } else {
            // Object CANNOT be placed!
            return false;
        }
    }

    public void RemoveItemAt(Vector2Int removeGridPosition) {
        PlacedObject placedObject = grid.GetGridObject(removeGridPosition.x, removeGridPosition.y).GetPlacedObject();

        if (placedObject != null) {
            // Demolish
            placedObject.DestroySelf();

            List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
            foreach (Vector2Int gridPosition in gridPositionList) {
                grid.GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
            }
        }
    }

    public RectTransform GetItemContainer() {
        return itemContainer;
    }



    [Serializable]
    public struct AddItemTetris {
        public string itemTetrisSOName;
        public Vector2Int gridPosition;
        public PlacedObjectTypeSO.Dir dir;
    }

    [Serializable]
    public struct ListAddItemTetris {
        public List<AddItemTetris> addItemTetrisList;
    }

    public string Save() {
        List<PlacedObject> placedObjectList = new List<PlacedObject>();
        for (int x = 0; x < grid.GetWidth(); x++) {
            for (int y = 0; y < grid.GetHeight(); y++) {
                if (grid.GetGridObject(x, y).HasPlacedObject()) {
                    placedObjectList.Remove(grid.GetGridObject(x, y).GetPlacedObject());
                    placedObjectList.Add(grid.GetGridObject(x, y).GetPlacedObject());
                }
            }
        }

        List<AddItemTetris> addItemTetrisList = new List<AddItemTetris>();
        foreach (PlacedObject placedObject in placedObjectList) {
            addItemTetrisList.Add(new AddItemTetris {
                dir = placedObject.GetDir(),
                gridPosition = placedObject.GetGridPosition(),
                itemTetrisSOName = (placedObject.GetPlacedObjectTypeSO() as ItemTetrisSO).name,
            });

        }

        return JsonUtility.ToJson(new ListAddItemTetris { addItemTetrisList = addItemTetrisList });
    }

    //public void Load(string loadString) {
    //    ListAddItemTetris listAddItemTetris = JsonUtility.FromJson<ListAddItemTetris>(loadString);

    //    foreach (AddItemTetris addItemTetris in listAddItemTetris.addItemTetrisList) {
    //        TryPlaceItem(InventoryTetrisAssets.Instance.GetItemTetrisSOFromName(addItemTetris.itemTetrisSOName), addItemTetris.gridPosition, addItemTetris.dir);
    //    }
    //}

    public void Load(string loadString)
    {
        var listAddItemTetris = JsonUtility.FromJson<ListAddItemTetris>(loadString);
        foreach (var addItemTetris in listAddItemTetris.addItemTetrisList)
        {
            var so = InventoryTetrisAssets.Instance.GetItemTetrisSOFromName(addItemTetris.itemTetrisSOName);
            if (so == null) continue;
            TryPlaceItem(so, addItemTetris.gridPosition, addItemTetris.dir);
        }
    }


    // Kiểm tra 1 ô (x,y) có được phép dùng theo shape hay không
    private bool IsCellEnabled(Vector2Int gridPosition)
    {
        if (!useCustomShape) return true;

        int height = grid.GetHeight();

        // Chuyển y (0 = dưới) thành index từ trên xuống
        int yFromTop = height - 1 - gridPosition.y;

        if (yFromTop < 0 || yFromTop >= rowShapes.Length) return false;

        RowShape row = rowShapes[yFromTop];
        if (row.slotCount <= 0) return false;

        return gridPosition.x >= row.startColumn &&
               gridPosition.x < row.startColumn + row.slotCount;
    }


    // DEBUG: kiểm tra 1 ô có thể đặt đồ hay không
    public bool CanPlaceOnCell(Vector2Int gridPosition)
    {
        // Hiện tại chỉ cần check trong biên grid.
        // Sau này bạn thêm custom shape thì chỉ cần sửa hàm này là debug tự đúng theo.
        return grid.IsValidGridPosition(gridPosition);
    }

    public void RemoveItem(PlacedObject placedObject)
    {
        if (placedObject == null) return;

        // Xoá reference khỏi grid
        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        foreach (Vector2Int gridPosition in gridPositionList)
        {
            grid.GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
        }

        // Destroy luôn GameObject của item
        placedObject.DestroySelf();
    }

}
