using UnityEngine;
[ExecuteInEditMode]
public class FpsCap : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;

    private void Start()
    {
        QualitySettings.vSyncCount = 0; // Disable VSync in editor
        Application.targetFrameRate = targetFps;
    }
}
