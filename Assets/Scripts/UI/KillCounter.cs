using UnityEngine;
using System;

[DisallowMultipleComponent]
public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance { get; private set; }

    [Tooltip("Tự reset về 0 khi Enable")]
    public bool resetOnEnable = true;

    public int TotalKills { get; private set; }

    public event Action<int> OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        if (resetOnEnable) ResetCounter();
        else OnChanged?.Invoke(TotalKills);
    }

    public void AddKills(int n = 1)
    {
        if (n <= 0) return;
        TotalKills += n;
        OnChanged?.Invoke(TotalKills);
    }

    public void ResetCounter()
    {
        TotalKills = 0;
        OnChanged?.Invoke(TotalKills);
    }

    // tiện gọi từ nơi khác mà không cần giữ reference
    public static void AddKillStatic(int n = 1) { if (Instance) Instance.AddKills(n); }
    public static void ResetStatic() { if (Instance) Instance.ResetCounter(); }
}
