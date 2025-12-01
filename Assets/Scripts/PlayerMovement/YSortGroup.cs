using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(SortingGroup))]
public class YSortGroup : MonoBehaviour
{
    [Tooltip("Dương = nâng object lên trước, âm = đẩy ra sau")]
    public int orderOffset = 0;
    [Tooltip("Độ nhạy theo trục Y (100–1000 tùy map)")]
    public float pixelsPerUnit = 100f;

    SortingGroup sg;

    void Awake() => sg = GetComponent<SortingGroup>();

    void LateUpdate()
    {
        // Y thấp hơn (gần camera hơn) => sortingOrder lớn hơn (vẽ đè lên)
        int order = orderOffset - Mathf.RoundToInt(transform.position.y * pixelsPerUnit);
        sg.sortingOrder = order;
    }
}
