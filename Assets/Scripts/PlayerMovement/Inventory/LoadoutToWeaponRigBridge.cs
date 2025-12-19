using UnityEngine;

[DisallowMultipleComponent]
public class LoadoutToWeaponRigBridge : MonoBehaviour
{
    [Header("Source (UI Loadout)")]
    [SerializeField] private ActiveLoadoutState loadout;

    [Header("Destination (runtime weapons under Player)")]
    [Tooltip("Nơi chứa runtime weapon instances. Ví dụ: Player/EquipmentRig/WeaponsRig")]
    [SerializeField] private Transform weaponsRigRoot;

    [Tooltip("Có thể để trống, script sẽ tự tạo 3 mount con dưới WeaponsRigRoot")]
    [SerializeField] private Transform[] weaponSlotMounts = new Transform[3];

    [Header("Options")]
    [SerializeField] private bool autoCreateMountsIfMissing = true;
    [SerializeField] private bool logWarnings = true;

    private readonly GameObject[] _instances = new GameObject[3];
    private readonly ItemTetrisSO[] _lastWeapons = new ItemTetrisSO[3];

    private void Reset()
    {
        if (!weaponsRigRoot) weaponsRigRoot = transform;
#if UNITY_2023_1_OR_NEWER
        if (!loadout) loadout = Object.FindAnyObjectByType<ActiveLoadoutState>(FindObjectsInactive.Include);
#else
        if (!loadout) loadout = FindObjectOfType<ActiveLoadoutState>();
#endif
    }

    private void Awake()
    {
        if (!weaponsRigRoot) weaponsRigRoot = transform;
        EnsureMounts();
    }

    private void OnEnable()
    {
        if (!loadout)
        {
#if UNITY_2023_1_OR_NEWER
            loadout = Object.FindAnyObjectByType<ActiveLoadoutState>(FindObjectsInactive.Include);
#else
            loadout = FindObjectOfType<ActiveLoadoutState>();
#endif
        }

        if (loadout != null)
            loadout.OnLoadoutChanged += HandleLoadoutChanged;
        else if (logWarnings)
            Debug.LogWarning($"[{nameof(LoadoutToWeaponRigBridge)}] Missing ActiveLoadoutState reference.");

        SyncNow();
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.OnLoadoutChanged -= HandleLoadoutChanged;
    }

    private void HandleLoadoutChanged()
    {
        SyncNow();
    }

    [ContextMenu("Sync Now")]
    public void SyncNow()
    {
        EnsureMounts();

        for (int i = 0; i < 3; i++)
        {
            ItemTetrisSO desired = loadout != null ? loadout.GetWeapon(i) : null;

            // nếu giống item và instance còn tồn tại thì thôi
            if (desired == _lastWeapons[i] && _instances[i] != null)
                continue;

            ApplySlot(i, desired);
        }
    }

    private void ApplySlot(int index, ItemTetrisSO weaponSO)
    {
        // Clear nếu slot trống
        if (weaponSO == null)
        {
            ClearSlot(index);
            return;
        }

        // Nếu lỡ kéo item thường vào weapon slot (đáng ra UI đã chặn), vẫn fail-safe
        if (!weaponSO.isWeapon)
        {
            if (logWarnings)
                Debug.LogWarning($"[{nameof(LoadoutToWeaponRigBridge)}] Slot {index} got non-weapon ItemTetrisSO: {weaponSO.name}");
            ClearSlot(index);
            return;
        }

        // Bắt buộc weapon phải có runtime prefab
        if (weaponSO.weaponRuntimePrefab == null)
        {
            if (logWarnings)
                Debug.LogWarning($"[{nameof(LoadoutToWeaponRigBridge)}] Weapon '{weaponSO.name}' has no weaponRuntimePrefab assigned.");
            ClearSlot(index);
            return;
        }

        // Replace
        ClearSlot(index);

        Transform mount = (weaponSlotMounts != null && weaponSlotMounts.Length > index && weaponSlotMounts[index] != null)
            ? weaponSlotMounts[index]
            : weaponsRigRoot;

        GameObject instance = Instantiate(weaponSO.weaponRuntimePrefab, mount);
        instance.name = $"{weaponSO.weaponRuntimePrefab.name}_Slot{index}";

        _instances[index] = instance;
        _lastWeapons[index] = weaponSO;
    }

    private void ClearSlot(int index)
    {
        if (_instances[index] != null)
        {
            Destroy(_instances[index]);
            _instances[index] = null;
        }
        _lastWeapons[index] = null;
    }

    private void EnsureMounts()
    {
        if (!autoCreateMountsIfMissing) return;
        if (!weaponsRigRoot) return;

        if (weaponSlotMounts == null || weaponSlotMounts.Length != 3)
            weaponSlotMounts = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            if (weaponSlotMounts[i] != null) continue;

            Transform existing = weaponsRigRoot.Find($"WeaponSlot_{i}");
            if (existing != null)
            {
                weaponSlotMounts[i] = existing;
                continue;
            }

            GameObject go = new GameObject($"WeaponSlot_{i}");
            go.transform.SetParent(weaponsRigRoot, false);
            weaponSlotMounts[i] = go.transform;
        }
    }
}
