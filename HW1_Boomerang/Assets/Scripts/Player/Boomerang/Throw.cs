using UnityEngine;

public class Throw : MonoBehaviour
{
    [Header("Refs")]
    public BoomerangFactory factory;
    public Transform throwPoint;

    [Header("Timing")]
    public float cooldown = 3f;

    [Header("Lock")]
    public float throwLockTime = 1.2f;   // 投擲動畫長度
    public float catchLockTime = 1.2f;   // 接住動畫長度

    float lockUntil;

    public bool IsLocked => Time.time < lockUntil;

    [Header("Audio")]
    public AudioClip throwSound;

    AudioSource audioSrc;

    Animator anim;
    float nextThrowTime;
    bool busy;
    bool released;

    public bool IsBusy => busy;
    public float CooldownRemaining => Mathf.Max(0f, nextThrowTime - Time.time);

    public bool HasWeapon => !released || !busy;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (!factory) factory = GetComponent<BoomerangFactory>();

        audioSrc = GetComponent<AudioSource>();
        if (!audioSrc) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
    }


    public void TryThrow(Transform target)
    {
        if (busy || Time.time < nextThrowTime || !target) return;

        pendingTarget = target;
        busy = true;
        released = false;
        nextThrowTime = Time.time + cooldown;
        lockUntil = Time.time + throwLockTime;    // 鎖住

        if (anim)
        {
            anim.ResetTrigger("Catch");
            anim.SetTrigger("Throw");
        }

        CancelInvoke(nameof(ForceRelease));
        Invoke(nameof(ForceRelease), 3f);
    }

    Transform pendingTarget;

    public void Release()
    {
        if (released || !pendingTarget) return;
        released = true;

        if (throwSound) audioSrc.PlayOneShot(throwSound);

        Vector3 pos = throwPoint ? throwPoint.position
                                : transform.position + Vector3.up * 1.2f;

        var b = factory.Create(pos, Quaternion.LookRotation(pendingTarget.position - pos));
        b.Launch(transform, pendingTarget, OnReturning, OnCaught, throwPoint);

        pendingTarget = null;                     
        CancelInvoke(nameof(ForceRelease));
    }

    void ForceRelease()
    {
        if (busy && !released) Release();
    }

    void OnReturning()
    {
        // Debug.Log("Set CanCatch true");
        if (anim) anim.SetTrigger("Catch");
        lockUntil = Time.time + catchLockTime;
    }

    void OnCaught()
    {
        // Debug.Log("Set CanCatch false");
        busy = false;
        released = false;
        CancelInvoke(nameof(ForceRelease));
    }

    public enum ThrowState { Ready, Flying, Cooldown }

    public ThrowState State
    {
        get
        {
            if (busy) return ThrowState.Flying;
            return Time.time < nextThrowTime ? ThrowState.Cooldown : ThrowState.Ready;
        }
    }

    public float CooldownProgress =>
        cooldown <= 0f ? 1f : Mathf.Clamp01(1f - (nextThrowTime - Time.time) / cooldown);
}