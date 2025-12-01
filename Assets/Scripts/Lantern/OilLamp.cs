using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// OilLamp:
/// - Manages oil capacity / current amount.
/// - Drains oil over time (base drain + external drains from weapons, curses, etc.).
/// - Controls 2 lights:
///     + FOV light (cone-like, using outer/inner angle + radius).
///     + Aura light (round light around the player).
/// - Has 3 "level profiles" (L1 / L2 / L3) based on oil thresholds:
///     L1: oil >  T1_Low
///     L2: T2_Low < oil <= T1_Low
///     L3: oil <= T2_Low (including 0)
/// - When crossing thresholds, it switches target level ONCE and tween toward that
///   profile over time (does not continuously remap based on oil inside the band).
/// - Optionally keeps the legacy stepped behaviour for backward compatibility.
/// </summary>
[DisallowMultipleComponent]
public class OilLamp : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Oil basic values
    // -------------------------------------------------------------------------

    [Header("Oil")]
    [Tooltip("Maximum oil capacity (u).")]
    public float capacity = 100f;

    [Tooltip("Current oil amount (u).")]
    public float current = 100f;

    [Tooltip("Base drain (units per second).")]
    public float baseDrainPerSecond = 1.0f;

    [Tooltip("Multiplier applied to total drain (base + external).")]
    public float drainMultiplier = 1f;

    // ---- External access for items (StatEffect) ----

    /// <summary>Max oil (u). Clamped >= 0. Nếu giảm capacity mà current > capacity thì current cũng bị kẹp lại.</summary>
    public float Capacity
    {
        get => capacity;
        set
        {
            capacity = Mathf.Max(0f, value);
            if (current > capacity)
                current = capacity;
        }
    }

    /// <summary>Current oil (u). Setter đi qua AddOil/DrainOil để vẫn kích ngưỡng L1/L2/L3 đúng.</summary>
    public float Current
    {
        get => current;
        set
        {
            float clamped = Mathf.Clamp(value, 0f, capacity);
            if (Mathf.Approximately(clamped, current)) return;

            float delta = clamped - current;
            if (delta > 0f) AddOil(delta);
            else if (delta < 0f) DrainOil(-delta);
        }
    }

    /// <summary>Base drain (không tính multiplier). Item có thể chỉnh thẳng số này.</summary>
    public float BaseDrainPerSecond
    {
        get => baseDrainPerSecond;
        set => baseDrainPerSecond = Mathf.Max(0f, value);
    }

    /// <summary>Multiplier cho tổng drain (ví dụ curse x1.5).</summary>
    public float DrainMultiplier
    {
        get => drainMultiplier;
        set => drainMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>Ngưỡng chuyển L1->L2.</summary>
    public float Threshold1Low
    {
        get => T1_Low;
        set => T1_Low = Mathf.Max(0f, value);
    }

    /// <summary>Ngưỡng chuyển L2->L3.</summary>
    public float Threshold2Low
    {
        get => T2_Low;
        set => T2_Low = Mathf.Max(0f, value);
    }

    [Header("External multipliers (for items)")]
    [Tooltip("Nhân bán kính FOV của mọi level (L1/L2/L3). 1 = giữ nguyên.")]
    [SerializeField] private float fovRadiusMultiplier = 1f;

    [Tooltip("Nhân bán kính Aura của mọi level (L1/L2/L3).")]
    [SerializeField] private float auraRadiusMultiplier = 1f;

    [Tooltip("Nhân độ sáng FOV.")]
    [SerializeField] private float fovIntensityMultiplier = 1f;

    [Tooltip("Nhân độ sáng Aura.")]
    [SerializeField] private float auraIntensityMultiplier = 1f;

    public float FovRadiusMultiplier
    {
        get => fovRadiusMultiplier;
        set => fovRadiusMultiplier = Mathf.Max(0f, value);
    }

    public float AuraRadiusMultiplier
    {
        get => auraRadiusMultiplier;
        set => auraRadiusMultiplier = Mathf.Max(0f, value);
    }

    public float FovIntensityMultiplier
    {
        get => fovIntensityMultiplier;
        set => fovIntensityMultiplier = Mathf.Max(0f, value);
    }

    public float AuraIntensityMultiplier
    {
        get => auraIntensityMultiplier;
        set => auraIntensityMultiplier = Mathf.Max(0f, value);
    }



    // -------------------------------------------------------------------------
    // Light references
    // -------------------------------------------------------------------------

    [Header("Lights (drag from Lantern)")]
    [Tooltip("Aura light (round Point Light).")]
    public Light2D auraLight; // Point Light 2D (round aura)

    [Tooltip("FOV light (cone-like Point Light using angles).")]
    public Light2D fovLight;  // Point Light 2D (use outer/inner angles)


    // -------------------------------------------------------------------------
    // Instant thresholds (L1 -> L2 -> L3)
    // -------------------------------------------------------------------------

    [Header("Thresholds (for L1→L2 and L2→L3)")]
    [Tooltip("Threshold for L1 -> L2 (default 60u).")]
    public float T1_Low = 60f;

    [Tooltip("Threshold for L2 -> L3 (default 20u).")]
    public float T2_Low = 20f;

    // Old ramp values kept only so Inspector does not break existing scenes.
    // They are only used in legacy stepped mode.
    [HideInInspector] public float T1_High = 100f;
    [HideInInspector] public float T1to2 = 50f;
    [HideInInspector] public float T2to3 = 10f;


    // -------------------------------------------------------------------------
    // Exposed runtime value for other systems (melee, AI, etc.)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Current aura radius being applied to the aura Light2D.
    /// Melee and other systems use this to know "how far the light reaches".
    /// </summary>
    public float CurrentAuraRadius => curAuraRadius;


    // -------------------------------------------------------------------------
    // Per-level light profile
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class LevelProfile
    {
        [Header("FOV")]
        public float fovOuterRadius = 9f;
        public float fovOuterAngle = 70f;
        public float fovInnerAngle = 40f;
        public float fovIntensity = 1.2f;

        [Header("Aura")]
        public float auraRadius = 3f;
        public float auraIntensity = 0.6f;
    }

    [Header("Level 1 (oil >= T1_Low)")]
    public LevelProfile level1 = new LevelProfile
    {
        fovOuterRadius = 9.5f,
        fovOuterAngle = 80f,
        fovInnerAngle = 50f,
        fovIntensity = 1.2f,
        auraRadius = 3.2f,
        auraIntensity = 0.6f
    };

    [Header("Level 2 (T2_Low .. T1_Low)")]
    public LevelProfile level2 = new LevelProfile
    {
        fovOuterRadius = 7.2f,
        fovOuterAngle = 70f,
        fovInnerAngle = 42f,
        fovIntensity = 1.0f,
        auraRadius = 2.7f,
        auraIntensity = 0.55f
    };

    [Header("Level 3 (oil <= T2_Low)")]
    public LevelProfile level3 = new LevelProfile
    {
        fovOuterRadius = 5.2f,
        fovOuterAngle = 58f,
        fovInnerAngle = 34f,
        fovIntensity = 0.85f,
        auraRadius = 2.1f,
        auraIntensity = 0.48f
    };

    [Header("When oil = 0u")]
    [Tooltip("If true, turn FOV light off when oil hits 0.")]
    public bool turnOffFOVAtZero = true;

    [Tooltip("Aura radius to use when oil = 0 (can be smaller than level3).")]
    public float auraRadiusAtZero = 1.2f;

    [Tooltip("Aura intensity to use when oil = 0.")]
    public float auraIntensityAtZero = 0.4f;


    // -------------------------------------------------------------------------
    // Mode & tween config
    // -------------------------------------------------------------------------

    [Header("Mode & Tween")]
    [Tooltip("Use instant thresholds (L1/L2/L3 switch events) instead of old ramps.")]
    public bool useInstantThresholds = true;

    [Tooltip("Default tween duration between level profiles (seconds).")]
    [Min(0f)] public float transitionDuration = 0.35f;

    [Tooltip("Specific tween duration for L1 -> L2 (seconds).")]
    [Min(0f)] public float transitionDurationL1toL2 = 0.9f;

    [Tooltip("Specific tween duration for L2 -> L3 (seconds).")]
    [Min(0f)] public float transitionDurationL2toL3 = 0.35f;

    [Tooltip("Tween duration for going back up (L3->L2, L2->L1).")]
    [Min(0f)] public float transitionDurationBack = 0.35f;

    /// <summary>Runtime: active tween duration for the current transition.</summary>
    float _activeDuration = -1f;


    // -------------------------------------------------------------------------
    // External drains (weapons, curses, buffs, etc.)
    // -------------------------------------------------------------------------

    [Header("External drains (weapons, curses, etc.)")]
    private float extraDrainPerSecond = 0f;

    /// <summary>
    /// Register an additional drain per second (e.g. from a ranged weapon).
    /// Call this from OnEnable of the source.
    /// </summary>
    public void RegisterExtraDrain(float amount)
    {
        extraDrainPerSecond += amount;
    }

    /// <summary>
    /// Unregister a previously added drain amount.
    /// Call this from OnDisable of the source.
    /// </summary>
    public void UnregisterExtraDrain(float amount)
    {
        extraDrainPerSecond -= amount;
    }


    // -------------------------------------------------------------------------
    // Runtime state for instant-threshold mode
    // -------------------------------------------------------------------------

    private enum LampLevel { L1, L2, L3 }

    /// <summary>Current logical level (L1/L2/L3) based on last threshold event.</summary>
    private LampLevel currentLevel;

    /// <summary>Target level we are tweening toward.</summary>
    private LampLevel targetLevel;

    // Current values applied to lights (tweened over time).
    float curFovRadius, curFovAngle, curFovInner, curFovIntensity;
    float curAuraRadius, curAuraIntensity;

    // Target values we are tweening toward.
    float tgtFovRadius, tgtFovAngle, tgtFovInner, tgtFovIntensity;
    float tgtAuraRadius, tgtAuraIntensity;

    // Tween speed (1/duration).
    float tweenSpeed =>
        (_activeDuration > 0f
            ? 1f / _activeDuration
            : (transitionDuration > 0f ? 1f / transitionDuration : float.PositiveInfinity));


    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Decide starting level based on current oil.
        currentLevel = targetLevel = DecideLevel(current);

        // Set target values for that level.
        ApplyTargetFromLevel(targetLevel);

        // Initialize current values to target so first frame is not popping.
        curFovRadius = tgtFovRadius;
        curFovAngle = tgtFovAngle;
        curFovInner = tgtFovInner;
        curFovIntensity = tgtFovIntensity;
        curAuraRadius = tgtAuraRadius;
        curAuraIntensity = tgtAuraIntensity;

        // Push initial values to the lights.
        PushToLightsImmediate();
    }

    private void Update()
    {
        // ---------------------------------------------------------------------
        // 1) Drain oil over time
        // ---------------------------------------------------------------------
        float drain = (baseDrainPerSecond + extraDrainPerSecond) * drainMultiplier;
        current = Mathf.Max(0f, current - drain * Time.deltaTime);

        // ---------------------------------------------------------------------
        // 2) Choose logic mode (instant thresholds vs legacy ramp)
        // ---------------------------------------------------------------------
        if (!useInstantThresholds)
        {
            // Use legacy ramp behaviour (kept for backward compatibility).
            ApplySteppedLegacy();
            return;
        }

        // ---------------------------------------------------------------------
        // 3) Decide desired level based on current oil and thresholds
        // ---------------------------------------------------------------------
        LampLevel desired = DecideLevel(current);

        // If target level changes, we set a new tween duration and new targets.
        if (desired != targetLevel)
        {
            // Choose specific tween duration based on transition direction.
            _activeDuration =
                (targetLevel == LampLevel.L1 && desired == LampLevel.L2) ? transitionDurationL1toL2 :
                (targetLevel == LampLevel.L2 && desired == LampLevel.L3) ? transitionDurationL2toL3 :
                transitionDurationBack; // for going back up

            targetLevel = desired;
            ApplyTargetFromLevel(targetLevel);
        }

        // ---------------------------------------------------------------------
        // 4) Tween current values towards target values
        // ---------------------------------------------------------------------
        float step = tweenSpeed * Time.deltaTime;

        curFovRadius = Mathf.MoveTowards(curFovRadius, tgtFovRadius, Mathf.Abs(tgtFovRadius - curFovRadius) * step + 0.0001f);
        curFovAngle = Mathf.MoveTowards(curFovAngle, tgtFovAngle, Mathf.Abs(tgtFovAngle - curFovAngle) * step + 0.0001f);
        curFovInner = Mathf.MoveTowards(curFovInner, tgtFovInner, Mathf.Abs(tgtFovInner - curFovInner) * step + 0.0001f);
        curFovIntensity = Mathf.MoveTowards(curFovIntensity, tgtFovIntensity, Mathf.Abs(tgtFovIntensity - curFovIntensity) * step + 0.0001f);

        curAuraRadius = Mathf.MoveTowards(curAuraRadius, tgtAuraRadius, Mathf.Abs(tgtAuraRadius - curAuraRadius) * step + 0.0001f);
        curAuraIntensity = Mathf.MoveTowards(curAuraIntensity, tgtAuraIntensity, Mathf.Abs(tgtAuraIntensity - curAuraIntensity) * step + 0.0001f);

        // ---------------------------------------------------------------------
        // 5) Apply tweened values to actual URP lights
        // ---------------------------------------------------------------------
        PushToLightsInstantMode();
    }


    // -------------------------------------------------------------------------
    // Instant-threshold core helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Decide which logical level (L1/L2/L3) we should be in based on oil.
    /// </summary>
    private LampLevel DecideLevel(float oil)
    {
        if (oil <= 0f) return LampLevel.L3; // treat 0 as L3
        if (oil <= T2_Low) return LampLevel.L3;
        if (oil <= T1_Low) return LampLevel.L2;
        return LampLevel.L1;
    }

    /// <summary>
    /// Sets the target values (tgt*) from a given level profile.
    /// </summary>
    private void ApplyTargetFromLevel(LampLevel lvl)
    {
        LevelProfile p = lvl == LampLevel.L1 ? level1 : (lvl == LampLevel.L2 ? level2 : level3);

        // Bán kính + độ sáng bị item ảnh hưởng, còn góc giữ nguyên (góc đặc biệt để item LightModifierEffect lo riêng).
        tgtFovRadius = p.fovOuterRadius * fovRadiusMultiplier;
        tgtFovAngle = p.fovOuterAngle;
        tgtFovInner = p.fovInnerAngle;
        tgtFovIntensity = p.fovIntensity * fovIntensityMultiplier;
        tgtAuraRadius = p.auraRadius * auraRadiusMultiplier;
        tgtAuraIntensity = p.auraIntensity * auraIntensityMultiplier;
    }


    /// <summary>
    /// Pushes tweened values (cur*) to Light2D while in instant-threshold mode.
    /// Handles special behaviour when oil hits 0.
    /// </summary>
    private void PushToLightsInstantMode()
    {
        if (fovLight)
        {
            float intensity = curFovIntensity;
            if (turnOffFOVAtZero && current <= 0.0001f)
                intensity = 0f;

            fovLight.pointLightOuterRadius = curFovRadius;
            fovLight.pointLightOuterAngle = curFovAngle;
            fovLight.pointLightInnerAngle = curFovInner;
            fovLight.intensity = intensity;
        }

        if (auraLight)
        {
            float rad = curAuraRadius;
            float inten = curAuraIntensity;

            if (current <= 0.0001f)
            {
                rad = auraRadiusAtZero;
                inten = auraIntensityAtZero;
            }

            auraLight.pointLightOuterRadius = rad;
            auraLight.intensity = inten;
        }
    }

    /// <summary>
    /// Immediately push target values (tgt*) to lights. Used at startup.
    /// </summary>
    private void PushToLightsImmediate()
    {
        if (fovLight)
        {
            float intensity = tgtFovIntensity;
            if (turnOffFOVAtZero && current <= 0.0001f)
                intensity = 0f;

            fovLight.pointLightOuterRadius = tgtFovRadius;
            fovLight.pointLightOuterAngle = tgtFovAngle;
            fovLight.pointLightInnerAngle = tgtFovInner;
            fovLight.intensity = intensity;
        }

        if (auraLight)
        {
            float rad = tgtAuraRadius;
            float inten = tgtAuraIntensity;

            if (current <= 0.0001f)
            {
                rad = auraRadiusAtZero;
                inten = auraIntensityAtZero;
            }

            auraLight.pointLightOuterRadius = rad;
            auraLight.intensity = inten;
        }
    }


    // -------------------------------------------------------------------------
    // LEGACY: stepped ramp logic (kept for backward compatibility)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Legacy behaviour: gradually ramps values based on oil bands
    /// instead of discrete level switches + tween.
    /// Only used when useInstantThresholds = false.
    /// </summary>
    private void ApplySteppedLegacy()
    {
        if (!auraLight && !fovLight) return;

        float fovRadius, fovAngle, fovInner, fovInt;
        float auraRad, auraInt;

        EvaluateSteppedLegacy(
            current,
            out fovRadius, out fovAngle, out fovInner, out fovInt,
            out auraRad, out auraInt
        );

        if (fovLight)
        {
            if (turnOffFOVAtZero && current <= 0.0001f)
                fovInt = 0f;

            fovLight.pointLightOuterRadius = fovRadius;
            fovLight.pointLightOuterAngle = fovAngle;
            fovLight.pointLightInnerAngle = fovInner;
            fovLight.intensity = fovInt;
        }

        if (auraLight)
        {
            if (current <= 0.0001f)
            {
                auraRad = auraRadiusAtZero;
                auraInt = auraIntensityAtZero;
            }

            auraLight.pointLightOuterRadius = auraRad;
            auraLight.intensity = auraInt;
        }
    }

    /// <summary>
    /// Legacy evaluation: computes interpolated light values based on oil
    /// using old T1/T2 ramp design (60→50, 20→10 etc.).
    /// </summary>
    private void EvaluateSteppedLegacy(
        float oil,
        out float fovRadius, out float fovAngle, out float fovInner, out float fovInt,
        out float auraRad, out float auraInt)
    {
        LevelProfile a = level1;
        LevelProfile b = level1;
        float t = 0f;

        if (oil <= 0f)
        {
            fovRadius = level3.fovOuterRadius;
            fovAngle = level3.fovOuterAngle;
            fovInner = level3.fovInnerAngle;
            fovInt = 0f;
            auraRad = auraRadiusAtZero;
            auraInt = auraIntensityAtZero;
            return;
        }

        if (oil >= T1_Low)
        {
            a = level1; b = level1; t = 0f;
        }
        else if (oil < T1_Low && oil > T1to2)
        {
            a = level1; b = level2;
            t = Mathf.InverseLerp(T1_Low, T1to2, oil);
        }
        else if (oil <= T1to2 && oil > T2_Low)
        {
            a = level2; b = level2; t = 0f;
        }
        else if (oil <= T2_Low && oil > T2to3)
        {
            a = level2; b = level3;
            t = Mathf.InverseLerp(T2_Low, T2to3, oil);
        }
        else // oil <= T2to3 .. > 0
        {
            a = level3; b = level3; t = 0f;
        }

        fovRadius = Mathf.Lerp(a.fovOuterRadius, b.fovOuterRadius, t);
        fovAngle = Mathf.Lerp(a.fovOuterAngle, b.fovOuterAngle, t);
        fovInner = Mathf.Lerp(a.fovInnerAngle, b.fovInnerAngle, t);
        fovInt = Mathf.Lerp(a.fovIntensity, b.fovIntensity, t);
        auraRad = Mathf.Lerp(a.auraRadius, b.auraRadius, t);
        auraInt = Mathf.Lerp(a.auraIntensity, b.auraIntensity, t);
    }


    // -------------------------------------------------------------------------
    // Public API: manual oil changes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds oil (clamped to [0, capacity]) and, in instant-threshold mode,
    /// updates target level if we cross a threshold upward.
    /// </summary>
    public void AddOil(float u)
    {
        float before = current;
        current = Mathf.Clamp(before + u, 0f, capacity);

        if (useInstantThresholds)
        {
            LampLevel desired = DecideLevel(current);
            if (desired != targetLevel)
            {
                targetLevel = desired;
                ApplyTargetFromLevel(targetLevel);
            }
        }
    }

    public void ReapplyCurrentLevelImmediate()
    {
        if (!auraLight && !fovLight) return;

        if (!useInstantThresholds)
        {
            // Legacy mode: tính lại và push luôn.
            ApplySteppedLegacy();
            return;
        }

        // Tính lại target dựa trên level hiện tại + multipliers mới.
        ApplyTargetFromLevel(targetLevel);

        // Snap current = target để đổi tức thời.
        curFovRadius = tgtFovRadius;
        curFovAngle = tgtFovAngle;
        curFovInner = tgtFovInner;
        curFovIntensity = tgtFovIntensity;
        curAuraRadius = tgtAuraRadius;
        curAuraIntensity = tgtAuraIntensity;

        // Đẩy ra Light2D.
        PushToLightsImmediate();
    }


    /// <summary>
    /// Removes oil (never below 0) and, in instant-threshold mode,
    /// updates target level if we cross a threshold downward in one hit.
    /// </summary>
    public void DrainOil(float u)
    {
        float before = current;
        current = Mathf.Max(0f, before - u);

        if (useInstantThresholds)
        {
            LampLevel desired = DecideLevel(current);
            if (desired != targetLevel)
            {
                targetLevel = desired;
                ApplyTargetFromLevel(targetLevel);
            }
        }
    }

    /// <summary>
    /// Total drain per second including external drains and multiplier.
    /// Used by HUD (RateText) and any system that needs drain info.
    /// </summary>
    public float CurrentDrainPerSecond =>
        (baseDrainPerSecond + extraDrainPerSecond) * drainMultiplier;

    /// <summary>
    /// Estimated time until oil reaches 0, based on current drain rate.
    /// Returns Infinity if drain is 0.
    /// </summary>
    public float EstimatedTimeToEmpty()
        => CurrentDrainPerSecond > 0f ? current / CurrentDrainPerSecond : float.PositiveInfinity;
}
