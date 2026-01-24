using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Universal StatEffect (MVP):
/// - Equip/Unequip: apply persistent stat modifiers.
/// - Burn: optional TEMP buff (duration) + optional instant heal.
/// - Supports: Player move, Dash, OilLamp (capacity/drain + aura/fov multipliers + optional angles),
///             Ranged (AutoAttackRunner), Melee (MeleeAutoRunner), Camera (CinemachineCamera lens ortho size),
///             HealInstant (via reflection on PlayerHealth).
///
/// Goal: 1 file only. Add new stat later = add enum + switch case.
/// </summary>
[CreateAssetMenu(fileName = "StatEffect", menuName = "Inventory/Effects/Stat (Modifier)")]
public class StatEffect : ItemEffect
{
    public enum Stat
    {
        // Player
        MoveSpeed,

        // Dash
        DashDistance,
        DashCooldown,
        DashOilCost,

        // Oil
        OilCapacity,
        OilDrainMultiplier,
        OilBaseDrainPerSecond,

        // Lantern / vision multipliers (these DO work with current OilLamp)
        FovRadiusMultiplier,
        AuraRadiusMultiplier,
        FovIntensityMultiplier,
        AuraIntensityMultiplier,

        // Optional: angle tweaks (OilLamp currently keeps angles from profiles; we modify profiles directly)
        FovOuterAngle,
        FovInnerAngle,

        // Ranged (AutoAttackRunner)
        RangedDamageMultiplier,
        RangedFireRateMultiplier,
        RangedProjectileSpeedMultiplier,
        RangedProjectileLifetimeMultiplier,
        RangedKnockbackBonus,

        // Melee (MeleeAutoRunner)
        MeleeDamageMultiplier,
        MeleeSwingCooldown,
        MeleeWeaponRange,
        MeleeKnockbackForce,

        // Camera
        CameraOrthoSize,

        // Utility
        HealInstant,
    }

    public enum Op { Add, Multiply }

    [System.Serializable]
    public class Modifier
    {
        public Stat stat;
        public Op op = Op.Add;

        [Tooltip("Add: +value. Multiply: *value (e.g. 1.2 = +20%).")]
        public float value = 1f;

        [Header("Apply timing")]
        public bool applyOnEquip = true;
        public bool applyOnUnequip = true;

        [Header("Burn temporary buff")]
        public bool applyTempOnBurn = false;
        [Min(0.05f)] public float burnDuration = 3f;
        public bool refreshIfActive = true;
    }

    [Header("Modifiers")]
    public List<Modifier> modifiers = new List<Modifier>();

    // Track active burn coroutine per target+stat (per asset)
    private readonly Dictionary<PlayerItemSlots, Dictionary<Stat, Coroutine>> _activeBurn =
        new Dictionary<PlayerItemSlots, Dictionary<Stat, Coroutine>>();

    // ---------------- Public hooks ----------------

    public override void OnEquip(PlayerItemSlots target)
    {
        if (target == null || modifiers == null) return;

        bool lampDirty = false;

        foreach (var m in modifiers)
        {
            if (m == null || !m.applyOnEquip) continue;
            if (m.stat == Stat.HealInstant) continue; // don't heal on equip
            lampDirty |= ApplyModifier(target, m);
        }

        if (lampDirty)
        {
            var lamp = GetLamp(target);
            lamp?.ReapplyCurrentLevelImmediate();
        }
    }

    public override void OnUnequip(PlayerItemSlots target)
    {
        if (target == null || modifiers == null) return;

        bool lampDirty = false;

        foreach (var m in modifiers)
        {
            if (m == null || !m.applyOnUnequip) continue;
            if (m.stat == Stat.HealInstant) continue;
            lampDirty |= UndoModifier(target, m);
        }

        if (lampDirty)
        {
            var lamp = GetLamp(target);
            lamp?.ReapplyCurrentLevelImmediate();
        }
    }

