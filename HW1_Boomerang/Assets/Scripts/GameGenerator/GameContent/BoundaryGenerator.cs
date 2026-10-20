using UnityEngine;
using System.Collections.Generic;

public class BoundaryGenerator : MonoBehaviour
{
    [Header("Ground")]
    public float width = 50f;
    public float depth = 50f;
    // 地板用有厚度的方塊而不是 Plane：Plane 的 MeshCollider 是一張無限薄的面，
    // CharacterController 一旦被擠到面的下方就再也上不來，會一路往下掉
    public float groundThickness = 1f;

    [Header("Walls")]
    public float wallHeight = 3f;
    public float wallThickness = 0.2f;
    public bool wallsVisible = true;

    [Header("Center Platform")]
    public bool createPlatform = true;
    // 高度壓在 CharacterController 的 Step Offset（預設 0.3）以內，
    // 這樣四面八方都走得上去，不需要階梯
    public Vector3 platformSize = new Vector3(8f, 0.25f, 8f);
    public float triggerHeight = 3f;        // 偵測區從平台頂面往上延伸多高

    [Header("Platform Steps")]
    // 平台夠矮就用不到。只有把 platformSize.y 調高到超過 Step Offset 時才需要打開
    public bool createSteps = false;
    public int stepCount = 4;
    public float stepWidth = 3f;
    public float stepDepth = 0.4f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material platformMaterial;

    // 貼圖每隔幾公尺重複一次。數字越小，紋理看起來越細、越密。
    // 木地板大約 2~4，地毯大約 1~2。
    [Header("Texture Tiling (metres per tile)")]
    public float floorTileSize = 4f;
    public float wallTileSize = 2f;
    public float platformTileSize = 1.5f;

    [Header("Platform Audio")]
    public AudioClip platformEnterSound;
    public AudioClip platformLeaveSound;

    [Header("Platform Trigger")]
    // 玩家生成在平台上時，開場那一瞬間就會觸發「進入平台」。
    // 0 = 照實觸發，開場全場物件立刻消失，走下平台才看得到世界。
    // 大於 0 則忽略這段秒數內的進入事件，開場就看得到整片場地。
    public float ignoreStartupSeconds = 0f;

    // 供其他腳本查詢可用範圍
    public float MinX => transform.position.x - width / 2f + wallThickness;
    public float MaxX => transform.position.x + width / 2f - wallThickness;
    public float MinZ => transform.position.z - depth / 2f + wallThickness;
    public float MaxZ => transform.position.z + depth / 2f - wallThickness;

