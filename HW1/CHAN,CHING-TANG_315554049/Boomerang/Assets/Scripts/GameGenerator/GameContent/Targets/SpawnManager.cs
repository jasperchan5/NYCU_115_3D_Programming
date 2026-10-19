using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Counts")]
    [Range(100, 300)] public int targetCount = 150;
    [Range(100, 300)] public int obstacleCount = 150;

    [Header("Placement")]
    public float spawnRadius = 0.6f;      // 每個物件佔用的半徑
    public float minGap = 0.3f;           // 物件之間額外的間距
    public int maxAttemptsPerObject = 30;
    public float edgeMargin = 2f;         // 離牆的距離
    public float platformClearRadius = 7f;// 平台周圍淨空

    [Header("Appearance")]
    public Material targetMaterial;
    public Material obstacleMaterial;

    [Header("Refs")]
    public BoundaryGenerator boundary;

    [Header("Audio")]
    public AudioClip targetHitSound;
    public AudioClip obstacleHitSound;

    [Header("Target Materials")]
    public Material headMaterial;
    public Material bodyMaterial;
    public Material armMaterial;

    // 被擊中之後各部位換成的材質。留空的部位會改用自動轉灰階
    [Header("Target Death Materials")]
    public Material deadHeadMaterial;
    public Material deadBodyMaterial;
    public Material deadArmMaterial;

    [Header("Collectibles")]
    [Range(0, 100)] public int collectibleCount = 30;
    public Material collectibleMaterial;
    public AudioClip collectSound;
    public int collectibleScore = 25;
    public float collectibleSize = 0.5f;       // 三角錐的整體大小
    public float collectibleHoverHeight = 0.9f;// 漂浮的基準高度
    public bool hideCollectiblesOnPlatform = true;

    [Header("Leave Platform")]
    // 離開平台時把倖存物件搬回原位（而不是放著不管只補數量）
    public bool restoreLayoutOnLeave = true;
    // 連分數一起歸零。作業說明沒有要求，預設關閉
    public bool resetScoreOnLeave = false;


    // 一個物件在佈局快照裡的位置。開場決定一次，之後每次離開平台都還原到這裡
    struct Slot
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    // 佈局與實體一一對應：slots[i] 永遠對應 live[i]，
    // live[i] 為 null 就表示那個位置的物件被消滅了，要補回來
    class Group
    {
        public readonly List<Slot> slots = new List<Slot>();
        public readonly List<GameObject> live = new List<GameObject>();

        public void Clear() { slots.Clear(); live.Clear(); }
    }

    readonly Group targets = new Group();
    readonly Group obstacles = new Group();
    readonly Group collectibles = new Group();

    public List<GameObject> Targets => targets.live;
    public List<GameObject> Obstacles => obstacles.live;
    public List<GameObject> Collectibles => collectibles.live;

    readonly List<Vector3> occupied = new List<Vector3>();

    public int TotalSpawned => CountLive(targets) + CountLive(obstacles) + CountLive(collectibles);

    static int CountLive(Group g)
    {
        int n = 0;
        foreach (var o in g.live) if (o) n++;
        return n;
    }

    // ---------- 開場生成 ----------

    public void SpawnAll()
    {
        ClearAll();
        BuildLayout();
        CreateMissing();

        Debug.Log($"Spawned {CountLive(targets)} targets, {CountLive(obstacles)} obstacles, " +
                  $"{CountLive(collectibles)} collectibles (total {TotalSpawned})");

        if (targets.slots.Count < targetCount || obstacles.slots.Count < obstacleCount)
            Debug.LogWarning($"場地放不下設定的數量：目標 {targets.slots.Count}/{targetCount}、" +
                             $"障礙 {obstacles.slots.Count}/{obstacleCount}。" +
                             $"請把 BoundaryGenerator 的 width/depth 調大");
    }

    // 隨機決定每個物件的位置，只做一次。之後的還原都照這份快照，
    // 所以不會因為反覆進出平台而越來越擠、越來越放不下
    void BuildLayout()
    {
        occupied.Clear();

        for (int i = 0; i < targetCount; i++)
            if (TryFindPosition(out Vector3 p))
                targets.slots.Add(new Slot {
                    position = p,
                    rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) });

        for (int i = 0; i < obstacleCount; i++)
            if (TryFindPosition(out Vector3 p))
                obstacles.slots.Add(new Slot {
                    position = p,
                    rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) });

        for (int i = 0; i < collectibleCount; i++)
            if (TryFindPosition(out Vector3 p))
                collectibles.slots.Add(new Slot {
                    position = new Vector3(p.x, collectibleHoverHeight, p.z),
                    rotation = Quaternion.identity });

        // 清單長度固定下來，用 null 佔位
        Pad(targets);
        Pad(obstacles);
        Pad(collectibles);
    }

    static void Pad(Group g)
    {
        while (g.live.Count < g.slots.Count) g.live.Add(null);
    }

    // ---------- 平台進出 ----------

    public void HideAll()
    {
        SetActiveAll(false);
    }

    public void ShowAll()
    {
        // 題目要求的是 respawn 而不是 re-enable：被打掉的 target 和被撿走的
        // collectible 已經 Destroy 了，SetActive 救不回來，必須重建
        if (restoreLayoutOnLeave) RestoreSurvivors();

        int created = CreateMissing();
        SetActiveAll(true);

        if (resetScoreOnLeave && ScoreManager.Instance)
            ScoreManager.Instance.ResetScore();

        Debug.Log($"Leave platform: 重建 {created} 個物件" +
                  (restoreLayoutOnLeave ? "，其餘搬回原位" : "") +
                  $"，目前 {CountLive(targets)} targets / {CountLive(obstacles)} obstacles / " +
                  $"{CountLive(collectibles)} collectibles");
    }

    // 把還活著的物件搬回快照裡的位置，並把被打飛累積的速度清掉
    void RestoreSurvivors()
    {
        RestoreGroup(targets);
        RestoreGroup(obstacles);
        RestoreGroup(collectibles);
    }

    static void RestoreGroup(Group g)
    {
        for (int i = 0; i < g.live.Count; i++)
        {
            var go = g.live[i];
            if (!go) continue;

            var rb = go.GetComponent<Rigidbody>();
            if (rb)
            {
                // 不清速度的話，物件會帶著被打飛時的動量繼續滾走
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            go.transform.SetPositionAndRotation(g.slots[i].position, g.slots[i].rotation);
        }
    }

    // 把快照裡空掉的位置重新長出物件，回傳新建的數量
    int CreateMissing()
    {
        int created = 0;

        for (int i = 0; i < targets.slots.Count; i++)
            if (!targets.live[i])
            {
                targets.live[i] = CreateTarget($"Target_{i:D3}", targets.slots[i]);
                created++;
            }

        for (int i = 0; i < obstacles.slots.Count; i++)
            if (!obstacles.live[i])
            {
                obstacles.live[i] = CreateObstacle($"Obstacle_{i:D3}", obstacles.slots[i]);
                created++;
            }

        for (int i = 0; i < collectibles.slots.Count; i++)
            if (!collectibles.live[i])
            {
                collectibles.live[i] = CreateCollectible($"Collectible_{i:D3}", collectibles.slots[i]);
                created++;
            }

        return created;
    }

    void SetActiveAll(bool active)
    {
        foreach (var g in targets.live)   if (g) g.SetActive(active);
        foreach (var g in obstacles.live) if (g) g.SetActive(active);

        if (hideCollectiblesOnPlatform)
            foreach (var g in collectibles.live) if (g) g.SetActive(active);
    }

    public void ClearAll()
    {
        DestroyGroup(targets);
        DestroyGroup(obstacles);
        DestroyGroup(collectibles);
        occupied.Clear();
    }

    void DestroyGroup(Group g)
    {
        foreach (var o in g.live) if (o) Destroy(o);
        g.Clear();
    }

    // ---------- 位置搜尋與重疊檢測 ----------

    bool TryFindPosition(out Vector3 result)
    {
        float minX = boundary ? boundary.MinX + edgeMargin : -18f;
        float maxX = boundary ? boundary.MaxX - edgeMargin :  18f;
        float minZ = boundary ? boundary.MinZ + edgeMargin : -18f;
        float maxZ = boundary ? boundary.MaxZ - edgeMargin :  18f;

        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            Vector3 p = new Vector3(
                Random.Range(minX, maxX),
                spawnRadius,
                Random.Range(minZ, maxZ));

            // 避開中央平台
            if (new Vector2(p.x, p.z).magnitude < platformClearRadius) continue;

            if (IsFree(p))
            {
                occupied.Add(p);
                result = p;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    bool IsFree(Vector3 p)
    {
        float need = spawnRadius * 2f + minGap;
        float needSqr = need * need;

        foreach (var o in occupied)
            if ((o - p).sqrMagnitude < needSqr) return false;

        return true;
    }

    // ---------- 物件建立 ----------

    GameObject CreateObstacle(string objName, Slot slot)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objName;
        cube.tag = "Obstacle";
        cube.transform.SetParent(transform);
        cube.transform.SetPositionAndRotation(slot.position, slot.rotation);
        cube.transform.localScale = Vector3.one * (spawnRadius * 2f);

        var rb = cube.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;

        // 障礙物是等比例縮放的 Cube，每個面的 UV 剛好是 0~1，
        // 鐵箱貼圖一張對一個面，不需要調 tiling。
        // 用 sharedMaterial：material 每讀一次就複製一份新材質，150 份會讓批次繪製失效
        if (obstacleMaterial) cube.GetComponent<Renderer>().sharedMaterial = obstacleMaterial;
        cube.AddComponent<ObstacleObject>().hitSound = obstacleHitSound;

        return cube;
    }

    GameObject CreateTarget(string objName, Slot slot)
    {
        var root = new GameObject(objName);
        root.tag = "Target";
        root.transform.SetParent(transform);
        root.transform.SetPositionAndRotation(slot.position, slot.rotation);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        float r = spawnRadius * 0.75f;   // Body radius
        float h = spawnRadius * 2.25f;   // Body height

        // 身體（圓柱）
        CreatePart(visual.transform, PrimitiveType.Cylinder,
            Vector3.zero,
            new Vector3(r * 2f, h * 0.5f, r * 2f),
            default, bodyMaterial);

        // 頭（球，放在圓柱頂端）
        CreatePart(visual.transform, PrimitiveType.Sphere,
            new Vector3(0f, h * 0.5f, 0f),
            Vector3.one * (r * 1.6f),
            default, headMaterial);

        // 兩隻手臂
        float armLen = r * 0.8f;
        float armRadius = r * 0.05f;
        float armAngle = 30f;
        float armY = h * 0.25f;

        float rad = armAngle * Mathf.Deg2Rad;
        float dx = Mathf.Cos(rad) * armLen * 0.5f;
        float dy = Mathf.Sin(rad) * armLen * 0.5f;

        // Cylinder 的 scale.y 是半高，所以長度要除以 2
        Vector3 armScale = new Vector3(armRadius * 2f, armLen * 0.5f, armRadius * 2f);

        // 右臂：先轉 -90 讓軸朝 +X，再往下傾 30
        CreatePart(visual.transform, PrimitiveType.Cylinder,
            new Vector3(r + dx, armY - dy, 0f),
            armScale,
            new Vector3(0f, 0f, -90f - armAngle),
            armMaterial, keepCollider: true);

        // 左臂：轉 +90 讓軸朝 -X，再往下傾 30
        CreatePart(visual.transform, PrimitiveType.Cylinder,
            new Vector3(-(r + dx), armY - dy, 0f),
            armScale,
            new Vector3(0f, 0f, 90f + armAngle),
            armMaterial, keepCollider: true);

        // 整體碰撞用膠囊包住
        var col = root.AddComponent<CapsuleCollider>();
        col.height = h + r * 1.6f;
        col.radius = r * 1.2f;
        col.center = new Vector3(0f, h * 0.25f, 0f);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;

        var to = root.AddComponent<TargetObject>();
        to.hitSound = targetHitSound;
        to.liveMaterials = LiveMats;
        to.deadMaterials = DeadMats;

        return root;
    }

    // 配對表建一次就好，全部目標共用同一組陣列
    Material[] liveMats, deadMats;

    Material[] LiveMats
    {
        get
        {
            if (liveMats == null)
                liveMats = new[] { headMaterial, bodyMaterial, armMaterial };
            return liveMats;
        }
    }

    Material[] DeadMats
    {
        get
        {
            if (deadMats == null)
                deadMats = new[] { deadHeadMaterial, deadBodyMaterial, deadArmMaterial };
            return deadMats;
        }
    }

    // ---------- 可撿拾物（三角錐） ----------

    GameObject CreateCollectible(string objName, Slot slot)
    {
        var root = new GameObject(objName);
        root.transform.SetParent(transform);
        root.transform.SetPositionAndRotation(slot.position, slot.rotation);

        // 只有視覺部分自轉，trigger 保持不動
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * collectibleSize;

        visual.AddComponent<MeshFilter>().sharedMesh = PyramidMesh;
        var mr = visual.AddComponent<MeshRenderer>();
        if (collectibleMaterial) mr.sharedMaterial = collectibleMaterial;

        // 撿取範圍：球形 trigger 比 convex MeshCollider 好走進去，也便宜得多
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = collectibleSize * 0.8f;

        var c = root.AddComponent<Collectible>();
        c.visual = visual.transform;
        c.scoreValue = collectibleScore;
        c.collectSound = collectSound;

        return root;
    }

    // 全部 collectible 共用同一份 mesh，不要每個都 new 一個
    static Mesh pyramidMesh;
    static Mesh PyramidMesh
    {
        get
        {
            if (!pyramidMesh) pyramidMesh = BuildPyramid();
            return pyramidMesh;
        }
    }

    // 底面為正三角形的三角錐，中心在原點
    static Mesh BuildPyramid()
    {
        const float r = 0.5f;          // 底面外接圓半徑
        const float h = 1.0f;          // 高
        const float s = 0.8660254f;    // sqrt(3)/2

        float yb = -h * 0.5f;
        Vector3 b0 = new Vector3(    0f, yb,        r);   // 前
        Vector3 b1 = new Vector3(-s * r, yb, -0.5f * r);  // 左後
        Vector3 b2 = new Vector3( s * r, yb, -0.5f * r);  // 右後
        Vector3 ap = new Vector3(    0f,  h * 0.5f, 0f);  // 頂點

        // 每個面用自己的三個頂點，法線才會是硬邊（不會被平滑成球）
        Vector3[] v =
        {
            b0, ap, b1,   // 左前側面
            b1, ap, b2,   // 後側面
            b2, ap, b0,   // 右前側面
            b0, b1, b2,   // 底面
        };

        int[] t = new int[v.Length];
        for (int i = 0; i < t.Length; i++) t[i] = i;

        var m = new Mesh { name = "CollectiblePyramid" };
        m.vertices = v;
        m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    GameObject CreatePart(Transform parent, PrimitiveType type,
                        Vector3 localPos, Vector3 scale,
                        Vector3 euler = default,
                        Material mat = null,
                        bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;

        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;

        return go;
    }
}