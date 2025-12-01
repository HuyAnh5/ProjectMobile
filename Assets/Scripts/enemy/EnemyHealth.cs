using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] float maxHP = 10f;
    [SerializeField] Color hitFlashColor = new Color(1f, .3f, .3f);
    [SerializeField] float hitFlashTime = 0.08f;

    public static int ActiveCount { get; private set; }

    float hp;
    bool counted;                      // <--- NEW: tránh đếm đôi
    SpriteRenderer[] srs;
    Color[] baseColors;

    void Awake()
    {
        hp = maxHP;
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) baseColors[i] = srs[i].color;
    }

    void OnEnable()
    {
        if (!counted)
        {
            ActiveCount++;
            counted = true;
            // Debug.Log($"Enemy ++ => {ActiveCount}");
        }
    }

    void OnDisable()
    {
        if (counted)
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            counted = false;
            // Debug.Log($"Enemy -- => {ActiveCount}");
        }
    }

    public void TakeDamage(float dmg)
    {
        if (hp <= 0) return;
        hp -= Mathf.Max(0f, dmg);
        if (hitFlashTime > 0f) StartCoroutine(HitFlash());
        if (hp <= 0f) Die();
    }

    System.Collections.IEnumerator HitFlash()
    {
        for (int i = 0; i < srs.Length; i++) srs[i].color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashTime);
        for (int i = 0; i < srs.Length; i++) srs[i].color = baseColors[i];
    }

    void Die()
    {
        // sau này: rơi loot/effect
        Destroy(gameObject);           // <--- gọi Destroy trên root có EnemyHealth
    }

    public void ApplyKnockback(Vector2 direction, float distance)
    {
        if (distance <= 0f) return;

        // chuẩn hóa và dịch chuyển enemy một đoạn
        Vector2 delta = direction.normalized * distance;
        transform.position = (Vector2)transform.position + delta;
    }

}
