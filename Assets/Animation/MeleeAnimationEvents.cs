using UnityEngine;

public class MeleeAnimationEvents : MonoBehaviour
{
    private MeleeAutoRunner runner;

    void Awake()
    {
        // Tự động tìm script MeleeAutoRunner ở object cha (meleeAttack)
        runner = GetComponentInParent<MeleeAutoRunner>();
    }

    // Hàm này sẽ được Animation Event gọi
    public void CallOnSwingHit()
    {
        if (runner != null)
        {
            runner.OnSwingHit(); // Gọi hàm thật sự ở script cha
        }
    }
}