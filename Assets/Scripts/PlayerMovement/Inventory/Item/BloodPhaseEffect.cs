using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "BloodPhaseEffect", menuName = "Inventory/Effects/Blood Phase")]
public class BloodPhaseEffect : ItemEffect
{
    [Header("Blood Phase config")]
    public float duration = 4f;
    public LayerMask enemyMask;
    public float hitRadius = 1.5f;

    [Tooltip("Damage per enemy touched per tick")]
    public float damagePerEnemy = 3f;

    [Tooltip("Oil gained per enemy hit")]
    public float oilGainPerEnemy = 5f;

    [Tooltip("Half-hearts healed per enemy hit (1 = +0.5 tim)")]
    public int healHalvesPerEnemy = 1;

    public override void OnBurn(PlayerItemSlots target)
    {
        if (!target.Player) return;
        target.RunCoroutine(BloodPhaseRoutine(target));
    }

    private IEnumerator BloodPhaseRoutine(PlayerItemSlots target)
    {
        var player = target.Player;
        var lamp = target.OilLamp;
        var health = player.GetComponent<PlayerHealth>();

        // 1) Bật trạng thái "bóng máu"
        // - Disable PlayerHealth để không nhận damage
        if (health) health.enabled = false;

        // - Cho collider thành trigger để xuyên qua
        var colliders = player.GetComponents<Collider2D>();
        var oldTrigger = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            oldTrigger[i] = colliders[i].isTrigger;
            colliders[i].isTrigger = true;
        }

        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            // Quét quanh player để gây damage + hút máu/dầu
            Vector2 pos = player.transform.position;
            var hits = Physics2D.OverlapCircleAll(pos, hitRadius, enemyMask);

            foreach (var col in hits)
            {
                var hb = col.GetComponent<Hurtbox>();
                if (hb != null && hb.enemyHealth != null)
                {
                    hb.enemyHealth.TakeDamage(damagePerEnemy);

                    if (lamp != null && oilGainPerEnemy > 0f)
                    {
                        // Dùng property để trigger logic OilLamp (level, tween, v.v.)
                        lamp.Current = lamp.Current + oilGainPerEnemy;
                    }


                    if (health != null && healHalvesPerEnemy > 0)
                    {
                        // tạm thời bật lại để gọi Heal, rồi tắt đi tiếp
                        health.enabled = true;
                        health.HealHalfHearts(healHalvesPerEnemy);
                        health.enabled = false;
                    }
                }
            }

            yield return null;
        }

        // 2) Tắt trạng thái blood phase, trả collider & health
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = oldTrigger[i];

        if (health) health.enabled = true;
    }
}
