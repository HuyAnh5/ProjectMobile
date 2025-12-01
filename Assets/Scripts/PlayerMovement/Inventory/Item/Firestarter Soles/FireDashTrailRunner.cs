using UnityEngine;
using System.Collections;

/// <summary>
/// Gắn lên cùng GameObject với DashController.
/// Theo dõi IsDashing để spawn lửa ở điểm xuất phát + vệt lửa sau dash.
/// Không đụng tới logic dash cốt lõi.
/// </summary>
[RequireComponent(typeof(DashController))]
public class FireDashTrailRunner : MonoBehaviour
{
    [Header("VFX / Prefabs")]
    public GameObject startFirePrefab;   // đốm lửa tròn tại điểm bắt đầu
    public GameObject trailFirePrefab;   // vệt lửa
    public float trailDuration = 2f;
    public float trailSpawnInterval = 0.06f;

    private DashController dash;
    private bool wasDashing;
    private Coroutine trailRoutine;

    void Awake()
    {
        dash = GetComponent<DashController>();
    }

    void Update()
    {
        bool isDashingNow = dash.IsDashing;

        // Dash bắt đầu
        if (isDashingNow && !wasDashing)
        {
            wasDashing = true;
            Vector3 startPos = transform.position;
            if (startFirePrefab)
                Instantiate(startFirePrefab, startPos, Quaternion.identity);
        }
        // Dash kết thúc
        else if (!isDashingNow && wasDashing)
        {
            wasDashing = false;

            if (trailRoutine != null)
                StopCoroutine(trailRoutine);

            if (trailFirePrefab)
                trailRoutine = StartCoroutine(SpawnTrail());
        }
    }

    private IEnumerator SpawnTrail()
    {
        float endTime = Time.time + trailDuration;
        while (Time.time < endTime)
        {
            Vector3 pos = transform.position;
            Instantiate(trailFirePrefab, pos, Quaternion.identity);
            yield return new WaitForSeconds(trailSpawnInterval);
        }
    }
}
