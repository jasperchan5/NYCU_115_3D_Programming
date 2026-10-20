using UnityEngine;

public class PlatformTrigger : MonoBehaviour
{
    [Header("Refs")]
    public SpawnManager spawnManager;

    [Header("Audio")]
    public AudioClip enterSound;
    public AudioClip leaveSound;

    [Header("Startup")]
    // 0 = 照實觸發：玩家生成在平台上時，開場那一瞬間就會觸發 OnTriggerEnter，
    // 全場物件立刻消失，要走下平台才看得到世界。
    // 設成大於 0 的秒數則忽略這段時間內的進入事件，開場就看得到整片場地。
    // 由 BoundaryGenerator 在建立這個元件時指定。
    public float ignoreStartupSeconds = 0f;

    AudioSource audioSrc;
    bool playerInside;

    // 用 Awake 而不是 Start：這個元件是在別的腳本的 Start() 裡被
    // AddComponent 出來的，Awake 會立刻執行，而 Start 要等到下一輪
    // Update 之前。玩家在同一幀就被放到平台上，物理回呼會插在中間，
    // 寫在 Start 裡的話 audioSrc 還沒建好就被用了
    void Awake()
    {
        EnsureInit();
    }

    void EnsureInit()
    {
        if (audioSrc) return;

        if (!spawnManager) spawnManager = FindFirstObjectByType<SpawnManager>();

        audioSrc = GetComponent<AudioSource>();
        if (!audioSrc) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (playerInside) return;
        if (!IsPlayer(other)) return;

        // 開場就站在平台上不算「進入」。這裡不設 playerInside，
        // 所以之後走下平台時 OnTriggerExit 也會被忽略，
        // 真正走回平台上時才會正常觸發
        if (Time.timeSinceLevelLoad < ignoreStartupSeconds) return;

        playerInside = true;
        if (spawnManager) spawnManager.HideAll();
        Play(enterSound, "Enter platform");
    }

    void OnTriggerExit(Collider other)
    {
        if (!playerInside) return;
        if (!IsPlayer(other)) return;

        playerInside = false;
        if (spawnManager) spawnManager.ShowAll();
        Play(leaveSound, "Leave platform");
    }

    bool IsPlayer(Collider c)
    {
        return c.GetComponentInParent<PlayerMovement>() != null;
    }

    void Play(AudioClip clip, string fallbackLog)
    {
        EnsureInit();   // 物理回呼有可能比 Awake 之外的初始化更早到

        if (clip) audioSrc.PlayOneShot(clip);
        else Debug.Log(fallbackLog);
    }
}