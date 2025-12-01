using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Cập nhật thanh dầu (fill + màu theo ngưỡng) và 3 text: Oil, Rate, ETA.
/// GẮN LÊN MỘT OBJECT TRONG CANVAS (ví dụ HUD_Oil).
/// </summary>
public class OilHUD : MonoBehaviour
{
    [Header("Nguồn dữ liệu")]
    [SerializeField] private OilLamp lamp;

    [Header("UI References")]
    [SerializeField] private Image barFill;
    [SerializeField] private TextMeshProUGUI oilText;
    [SerializeField] private TextMeshProUGUI rateText;
    [SerializeField] private TextMeshProUGUI etaText;

    [Header("Màu & ngưỡng (theo đơn vị u, không phải %)")]
    [SerializeField] private Color colorBlue = new Color(0.20f, 0.60f, 1.00f); // xanh biển
    [SerializeField] private Color colorYellow = new Color(1.00f, 0.85f, 0.25f);
    [SerializeField] private Color colorRed = new Color(1.00f, 0.30f, 0.25f);
    [SerializeField] private float yellowThreshold = 50f; // ≤50u → vàng
    [SerializeField] private float redThreshold = 10f; // ≤10u → đỏ

    private void Reset()
    {
        if (!lamp)
        {
            #if UNITY_2023_1_OR_NEWER
                        lamp = Object.FindFirstObjectByType<OilLamp>();
            #else
                    lamp = FindObjectOfType<OilLamp>();
            #endif
        }
    }


    private void Update()
    {
        if (!lamp) return;

        // 1) Cập nhật fill: current / capacity
        float frac = (lamp.capacity > 0f) ? (lamp.current / lamp.capacity) : 0f;
        if (barFill) barFill.fillAmount = Mathf.Clamp01(frac);

        // 2) Đổi màu theo NGƯỠNG (u)
        if (barFill)
        {
            float u = lamp.current;
            if (u <= redThreshold) barFill.color = colorRed;
            else if (u <= yellowThreshold) barFill.color = colorYellow;
            else barFill.color = colorBlue;
        }

        // 3) Texts
        if (oilText) oilText.text = $"{lamp.current:0}u / {lamp.capacity:0}u";
        if (rateText) rateText.text = $"-{lamp.CurrentDrainPerSecond:0.00}u/s";
        if (etaText)
        {
            float eta = lamp.EstimatedTimeToEmpty();
            etaText.text = float.IsInfinity(eta) ? "∞" : $"{eta:0.0}s";
        }
    }
}
