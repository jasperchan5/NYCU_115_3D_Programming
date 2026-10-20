using System.Collections;
using UnityEngine;

public class TargetObject : MonoBehaviour
{
    [Header("Hit Response")]
    public float pushForce = 8f;
    public float upwardForce = 3f;
    public float visibleAfterHit = 2f;
    public int scoreValue = 10;

    [Header("Death Effect")]
    // 「原材質 → 死亡材質」的配對，由 SpawnManager 傳進來。
    // 用材質本身比對而不是子物件名稱或順序，所以兩隻手臂共用同一份
    // armMaterial 時會自動一起換掉。配不到的部位會退回自動轉灰階。
    public Material[] liveMaterials;
    public Material[] deadMaterials;

    public float shrinkTime = 0.5f;     // 消失前縮小的時間，含在 visibleAfterHit 內

    [Header("Audio")]
    public AudioClip hitSound;

    // URP/Lit 用 _BaseColor，Built-in Standard 用 _Color
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Transform visual;
    bool hit;

    // r.material 產生的副本不會隨著物件自動回收，要自己收掉
    readonly System.Collections.Generic.List<Material> instanced =
        new System.Collections.Generic.List<Material>();

    void Awake()
    {
        // 只縮視覺部分，碰撞體留在原尺寸
        visual = transform.Find("Visual");
        if (!visual) visual = transform;
    }

    // 回傳這一下是否真的生效。目標身上有三個碰撞體（軀幹膠囊 + 兩隻手臂），
    // 同一次飛行可能被觸發好幾次，呼叫端要靠這個回傳值才不會重複計算連擊
    public bool OnHitByBoomerang(Vector3 fromPosition)
    {
        if (hit) return false;
        hit = true;

        if (hitSound) AudioSource.PlayClipAtPoint(hitSound, transform.position);

        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            Vector3 dir = (transform.position - fromPosition).normalized;
            rb.AddForce(dir * pushForce + Vector3.up * upwardForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * pushForce, ForceMode.Impulse);
        }

        if (ScoreManager.Instance)
            ScoreManager.Instance.AddTargetScore(scoreValue);

        TurnGreyscale();
        StartCoroutine(DisappearAfterDelay());

        return true;
    }

    void TurnGreyscale()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r) continue;

            // 有配對好的死亡材質就直接換。用 sharedMaterial，不產生副本
            var dead = FindDeadMaterial(r.sharedMaterial);
            if (dead)
            {
                r.sharedMaterial = dead;
                continue;
            }

            // 沒配到就就地把顏色轉成灰階。
            // 這裡必須用 material 而不是 sharedMaterial：後者會改到材質資產本身，
            // 場上所有還活著的目標會一起變灰
            var m = r.material;
            instanced.Add(m);

            if (m.HasProperty(BaseColorId)) Desaturate(m, BaseColorId);
            else if (m.HasProperty(ColorId)) Desaturate(m, ColorId);

            // 有自發光的話也要關掉，不然灰階上面會浮一層顏色
            if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, Color.black);
        }
    }

    Material FindDeadMaterial(Material live)
    {
        if (!live || liveMaterials == null || deadMaterials == null) return null;

        int n = Mathf.Min(liveMaterials.Length, deadMaterials.Length);
        for (int i = 0; i < n; i++)
            if (liveMaterials[i] == live) return deadMaterials[i];

        return null;
    }

    // Rec.601 的亮度權重。直接取三色平均會讓綠色看起來太暗、藍色太亮
    static void Desaturate(Material m, int propertyId)
    {
        Color c = m.GetColor(propertyId);
        float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        m.SetColor(propertyId, new Color(lum, lum, lum, c.a));
    }

    IEnumerator DisappearAfterDelay()
    {
        // 先維持灰階站著，最後才縮小，兩段加起來等於 visibleAfterHit
        float hold = Mathf.Max(0f, visibleAfterHit - shrinkTime);
        yield return new WaitForSeconds(hold);

        Vector3 from = visual.localScale;
        float t = 0f;

        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            visual.localScale = Vector3.Lerp(from, Vector3.zero, t / shrinkTime);
            yield return null;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        foreach (var m in instanced) if (m) Destroy(m);
        instanced.Clear();
    }

    void OnDisable()
    {
        // 被打中之後兩秒內玩家踏上平台的話，SetActive(false) 會直接中止協程，
        // 這個物件就永遠不會被 Destroy，離開平台後會變成一個打不中的殭屍目標。
        // 這裡直接收掉，補生成機制會補一個新的回來。
        if (hit) Destroy(gameObject);
    }
}