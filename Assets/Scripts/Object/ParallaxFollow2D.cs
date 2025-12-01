using UnityEngine;

[DefaultExecutionOrder(100)] // chạy sau chuyển động player/camera 1 chút
public class ParallaxFollow2D : MonoBehaviour
{
    [Header("Target (Camera hoặc Player)")]
    public Transform target;                  // kéo Main Camera hoặc Player
    public Vector2 offset;                    // bù vị trí
    [Range(0f, 1.5f)] public float parallax = 1f; // 1=bám hệt, 0=đứng yên

    [Header("Axes")]
    public bool followX = true;
    public bool followY = true;
    public bool preserveZ = true;
    public float fixedZ = 0f;                 // dùng khi không preserveZ

    [Header("Smoothing")]
    [Min(0f)] public float smoothTime = 0.12f;
    [Min(0f)] public float maxSpeed = 50f;

    [Header("Pixel Snap (tùy chọn)")]
    public bool pixelSnap = false;
    public float pixelsPerUnit = 100f;        // trùng với PPU của sprite

    Vector3 _vel;

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!target)
        {
            // ưu tiên camera
            var cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (cam) target = cam.transform;
            else
            {
                // fallback: player
                var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (pc) target = pc.transform;
            }
        }
#else
        if (!target) { if (Camera.main) target = Camera.main.transform; }
#endif
    }

    void LateUpdate()
    {
        if (!target) return;

        Vector3 t = target.position;
        Vector3 cur = transform.position;
        Vector3 want = cur;

        if (followX) want.x = t.x * parallax + offset.x;
        if (followY) want.y = t.y * parallax + offset.y;
        want.z = preserveZ ? cur.z : fixedZ;

        // mượt
        Vector3 next = (smoothTime > 0f)
            ? Vector3.SmoothDamp(cur, want, ref _vel, smoothTime, maxSpeed)
            : want;

        // pixel snap (nếu cần)
        if (pixelSnap && pixelsPerUnit > 0.01f)
        {
            next.x = Mathf.Round(next.x * pixelsPerUnit) / pixelsPerUnit;
            next.y = Mathf.Round(next.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position = next;
    }
}