    public override void OnBurn(PlayerItemSlots target)
    {
        if (target == null || modifiers == null) return;

        bool lampDirty = false;

        foreach (var m in modifiers)
        {
            if (m == null) continue;

            // Instant heal on burn
            if (m.stat == Stat.HealInstant)
            {
                if (m.op == Op.Add && m.value > 0f)
                    TryHeal(target, m.value);
                continue;
            }

            if (!m.applyTempOnBurn) continue;

            // Refresh logic: stop old timer + undo old buff then reapply
            if (m.refreshIfActive &&
                _activeBurn.TryGetValue(target, out var dict) && dict != null &&
                dict.TryGetValue(m.stat, out var running) && running != null)
            {
                target.StopCoroutine(running);
                dict.Remove(m.stat);
                lampDirty |= UndoModifier(target, m);
            }

            lampDirty |= ApplyModifier(target, m);

            // schedule revert
            var co = target.StartCoroutine(RevertAfter(target, m));
            if (!_activeBurn.TryGetValue(target, out dict) || dict == null)
            {
                dict = new Dictionary<Stat, Coroutine>();
                _activeBurn[target] = dict;
            }
            dict[m.stat] = co;
        }

        if (lampDirty)
        {
            var lamp = GetLamp(target);
            lamp?.ReapplyCurrentLevelImmediate();
        }
    }

    private IEnumerator RevertAfter(PlayerItemSlots target, Modifier m)
    {
        float dur = Mathf.Max(0.05f, m.burnDuration);
        yield return new WaitForSeconds(dur);

        bool lampDirty = UndoModifier(target, m);
        if (lampDirty)
        {
            var lamp = GetLamp(target);
            lamp?.ReapplyCurrentLevelImmediate();
        }

        if (_activeBurn.TryGetValue(target, out var dict) && dict != null)
        {
            dict.Remove(m.stat);
            if (dict.Count == 0) _activeBurn.Remove(target);
        }
    }

    // ---------------- Core math ----------------

    private static float Apply(float cur, Modifier m)
    {
        if (m.op == Op.Add) return cur + m.value;

        float mul = Mathf.Approximately(m.value, 0f) ? 0.0001f : m.value;
        return cur * mul;
    }

    private static float Undo(float cur, Modifier m)
    {
        if (m.op == Op.Add) return cur - m.value;

        float mul = Mathf.Approximately(m.value, 0f) ? 0.0001f : m.value;
        return cur / mul;
    }

