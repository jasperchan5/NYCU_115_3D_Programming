using UnityEngine;
using System.Collections.Generic;

public class Boomerang : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 18f;
    public float returnSpeed = 22f;
    public float turnRate = 8f;
    public float spinSpeed = 540f;
    // 迴力鏢是繞 Y 軸平轉，木刀要繞 X 軸翻跟斗
    public Vector3 spinAxis = Vector3.up;

    [Header("Distance")]
    public float hitDistance = 1f;
    public float catchDistance = 1f;
    public float notifyDistance = 3f;
    public float maxLifetime = 6f;

    // 回程進到這個距離就直接朝玩家飛，不再受 turnRate 限制。
    // returnSpeed / turnRate 算出來的迴轉半徑大約 2.7 公尺，遠大於
    // catchDistance，不鎖定的話一旦錯過就會繞著玩家轉圈永遠接不到
    public float homingLockDistance = 4f;
    // 最後的保險：回程超過這個秒數就當作接住，不讓武器卡在空中
    public float maxReturnTime = 5f;

    [Header("Refs")]
    public Transform spinVisual;
    public Transform catchPoint;
    // 握把位置。飛行時用不到，是給 HolsterVisual 把手持的那把對到手上用的
    public Transform gripPoint;

    [Header("Catch")]
    public float catchAnimLead = 0.4f;    // 提前多少秒通知

    [Header("Return Hit")]
    // 回程最多擊飛幾個物件。去重的 key 修好之後這個數字才真的生效，
    // 設太大的話一次投擲會掃掉沿路十幾個目標
    public int maxReturnHits = 3;

    [Header("Ground")]
    public float minHeight = 0.3f;

    [Header("Visual")]
    public Material normalMaterial;
    public Material returnMaterial;
    public Color returnEmission = new Color(1f, 0.4f, 0.1f);
    Renderer[] visualRenderers;

    Transform owner, target;
    System.Action onReturning, onCaught;
    bool returning, notified;
    float born;
    float returnStart;

    int returnHitCount;
    int targetHits;                 // 這一次投擲總共打中幾個目標，用來算連擊
    readonly HashSet<Transform> alreadyHit = new HashSet<Transform>();

    void Awake()
    {
        visualRenderers = GetComponentsInChildren<Renderer>();
    }

    void ApplyReturnVisual()
    {
        if (visualRenderers == null) return;

        // 材質
        foreach (var r in visualRenderers)
            if (r && returnMaterial) r.sharedMaterial = returnMaterial;

        // 轉得更快
        spinSpeed *= 1.5f;

        // 變大一點
        if (spinVisual) spinVisual.localScale *= 1.2f;
    }

    void SetReturning()
    {
        if (returning) return;
        returning = true;
        returnStart = Time.time;
        ApplyReturnVisual();
    }

    public void Launch(Transform thrower, Transform aim,
                   System.Action returningCb = null,
                   System.Action caughtCb = null,
                   Transform hand = null)
    {
        owner = thrower;
        target = aim;
        catchPoint = hand;
        onReturning = returningCb;
        onCaught = caughtCb;
        returning = false;
        notified = false;
        born = Time.time;
    }

    void Update()
    {
        if (spinVisual)
            spinVisual.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);

        if (!returning && Time.time - born > maxLifetime) SetReturning();

        Vector3 ownerPoint = catchPoint ? catchPoint.position
                                        : (owner ? owner.position + Vector3.up * 1.2f
                                                : transform.position);

        Vector3 dest = returning
            ? ownerPoint
            : (target ? target.position + Vector3.up * 0.8f
                    : transform.position + transform.forward);

        Vector3 to = dest - transform.position;
        float step = (returning ? returnSpeed : speed) * Time.deltaTime;

        // 回程的近距離直接鎖定玩家，繞過轉向率的限制，保證一定收得回來
        bool homingLocked = returning && to.magnitude < homingLockDistance;

        Vector3 dir = homingLocked
            ? to.normalized
            : Vector3.Slerp(transform.forward, to.normalized, turnRate * Time.deltaTime);

        if (dir.sqrMagnitude > 1e-6f) transform.forward = dir;
        transform.position += dir * step;

        // 離地高度只在去程限制。回程要飛回手上，玩家蹲低或接點偏低時
        // 這個夾擠會擋住最後的收尾
        if (!homingLocked)
        {
            Vector3 p = transform.position;
            if (p.y < minHeight)
            {
                p.y = minHeight;
                transform.position = p;
            }
        }

        if (!returning)
        {
            if (!target) SetReturning();    // 目標被銷毀了就回來
            return;                  // 去程到此結束
        }

        // ---- 以下只在返回時執行 ----

        if (!owner) { Destroy(gameObject); return; }

        float eta = to.magnitude / returnSpeed;     // 預估還要幾秒到
        if (!notified && eta < catchAnimLead)
        {
            notified = true;
            onReturning?.Invoke();
        }

        bool caught = to.magnitude < catchDistance;

        // 保險：萬一還是收不回來（玩家掉出場景、接點被卡住之類），
        // 時間到就強制結束。不然 Throw.busy 會永遠卡在 true，
        // 狀態列一直顯示 In flight，玩家也再也丟不出第二把
        if (!caught && Time.time - returnStart > maxReturnTime)
        {
            Debug.LogWarning($"Boomerang: 回程 {maxReturnTime} 秒仍未接住" +
                             $"（距離 {to.magnitude:F1} m），強制收回");
            caught = true;
        }

        if (caught)
        {
            onCaught?.Invoke();
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (owner && other.transform.IsChildOf(owner)) return;

        // 可撿拾物是 trigger，不該把飛行中的武器攔下來
        if (other.GetComponentInParent<Collectible>()) return;

        if (!returning)
        {
            // 去程：碰到任何東西就返回。
            // 這裡也要登記去重，否則同一個目標的其他碰撞體（手臂）
            // 會在下一幀被回程分支當成新目標再算一次
            var outKey = HitKey(other);
            if (outKey) alreadyHit.Add(outKey);

            NotifyHit(other);
            SetReturning();
            return;
        }

        // 回程：擊飛沿途的一切
        if (returnHitCount >= maxReturnHits) return;

        // 去重的 key 必須是「物件本身」。原本用 transform.root，但所有目標和
        // 障礙物都是 SpawnManager 的子物件，root 會是同一個 transform，
        // 打到第一個之後其餘全部會被這行擋掉
        var key = HitKey(other);
        if (!key || !alreadyHit.Add(key)) return;

        if (NotifyHit(other) != HitKind.None) returnHitCount++;
    }

    enum HitKind { None, Target, Obstacle }

    // 把「被打到」這件事交給物件自己處理：
    // TargetObject 負責計分與消失，ObstacleObject 負責擊飛，兩邊都各自播自己的音效。
    // 回傳打到的種類，牆和地板算 None
    HitKind NotifyHit(Collider other)
    {
        var target = other.GetComponentInParent<TargetObject>();
        if (target)
        {
            // 只有目標真的接受了這一下才算進連擊。
            // 已經被打過的目標會回傳 false，重複觸發就不會灌水
            if (target.OnHitByBoomerang(transform.position))
            {
                // 連擊算的是「這一次投擲總共打掉幾個目標」，去程那一下也算進去
                targetHits++;
                if (ComboDisplay.Instance) ComboDisplay.Instance.Show(targetHits);
            }

            return HitKind.Target;
        }

        var obstacle = other.GetComponentInParent<ObstacleObject>();
        if (obstacle)
        {
            obstacle.OnHitByBoomerang(transform.position);
            return HitKind.Obstacle;
        }

        return HitKind.None;
    }

    // 以 Rigidbody 所在的物件為單位去重；沒有 Rigidbody 就用碰撞體本身
    static Transform HitKey(Collider c)
    {
        var rb = c.attachedRigidbody;
        return rb ? rb.transform : c.transform;
    }
}