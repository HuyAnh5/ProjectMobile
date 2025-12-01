using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Giữ ngón ở nửa PHẢI để bật lock-on (mobile friendly, New Input System friendly).
/// - KHÔNG dùng Input.GetTouch; chỉ dùng UI pointer events nên chạy trên cả New Input System.
/// - Nếu kéo vượt moveTolerancePx → hủy chờ (không cản gesture dash).
/// - Tự thêm Image trong suốt nếu thiếu Graphic để nhận raycast.
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class LockOnGestureRight : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Refs")]
    [SerializeField] private LockOnController lockOn;   // kéo từ Player

    [Header("Hold to lock")]
    [SerializeField, Tooltip("Thời gian giữ để bật lock (giây)")]
    private float holdTime = 0.18f;

    [SerializeField, Tooltip("Cho phép ngón tay dịch chuyển (px) trước khi coi là swipe/dash và hủy chờ")]
    private float moveTolerancePx = 18f;

    [SerializeField, Tooltip("Chỉ nhận ở nửa phải màn hình")]
    private bool rightHalfOnly = true;

    // --- runtime
    const int NO_POINTER = int.MinValue;
    int activeId = NO_POINTER;
    Vector2 startPos;
    float startT;
    bool locking;                // đã bật lock-on chưa
    bool canceledByMove;         // bị kéo quá tolerance
    bool pointerDown;
    Coroutine holdCo;

    void Awake()
    {
        // Đảm bảo có Graphic để nhận raycast
        var g = GetComponent<Graphic>();
        if (!g)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.003f);
            img.raycastTarget = true;
        }

        if (!lockOn)
#if UNITY_2023_1_OR_NEWER
            lockOn = Object.FindFirstObjectByType<LockOnController>(FindObjectsInactive.Include);
#else
            lockOn = FindObjectOfType<LockOnController>();
#endif
    }

    void OnDisable() => CancelAll();
    void OnApplicationFocus(bool focus) { if (!focus) CancelAll(); }
    void OnApplicationPause(bool pause) { if (pause) CancelAll(); }

    public void OnPointerDown(PointerEventData e)
    {
        // Lọc nửa phải nếu cần
        if (rightHalfOnly && e.position.x < Screen.width * 0.5f) return;

        // Nếu đã theo dõi pointer khác thì bỏ qua
        if (pointerDown) return;

        pointerDown = true;
        activeId = e.pointerId;
        startPos = e.position;
        startT = Time.unscaledTime;
        canceledByMove = false;
        locking = false;

        if (holdCo != null) StopCoroutine(holdCo);
        holdCo = StartCoroutine(HoldCheck());
    }

    public void OnDrag(PointerEventData e)
    {
        if (!pointerDown || e.pointerId != activeId) return;

        // Nếu người chơi bắt đầu swipe/dash → hủy chờ lock để không cản gesture khác
        if ((e.position - startPos).magnitude > moveTolerancePx)
        {
            canceledByMove = true;
            CancelPendingHoldOnly();
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!pointerDown || e.pointerId != activeId) return;

        // Nếu đã bật lock khi thả ngón → tắt giữ
        if (locking && lockOn) lockOn.EndHold();

        CancelAll();
    }

    System.Collections.IEnumerator HoldCheck()
    {
        // Chỉ đợi theo thời gian; không đọc Input.GetTouch
        while (pointerDown && !canceledByMove && !locking)
        {
            float dt = Time.unscaledTime - startT;
            if (dt >= holdTime)
            {
                locking = true;
                if (lockOn) lockOn.BeginHold();   // BẬT lock-on (giữ)
                yield break;
            }
            yield return null;
        }
    }

    void CancelPendingHoldOnly()
    {
        if (holdCo != null) { StopCoroutine(holdCo); holdCo = null; }
        // không reset pointerDown để tiếp tục nhận Up và không bị kẹt
    }

    void CancelAll()
    {
        if (holdCo != null) { StopCoroutine(holdCo); holdCo = null; }
        if (locking && lockOn) lockOn.EndHold(); // dọn trạng thái nếu đang giữ
        locking = false;
        pointerDown = false;
        canceledByMove = false;
        activeId = NO_POINTER;
    }
}