    // Return true if this modifier touched OilLamp light/profile (needs refresh)
    private bool ApplyModifier(PlayerItemSlots t, Modifier m)
    {
        bool lampDirty = false;

        var player = t.Player;
        var dash = t.Dash;
        var lamp = GetLamp(t);
        var ranged = GetRanged(t);
        var melee = GetMelee(t);

        switch (m.stat)
        {
            // --- Player ---
            case Stat.MoveSpeed:
                if (player != null) player.MoveSpeed = Mathf.Max(0f, Apply(player.MoveSpeed, m));
                break;

            // --- Dash ---
            case Stat.DashDistance:
                if (dash != null) dash.DefaultDashDistance = Mathf.Max(0.1f, Apply(dash.DefaultDashDistance, m));
                break;

            case Stat.DashCooldown:
                if (dash != null) dash.Cooldown = Mathf.Max(0f, Apply(dash.Cooldown, m));
                break;

            case Stat.DashOilCost:
                if (dash != null) dash.OilCost = Mathf.Max(0f, Apply(dash.OilCost, m));
                break;

            // --- Oil ---
            case Stat.OilCapacity:
                if (lamp != null) lamp.Capacity = Mathf.Max(0f, Apply(lamp.Capacity, m));
                break;

            case Stat.OilDrainMultiplier:
                if (lamp != null) lamp.DrainMultiplier = Mathf.Max(0f, Apply(lamp.DrainMultiplier, m));
                break;

            case Stat.OilBaseDrainPerSecond:
                if (lamp != null) lamp.BaseDrainPerSecond = Mathf.Max(0f, Apply(lamp.BaseDrainPerSecond, m));
                break;

            // --- Lamp multipliers (needs refresh) ---
            case Stat.FovRadiusMultiplier:
                if (lamp != null) { lamp.FovRadiusMultiplier = Mathf.Max(0f, Apply(lamp.FovRadiusMultiplier, m)); lampDirty = true; }
                break;

            case Stat.AuraRadiusMultiplier:
                if (lamp != null) { lamp.AuraRadiusMultiplier = Mathf.Max(0f, Apply(lamp.AuraRadiusMultiplier, m)); lampDirty = true; }
                break;

            case Stat.FovIntensityMultiplier:
                if (lamp != null) { lamp.FovIntensityMultiplier = Mathf.Max(0f, Apply(lamp.FovIntensityMultiplier, m)); lampDirty = true; }
                break;

            case Stat.AuraIntensityMultiplier:
                if (lamp != null) { lamp.AuraIntensityMultiplier = Mathf.Max(0f, Apply(lamp.AuraIntensityMultiplier, m)); lampDirty = true; }
                break;

            // --- Lamp angles (edit profiles directly; needs refresh) ---
            case Stat.FovOuterAngle:
                if (lamp != null)
                {
                    lamp.level1.fovOuterAngle = Apply(lamp.level1.fovOuterAngle, m);
                    lamp.level2.fovOuterAngle = Apply(lamp.level2.fovOuterAngle, m);
                    lamp.level3.fovOuterAngle = Apply(lamp.level3.fovOuterAngle, m);
                    lampDirty = true;
                }
                break;

            case Stat.FovInnerAngle:
                if (lamp != null)
                {
                    lamp.level1.fovInnerAngle = Apply(lamp.level1.fovInnerAngle, m);
                    lamp.level2.fovInnerAngle = Apply(lamp.level2.fovInnerAngle, m);
                    lamp.level3.fovInnerAngle = Apply(lamp.level3.fovInnerAngle, m);
                    lampDirty = true;
                }
                break;

            // --- Ranged (AutoAttackRunner) ---
            case Stat.RangedDamageMultiplier:
                if (ranged != null) ranged.DamageMultiplier = Mathf.Max(0f, Apply(ranged.DamageMultiplier, m));
                break;

            case Stat.RangedFireRateMultiplier:
                if (ranged != null) ranged.FireRateMultiplier = Mathf.Max(0.01f, Apply(ranged.FireRateMultiplier, m));
                break;

            case Stat.RangedProjectileSpeedMultiplier:
                if (ranged != null) ranged.ProjectileSpeedMultiplier = Mathf.Max(0.01f, Apply(ranged.ProjectileSpeedMultiplier, m));
                break;

            case Stat.RangedProjectileLifetimeMultiplier:
                if (ranged != null) ranged.ProjectileLifetimeMultiplier = Mathf.Max(0.01f, Apply(ranged.ProjectileLifetimeMultiplier, m));
                break;

            case Stat.RangedKnockbackBonus:
                if (ranged != null) ranged.KnockbackBonus = Mathf.Max(0f, Apply(ranged.KnockbackBonus, m));
                break;

            // --- Melee (MeleeAutoRunner) ---
            case Stat.MeleeDamageMultiplier:
                if (melee != null) melee.DamageMultiplier = Mathf.Max(0f, Apply(melee.DamageMultiplier, m));
                break;

            case Stat.MeleeSwingCooldown:
                if (melee != null) melee.SwingCooldown = Mathf.Max(0f, Apply(melee.SwingCooldown, m));
                break;

            case Stat.MeleeWeaponRange:
                if (melee != null) melee.WeaponRange = Mathf.Max(0f, Apply(melee.WeaponRange, m));
                break;

            case Stat.MeleeKnockbackForce:
                if (melee != null) melee.KnockbackForce = Mathf.Max(0f, Apply(melee.KnockbackForce, m));
                break;

            // --- Camera (CinemachineCamera Lens.OrthographicSize) ---
            case Stat.CameraOrthoSize:
                {
                    float cur = GetCinemachineOrMainOrthoSize();
                    float next = Mathf.Max(0.1f, Apply(cur, m));
                    SetCinemachineOrMainOrthoSize(next);
                }
                break;
        }

        return lampDirty;
    }

