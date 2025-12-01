using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileImpactEnvironment : MonoBehaviour
{
    [Header("Environment Masks")]
    public LayerMask blockMask;              // Walls | TreeSolid
    public LayerMask fenceMask;              // Fence
    public bool blockFence = false;          // false = xuy�n r�o

    [Header("VFX (optional)")]
    public GameObject hitVfxPrefab;
    public float vfxLifetime = 0.8f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public float gizmoPredictSeconds = 0.25f;

    Rigidbody2D rb;
    Collider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (col && !col.isTrigger)
        {
            Debug.LogWarning("[ProjectileImpactEnvironment] N�n d�ng Collider2D isTrigger=ON ?? hitbox ??ch v?n d�ng trigger.", this);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        int layer = other.gameObject.layer;

        if (IsInMask(layer, blockMask))
        {
            Impact(other);
            return;
        }

        if (blockFence && IsInMask(layer, fenceMask))
        {
            Impact(other);
            return;
        }
    }

    void Impact(Collider2D other)
    {
        if (hitVfxPrefab)
        {
            var go = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            if (vfxLifetime > 0) Destroy(go, vfxLifetime);
        }
        Destroy(gameObject);
    }

    bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var rb2 = GetComponent<Rigidbody2D>();
        Vector3 pos = transform.position;
        Vector3 vel = rb2 ? (Vector3)rb2.linearVelocity : Vector3.right * 10f;

        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawLine(pos, pos + vel * Mathf.Max(0f, gizmoPredictSeconds));
        Gizmos.DrawSphere(pos, 0.03f);
    }
}
