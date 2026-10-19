using UnityEngine;

// 角色手上那把（不會飛的純外觀）。投擲到接住之間隱藏起來。
public class HolsterVisual : MonoBehaviour
{
    [Header("Refs")]
    public BoomerangFactory factory;
    public Transform holsterPoint;    // 手部骨架，或掛在手上的空物件
    public Throw thrower;

    [Header("握在手上的姿勢")]
    // 相對於 holsterPoint 的微調。位移的基準點是「握把」而不是刀的中心，
    // 所以 (0,0,0) 就是握把剛好落在 holsterPoint 上
    public Vector3 gripOffset = Vector3.zero;
    public Vector3 gripEuler = new Vector3(0f, 0f, -20f);   // 刀尖稍微往外斜

    GameObject held;
    Transform pivot;

    void Start()
    {
        if (!factory) factory = GetComponent<BoomerangFactory>();
        if (!thrower) thrower = GetComponent<Throw>();
        CreateHeld();
    }

    void Update()
    {
        if (!held) return;

        // 每幀套用，這樣在 Play 模式下拉 Inspector 就能即時看到握姿變化。
        // 只在 Start 讀一次的話，調整完全沒有反應
        if (pivot)
        {
            pivot.localPosition = gripOffset;
            pivot.localRotation = Quaternion.Euler(gripEuler);
        }

        // 投擲到接住期間隱藏
        bool shouldShow = thrower && thrower.HasWeapon;
        if (held.activeSelf != shouldShow) held.SetActive(shouldShow);
    }

    void CreateHeld()
    {
        if (!factory || !holsterPoint) return;

        var b = factory.Create(holsterPoint.position, holsterPoint.rotation);
        held = b.gameObject;

        // 握把的位置要在拆掉 Boomerang 之前先拿到
        Transform grip = b.gripPoint;

        // 只要外觀，拿掉飛行邏輯和碰撞
        Destroy(b);
        var col = held.GetComponent<Collider>();
        if (col) Destroy(col);
        var rb = held.GetComponent<Rigidbody>();
        if (rb) Destroy(rb);

        held.name = "HeldKatana";

        // 中間插一層樞紐，讓姿勢調整和「握把對位」互不干擾
        pivot = new GameObject("HeldPivot").transform;
        pivot.SetParent(holsterPoint, false);
        pivot.localPosition = gripOffset;
        pivot.localRotation = Quaternion.Euler(gripEuler);

        held.transform.SetParent(pivot, false);
        held.transform.localRotation = Quaternion.identity;

        // 刀是以幾何中心為支點做的（翻滾需要），直接掛上去會變成
        // 手握在刀身中段。這裡把整把刀平移，讓握把落到樞紐原點
        held.transform.localPosition =
            grip ? -held.transform.InverseTransformPoint(grip.position) : Vector3.zero;
    }
}