    private bool UndoModifier(PlayerItemSlots t, Modifier m)
    {
        bool lampDirty = false;

        var player = t.Player;
        var dash = t.Dash;
        var lamp = GetLamp(t);
        var ranged = GetRanged(t);
        var melee = GetMelee(t);

        switch (m.stat)
        {
            // --- Player ---
            case Stat.MoveSpeed:
                if (player != null) player.MoveSpeed = Mathf.Max(0f, Undo(player.MoveSpeed, m));
                break;

            // --- Dash ---
            case Stat.DashDistance:
                if (dash != null) dash.DefaultDashDistance = Mathf.Max(0.1f, Undo(dash.DefaultDashDistance, m));
                break;

            case Stat.DashCooldown:
                if (dash != null) dash.Cooldown = Mathf.Max(0f, Undo(dash.Cooldown, m));
                break;

            case Stat.DashOilCost:
                if (dash != null) dash.OilCost = Mathf.Max(0f, Undo(dash.OilCost, m));
                break;

            // --- Oil ---
            case Stat.OilCapacity:
                if (lamp != null) lamp.Capacity = Mathf.Max(0f, Undo(lamp.Capacity, m));
                break;

            case Stat.OilDrainMultiplier:
                if (lamp != null) lamp.DrainMultiplier = Mathf.Max(0f, Undo(lamp.DrainMultiplier, m));
                break;

            case Stat.OilBaseDrainPerSecond:
                if (lamp != null) lamp.BaseDrainPerSecond = Mathf.Max(0f, Undo(lamp.BaseDrainPerSecond, m));
                break;

            // --- Lamp multipliers (needs refresh) ---
            case Stat.FovRadiusMultiplier:
                if (lamp != null) { lamp.FovRadiusMultiplier = Mathf.Max(0f, Undo(lamp.FovRadiusMultiplier, m)); lampDirty = true; }
                break;

            case Stat.AuraRadiusMultiplier:
                if (lamp != null) { lamp.AuraRadiusMultiplier = Mathf.Max(0f, Undo(lamp.AuraRadiusMultiplier, m)); lampDirty = true; }
                break;

            case Stat.FovIntensityMultiplier:
                if (lamp != null) { lamp.FovIntensityMultiplier = Mathf.Max(0f, Undo(lamp.FovIntensityMultiplier, m)); lampDirty = true; }
                break;

            case Stat.AuraIntensityMultiplier:
                if (lamp != null) { lamp.AuraIntensityMultiplier = Mathf.Max(0f, Undo(lamp.AuraIntensityMultiplier, m)); lampDirty = true; }
                break;

            // --- Lamp angles (edit profiles; needs refresh) ---
            case Stat.FovOuterAngle:
                if (lamp != null)
                {
                    lamp.level1.fovOuterAngle = Undo(lamp.level1.fovOuterAngle, m);
                    lamp.level2.fovOuterAngle = Undo(lamp.level2.fovOuterAngle, m);
                    lamp.level3.fovOuterAngle = Undo(lamp.level3.fovOuterAngle, m);
                    lampDirty = true;
                }
                break;

            case Stat.FovInnerAngle:
                if (lamp != null)
                {
                    lamp.level1.fovInnerAngle = Undo(lamp.level1.fovInnerAngle, m);
                    lamp.level2.fovInnerAngle = Undo(lamp.level2.fovInnerAngle, m);
                    lamp.level3.fovInnerAngle = Undo(lamp.level3.fovInnerAngle, m);
                    lampDirty = true;
                }
                break;

            // --- Ranged ---
            case Stat.RangedDamageMultiplier:
                if (ranged != null) ranged.DamageMultiplier = Mathf.Max(0f, Undo(ranged.DamageMultiplier, m));
                break;

            case Stat.RangedFireRateMultiplier:
                if (ranged != null) ranged.FireRateMultiplier = Mathf.Max(0.01f, Undo(ranged.FireRateMultiplier, m));
                break;

            case Stat.RangedProjectileSpeedMultiplier:
                if (ranged != null) ranged.ProjectileSpeedMultiplier = Mathf.Max(0.01f, Undo(ranged.ProjectileSpeedMultiplier, m));
                break;

            case Stat.RangedProjectileLifetimeMultiplier:
                if (ranged != null) ranged.ProjectileLifetimeMultiplier = Mathf.Max(0.01f, Undo(ranged.ProjectileLifetimeMultiplier, m));
                break;

            case Stat.RangedKnockbackBonus:
                if (ranged != null) ranged.KnockbackBonus = Mathf.Max(0f, Undo(ranged.KnockbackBonus, m));
                break;

            // --- Melee ---
            case Stat.MeleeDamageMultiplier:
                if (melee != null) melee.DamageMultiplier = Mathf.Max(0f, Undo(melee.DamageMultiplier, m));
                break;

            case Stat.MeleeSwingCooldown:
                if (melee != null) melee.SwingCooldown = Mathf.Max(0f, Undo(melee.SwingCooldown, m));
                break;

            case Stat.MeleeWeaponRange:
                if (melee != null) melee.WeaponRange = Mathf.Max(0f, Undo(melee.WeaponRange, m));
                break;

            case Stat.MeleeKnockbackForce:
                if (melee != null) melee.KnockbackForce = Mathf.Max(0f, Undo(melee.KnockbackForce, m));
                break;

            // --- Camera ---
            case Stat.CameraOrthoSize:
                {
                    float cur = GetCinemachineOrMainOrthoSize();
                    float prev = Mathf.Max(0.1f, Undo(cur, m));
                    SetCinemachineOrMainOrthoSize(prev);
                }
                break;
        }

        return lampDirty;
    }

