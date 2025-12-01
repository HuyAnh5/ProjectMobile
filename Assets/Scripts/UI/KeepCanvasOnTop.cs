using UnityEngine;
[RequireComponent(typeof(Canvas))]
public class KeepCanvasOnTop : MonoBehaviour
{
    public int sortingOrder = 5000;
    void Awake()
    {
        var c = GetComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = sortingOrder;
    }
}
