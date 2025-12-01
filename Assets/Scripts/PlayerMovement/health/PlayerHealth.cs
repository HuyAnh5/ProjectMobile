using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Player máu kiểu trái tim (half-heart unit).
/// - Bắt đầu với maxHearts (mặc định 3) => currentHalves = maxHearts * 2.
/// - TakeDamageHalfHearts(1) = mất 1/2 tim.
/// - Khi OilLamp.current <= 0: tự trừ 1/2 tim mỗi oilEmptyTickInterval giây.
/// - Tôn trọng i-frames khi dash: bỏ qua damage nếu DashController.IsInvulnerable = true.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Hearts")]
    [Min(1)] public int maxHearts = 3;              // có thể nâng lên 4+
    [Tooltip("Hồi đầy khi tăng maxHearts")]
    public bool healToFullOnMaxChange = true;

    [Header("Invulnerability")]
    [Tooltip("Có dùng i-frames của DashController không")]
    public bool respectDashIFrames = true;
    public DashController dash;                     // optional, auto-find nếu trống

    [Header("Oil Drain → HP")]
    [Tooltip("Nguồn đọc dầu hiện tại; khi hết dầu sẽ trừ máu theo chu kỳ")]
    public OilLamp oilLamp;                         // optional, auto-find nếu trống
    [Tooltip("Khi hết dầu: trừ 1/2 tim mỗi bao lâu (giây)")]
    public float oilEmptyTickInterval = 0.5f;

    [Header("Debug")]
    public bool logEvents = false;

    // ---- State (public read-only) ----
    public int CurrentHalves => _currentHalves;
    public int MaxHalves => Mathf.Max(0, maxHearts) * 2;
    public float CurrentHeartsFloat => _currentHalves * 0.5f;

    [Header("External modifiers (items)")]
    [SerializeField, Tooltip("Nhân mọi damage nhận vào (half-hearts). 1 = bình thường.")]
    private float damageTakenMultiplier = 1f;

    [SerializeField, Tooltip("Cộng thêm/bớt damage mỗi hit (đơn vị half-heart, có thể âm).")]
    private int flatDamageBonusHalves = 0;

    [SerializeField, Tooltip("Nhân mọi lượng heal (half-hearts). 1 = bình thường.")]
    private float healMultiplier = 1f;

    /// <summary>Max hearts theo đơn vị tim (3,4,...). Item có thể chỉnh trực tiếp.</summary>
    public int MaxHearts
    {
        get => maxHearts;
        set
        {
            maxHearts = Mathf.Max(1, value);
            _currentHalves = Mathf.Clamp(_currentHalves, 0, MaxHalves);
            NotifyChanged();
        }
    }

    public float DamageTakenMultiplier
    {
        get => damageTakenMultiplier;
        set => damageTakenMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>Flat damage cộng thêm mỗi hit (half-hearts). 4 = +2 tim, -2 = giảm 1 tim.</summary>
    public int FlatDamageBonusHalves
    {
        get => flatDamageBonusHalves;
        set => flatDamageBonusHalves = value;
    }

    public float HealMultiplier
    {
        get => healMultiplier;
        set => healMultiplier = Mathf.Max(0f, value);
    }


    public System.Func<bool> OnTryCheatDeath;

    // ---- Events ----
    public event Action<int, int> OnHealthChanged;   // (currentHalves, maxHalves)
    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDied;

    int _currentHalves;
    Coroutine _oilTickCo;

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!dash)
            dash = UnityEngine.Object.FindFirstObjectByType<DashController>(FindObjectsInactive.Include);
        if (!oilLamp)
            oilLamp = UnityEngine.Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include);
#else
    if (!dash)
        dash = UnityEngine.Object.FindObjectOfType<DashController>();
    if (!oilLamp)
        oilLamp = UnityEngine.Object.FindObjectOfType<OilLamp>();
