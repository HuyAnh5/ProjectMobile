using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// HUD tim kiểu Zelda: cập nhật theo PlayerHealth (đơn vị half-heart).
/// - Kéo 3 sprite: full / half / empty.
/// - Có thể tăng maxHearts runtime: UI sẽ tự mở rộng.
/// </summary>
[DisallowMultipleComponent]
public class HeartsHUD : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth playerHealth;
    public Transform container;          // nơi chứa các Image (Horizontal Layout Group khuyến nghị)
    public Image heartPrefab;            // prefab 1 Image (tắt raycast)

    [Header("Sprites")]
    public Sprite fullHeart;
    public Sprite halfHeart;
    public Sprite emptyHeart;

    [Header("Refresh")]
    public bool rebuildOnEnable = true;

    readonly List<Image> images = new();

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!playerHealth)
            playerHealth = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
#else
    if (!playerHealth)
        playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
#endif
    }


    void OnEnable()
    {
        if (!playerHealth) return;
        playerHealth.OnHealthChanged += HandleChanged;
        if (rebuildOnEnable) Rebuild();
        else UpdateVisual(playerHealth.CurrentHalves, playerHealth.MaxHalves);
    }

    void OnDisable()
    {
        if (playerHealth) playerHealth.OnHealthChanged -= HandleChanged;
    }

    void HandleChanged(int currentHalves, int maxHalves)
    {
        // Max thay đổi? → Rebuild số lượng ảnh
        if (images.Count != maxHalves / 2)
            Rebuild();
        UpdateVisual(currentHalves, maxHalves);
    }

    void Rebuild()
    {
        // Xoá cũ
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
        images.Clear();

        int hearts = Mathf.Max(1, playerHealth.maxHearts);
        for (int i = 0; i < hearts; i++)
        {
            var img = Instantiate(heartPrefab, container);
            img.gameObject.SetActive(true);
            images.Add(img);
        }
        UpdateVisual(playerHealth.CurrentHalves, playerHealth.MaxHalves);
    }

    void UpdateVisual(int currentHalves, int maxHalves)
    {
        int hearts = Mathf.Max(1, maxHalves / 2);
        int remain = Mathf.Clamp(currentHalves, 0, maxHalves);

        for (int i = 0; i < hearts; i++)
        {
            Image img = images[i];
            if (!img) continue;

            // Mỗi tim tiêu thụ 2 "halves"
            if (remain >= 2) { img.sprite = fullHeart; remain -= 2; }
            else if (remain == 1) { img.sprite = halfHeart; remain = 0; }
            else { img.sprite = emptyHeart; }
        }
    }
}
