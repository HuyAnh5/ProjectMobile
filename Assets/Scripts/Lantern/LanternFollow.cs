using UnityEngine;

/// <summary>
/// Điều khiển đèn lồng (Lantern) là 1 GameObject RIÊNG, không dính chặt Player:
/// - Vị trí đích = vị trí Player + offset (xoay theo hướng nhìn hiện tại).
/// - Di chuyển mượt (SmoothDamp) → có quán tính, trễ vừa phải như cầm đèn tay.
/// - Xoay mượt (SmoothDampAngle) → nón sáng không quay đột ngột.
/// 
/// Cách dùng:
/// - Kéo Player (Transform) vào "target", kéo PlayerController vào "player".
/// - Đặt forwardOffset = (0.9, 1.5) là vị trí "trước mặt" khi Player nhìn LÊN.
/// - Tinh chỉnh posSmoothTime / angleSmoothTime để điều khiển độ "trôi".
/// </summary>
public class LanternFollow : MonoBehaviour
{
    [Header("Tham chiếu")]
    [SerializeField] private Transform target;            // Player transform
    [SerializeField] private PlayerController player;     // để đọc Facing

    [Header("Offset cơ sở (khi nhìn LÊN)")]
    [SerializeField] private Vector2 forwardOffset = new Vector2(0.9f, 1.5f);

    [Header("Độ mượt (thời hằng)")]
    [Tooltip("Thời gian tiến gần 63% khoảng cách còn lại. Lớn hơn = trôi nhiều hơn.")]
    [SerializeField] private float posSmoothTime = 0.08f;
    [SerializeField] private float angleSmoothTime = 0.06f;

    [Header("Chống 'tụt lại' quá xa (tùy chọn)")]
    [SerializeField] private bool snapIfTooFar = true;
    [SerializeField] private float maxDistanceToGoal = 6f;

    // Biến nội bộ để SmoothDamp ghi nhớ "vận tốc ẩn"
    private Vector3 posVelocity;
    private float angleVelocity;

    private void Reset()
    {
        // Thử auto-link khi Add Component
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }
        if (player == null && target != null)
        {
            player = target.GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        if (target == null || player == null)
            return;

        // 1) Lấy hướng nhìn hiện tại; nếu chưa có thì mặc định lên trên
        Vector2 f = (player.Facing.sqrMagnitude > 0.0001f) ? player.Facing : Vector2.up;

        // 2) Đổi hướng → góc (độ). Góc nhìn theo trục X là 0°, trục Y+ là 90°
        float faceDeg = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;

        // 3) Hướng "nón sáng" mặc định coi là đang trỏ lên (Y+) → trừ 90° để khớp
        float desiredDeg = faceDeg - 90f;

        // 4) Xoay offset cơ sở (định nghĩa khi nhìn LÊN) theo hướng hiện tại
        //    Quaternion biểu diễn phép quay; nhân với vector để xoay vector.
        Quaternion rot = Quaternion.Euler(0f, 0f, desiredDeg);
        Vector3 off3 = new Vector3(forwardOffset.x, forwardOffset.y, 0f);
        Vector3 rotatedOffset = rot * off3;

        // 5) Tính điểm đích của Lantern trong thế giới
        Vector3 goalPos = target.position + rotatedOffset;

        // (Tùy chọn) Nếu vì lý do nào đó đèn "tụt" xa đột ngột (teleport, lag),
        // thì snap ngay về gần điểm đích, tránh trôi quá xa nhìn kỳ.
        if (snapIfTooFar)
        {
            float sq = (transform.position - goalPos).sqrMagnitude;
            if (sq > maxDistanceToGoal * maxDistanceToGoal)
            {
                transform.position = goalPos;
            }
        }

        // 6) Di chuyển mượt về goalPos (không dính cứng), tạo cảm giác "lủng lẳng"
        transform.position = Vector3.SmoothDamp(
            transform.position,        // vị trí hiện tại
            goalPos,                   // mục tiêu
            ref posVelocity,           // vận tốc ẩn (bị hàm cập nhật)
            posSmoothTime              // thời hằng mượt hóa (giây)
        );

        // 7) Xoay mượt về góc mong muốn (tránh quay giật, xử lý wrap 0..360)
        float smDeg = Mathf.SmoothDampAngle(
            transform.eulerAngles.z,   // góc hiện tại (độ)
            desiredDeg,                // góc đích (độ)
            ref angleVelocity,         // vận tốc góc ẩn
            angleSmoothTime            // thời hằng mượt hóa
        );
        transform.rotation = Quaternion.Euler(0f, 0f, smDeg);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Vẽ thử offset cơ sở khi nhìn LÊN để bạn hình dung trong Scene view
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 basePos = target.position + new Vector3(forwardOffset.x, forwardOffset.y, 0f);
            Gizmos.DrawLine(target.position, basePos);
            Gizmos.DrawSphere(basePos, 0.06f);
        }
    }
#endif
}
