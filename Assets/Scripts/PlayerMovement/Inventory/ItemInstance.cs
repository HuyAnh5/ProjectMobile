using System;
using UnityEngine;

/// <summary>
/// Runtime wrapper cho ItemData:
/// - chứa tham chiếu data,
/// - trạng thái xoay,
/// - decayTimer & warning flag.
/// </summary>
[Serializable]
public class ItemInstance
{
    [SerializeField] private ItemData data;
    [SerializeField] private bool isRotated;
    [SerializeField] private float decayTimer;
    [SerializeField] private bool hasDecayWarning;

    public ItemData Data => data;

    public bool IsRotated
    {
        get => isRotated;
        set => isRotated = value;
    }

    /// <summary>Thời gian đã nằm trong Dark Row (giây).</summary>
    public float DecayTimer
    {
        get => decayTimer;
        set => decayTimer = Mathf.Max(0f, value);
    }

    /// <summary>Đã bắn cảnh báo warning hay chưa.</summary>
    public bool HasDecayWarning
    {
        get => hasDecayWarning;
        set => hasDecayWarning = value;
    }

    public bool IsEmpty => data == null;

    public ItemInstance(ItemData data)
    {
        this.data = data;
        isRotated = false;
        decayTimer = 0f;
        hasDecayWarning = false;
    }

    public void ResetDecay()
    {
        decayTimer = 0f;
        hasDecayWarning = false;
    }
}