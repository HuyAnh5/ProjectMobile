using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Dash theo hướng vuốt, kích hoạt NGAY khi kéo vượt ngưỡng (không cần nhả tay).
/// GẮN LÊN Panel nửa phải (hoặc full), Panel cần nhận raycast.
/// </summary>
public class SwipeDashArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Dash target")]
    [SerializeField] private DashController dash;

    [Header("Khu vực kích hoạt")]
    [Tooltip("Chỉ nhận bắt đầu ở nửa phải màn hình")]
    [SerializeField] private bool rightHalfOnly = true;

    [Header("Phát hiện vuốt")]
    [SerializeField] private float minDistancePx = 70f;  // ngưỡng flick
    [SerializeField] private float maxDistancePx = 220f; // để scale quãng dash
    [SerializeField] private bool requireQuickFlick = true;
    [SerializeField] private float maxTime = 0.25f;      // thời gian tối đa cho 1 flick

    [Header("Scale quãng dash theo độ dài vuốt")]
    [SerializeField] private bool scaleDistance = true;
    [SerializeField] private float distanceAtMinSwipe = 3.0f;
    [SerializeField] private float distanceAtMaxSwipe = 4.2f;

    private const int NO_POINTER = int.MinValue;
    private int activePointerId = NO_POINTER;

    private Vector2 anchorPos;     // mốc hiện tại để đo delta
    private float anchorTime;    // thời gian bắt đầu segment hiện tại

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointerId != NO_POINTER) return;

        if (rightHalfOnly && e.position.x < Screen.width * 0.5f) return;

        activePointerId = e.pointerId;
        anchorPos = e.position;
        anchorTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;

        // Khoảng cách & thời gian kể từ mốc hiện tại
        Vector2 delta = e.position - anchorPos;
        float dist = delta.magnitude;
        float dt = Time.unscaledTime - anchorTime;

        // Nếu yêu cầu flick nhanh mà kéo quá lâu → reset mốc để chờ flick mới
        if (requireQuickFlick && dt > maxTime)
        {
            anchorPos = e.position;
            anchorTime = Time.unscaledTime;
            return;
        }

        if (dist < minDistancePx) return;            // chưa đủ ngưỡng
        if (requireQuickFlick && dt > maxTime) return; // quá chậm (đã reset ở trên)

        if (dash && dash.CanDash)
        {
            Vector2 dir = delta.normalized;

            float? customDistance = null;
            if (scaleDistance)
            {
                float t = Mathf.InverseLerp(minDistancePx, maxDistancePx, dist); // 0..1
                float d = Mathf.Lerp(distanceAtMinSwipe, distanceAtMaxSwipe, t);
                customDistance = d;
            }

            dash.TryStartDash(dir, customDistance);

            // Rất quan trọng: reset mốc NGAY sau khi dash
            // để có thể flick tiếp (không cần nhấc tay)
            anchorPos = e.position;
            anchorTime = Time.unscaledTime;
        }
        else
        {
            // Chưa sẵn sàng (cooldown/dầu): để “ăn” flick kế tiếp mượt,
            // ta cập nhật mốc theo thời gian, tránh giữ delta lớn quá lâu
            if (dt > 0.05f)
            {
                anchorPos = e.position;
                anchorTime = Time.unscaledTime;
            }
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;
        activePointerId = NO_POINTER;
    }
}
