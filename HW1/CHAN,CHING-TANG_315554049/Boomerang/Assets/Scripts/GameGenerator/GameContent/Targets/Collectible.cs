using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Score")]
    public int scoreValue = 25;

    [Header("Motion")]
    public float spinSpeed = 90f;      // 每秒轉幾度
    public float bobHeight = 0.25f;    // 上下擺動的振幅
    public float bobSpeed = 2f;        // 上下擺動的角頻率

    [Header("Audio")]
    public AudioClip collectSound;

    [Header("Refs")]
    public Transform visual;           // 只有視覺部分在轉，碰撞體不跟著轉

    float baseY;
    float phase;
    bool collected;

    void Start()
    {
        baseY = transform.position.y;
        phase = Random.Range(0f, Mathf.PI * 2f);   // 錯開相位，避免整場同步上下
    }

    void Update()
    {
        if (collected) return;

        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        transform.position = p;

        if (visual) visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.GetComponentInParent<PlayerMovement>()) return;

        collected = true;

        // PlayClipAtPoint 會自己建一個暫時的 AudioSource，所以這裡 Destroy 掉也聽得到
        if (collectSound) AudioSource.PlayClipAtPoint(collectSound, transform.position);

        if (ScoreManager.Instance)
            ScoreManager.Instance.AddCollectibleScore(scoreValue);

        Destroy(gameObject);
    }
}