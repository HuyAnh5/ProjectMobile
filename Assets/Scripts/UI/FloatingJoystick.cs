using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Joystick n?i: ?n m?c ??nh, ch? hi?n khi ch?m n?a trái màn hình.
/// - G?n lên UI Panel ph? full màn hình (JoystickArea).
/// - Hi?n th? trong "visualsRoot" (b?t/t?t), ??t Base/Knob t?i ?i?m ch?m.
/// - Xu?t Value ? [-1..1] theo h??ng/?? m?nh kéo.
/// </summary>
public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Refs (kéo trong Inspector)")]
    [SerializeField] private RectTransform visualsRoot;  // Nhóm hi?n th? (Visuals)
    [SerializeField] private RectTransform baseRect;     // ?nh n?n joystick (Base)
    [SerializeField] private RectTransform knobRect;     // Nút (Knob)
    [SerializeField] private Canvas canvas;              // Canvas cha (Screen Space - Overlay)

    [Header("Tuning")]
    [Tooltip("Bán kính kéo t?i ?a c?a Knob (pixel màn hình)")]
    [SerializeField] private float radius = 90f;
    [Tooltip("Ng??ng b? rung tay (0..1) tính theo kho?ng/kho?ng t?i ?a)")]
    [Range(0f, 1f)][SerializeField] private float deadzone = 0.12f;

    private const int NO_POINTER = int.MinValue;
    private int activePointerId = NO_POINTER;
    private Vector2 startScreenPos;     // v? trí ch?m ban ??u (pixel màn hình)
    private Vector2 value;              // -1..1

    /// <summary> Giá tr? chu?n hoá [-1..1] </summary>
    public Vector2 Value => value;

    private void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        ShowVisuals(false);
        value = Vector2.zero;
    }

    private void ShowVisuals(bool on)
    {
        if (visualsRoot && visualsRoot.gameObject.activeSelf != on)
            visualsRoot.gameObject.SetActive(on);
    }

    public void OnPointerDown(PointerEventData e)
    {
        // Ch? nh?n pointer ??u tiên ? N?A TRÁI màn hình
        if (activePointerId != NO_POINTER) return;
        if (e.position.x >= Screen.width * 0.5f) return; // b? n?a ph?i

        activePointerId = e.pointerId;
        startScreenPos = e.position;

        // Hi?n joystick t?i ?i?m ch?m
        ShowVisuals(true);
        if (baseRect) baseRect.position = startScreenPos;
        if (knobRect) knobRect.position = startScreenPos;

        value = Vector2.zero;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;

        Vector2 current = e.position;               // v? trí ngón tay (pixel)
        Vector2 delta = current - startScreenPos;   // vector t? tâm -> ngón tay

        // Clamp trong bán kính
        if (delta.magnitude > radius)
            delta = delta.normalized * radius;

        if (knobRect) knobRect.position = startScreenPos + delta;

        // Chu?n hoá [-1..1] tính theo bán kính
        Vector2 v = delta / Mathf.Max(1f, radius);
        float mag = v.magnitude;

        // Deadzone ch?ng rung
        value = (mag < deadzone) ? Vector2.zero : (mag > 1f ? v.normalized : v);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;

        activePointerId = NO_POINTER;
        value = Vector2.zero;
        ShowVisuals(false);
    }
}
