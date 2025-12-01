using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileDebugGizmo : MonoBehaviour
{
    [Header("Predicted path (từ vận tốc hiện tại)")]
    public bool enablePredicted = true;
    [Tooltip("Độ dài dự đoán (giây). Nên đặt ≈ projectileLifetime để thấy cả đường thẳng.")]
    public float predictedSeconds = 1.5f;
    public Color predictedColor = new Color(0f, 1f, 1f, 0.85f); // cyan

    [Header("Breadcrumb trail (dấu bánh mì)")]
    public bool enableTrail = true;
    [Tooltip("Số điểm tối đa lưu lại trên đường bay thực tế")]
    public int maxTrailPoints = 64;
    [Tooltip("Chu kỳ lấy mẫu vị trí (giây). 0.03 ≈ 30Hz")]
    public float sampleEvery = 0.033f;
    public Color trailColor = new Color(1f, 0.9f, 0.2f, 0.9f);  // vàng
    public float pointRadius = 0.04f;

    [Header("Vẽ khi nào")]
    [Tooltip("Chỉ vẽ khi chọn object (đỡ rối Scene)")]
    public bool onlyWhenSelected = false;
    [Tooltip("Kẻ line realtime bằng Debug.DrawLine trong Game View")]
    public bool alsoDebugDrawInGame = true;

    private Rigidbody2D rb;
    private readonly List<Vector3> pts = new List<Vector3>();
    private float sampleTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        pts.Clear();
        sampleTimer = 0f;
    }

    void Update()
    {
        if (!Application.isPlaying || !enableTrail) return;

        sampleTimer -= Time.deltaTime;
        if (sampleTimer <= 0f)
        {
            pts.Add(transform.position);
            if (pts.Count > maxTrailPoints) pts.RemoveAt(0);
            sampleTimer = Mathf.Max(0.001f, sampleEvery);
        }

        // Vẽ đường ngay trong Game view (tắt nếu rối)
        if (alsoDebugDrawInGame && pts.Count >= 2)
        {
            var a = pts[pts.Count - 2];
            var b = pts[pts.Count - 1];
            Debug.DrawLine(a, b, trailColor, 0f, false);
        }
    }

    void OnDrawGizmos()
    {
        if (onlyWhenSelected) return;
        DrawGizmosInternal();
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmosInternal();
    }

    void DrawGizmosInternal()
    {
        if (!isActiveAndEnabled) return;

        // 1) Đường dự đoán theo vận tốc hiện tại (thẳng nếu không có homing/lực)
        if (enablePredicted && rb)
        {
            Gizmos.color = predictedColor;
            Vector3 v = rb.linearVelocity;
            Vector3 start = transform.position;
            Vector3 end = start + v * Mathf.Max(0f, predictedSeconds);
            Gizmos.DrawLine(start, end);

            // vẽ đầu mũi tên nhỏ
            Vector3 dir = v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.right;
            Vector3 left = Quaternion.Euler(0, 0, 25f) * (-dir);
            Vector3 right = Quaternion.Euler(0, 0, -25f) * (-dir);
            Gizmos.DrawLine(end, end + left * 0.3f);
            Gizmos.DrawLine(end, end + right * 0.3f);
        }

        // 2) Vệt dấu (breadcrumb) của đường bay thực tế
        if (enableTrail && pts.Count > 1)
        {
            Gizmos.color = trailColor;
            for (int i = 1; i < pts.Count; i++)
            {
                Gizmos.DrawLine(pts[i - 1], pts[i]);
                Gizmos.DrawSphere(pts[i], pointRadius);
            }
        }
    }
}