    // ---------------- Target getters (robust) ----------------

    private static OilLamp GetLamp(PlayerItemSlots t)
    {
        if (t.OilLamp != null) return t.OilLamp;
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<OilLamp>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<OilLamp>();
#endif
    }

    private static AutoAttackRunner GetRanged(PlayerItemSlots t)
    {
        if (t.RangedRunner != null) return t.RangedRunner;
        // runner thường nằm dưới children
        var rr = t.GetComponentInChildren<AutoAttackRunner>(true);
        if (rr != null) return rr;
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<AutoAttackRunner>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<AutoAttackRunner>();
#endif
    }

    private static MeleeAutoRunner GetMelee(PlayerItemSlots t)
    {
        if (t.MeleeRunner != null) return t.MeleeRunner;
        var mr = t.GetComponentInChildren<MeleeAutoRunner>(true);
        if (mr != null) return mr;
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<MeleeAutoRunner>(FindObjectsInactive.Include);
#else
        return Object.FindObjectOfType<MeleeAutoRunner>();
#endif
    }

    // ---------------- Heal (reflection) ----------------

    private static void TryHeal(PlayerItemSlots t, float amount)
    {
        if (t.Health == null) return;
        var health = t.Health;
        var type = health.GetType();

        MethodInfo mi =
            type.GetMethod("Heal", new[] { typeof(float) }) ??
            type.GetMethod("Heal", new[] { typeof(int) }) ??
            type.GetMethod("AddHealth", new[] { typeof(float) }) ??
            type.GetMethod("AddHealth", new[] { typeof(int) });

        if (mi == null) return;

        var p = mi.GetParameters();
        if (p.Length == 1 && p[0].ParameterType == typeof(int))
            mi.Invoke(health, new object[] { Mathf.RoundToInt(amount) });
        else
            mi.Invoke(health, new object[] { amount });
    }