#endif
    }


    void OnEnable()
    {
        _currentHalves = MaxHalves;
        NotifyChanged();
        TryStartOilTick();
    }

    void OnDisable()
    {
        StopOilTick();
    }

    void Update()
    {
        // Giám sát trạng thái dầu để start/stop tick
        TryStartOilTick();
        TryStopOilTick();
    }

    // ======== API chính ========

    /// <summary>Gây sát thương theo đơn vị half-heart (>=1).</summary>
    /// <summary>Gây sát thương theo đơn vị half-heart (>=1).</summary>
    public bool TakeDamageHalfHearts(int halves)
    {
        if (halves <= 0) return false;

        // Bỏ qua damage khi đang dash invuln (nếu bật)
        if (respectDashIFrames && dash && dash.IsInvulnerable)
            return false;

        // Áp dụng modifier từ item (curse/buff)
        int modified = Mathf.CeilToInt(halves * damageTakenMultiplier) + flatDamageBonusHalves;
        if (modified <= 0) return false; // ví dụ buff giảm hết damage

        int before = _currentHalves;
        _currentHalves = Mathf.Clamp(_currentHalves - modified, 0, MaxHalves);

        if (logEvents)
            Debug.Log($"[PlayerHealth] Damage {modified * 0.5f} heart(s) → {before / 2f} → {CurrentHeartsFloat}");

        if (_currentHalves < before)
        {
            OnDamaged?.Invoke();
            NotifyChanged();
            if (_currentHalves <= 0) Die();
            return true;
        }

        return false;
    }



    /// <summary>Hồi máu theo half-heart.</summary>
    /// <summary>Hồi máu theo half-heart.</summary>
    public bool HealHalfHearts(int halves)
    {
        if (halves <= 0 || _currentHalves <= 0) return false;

        int modified = Mathf.CeilToInt(halves * healMultiplier);
        if (modified <= 0) return false;

        int before = _currentHalves;
        _currentHalves = Mathf.Clamp(_currentHalves + modified, 0, MaxHalves);

        if (_currentHalves > before)
        {
            if (logEvents)
                Debug.Log($"[PlayerHealth] Heal {modified * 0.5f} heart(s) → {before / 2f} → {CurrentHeartsFloat}");
            OnHealed?.Invoke();
            NotifyChanged();
            return true;
        }

        return false;
    }


    /// <summary>Tăng tối đa tim (có thể hồi đầy tuỳ cờ).</summary>
    public void AddMaxHearts(int add)
    {
        if (add <= 0) return;
        int oldMax = MaxHalves;
        maxHearts += add;
        int newMax = MaxHalves;

        if (healToFullOnMaxChange) _currentHalves = newMax;  // hồi đầy
        else _currentHalves = Mathf.Clamp(_currentHalves, 0, newMax);

        if (logEvents) Debug.Log($"[PlayerHealth] Max hearts: {oldMax / 2} → {newMax / 2} (current={CurrentHeartsFloat})");
        NotifyChanged();
    }

    // ======== Nội bộ ========
    void Die()
    {
        if (logEvents) Debug.Log("[PlayerHealth] DEAD");
        OnDied?.Invoke();

        // Tối thiểu cho prototype: khoá điều khiển
        var pc = GetComponent<PlayerController>();
        if (pc) pc.MovementLocked = true;

        var aa = GetComponent<AutoAttackRunner>();
        if (aa) aa.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;

        // TODO: show UI death / restart
    }

    void NotifyChanged() => OnHealthChanged?.Invoke(_currentHalves, MaxHalves);

    void TryStartOilTick()
    {
        if (!oilLamp) return;
        // AutoAttackRunner cũng đọc oilLamp.current để quyết định bắn:contentReference[oaicite:1]{index=1}
        if (_oilTickCo == null && oilLamp.current <= 0f)
            _oilTickCo = StartCoroutine(CoOilEmptyTick());
    }

    void TryStopOilTick()
    {
        if (!oilLamp) return;
        if (_oilTickCo != null && oilLamp.current > 0f)
            StopOilTick();
    }

    void StopOilTick()
    {
        if (_oilTickCo != null) { StopCoroutine(_oilTickCo); _oilTickCo = null; }
    }

    IEnumerator CoOilEmptyTick()
    {
        // Lặp trong khi vẫn hết dầu
        while (oilLamp && oilLamp.current <= 0f)
        {
            TakeDamageHalfHearts(1); // 1 half-heart mỗi tick
            if (_currentHalves <= 0) { _oilTickCo = null; yield break; }
            yield return new WaitForSeconds(Mathf.Max(0.05f, oilEmptyTickInterval));
        }
        _oilTickCo = null;
    }
}
