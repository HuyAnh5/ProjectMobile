using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class KillCounterUI : MonoBehaviour
{
    [Header("Source")]
    public KillCounter counter;

    [Header("Targets (1 trong 2 cái hoặc cả 2)")]
    public Text uiText;
    public TextMeshProUGUI tmpText;

    [Header("Format")]
    public string format = "Kills: {0}";

    void Awake()
    {
        if (!uiText) uiText = GetComponent<Text>();
        if (!tmpText) tmpText = GetComponent<TextMeshProUGUI>();
        if (!counter) counter = KillCounter.Instance;
    }

    void OnEnable()
    {
        if (!counter) counter = KillCounter.Instance;
        if (counter != null)
        {
            counter.OnChanged += HandleChanged;
            HandleChanged(counter.TotalKills);
        }
        else HandleChanged(0);
    }

    void OnDisable()
    {
        if (counter != null) counter.OnChanged -= HandleChanged;
    }

    void HandleChanged(int v)
    {
        string s = string.Format(format, v);
        if (uiText) uiText.text = s;
        if (tmpText) tmpText.text = s;
    }
}
