using System.Reflection;
using UnityEngine;

/// <summary>
/// Minimal UI binding for Inventory Burn preview text.
/// Works with both UnityEngine.UI.Text and TMPro.TMP_Text via reflection (property name: "text").
/// 
/// Format is provided by InventoryBurnSession.GetPreviewText():
/// - 1 item:  "20"
/// - >=2:    "30 (+1)"
/// </summary>
public class InventoryBurnSessionUI : MonoBehaviour
{
    [SerializeField] private InventoryBurnSession session;
    [Tooltip("Assign a Text (uGUI) or TMP_Text component here.")]
    [SerializeField] private Component textComponent;
    [SerializeField] private string emptyWhenNone = "";

    private PropertyInfo textProp;

    private void Awake()
    {
        CacheTextProperty();
        Apply();
    }

    private void OnEnable()
    {
        if (session != null) session.OnSessionChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (session != null) session.OnSessionChanged -= Apply;
    }

    private void CacheTextProperty()
    {
        textProp = null;
        if (textComponent == null) return;

        // Both UnityEngine.UI.Text and TMPro.TMP_Text expose a public "text" property.
        textProp = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
    }

    private void Apply()
    {
        if (textProp == null || textComponent == null)
            return;

        string s = emptyWhenNone;
        if (session != null)
        {
            string preview = session.GetPreviewText();
            s = string.IsNullOrEmpty(preview) ? emptyWhenNone : preview;
        }

        textProp.SetValue(textComponent, s, null);
    }

    // In case you assign the text component at runtime or via inspector late
    private void OnValidate()
    {
        CacheTextProperty();
        Apply();
    }
}
