using UnityEngine;

/// Tam giác dầu: lại gần là tự ăn (snake-like).
/// - Dùng Collider2D (isTrigger) làm bán kính "ăn".
/// - Có bán kính "hút" lớn hơn: tiến dần về player để feedback rõ ràng.
/// - Bé: +5 dầu | To: +15 dầu (set trong Inspector).
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class OilTrianglePickup : MonoBehaviour
{
    [Header("Oil")]
    [Tooltip("Lượng dầu cộng thêm khi nhặt (bé=5, to=15)")]
    public float oilAmount = 5f;

    [Header("Magnet (hút về player)")]
    [Tooltip("Bán kính bắt đầu hút (lớn hơn collider). 0 = tắt hút.")]
    public float magnetRadius = 2.5f;
    [Tooltip("Gia tốc hút khi trong magnet radius")]
    public float magnetAccel = 40f;
    [Tooltip("Giới hạn tốc độ hút")]
    public float magnetMaxSpeed = 12f;

    [Header("VFX/SFX (tùy chọn)")]
    public GameObject collectVfx;
    public AudioClip collectSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    // --- runtime
    OilLamp oilLamp;               // sẽ auto-find
    Transform player;              // sẽ auto-find
    Collider2D col;
    Rigidbody2D rb;
    bool collected;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

#if UNITY_2023_1_OR_NEWER
        oilLamp = Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include);
        var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
#else
        oilLamp = FindObjectOfType<OilLamp>();
        var pc = FindObjectOfType<PlayerController>();
#endif
        if (pc) player = pc.transform;

        rb = GetComponent<Rigidbody2D>(); // không bắt buộc
        if (rb)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void Update()
    {
        if (collected || player == null) return;

        // 1) Hút về player nếu trong bán kính magnet
        if (magnetRadius > 0f)
        {
            float d = Vector2.Distance(transform.position, player.position);
            if (d <= magnetRadius)
            {
                Vector2 dir = (player.position - transform.position).normalized;
                if (rb)
                {
                    // dùng physics nếu có RB
                    rb.linearVelocity = Vector2.ClampMagnitude(
                        rb.linearVelocity + dir * magnetAccel * Time.deltaTime,
                        magnetMaxSpeed
                    );
                }
                else
                {
                    // không có RB: dịch chuyển thủ công
                    transform.position += (Vector3)(dir * Mathf.Min(magnetMaxSpeed, 3f) * Time.deltaTime);
                }
            }
        }

        // 2) Fallback: nếu không muốn dùng collider, có thể auto-check theo khoảng cách "ăn"
        // (nhưng mặc định ta dùng collider isTrigger để quyết định nhặt)
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        // Nhận diện player qua PlayerController ở parent (khỏi lệ thuộc Tag)
        var pc = other.GetComponentInParent<PlayerController>();
        if (!pc) return;

        Collect();
    }

    void Collect()
    {
        collected = true;

        // + Dầu: dựa trên OilLamp hiện có trong dự án (DashController đang dùng oilLamp.current)
        if (oilLamp)
        {
            // Đơn giản: tăng trực tiếp. (Nếu bạn có clamp trong OilLamp thì sẽ tự giới hạn.)
            oilLamp.current += Mathf.Max(0f, oilAmount);

            // Nếu OilLamp của bạn có hàm AddOil/DrainOil thì có thể đổi sang:
            // oilLamp.DrainOil(-Mathf.Max(0f, oilAmount));
            // hoặc  oilLamp.AddOil(Mathf.Max(0f, oilAmount));
        }

        // VFX/SFX nhỏ
        if (collectVfx) Instantiate(collectVfx, transform.position, Quaternion.identity);
        if (collectSfx) AudioSource.PlayClipAtPoint(collectSfx, transform.position, sfxVolume);

        // huỷ vật phẩm
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Vẽ bán kính hút
        if (magnetRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, magnetRadius);
        }
    }
#endif
}
