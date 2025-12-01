using System.Collections;
using UnityEngine;

/// <summary>
/// Dash theo hướng, có i-frames, cooldown, tiêu dầu, scale quãng theo gesture.
/// Trong lúc dash: tạm Ignore collision giữa Player <-> Fence (nhảy qua rào),
/// nhưng KHÔNG bỏ va chạm với TreeSolid (vẫn bị chặn).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DashController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;   // có Facing + MovementLocked
    [SerializeField] private OilLamp oilLamp;           // để trừ dầu
    private Rigidbody2D rb;

    [Header("Dash params")]
    [Tooltip("Thời lượng dash (giây)")]
    [SerializeField, Min(0.01f)] private float dashDuration = 0.14f;

    [Tooltip("Quãng dash mặc định (units)")]
    [SerializeField, Min(0.1f)] private float defaultDashDistance = 4.0f;

    [Tooltip("Cooldown giữa 2 lần dash (giây)")]
    [SerializeField, Min(0f)] private float cooldown = 0.35f;

    [Tooltip("Dầu trừ ngay khi bắt đầu dash (mặc định 0, chỉ tăng khi có item đặc biệt)")]
    [SerializeField, Min(0f)] private float oilCost = 0f;

    // Expose oilCost so items can temporarily change dash cost
    public float OilCost
    {
        get => oilCost;
        set => oilCost = Mathf.Max(0f, value);
    }

    [Header("Scale theo gesture (0..1)")]
    [SerializeField, Range(0.1f, 1f)] private float minDistanceScale = 0.6f;
    [SerializeField, Range(0.1f, 1.5f)] private float maxDistanceScale = 1.0f;

    [Header("I-Frames")]
    [Tooltip("Bật vô hiệu hoá sát thương trong lúc dash")]
    [SerializeField] private bool invulnerableDuringDash = true;
    [Tooltip("Cộng thêm i-frames sau khi kết thúc dash (giây)")]
    [SerializeField, Min(0f)] private float extraInvulnAfter = 0.0f;


    // ---- External access for items (StatEffect) ----

    public float DashDuration
    {
        get => dashDuration;
        set => dashDuration = Mathf.Max(0.01f, value);
    }

    public float DefaultDashDistance
    {
        get => defaultDashDistance;
        set => defaultDashDistance = Mathf.Max(0.1f, value);
    }

    public float Cooldown
    {
        get => cooldown;
        set => cooldown = Mathf.Max(0f, value);
    }

    /// <summary>Oil cost mỗi lần dash. Mặc định bạn có thể set = 0, item sẽ cộng thêm khi cần.</summary>
    public float MinDistanceScale
    {
        get => minDistanceScale;
        set => minDistanceScale = Mathf.Clamp(value, 0.1f, 1f);
    }

    public float MaxDistanceScale
    {
        get => maxDistanceScale;
        set => maxDistanceScale = Mathf.Clamp(value, 0.1f, 1.5f);
    }

    public bool InvulnerableDuringDash
    {
        get => invulnerableDuringDash;
        set => invulnerableDuringDash = value;
    }

    public float ExtraInvulnAfter
    {
        get => extraInvulnAfter;
        set => extraInvulnAfter = Mathf.Max(0f, value);
    }


    [Header("Input test (PC)")]
    [SerializeField] private bool spaceToDash = true;

    [Header("Layers (bỏ va chạm khi dash)")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string fenceLayerName = "Fence";

    // ---- State
    private bool isDashing;
    private float nextReadyTime;
    private float invulnEndTime;
    private int playerLayer;
    private int fenceLayer;
    private bool fenceIgnored;

    // Expose cho HUD/Debug
    public bool IsDashing => isDashing;
    public bool IsOnCooldown => Time.time < nextReadyTime;
    public bool IsInvulnerable => invulnerableDuringDash && (Time.time < invulnEndTime);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!player) player = GetComponent<PlayerController>();
        if (!oilLamp)
        {
#if UNITY_2023_1_OR_NEWER
            oilLamp = Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include);
#else
            oilLamp = FindObjectOfType<OilLamp>();
