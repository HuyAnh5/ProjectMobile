using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5f;

    // Expose moveSpeed so items can modify it
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }


    [Header("Joystick (tùy chọn)")]
    [SerializeField] private FloatingJoystick joystick;   // Kéo JoystickArea vào (nếu có)

    [Header("Facing (mượt & giữ khi thả)")]
    [SerializeField] private float deadzone = 0.08f;      // 0.08–0.12
    [SerializeField] private float facingSmoothTime = 0.06f;

    // Cho DashController/khác khóa di chuyển thường
    public bool MovementLocked { get; set; } = false;

    // ---- External modifiers (items) ----


    /// <summary>Deadzone của input để đổi hướng nhìn.</summary>
    public float Deadzone
    {
        get => deadzone;
        set => deadzone = Mathf.Max(0f, value);
    }

    /// <summary>Thời gian smooth hướng nhìn.</summary>
    public float FacingSmoothTime
    {
        get => facingSmoothTime;
        set => facingSmoothTime = Mathf.Max(0f, value);
    }


    // Trạng thái & nội bộ
    private Rigidbody2D rb;
    private Vector2 moveInputRaw;           // input dùng cho di chuyển
    private float facingAngleDeg = 90f;     // 0°=phải, 90°=lên
    private float facingAngleVel;           // biến nội cho SmoothDampAngle
    private Vector2 lastFacingDir = Vector2.up;

    public Vector2 Facing { get; private set; } = Vector2.up;

    // --- Aim override (phục vụ Lock-On) ---
    private bool aimOverrideActive = false;
    private Vector2 aimOverrideDir = Vector2.up;

    /// <summary>
    /// Bật/tắt ép nhìn theo một hướng cố định (ví dụ hướng tới mục tiêu khi lock-on).
    /// Truyền null để hủy override.
    /// </summary>
    public void SetAimOverride(Vector2? dir)
    {
        if (dir.HasValue && dir.Value.sqrMagnitude > 0.0001f)
        {
            aimOverrideActive = true;
            aimOverrideDir = dir.Value.normalized;
        }
        else
        {
            aimOverrideActive = false;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Update()
    {
        // -------- 1) Lấy input di chuyển (ưu tiên joystick) --------
        Vector2 js = (joystick != null) ? joystick.Value : Vector2.zero;

        if (js.sqrMagnitude > 0f)
        {
            // Mobile
            moveInputRaw = js;
        }
        else
        {
            // PC test
            float rx = Input.GetAxisRaw("Horizontal");
            float ry = Input.GetAxisRaw("Vertical");
            moveInputRaw = new Vector2(rx, ry);
            if (moveInputRaw.sqrMagnitude > 1f) moveInputRaw = moveInputRaw.normalized;
        }

        // -------- 2) Cập nhật Facing (mượt + giữ khi thả + hỗ trợ lock-on) --------
        // Nguồn để xác định hướng nhìn (khi không override):
        Vector2 facingInput = (js.sqrMagnitude > 0f)
            ? js
            : new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")); // mượt hơn Raw

        Vector2 desiredDir;

        if (aimOverrideActive)
        {
            // Đang lock-on: luôn nhìn theo mục tiêu
            desiredDir = aimOverrideDir;
        }
        else if (facingInput.sqrMagnitude > deadzone * deadzone)
        {
            // Có input đủ lớn → cập nhật hướng
            desiredDir = facingInput.normalized;
        }
        else
        {
            // Không có input → giữ hướng gần nhất
            desiredDir = lastFacingDir;
        }

        // Mượt hóa góc rồi suy ra vector Facing
        float desiredDeg = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg;
        facingAngleDeg = Mathf.SmoothDampAngle(facingAngleDeg, desiredDeg, ref facingAngleVel, facingSmoothTime);

        float rad = facingAngleDeg * Mathf.Deg2Rad;
        Facing = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        lastFacingDir = Facing;
    }

    private void FixedUpdate()
    {
        if (MovementLocked) return; // đang dash/khóa → không di chuyển thường

        Vector2 next = rb.position + moveInputRaw * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(next);
    }
}
