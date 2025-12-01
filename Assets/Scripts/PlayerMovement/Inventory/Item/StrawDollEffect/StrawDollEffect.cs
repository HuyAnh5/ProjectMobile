using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "StrawDollEffect", menuName = "Inventory/Effects/Straw Doll")]
public class StrawDollEffect : ItemEffect
{
    [Header("Cheat death")]
    [Range(0f, 1f)]
    public float cheatDeathChance = 0.2f;   // 20%

    [Header("Burn: decoy + invisibility")]
    public GameObject decoyPrefab;
    public float decoyLifetime = 3f;

    [Tooltip("Thời gian tàng hình (giây)")]
    public float invisibleDuration = 3f;

    public override void OnEquip(PlayerItemSlots target)
    {
        var player = target.Player;
        if (!player) return;

        var health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        // Đăng ký callback cheat death (giả định chỉ có 1 item dùng hook này)
        health.OnTryCheatDeath = () => TryCheatDeath(health);
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        var player = target.Player;
        if (!player) return;

        var health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        // Bỏ callback khi unequip
        if (health.OnTryCheatDeath != null)
            health.OnTryCheatDeath = null;
    }

    private bool TryCheatDeath(PlayerHealth health)
    {
        float r = Random.value;
        if (r > cheatDeathChance)
            return false; // không cứu

        if (health.logEvents)
            Debug.Log("[StrawDoll] Cheat death triggered.");

        // cho PlayerHealth biết là “ok, cứu”
        return true;
    }

    public override void OnBurn(PlayerItemSlots target)
    {
        if (!target.Player) return;
        target.RunCoroutine(BurnRoutine(target));
    }

    private IEnumerator BurnRoutine(PlayerItemSlots target)
    {
        var player = target.Player;
        var srs = player.GetComponentsInChildren<SpriteRenderer>();
        var hurtboxes = player.GetComponentsInChildren<Hurtbox>();

        // Spawn hình nhân thế mạng
        if (decoyPrefab)
        {
            var decoy = Object.Instantiate(decoyPrefab, player.transform.position, Quaternion.identity);
            Object.Destroy(decoy, decoyLifetime);
        }

        // Tắt Hurtbox + làm player mờ đi
        foreach (var hb in hurtboxes) hb.enabled = false;

        Color[] originalColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
        {
            originalColors[i] = srs[i].color;
            var c = originalColors[i];
            c.a = 0.3f;
            srs[i].color = c;
        }

        yield return new WaitForSeconds(invisibleDuration);

        // Khôi phục
        foreach (var hb in hurtboxes) hb.enabled = true;
        for (int i = 0; i < srs.Length; i++)
            srs[i].color = originalColors[i];
    }
}
