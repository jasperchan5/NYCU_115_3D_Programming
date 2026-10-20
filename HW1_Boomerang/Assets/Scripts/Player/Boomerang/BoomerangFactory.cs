using UnityEngine;

// 產生「會回來的木刀」。飛行邏輯仍然是 Boomerang，只有外觀和翻滾軸不同。
//
// 造型是標準的劍道木刀（bokken）：沒有鍔、沒有柄頭，柄和刀身是同一根木頭
// 連續收窄下來，反り比真刀淺，刀尖斜切。
public class BoomerangFactory : MonoBehaviour
{
    [Header("Shape")]
    public float totalLength = 1.0f;                        // 全長，含柄
    [Range(0.15f, 0.40f)] public float handleFraction = 0.26f;  // 柄佔全長的比例
    [Range(0f, 30f)] public float curveAngle = 11f;         // 反り。木刀比真刀平直
    [Range(4, 20)] public int segments = 10;                // 整把刀用幾段方塊逼近

    [Header("Profile")]
    public float buttWidth = 0.030f;    // 柄頭
    public float maxWidth = 0.038f;     // 最寬處，在柄與刀身的交界
    public float tipWidth = 0.020f;     // 刀尖
    [Range(0.4f, 1f)] public float thicknessRatio = 0.62f;  // 厚度相對於寬度
    [Range(0f, 40f)] public float kissakiAngle = 14f;       // 刀尖斜切的角度

    [Header("Collision")]
    // 球形 trigger：木刀在空中翻滾，用球體當判定範圍才不會因為角度不同而忽大忽小
    public float colliderRadius = 0.45f;

    [Header("Appearance")]
    public Material normalMaterial;     // 整把刀的木紋
    public Material gripMaterial;       // 想做柄卷才填，留空就整把同一種木頭
    public Material returnMaterial;

    [Header("Flight")]
    public float speed = 18f;
    public float returnSpeed = 22f;
    public float turnRate = 8f;
    public float spinSpeed = 540f;      // 刀的翻滾比迴力鏢的自旋慢，才看得出形狀


    public Boomerang Create(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("WoodenSword");
        root.transform.SetPositionAndRotation(position, rotation);

        var col = root.AddComponent<SphereCollider>();
        col.radius = colliderRadius;
        col.isTrigger = true;

        // Trigger 需要至少一方有 Rigidbody
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 翻滾的視覺部分
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Transform grip = BuildBokken(visual.transform);

        var b = root.AddComponent<Boomerang>();
        b.spinVisual = visual.transform;
        b.gripPoint = grip;
        b.spinAxis = Vector3.right;   // 繞 X 軸翻滾，刀身在 YZ 平面上翻跟斗
        b.speed = speed;
        b.returnSpeed = returnSpeed;
        b.turnRate = turnRate;
        b.spinSpeed = spinSpeed;
        b.returnMaterial = returnMaterial;

        return b;
    }

    // 從柄頭一路往刀尖接。柄的部分不彎，過了交界才開始上反り，
    // 寬度則是一條連續曲線，所以看不出柄跟刀身的接縫。
    // 回傳握把位置，給 HolsterVisual 把刀對到手上用。
    Transform BuildBokken(Transform parent)
    {
        var model = new GameObject("Model").transform;
        model.SetParent(parent, false);

        float segLen = totalLength / segments;
        Vector3 pos = Vector3.zero;          // 這一段的起點，從柄頭開始
        Quaternion rot = Quaternion.identity;

        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments;              // 這一段起點在全長上的比例
            float tm = (i + 0.5f) / segments;            // 中點
            bool isTip = i == segments - 1;

            // 柄的範圍內保持筆直，之後才依比例累積彎曲
            if (t0 > handleFraction)
            {
                float bend = (t0 - handleFraction) / (1f - handleFraction);
                rot = Quaternion.Euler(curveAngle * bend, 0f, 0f);
            }

            // 刀尖那一段再多斜一點，做出斜切的切先
            Quaternion segRot = isTip
                ? rot * Quaternion.Euler(kissakiAngle, 0f, 0f)
                : rot;

            Vector3 up = rot * Vector3.up;         // 弧線前進的方向
            Vector3 dir = segRot * Vector3.up;     // 這一段方塊自己的長軸方向

            float w = WidthAt(tm);
            float len = segLen * (isTip ? 0.85f : 1.06f);   // 其餘段落稍微重疊，免得彎折處露縫

            bool inHandle = tm < handleFraction;
            var seg = CreatePart(model,
                inHandle ? $"Tsuka_{i}" : (isTip ? "Kissaki" : $"Blade_{i}"),
                // 中心必須沿著「這一段自己的方向」推半個長度，根部才會剛好
                // 落在 pos 上。用 up 算的話，傾斜的切先會繞自身中心轉開，
                // 根部整個橫移出去，看起來就像斷掉
                pos + dir * (len * 0.5f),
                new Vector3(w * thicknessRatio, len, w),
                inHandle && gripMaterial ? gripMaterial : normalMaterial);
            seg.transform.localRotation = segRot;

            pos += up * segLen;
        }

        // 握把：柄的中段
        var grip = new GameObject("GripPoint").transform;
        grip.SetParent(model, false);
        grip.localPosition = new Vector3(0f, totalLength * handleFraction * 0.5f, 0f);

        // 把整把刀往下推，讓幾何中心落在 Visual 的原點。
        // 翻滾必須繞刀的中段轉，不然會變成繞柄頭甩
        model.localPosition = new Vector3(0f, -(pos.y) * 0.5f, 0f);

        return grip;
    }

    // 柄頭最細，到柄與刀身的交界最寬，再一路收到刀尖
    float WidthAt(float t)
    {
        return t < handleFraction
            ? Mathf.Lerp(buttWidth, maxWidth, t / handleFraction)
            : Mathf.Lerp(maxWidth, tipWidth, (t - handleFraction) / (1f - handleFraction));
    }

    GameObject CreatePart(Transform parent, string partName,
                          Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        Destroy(go.GetComponent<Collider>());   // 碰撞統一由父物件的球體處理
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;

        return go;
    }
}