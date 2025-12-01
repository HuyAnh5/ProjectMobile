using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(fileName = "LightModifierEffect", menuName = "Inventory/Effects/Light Modifier")]
public class LightModifierEffect : ItemEffect
{
    [Header("FOV light (cone)")]
    public bool modifyFov = true;
    public float fovRangeMultiplier = 1.4f;

    public bool overrideFovOuterAngle = true;
    public float fovOuterAngle = 45f;
    public bool alsoScaleInnerAngle = true;

    public bool toggleFovEnabled = false;
    public bool fovEnabled = true;

    [Header("Aura light (vòng tròn 360°)")]
    public bool modifyAura = false;
    public float auraRangeMultiplier = 1f;

    public bool modifyAuraIntensity = false;
    public float auraIntensityMultiplier = 1f;

    public bool toggleAuraEnabled = false;
    public bool auraEnabled = true;

    [Header("Burn: spawn a laser prefab forward")]
    public GameObject laserBeamPrefab;
    public float laserLifetime = 3f;

    private struct LightState
    {
        // OilLamp multipliers
        public float fovRadiusMult;
        public float auraRadiusMult;
        public float fovIntensityMult;
        public float auraIntensityMult;

        // Per-level angles
        public float l1Outer, l2Outer, l3Outer;
        public float l1Inner, l2Inner, l3Inner;

        // Light enabled flags
        public bool fovEnabled;
        public bool auraEnabled;
    }

    private readonly Dictionary<PlayerItemSlots, LightState> _savedStates
        = new Dictionary<PlayerItemSlots, LightState>();

    public override void OnEquip(PlayerItemSlots target)
    {
        var lamp = target.OilLamp;
        if (!lamp) return;

        // Lưu state gốc (1 lần cho mỗi PlayerItemSlots)
        if (!_savedStates.ContainsKey(target))
        {
            var state = new LightState
            {
                fovRadiusMult = lamp.FovRadiusMultiplier,
                auraRadiusMult = lamp.AuraRadiusMultiplier,
                fovIntensityMult = lamp.FovIntensityMultiplier,
                auraIntensityMult = lamp.AuraIntensityMultiplier,
                l1Outer = lamp.level1.fovOuterAngle,
                l2Outer = lamp.level2.fovOuterAngle,
                l3Outer = lamp.level3.fovOuterAngle,
                l1Inner = lamp.level1.fovInnerAngle,
                l2Inner = lamp.level2.fovInnerAngle,
                l3Inner = lamp.level3.fovInnerAngle,
                fovEnabled = lamp.fovLight ? lamp.fovLight.enabled : false,
                auraEnabled = lamp.auraLight ? lamp.auraLight.enabled : false
            };

            _savedStates[target] = state;
        }

        // FOV
        if (modifyFov)
        {
            lamp.FovRadiusMultiplier *= fovRangeMultiplier;
        }

        if (overrideFovOuterAngle)
        {
            lamp.level1.fovOuterAngle = fovOuterAngle;
            lamp.level2.fovOuterAngle = fovOuterAngle;
            lamp.level3.fovOuterAngle = fovOuterAngle;

            if (alsoScaleInnerAngle)
            {
                var s = _savedStates[target];
                lamp.level1.fovInnerAngle = Mathf.Min(s.l1Inner, fovOuterAngle * 0.5f);
                lamp.level2.fovInnerAngle = Mathf.Min(s.l2Inner, fovOuterAngle * 0.5f);
                lamp.level3.fovInnerAngle = Mathf.Min(s.l3Inner, fovOuterAngle * 0.5f);
            }
        }

        if (toggleFovEnabled && lamp.fovLight)
        {
            lamp.fovLight.enabled = fovEnabled;
        }

        // Aura
        if (modifyAura)
        {
            lamp.AuraRadiusMultiplier *= auraRangeMultiplier;
        }

        if (modifyAuraIntensity)
        {
            lamp.AuraIntensityMultiplier *= auraIntensityMultiplier;
        }

        if (toggleAuraEnabled && lamp.auraLight)
        {
            lamp.auraLight.enabled = auraEnabled;
        }

        // Áp lại profile hiện tại với multiplier mới
        lamp.ReapplyCurrentLevelImmediate();
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        var lamp = target.OilLamp;
        if (!lamp) return;

        if (_savedStates.TryGetValue(target, out LightState s))
        {
            lamp.FovRadiusMultiplier = s.fovRadiusMult;
            lamp.AuraRadiusMultiplier = s.auraRadiusMult;
            lamp.FovIntensityMultiplier = s.fovIntensityMult;
            lamp.AuraIntensityMultiplier = s.auraIntensityMult;

            lamp.level1.fovOuterAngle = s.l1Outer;
            lamp.level2.fovOuterAngle = s.l2Outer;
            lamp.level3.fovOuterAngle = s.l3Outer;

            lamp.level1.fovInnerAngle = s.l1Inner;
            lamp.level2.fovInnerAngle = s.l2Inner;
            lamp.level3.fovInnerAngle = s.l3Inner;

            if (lamp.fovLight)
                lamp.fovLight.enabled = s.fovEnabled;
            if (lamp.auraLight)
                lamp.auraLight.enabled = s.auraEnabled;

            _savedStates.Remove(target);
        }

        lamp.ReapplyCurrentLevelImmediate();
    }

    public override void OnBurn(PlayerItemSlots target)
    {
        if (!laserBeamPrefab || !target.Player) return;

        Vector3 pos = target.Player.transform.position;
        Vector2 facing = target.Player.Facing.sqrMagnitude > 0.001f
            ? target.Player.Facing
            : Vector2.up;

        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        GameObject beam = GameObject.Instantiate(laserBeamPrefab, pos, rot);

        if (laserLifetime > 0f)
            target.RunCoroutine(DestroyAfterSeconds(beam, laserLifetime));
    }

    private System.Collections.IEnumerator DestroyAfterSeconds(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go) GameObject.Destroy(go);
    }
}
