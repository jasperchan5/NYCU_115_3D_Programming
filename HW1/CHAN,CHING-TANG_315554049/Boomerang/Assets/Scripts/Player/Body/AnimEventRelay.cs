using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    Throw thrower;

    void Awake()
    {
        thrower = GetComponentInParent<Throw>();
    }

    // 由 Animation Event 呼叫
    public void OnRelease()
    {
        if (thrower) thrower.Release();
    }
}