    readonly List<GameObject> spawned = new List<GameObject>();

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();
        CreateFloor();
        CreateWalls();
        if (createPlatform) CreatePlatform();
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        foreach (var g in spawned)
        {
            if (!g) continue;
            if (Application.isPlaying) Destroy(g);
            else DestroyImmediate(g);
        }
        spawned.Clear();
    }

    void CreateFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Ground";
        floor.transform.SetParent(transform);

        // 往下沉半個厚度，讓上表面剛好落在 y = 0
        floor.transform.localPosition = new Vector3(0f, -groundThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(width, groundThickness, depth);

        // Cube 每個面的 UV 都是 0~1，不管縮放多大，所以要自己算平鋪次數
        ApplyMaterial(floor, floorMaterial,
            new Vector2(width / floorTileSize, depth / floorTileSize));

        spawned.Add(floor);
    }

    void CreateWalls()
    {
        float hw = width / 2f, hd = depth / 2f, y = wallHeight / 2f;

        // 南北牆的正面是 width x wallHeight，東西牆是 depth x wallHeight
        Vector2 nsTiling = new Vector2(width / wallTileSize, wallHeight / wallTileSize);
        Vector2 ewTiling = new Vector2(depth / wallTileSize, wallHeight / wallTileSize);

        CreateCube("Wall_North", new Vector3(0, y,  hd), new Vector3(width, wallHeight, wallThickness), wallMaterial, wallsVisible, nsTiling);
        CreateCube("Wall_South", new Vector3(0, y, -hd), new Vector3(width, wallHeight, wallThickness), wallMaterial, wallsVisible, nsTiling);
        CreateCube("Wall_East",  new Vector3( hw, y, 0), new Vector3(wallThickness, wallHeight, depth), wallMaterial, wallsVisible, ewTiling);
        CreateCube("Wall_West",  new Vector3(-hw, y, 0), new Vector3(wallThickness, wallHeight, depth), wallMaterial, wallsVisible, ewTiling);
    }

    void CreatePlatform()
    {
        CreateCube("Platform",
            new Vector3(0f, platformSize.y / 2f, 0f),
            platformSize, platformMaterial, true,
            new Vector2(platformSize.x / platformTileSize, platformSize.z / platformTileSize));

        if (!createSteps && platformSize.y > 0.3f)
            Debug.LogWarning($"BoundaryGenerator: 平台高 {platformSize.y:F2} 公尺，" +
                             $"超過 CharacterController 的預設 Step Offset (0.3)，" +
                             $"角色會走不上去。請降低平台高度、打開 Create Steps，" +
                             $"或調高角色的 Step Offset");

        CreateSteps();
        CreatePlatformTrigger();
    }

    // 從 -Z 側往上走的階梯。每階的高度是 platformSize.y / stepCount，
    // 必須小於 CharacterController 的 Step Offset 才爬得上去。
    void CreateSteps()
    {
        if (!createSteps || stepCount <= 0) return;

        float rise = platformSize.y / stepCount;
        float edgeZ = -platformSize.z * 0.5f;

        if (rise > 0.3f)
            Debug.LogWarning($"BoundaryGenerator: 每階高度 {rise:F2} 公尺超過 " +
                             $"CharacterController 的預設 Step Offset (0.3)，" +
                             $"請增加 Step Count 或調高角色的 Step Offset");

        for (int i = 0; i < stepCount; i++)
        {
            float h = rise * (i + 1);   // i = 0 是最外側最矮的那階
            float z = edgeZ - stepDepth * (stepCount - i - 0.5f);

            CreateCube($"PlatformStep_{i:D2}",
                new Vector3(0f, h * 0.5f, z),
                new Vector3(stepWidth, h, stepDepth),
                platformMaterial, true,
                new Vector2(stepWidth / platformTileSize, h / platformTileSize));
        }
    }

    void CreatePlatformTrigger()
    {
        // 掛在 BoundaryGenerator 底下而不是平台底下：平台的縮放是 (8, 1, 8)，
        // 子物件會繼承這個非等比例縮放，BoxCollider 的 size 就不再是公尺數，
        // 改平台尺寸時偵測區會跟著歪掉
        var trigger = new GameObject("PlatformTrigger");
        trigger.transform.SetParent(transform, false);
        trigger.transform.localPosition =
            new Vector3(0f, platformSize.y + triggerHeight * 0.5f, 0f);

        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        // 比平台略小，站在邊緣時才不會因為一點點晃動反覆進出
        box.size = new Vector3(platformSize.x * 0.95f, triggerHeight, platformSize.z * 0.95f);

        var pt = trigger.AddComponent<PlatformTrigger>();
        pt.enterSound = platformEnterSound;
        pt.leaveSound = platformLeaveSound;
        pt.ignoreStartupSeconds = ignoreStartupSeconds;

        spawned.Add(trigger);
    }

    GameObject CreateCube(string cubeName, Vector3 localPos, Vector3 scale,
                          Material mat, bool visible, Vector2 tiling = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = cubeName;
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        ApplyMaterial(go, mat, tiling);
        go.GetComponent<Renderer>().enabled = visible;

        if (cubeName.StartsWith("Wall")) go.tag = "Wall";

        spawned.Add(go);
        return go;
    }

    // 牆、地板、平台各只有一兩個物件，所以用 .material（會產生實體副本）
    // 單獨設定 tiling 是可以接受的；數量多的物件絕對不能這樣做。
    void ApplyMaterial(GameObject go, Material mat, Vector2 tiling)
    {
        if (!mat) return;

        var rend = go.GetComponent<Renderer>();
        rend.material = mat;

        if (tiling.x > 0f && tiling.y > 0f)
        {
            // URP/Lit 的 _BaseMap 與 Built-in Standard 的 _MainTex 都被標成主貼圖，
            // 法線、粗糙度等貼圖共用同一組 _ST，所以設這一個就會全部一起平鋪
            rend.material.mainTextureScale = tiling;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * wallHeight / 2f,
                            new Vector3(width, wallHeight, depth));
    }
}