    // ---------------- Camera (CinemachineCamera via reflection) ----------------

    private static Component FindCinemachineCameraComponent()
    {
        // We look for a Component whose type name is "CinemachineCamera" (CM3).
#if UNITY_2023_1_OR_NEWER
        var comps = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var comps = Object.FindObjectsOfType<Component>(true);
#endif
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;
            var tn = c.GetType().Name;
            if (tn == "CinemachineCamera") return c;
        }
        return null;
    }

    private static float GetCinemachineOrMainOrthoSize()
    {
        // Prefer CinemachineCamera Lens.OrthographicSize
        var cm = FindCinemachineCameraComponent();
        if (cm != null)
        {
            if (TryGetLensOrthoSize(cm, out float size))
                return size;
        }

        // Fallback to Unity Camera
        var cam = Camera.main;
        if (cam != null && cam.orthographic) return cam.orthographicSize;

        // Last fallback: any camera
#if UNITY_2023_1_OR_NEWER
        cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
#else
        cam = Object.FindObjectOfType<Camera>();
#endif
        if (cam != null && cam.orthographic) return cam.orthographicSize;

        return 10f;
    }

    private static void SetCinemachineOrMainOrthoSize(float newSize)
    {
        var cm = FindCinemachineCameraComponent();
        if (cm != null)
        {
            if (TrySetLensOrthoSize(cm, newSize))
                return;
        }

        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = newSize;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        cam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
#else
        cam = Object.FindObjectOfType<Camera>();
#endif
        if (cam != null && cam.orthographic)
            cam.orthographicSize = newSize;
    }

    private static bool TryGetLensOrthoSize(Component cmCam, out float size)
    {
        size = 0f;
        var t = cmCam.GetType();

        // property or field "Lens"
        var lensProp = t.GetProperty("Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var lensField = t.GetField("Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        object lensObj = null;
        if (lensProp != null) lensObj = lensProp.GetValue(cmCam);
        else if (lensField != null) lensObj = lensField.GetValue(cmCam);

        if (lensObj == null) return false;

        var lt = lensObj.GetType();
        var orthoProp = lt.GetProperty("OrthographicSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var orthoField = lt.GetField("OrthographicSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (orthoProp != null)
        {
            size = (float)orthoProp.GetValue(lensObj);
            return true;
        }
        if (orthoField != null)
        {
            size = (float)orthoField.GetValue(lensObj);
            return true;
        }

        return false;
    }

    private static bool TrySetLensOrthoSize(Component cmCam, float newSize)
    {
        var t = cmCam.GetType();

        var lensProp = t.GetProperty("Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var lensField = t.GetField("Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        bool canSetLens = (lensProp != null && lensProp.CanWrite) || (lensField != null && !lensField.IsInitOnly);
        if (!canSetLens) return false;

        object lensObj = null;
        if (lensProp != null) lensObj = lensProp.GetValue(cmCam);
        else if (lensField != null) lensObj = lensField.GetValue(cmCam);

        if (lensObj == null) return false;

        // lens is likely a struct -> boxed. Modify boxed then set back.
        var lt = lensObj.GetType();
        var orthoProp = lt.GetProperty("OrthographicSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var orthoField = lt.GetField("OrthographicSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (orthoProp != null && orthoProp.CanWrite)
        {
            orthoProp.SetValue(lensObj, newSize);
        }
        else if (orthoField != null && !orthoField.IsInitOnly)
        {
            orthoField.SetValue(lensObj, newSize);
        }
        else
        {
            return false;
        }

        // set lens back to component
        if (lensProp != null) lensProp.SetValue(cmCam, lensObj);
        else lensField.SetValue(cmCam, lensObj);

        return true;
    }
}
