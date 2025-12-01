using UnityEngine;

[CreateAssetMenu(fileName = "FirestarterSolesEffect", menuName = "Inventory/Effects/Firestarter Soles")]
public class FirestarterSolesEffect : ItemEffect
{
    [Header("Dash oil cost")]
    public float extraDashOilCost = 2f;

    [Header("Fire VFX / Prefabs")]
    public GameObject startFirePrefab;
    public GameObject trailFirePrefab;

    [Header("Burn: Hellfire Nova")]
    public GameObject hellfireNovaPrefab;
    public float hellfireRadius = 7f; // tuỳ anh, prefab lo phần thực tế
    public float hellfireLifetime = 1.5f;

    // cache oilCost cũ để trả lại khi unequip
    private readonly System.Collections.Generic.Dictionary<PlayerItemSlots, float> _originalDashCost
        = new();

    public override void OnEquip(PlayerItemSlots target)
    {
        var dash = target.Dash;
        if (dash == null) return;

        if (!_originalDashCost.ContainsKey(target))
            _originalDashCost[target] = dash.OilCost;

        dash.OilCost = _originalDashCost[target] + extraDashOilCost;

        // đảm bảo có FireDashTrailRunner
        var runner = dash.GetComponent<FireDashTrailRunner>();
        if (!runner)
            runner = dash.gameObject.AddComponent<FireDashTrailRunner>();

        runner.startFirePrefab = startFirePrefab;
        runner.trailFirePrefab = trailFirePrefab;
        // trailDuration / interval chỉnh trong prefab component hoặc add field public
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        var dash = target.Dash;
        if (dash == null) return;

        if (_originalDashCost.TryGetValue(target, out float original))
        {
            dash.OilCost = original;
            _originalDashCost.Remove(target);
        }
        else
        {
            dash.OilCost = 0f;
        }

        var runner = dash.GetComponent<FireDashTrailRunner>();
        if (runner)
            Object.Destroy(runner);
    }

    public override void OnBurn(PlayerItemSlots target)
    {
        if (!hellfireNovaPrefab || !target.Player) return;

        Vector3 pos = target.Player.transform.position;
        var nova = Object.Instantiate(hellfireNovaPrefab, pos, Quaternion.identity);

        if (hellfireLifetime > 0f)
            target.RunCoroutine(DestroyAfterSeconds(nova, hellfireLifetime));
    }

    private System.Collections.IEnumerator DestroyAfterSeconds(GameObject go, float t)
    {
        yield return new WaitForSeconds(t);
        if (go) Object.Destroy(go);
    }
}
