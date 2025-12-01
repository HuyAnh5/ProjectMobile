using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/// Spawn theo lịch per-entry với Respawn Time dạng [min..max] (random).
/// Sửa "burst respawn": rải nhịp (stagger) và thêm jitter khi despawn.
public class SpawnDirector : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;

    [Header("Spawn Area")]
    public float minRadius = 8f;
    public float maxRadius = 16f;
    public LayerMask blockMask;      // Walls/TreeSolid… để tránh spawn chồng
    public float probeRadius = 0.4f;

    [Header("Limits & Despawn")]
    public int maxAlive = 24;
    public float despawnDistance = 28f;

    [Header("Gizmos")]
    public bool drawSpawnRings = true;
    public bool onlyWhenSelected = false;
    public Color gizMin = new Color(0.2f, 1f, 0.6f, 0.35f);
    public Color gizMax = new Color(0.2f, 0.6f, 1f, 0.30f);

    [Header("Hierarchy")]
    [SerializeField] private Transform enemyParent; // gán "EnemySpawner" trong Inspector


    [System.Serializable]
    public class Entry
    {
        [Header("Prefab & Caps")]
        public GameObject prefab;
        [Range(0, 100)] public int weight = 10;     // giữ để tương thích
        public int maxAliveOfType = 10;

        [Header("Respawn Time (random)")]
        [Tooltip("Giây tối thiểu giữa các lần spawn (lần đầu, chết, despawn).")]
        public float respawnTimeMin = 1f;
        [Tooltip("Giây tối đa giữa các lần spawn (lần đầu, chết, despawn).")]
        public float respawnTimeMax = 1f;

        [Header("Batch")]
        [Tooltip("Số lượng spawn cùng lúc mỗi lần đến lịch.")]
        public int spawnPerWave = 1;

        [Header("Policies")]
        [Tooltip("Đặt lịch đợt đầu khi game bắt đầu (sau một khoảng random trong [min..max]).")]
        public bool spawnOnStart = true;
        [Tooltip("Khi một cá thể CHẾT, lên lịch spawn 1 cá thể mới sau Respawn Time.")]
        public bool perDeathRespawn = true;
        [Tooltip("Khi một cá thể DESPAWN (xa quá), cũng lên lịch spawn 1 cá thể mới sau Respawn Time + jitter.")]
        public bool perDespawnRespawn = true;
        [Tooltip("Bật để cứ mỗi Respawn Time lại spawnPerWave cá thể (lặp, độc lập với chết/despawn).")]
        public bool loopWaves = false;

        [Header("Anti-burst (Stagger)")]
        [Tooltip("Rải nhịp: giới hạn số lượng spawn tối đa trong 1 tick.")]
        public int burstLimitPerTick = 1;
        [Tooltip("Khoảng thời gian random để dời phần còn lại khi có nhiều job cùng đến hạn.")]
        public Vector2 staggerIntervalMinMax = new Vector2(0.2f, 0.5f);

        [Header("Despawn jitter")]
        [Tooltip("Cộng thêm jitter 0..X giây nếu là despawn, để trải đều thời điểm quay lại.")]
        public float despawnExtraSpreadMax = 1.0f;

        // Runtime
        [HideInInspector] public int alive;
        [HideInInspector] public List<RespawnJob> jobs = new();
        [HideInInspector] public float nextLoopTime;
    }

    public class RespawnJob { public float time; public int count; }

    public Entry[] entries;

    // Track toàn cục
    readonly List<Spawned> aliveList = new();

    class Spawned
    {
        public Transform t;
        public int entryIndex;
    }

    void Reset()
    {
        if (!player)
        {
            var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc) player = pc.transform;
        }
    }


    void OnEnable()
    {
        InitializeSchedules();
    }

    void Update()
    {
        if (!player || entries == null || entries.Length == 0) return;

        CullFar(); // huỷ xa → marker báo despawn (có jitter)

        float now = Time.time;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (!e.prefab) continue;

            // Loop waves (độc lập)
            if (e.loopWaves && now >= e.nextLoopTime)
            {
                EnqueueJob(e, e.spawnPerWave, RandomRespawnDelay(e));
                e.nextLoopTime = now + RandomRespawnDelay(e);
            }

            // Gom tất cả job đã đến giờ rồi rải nhịp
            int dueCount = 0;
            for (int j = e.jobs.Count - 1; j >= 0; j--)
            {
                if (now >= e.jobs[j].time)
                {
                    dueCount += e.jobs[j].count;
                    e.jobs.RemoveAt(j);
                }
            }
            if (dueCount > 0)
                SpawnWithStagger(i, e, dueCount);
        }
    }

    // ===== Scheduling =====
    void InitializeSchedules()
    {
        aliveList.Clear();
        if (entries == null) return;

        float now = Time.time;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            e.alive = 0;
            e.jobs ??= new List<RespawnJob>();
            e.jobs.Clear();
            if (!e.prefab) continue;

            if (e.spawnOnStart)
            {
                float delay = RandomRespawnDelay(e);
                EnqueueJob(e, Mathf.Max(1, e.spawnPerWave), delay);
            }
            if (e.loopWaves)
                e.nextLoopTime = now + RandomRespawnDelay(e);
        }
    }

    float RandomRespawnDelay(Entry e)
    {
        float a = Mathf.Max(0f, e.respawnTimeMin);
        float b = Mathf.Max(0f, e.respawnTimeMax);
        if (b < a) { var t = a; a = b; b = t; }
        return (Mathf.Approximately(a, b)) ? a : Random.Range(a, b);
    }

    float RandomStagger(Entry e)
    {
        float a = Mathf.Max(0f, e.staggerIntervalMinMax.x);
        float b = Mathf.Max(0f, e.staggerIntervalMinMax.y);
        if (b < a) { var t = a; a = b; b = t; }
        return (Mathf.Approximately(a, b)) ? a : Random.Range(a, b);
    }

    void EnqueueJob(Entry e, int count, float delay)
    {
        e.jobs.Add(new RespawnJob { time = Time.time + Mathf.Max(0f, delay), count = Mathf.Max(1, count) });
    }

    // Khi Destroy (chết hoặc despawn)
    public void NotifyDestroyed(Transform t, bool isDespawn)
    {
        for (int i = aliveList.Count - 1; i >= 0; i--)
        {
            if (aliveList[i].t == t)
            {
                int idx = aliveList[i].entryIndex;
                var e = (idx >= 0 && idx < entries.Length) ? entries[idx] : null;
                if (e != null)
                {
                    e.alive = Mathf.Max(0, e.alive - 1);

                    // ✅ CHỈ cộng kill nếu KHÔNG phải despawn
                    if (!isDespawn) KillCounter.AddKillStatic();

                    // lịch respawn như cũ...
                    if (isDespawn)
                    {
                        if (e.perDespawnRespawn && e.prefab)
                            EnqueueJob(e, 1, RandomRespawnDelay(e) + /* nếu có jitter thì cộng ở đây */ 0f);
                    }
                    else
                    {
                        if (e.perDeathRespawn && e.prefab)
                            EnqueueJob(e, 1, RandomRespawnDelay(e));
                    }
                }
                aliveList.RemoveAt(i);
                break;
            }
        }
    }


    // ===== Spawn logic =====
    void SpawnWithStagger(int entryIndex, Entry e, int totalDue)
    {
        int burst = Mathf.Max(1, e.burstLimitPerTick);
        int toSpawnNow = Mathf.Min(totalDue, burst);
        if (toSpawnNow > 0)
            TrySpawnBatch(entryIndex, toSpawnNow);

        int remaining = totalDue - toSpawnNow;
        if (remaining > 0)
        {
            // Dời phần còn lại bằng stagger ngẫu nhiên
            EnqueueJob(e, remaining, RandomStagger(e));
        }
    }

    void TrySpawnBatch(int entryIndex, int count)
    {
        var e = entries[entryIndex];
        if (count <= 0) return;

        int canSpawnGlobal = Mathf.Max(0, maxAlive - aliveList.Count);
        int canSpawnType = Mathf.Max(0, e.maxAliveOfType - e.alive);
        int canSpawn = Mathf.Min(count, canSpawnGlobal, canSpawnType);
        if (canSpawn <= 0) return;

        int spawned = 0;
        for (int k = 0; k < canSpawn; k++)
        {
            if (!FindSpawnPosition(out Vector3 pos))
            {
                int remain = canSpawn - spawned;
                if (remain > 0) EnqueueJob(e, remain, 0.6f);
                break;
            }

            var parent = enemyParent ? enemyParent : transform;   // <- dùng Enemy Parent nếu có
            var go = Instantiate(e.prefab, pos, Quaternion.identity, parent);
            Hookup(go);

            aliveList.Add(new Spawned { t = go.transform, entryIndex = entryIndex });
            e.alive++;
            spawned++;
        }
    }


    bool FindSpawnPosition(out Vector3 pos)
    {
        pos = player ? player.position : transform.position;
        for (int i = 0; i < 32; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Random.Range(minRadius, maxRadius);
            Vector2 p = (Vector2)pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

            if (Physics2D.OverlapCircle(p, probeRadius, blockMask)) continue;
            pos = p; return true;
        }
        return false;
    }

    void Hookup(GameObject go)
    {
        var setter = go.GetComponent<AIDestinationSetter>();
        if (setter) setter.target = player;

        var r = go.GetComponent<EnemyRunner>(); if (r && !r.player) r.player = player;
        var p = go.GetComponent<EnemyPouncer>(); if (p && !p.player) p.player = player;
        var b = go.GetComponent<EnemyBomber>(); if (b && !b.player) b.player = player;

        go.AddComponent<SpawnedMarker>().Init(this, go.transform);
    }

    void CullFar()
    {
        if (!player) return;

        for (int i = aliveList.Count - 1; i >= 0; i--)
        {
            var s = aliveList[i];
            if (!s.t) { aliveList.RemoveAt(i); continue; }

            if (Vector2.Distance(player.position, s.t.position) > despawnDistance)
            {
                var mk = s.t.GetComponent<SpawnedMarker>();
                if (mk) mk.MarkDespawn();
                Destroy(s.t.gameObject);
            }
        }
    }

    private class SpawnedMarker : MonoBehaviour
    {
        SpawnDirector dir; Transform me;
        int safety = 0;
        bool isDespawn;

        public void Init(SpawnDirector d, Transform t) { dir = d; me = t; }
        public void MarkDespawn() { isDespawn = true; }

        void OnDestroy()
        {
            if (dir && me && safety++ == 0)
                dir.NotifyDestroyed(me, isDespawn);
        }
    }

    // ===== Gizmos =====
    void OnDrawGizmos()
    {
        if (!drawSpawnRings || onlyWhenSelected) return;
        DrawRings();
    }
    void OnDrawGizmosSelected()
    {
        if (!drawSpawnRings) return;
        DrawRings();
    }
    void DrawRings()
    {
        if (!player) return;
        Vector3 p = player.position;
        Gizmos.color = gizMin; Gizmos.DrawWireSphere(p, Mathf.Max(0f, minRadius));
        Gizmos.color = gizMax; Gizmos.DrawWireSphere(p, Mathf.Max(minRadius, maxRadius));
    }
}
