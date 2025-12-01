using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(50)] // chạy sau chuyển động player 1 chút
public class CameraFOVCenterTarget : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform player;       // Player transform
    [SerializeField] private OilLamp oilLamp;        // Có fovLight (Point/Spot 2D)
    [SerializeField] private LockOnController lockOn;

    [Header("Focus rules")]
    [Tooltip("Khi lock-on: tâm camera = player + (trục nón) * (bán kính FOV * factor)")]
    [Range(0f, 1f)] public float fovFocusFactor = 0.5f; // “giữa nón” = 0.5
    [Tooltip("Thời gian mượt dịch camera (giảm nếu muốn nhạy hơn)")]
    [Min(0f)] public float smoothTime = 0.12f;
    [Tooltip("Giới hạn tốc độ dịch (để tránh vọt)")]
    [Min(0f)] public float maxSpeed = 50f;

    public enum FovAxis { Up, Right, PlayerFacing }
    [Header("FOV axis")]
    public FovAxis fovAxis = FovAxis.Up;      // Trục nón FOV của Light2D
    public float axisOffsetDeg = 0f;          // Bù lệch nếu prefab xoay
    public float angleGraceDeg = 2f;          // Nới góc (nếu cần khớp hình)
    public bool snapOnStart = true;           // Snap ngay khung đầu

    [Header("Z handling")]
    [Tooltip("Giữ nguyên Z hiện tại của CamFocus, chỉ di XY")]
    public bool preserveCurrentZ = true;
    public float fixedZ = -10f;               // Dùng khi không preserve Z

    [Header("Debug")]
    public bool drawGizmos = false;
    public Color gizmoColorUnlocked = new(0.2f, 0.8f, 1f, 0.6f);
    public Color gizmoColorLocked = new(1f, 0.85f, 0.2f, 0.6f);

    Vector3 velocity; // cho SmoothDamp

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!player)
        {
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc) player = pc.transform;
        }
        if (!oilLamp)
        {
            oilLamp = UnityEngine.Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include);
        }
        if (!lockOn)
        {
            lockOn = UnityEngine.Object.FindFirstObjectByType<LockOnController>(FindObjectsInactive.Include);
        }
#else
        if (!player)  { var pc = FindObjectOfType<PlayerController>(); if (pc) player = pc.transform; }
        if (!oilLamp) { oilLamp = FindObjectOfType<OilLamp>(); }
        if (!lockOn)  { lockOn = FindObjectOfType<LockOnController>(); }
#endif
    }

    void OnEnable()
    {
        if (snapOnStart)
        {
            Vector3 p = ComputeTargetPosition();
            if (preserveCurrentZ) p.z = transform.position.z;
            else p.z = fixedZ;
            transform.position = p;
            velocity = Vector3.zero;
        }
    }

    void LateUpdate()
    {
        if (!player) return;
        Vector3 target = ComputeTargetPosition();

        // Z handling
        if (preserveCurrentZ) target.z = transform.position.z;
        else target.z = fixedZ;

        transform.position = Vector3.SmoothDamp(
            transform.position, target, ref velocity, smoothTime, maxSpeed
        );
    }

    Vector3 ComputeTargetPosition()
    {
        Vector3 playerPos = player ? player.position : transform.position;

        // Khi KHÔNG lock: camera bám player
        bool isLocked = lockOn && lockOn.IsHolding && lockOn.CurrentTarget;
        if (!isLocked || !oilLamp || !oilLamp.fovLight)
            return playerPos;

        // Khi lock: dịch tâm tới giữa nón FOV
        var fov = oilLamp.fovLight;
        float r = Mathf.Max(0f, fov.pointLightOuterRadius);
        Vector2 axis = GetFovForward(fov);

        // Bù góc nếu cần
        if (Mathf.Abs(axisOffsetDeg) > 0.001f)
        {
            float rad = axisOffsetDeg * Mathf.Deg2Rad;
            axis = new Vector2(
                axis.x * Mathf.Cos(rad) - axis.y * Mathf.Sin(rad),
                axis.x * Mathf.Sin(rad) + axis.y * Mathf.Cos(rad)
            ).normalized;
        }

        Vector2 offset = axis * (r * Mathf.Clamp01(fovFocusFactor));
        return playerPos + (Vector3)offset;
    }

    Vector2 GetFovForward(Light2D fov)
    {
        switch (fovAxis)
        {
            case FovAxis.Right: return fov.transform.right.normalized;
            case FovAxis.PlayerFacing:
                {
                    var pc = player ? player.GetComponent<PlayerController>() : null;
                    Vector2 facing = (pc && pc.Facing.sqrMagnitude > 0.0001f) ? pc.Facing : (Vector2)fov.transform.up;
                    return facing.normalized;
                }
            default: return fov.transform.up.normalized; // Spot 2D thường dùng Up làm trục
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = (lockOn && lockOn.IsHolding && lockOn.CurrentTarget) ? gizmoColorLocked : gizmoColorUnlocked;
        Vector3 p = Application.isPlaying ? ComputeTargetPosition() : (player ? player.position : transform.position);
        Gizmos.DrawWireSphere(p, 0.3f);
        Gizmos.DrawLine(p + Vector3.left * .3f, p + Vector3.right * .3f);
        Gizmos.DrawLine(p + Vector3.up * .3f, p + Vector3.down * .3f);
    }
#endif
}
