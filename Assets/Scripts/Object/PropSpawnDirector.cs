using System.Collections.Generic;
using UnityEngine;

/// Spawner dành riêng cho props tĩnh (Tree, Fence):
/// - Spawn trong vành [minRadius..maxRadius] quanh Player, BẮT BUỘC ngoài màn hình.
/// - Despawn chỉ khi VỪA quá xa VÀ ngoài màn hình (không biến mất trước mắt).
/// - Giữ khoảng cách giữa props, không đè lên blockMask.
/// - Duy trì "targetCount" mỗi loại; khi thiếu sẽ respawn sau [respawnMin..respawnMax].
[DefaultExecutionOrder(90)]
public class PropSpawnDirectorLite : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Camera cam; // auto-find nếu để trống

    [Header("Spawn Ring")]
    public float minRadius = 14f;
    public float maxRadius = 28f;

    [Header("Offscreen")]
    [Range(0f, 0.25f)]
    public float offscreenViewportPadding = 0.06f; // spawn/despawn phải ngoài vùng nhìn + padding

    [Header("Collision / Spacing")]
    public LayerMask blockMask;   // Walls, TreeSolid...
    public float probeRadius = 0.45f;
    public float minSpacing = 2.4f;

    [Header("Despawn / Limits")]
    public int maxAliveGlobal = 64;
    public float despawnDistance = 44f; // nên > maxRadius

    [Header("Hierarchy")]
    [SerializeField] private Transform propsParent; // gán "PropSpawner" trong Inspector



    [System.Serializable]
    public class Entry
    {
        [Header("Prefab & Target")]
        public GameObject prefab;
        [Tooltip("Số lượng props muốn DUY TRÌ cho loại này.")]
        public int targetCount = 8;

        [Header("Respawn time [min..max]")]
        public float respawnTimeMin = 6f;
        public float respawnTimeMax = 10f;

        [Header("Rotation (Fence khuyến nghị snap 45°)")]
        public bool randomRotation = true;
        public float snapAngleDeg = 0f;
        public bool randomFlipX = false;
        public bool randomFlipY = false;

        // runtime
        [HideInInspector] public int alive;
        [HideInInspector] public List<Job> jobs = new();
    }

    [System.Serializable]
    public class Job { public float time; } // spawn 1 prop khi tới giờ

    [Header("Entries")]
    public Entry[] entries;

    // runtime
    class Live { public Transform t; public int entryIndex; }
    readonly List<Live> aliveList = new();

    void Reset()
    {
        if (!player)
        {
            var pc = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (pc) player = pc.transform;
        }
        if (!cam)
        {
            cam = Camera.main ? Camera.main
                : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        }
    }


    void OnEnable()
    {
        aliveList.Clear();
        if (entries == null) return;

        // Lên lịch ban đầu để đạt targetCount (mỗi job lệch 0..0.5s cho êm)
        foreach (var e in entries)
        {
            if (!e?.prefab) continue;
            e.alive = 0;
            e.jobs ??= new List<Job>();
            e.jobs.Clear();

            int need = Mathf.Max(0, e.targetCount);
            for (int i = 0; i < need; i++)
                e.jobs.Add(new Job { time = Time.time + Random.Range(0f, 0.5f) });
        }
    }

    void Update()
    {
        if (!player || entries == null || entries.Length == 0) return;

        CullFarOffscreen();

        float now = Time.time;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (!e?.prefab) continue;

            // 1) Thực thi các job đến hạn
            for (int j = e.jobs.Count - 1; j >= 0; j--)
            {
                if (now < e.jobs[j].time) continue;
                if (e.alive >= e.targetCount) { e.jobs.RemoveAt(j); continue; }
                if (aliveList.Count >= maxAliveGlobal) break;

                if (FindSpawnPosition(out Vector3 pos))
                {
                    var parent = propsParent ? propsParent : transform;   // <- dùng Props Parent nếu có
                    var go = Instantiate(e.prefab, pos, Quaternion.identity, parent);
                    ApplyRandomOrientation(e, go);
                    go.AddComponent<Marker>().Init(this, go.transform, i);

                    aliveList.Add(new Live { t = go.transform, entryIndex = i });
                    e.alive++;

                    AstarGraphUpdater.UpdateFor(go);
                }

                e.jobs.RemoveAt(j);
            }

            // 2) Bảo đảm số job chờ đủ để bù thiếu (không để burst)
            int missing = Mathf.Max(0, e.targetCount - e.alive);
            int queued = CountJobs(e);
            int toQueue = Mathf.Max(0, missing - queued);
            for (int q = 0; q < toQueue; q++)
                e.jobs.Add(new Job { time = Time.time + RandomRespawn(e) });
        }
    }

    int CountJobs(Entry e)
    {
        int c = 0;
        if (e.jobs == null) return 0;
        float now = Time.time;
        for (int i = 0; i < e.jobs.Count; i++)
            if (e.jobs[i].time >= now) c++;
        return c;
    }

    float RandomRespawn(Entry e)
    {
        float a = Mathf.Max(0f, e.respawnTimeMin);
        float b = Mathf.Max(0f, e.respawnTimeMax);
        if (b < a) { var t = a; a = b; b = t; }
        return Mathf.Approximately(a, b) ? a : Random.Range(a, b);
    }

    // ---------- Spawn helpers ----------
    bool FindSpawnPosition(out Vector3 pos)
    {
        pos = player ? player.position : transform.position;
        if (!player || !cam) return false;

        Vector3 center = player.position;
        for (int i = 0; i < 36; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Random.Range(minRadius, maxRadius);
            Vector2 p = (Vector2)center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

            if (Physics2D.OverlapCircle(p, probeRadius, blockMask)) continue; // tránh địa hình cứng
            if (!IsOffscreen(cam, p, offscreenViewportPadding)) continue;     // BẮT BUỘC ngoài màn hình
            if (!PassSpacing(p)) continue;                                    // giữ khoảng cách với props khác

            pos = p; return true;
        }
        return false;
    }
    void TrySpawnBatch(int entryIndex, int count)
    {
        var e = entries[entryIndex];
        if (count <= 0) return;

        int canGlobal = Mathf.Max(0, maxAliveGlobal - aliveList.Count);
        int canType = Mathf.Max(0, e.targetCount - e.alive);
        int can = Mathf.Min(count, canGlobal, canType);
        if (can <= 0) return;

        int spawned = 0;
        for (int k = 0; k < can; k++)
        {
            if (!FindSpawnPosition(out Vector3 pos))
            {
                int remain = can - spawned;
                if (remain > 0) e.jobs.Add(new Job { time = Time.time + RandomRespawn(e) });
                break;
            }

            var parent = propsParent ? propsParent : transform;
            var go = Instantiate(e.prefab, pos, Quaternion.identity, parent);
            ApplyRandomOrientation(e, go);
            go.AddComponent<Marker>().Init(this, go.transform, entryIndex);

            aliveList.Add(new Live { t = go.transform, entryIndex = entryIndex });
            e.alive++;
            spawned++;
        }
    }


    bool IsOffscreen(Camera c, Vector3 world, float padViewport)
    {
        if (!c) return true;
        var v = c.WorldToViewportPoint(world);
        if (v.z < 0f) return true;
        return (v.x < -padViewport || v.x > 1f + padViewport ||
                v.y < -padViewport || v.y > 1f + padViewport);
    }

    bool PassSpacing(Vector2 p)
    {
        float minSqr = minSpacing * minSpacing;
        for (int i = 0; i < aliveList.Count; i++)
        {
            var t = aliveList[i].t;
            if (!t) continue;
            if (((Vector2)t.position - p).sqrMagnitude < minSqr) return false;
        }
        return true;
    }

    void ApplyRandomOrientation(Entry e, GameObject go)
    {
        if (e.randomRotation)
        {
            float z = Random.Range(0f, 360f);
            if (e.snapAngleDeg > 0.01f)
            {
                float step = e.snapAngleDeg;
                z = Mathf.Round(z / step) * step;
            }
            go.transform.rotation = Quaternion.Euler(0f, 0f, z);
        }

        if (e.randomFlipX || e.randomFlipY)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr)
            {
                if (e.randomFlipX) sr.flipX = (Random.value < 0.5f);
                if (e.randomFlipY) sr.flipY = (Random.value < 0.5f);
            }
        }
    }

    // ---------- Despawn: chỉ khi xa VÀ offscreen ----------
    void CullFarOffscreen()
    {
        if (!player || !cam) return;

        for (int i = aliveList.Count - 1; i >= 0; i--)
        {
            var s = aliveList[i];
            if (!s.t) { aliveList.RemoveAt(i); continue; }

            bool tooFar = Vector2.Distance(player.position, s.t.position) > despawnDistance;
            bool offscr = IsOffscreen(cam, s.t.position, offscreenViewportPadding);

            if (tooFar && offscr)
            {
                var mk = s.t.GetComponent<Marker>();
                if (mk) mk.MarkDespawn();
                Destroy(s.t.gameObject);
            }
        }
    }

    // ---------- Marker ----------
    class Marker : MonoBehaviour
    {
        PropSpawnDirectorLite dir; Transform me;
        int safety = 0; bool isDespawn; int entryIndex;

        public void Init(PropSpawnDirectorLite d, Transform t, int idx)
        { dir = d; me = t; entryIndex = idx; }

        public void MarkDespawn() { isDespawn = true; }

        //void OnDestroy()
        //{
        //    if (dir && me && safety++ == 0) dir.OnPropDestroyed(me, entryIndex, isDespawn);
        //}
        void OnDestroy()
        {
            if (dir && me && safety++ == 0)
            {
                AstarGraphUpdater.UpdateFor(gameObject); // mở lại vùng khi prop biến mất
                dir.OnPropDestroyed(me, entryIndex, isDespawn);
            }
        }

    }

    void OnPropDestroyed(Transform t, int entryIndex, bool isDespawn)
    {
        // giảm alive + lên lịch refill 1 slot (sau respawn time)
        for (int i = aliveList.Count - 1; i >= 0; i--)
        {
            if (aliveList[i].t == t)
            {
                aliveList.RemoveAt(i);
                break;
            }
        }

        if (entries == null || entryIndex < 0 || entryIndex >= entries.Length) return;
        var e = entries[entryIndex];
        e.alive = Mathf.Max(0, e.alive - 1);

        // refill 1 slot nếu đang thiếu
        if (e.alive < e.targetCount)
            e.jobs.Add(new Job { time = Time.time + RandomRespawn(e) });
    }

    // ---------- Gizmos ----------
    [Header("Gizmos")]
    public bool drawSpawnRings = true;
    public bool onlyWhenSelected = false;
    public Color gizMin = new Color(0.2f, 1f, 0.6f, 0.35f);
    public Color gizMax = new Color(0.2f, 0.6f, 1f, 0.30f);

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