#endif
        }

        playerLayer = LayerMask.NameToLayer(playerLayerName);
        fenceLayer = LayerMask.NameToLayer(fenceLayerName);

        if (playerLayer < 0) Debug.LogWarning($"[DashController] Layer '{playerLayerName}' không tồn tại.");
        if (fenceLayer < 0) Debug.LogWarning($"[DashController] Layer '{fenceLayerName}' không tồn tại.");
    }

    void OnDisable()
    {
        // đảm bảo khôi phục lại va chạm nếu đang tắt component giữa chừng
        if (fenceIgnored && playerLayer >= 0 && fenceLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, fenceLayer, false);
            fenceIgnored = false;
        }
    }

    void Update()
    {
        // Input test trên PC: nhấn Space để dash theo hướng Facing hiện tại
        if (spaceToDash && Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 dir = (player && player.Facing.sqrMagnitude > 0.001f) ? player.Facing : Vector2.up;
            RequestDash(dir, 1f);
        }
    }

    /// <summary>
    /// Gọi từ RightSwipeDash / gesture:
    /// strength01 nên là 0..1 (độ dài vuốt đã chuẩn hoá)
    /// </summary>
    public bool RequestDash(Vector2 desiredDir, float strength01 = 1f)
    {
        if (!enabled) return false;
        if (isDashing) return false;
        if (Time.time < nextReadyTime) return false;

        Vector2 dir = desiredDir.sqrMagnitude > 0.0001f
            ? desiredDir.normalized
            : ((player && player.Facing.sqrMagnitude > 0.0001f) ? player.Facing.normalized : Vector2.up);

        // đủ dầu?
        if (oilLamp && oilCost > 0f && oilLamp.current < oilCost) return false;

        // scale quãng theo strength
        float t = Mathf.Clamp01(strength01);
        float scale = Mathf.Lerp(minDistanceScale, maxDistanceScale, t);
        float dashDistance = defaultDashDistance * Mathf.Max(0.01f, scale);

        StartCoroutine(DashRoutine(dir, dashDistance));
        if (oilLamp && oilCost > 0f) oilLamp.DrainOil(oilCost);
        return true;
    }

    private IEnumerator DashRoutine(Vector2 dashDir, float dashDistance)
    {
        isDashing = true;
        float startTime = Time.time;
        float endTime = startTime + dashDuration;
        nextReadyTime = Mathf.Max(nextReadyTime, endTime + cooldown);

        // Khoá di chuyển thường trong lúc dash
        if (player != null) player.MovementLocked = true;

        // Bỏ va chạm Player <-> Fence để "nhảy qua rào"
        if (playerLayer >= 0 && fenceLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, fenceLayer, true);
            fenceIgnored = true;
        }

        // I-frames
        if (invulnerableDuringDash)
            invulnEndTime = endTime + Mathf.Max(0f, extraInvulnAfter);

        // Di chuyển theo quãng LERP (MovePosition để mượt & tôn trọng physics)
        Vector2 startPos = rb.position;
        Vector2 endPos = startPos + dashDir * dashDistance;

        // v=0 để không cộng dồn
        rb.linearVelocity = Vector2.zero;

        while (Time.time < endTime)
        {
            float t = Mathf.InverseLerp(startTime, endTime, Time.time);
            Vector2 target = Vector2.Lerp(startPos, endPos, t);
            rb.MovePosition(target);
            yield return null; // mỗi frame
        }

        // Đặt về cuối quãng (phòng drift khi fps thấp)
        rb.MovePosition(endPos);
        rb.linearVelocity = Vector2.zero;

        // Khôi phục va chạm Fence
        if (fenceIgnored && playerLayer >= 0 && fenceLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, fenceLayer, false);
            fenceIgnored = false;
        }

        // Mở khoá di chuyển
        if (player != null) player.MovementLocked = false;

        isDashing = false;
    }

    // --- API phụ cho HUD/debug ---
    public float GetCooldownRemaining() => Mathf.Max(0f, nextReadyTime - Time.time);
    public void SetOilCost(float cost) => OilCost = cost;

    // ======== Compatibility patch for RightSwipeDash ========

    // 1) Property thay cho method: cho phép dùng "dash && dash.CanDash"
    public bool CanDash =>
        !isDashing
        && Time.time >= nextReadyTime
        && (oilLamp == null || oilCost <= 0f || oilLamp.current >= oilCost);

    // 2) Shim giữ chữ ký cũ: TryStartDash(...) gọi về RequestDash(...)
    public bool TryStartDash(Vector2 dir, float strength01 = 1f)
    {
        return RequestDash(dir, strength01);
    }

    // 3) Overload nhận nullable để chấp nhận đối số float?
    public bool TryStartDash(Vector2 dir, float? strength01Nullable)
    {
        return RequestDash(dir, strength01Nullable.HasValue ? strength01Nullable.Value : 1f);
    }


#if UNITY_EDITOR
    void OnValidate()
    {
        if (minDistanceScale > maxDistanceScale)
            maxDistanceScale = minDistanceScale;

        // nhắc nhở nếu layer chưa có
        if (!Application.isPlaying)
        {
            if (LayerMask.NameToLayer(playerLayerName) < 0)
                Debug.LogWarning($"[DashController] Chưa tạo Layer '{playerLayerName}' trong Project Settings > Tags & Layers.");
            if (LayerMask.NameToLayer(fenceLayerName) < 0)
                Debug.LogWarning($"[DashController] Chưa tạo Layer '{fenceLayerName}' trong Project Settings > Tags & Layers.");
        }
    }
#endif
}
