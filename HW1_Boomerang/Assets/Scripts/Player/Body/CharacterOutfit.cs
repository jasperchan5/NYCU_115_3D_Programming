using UnityEngine;

// 用基本幾何體拼出衣著，掛在 Humanoid 骨架上。
// 每一件都只跟著單一根骨頭走，所以不會隨著蒙皮變形——這對軀幹、
// 肩膀、腳掌、頭部沒什麼問題，因為那些部位本來就不太彎。
//
// 掛在 Player 的根物件上（跟 PlayerMovement 同一層）即可。
public class CharacterOutfit : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;          // 留空會自己往子物件找

    [Header("Materials")]
    public Material clothMaterial;     // 上衣、褲子
    public Material trimMaterial;      // 腰帶、護肩、靴子；留空就沿用 clothMaterial

    [Header("整體縮放")]
    // 所有尺寸都是公尺。模型身高不是 1.8 左右的話用這個整體調整
    public float scale = 1f;

    [Header("上衣")]
    public bool vest = true;
    public Vector3 vestSize = new Vector3(0.40f, 0.46f, 0.26f);
    public Vector3 vestOffset = new Vector3(0f, -0.02f, 0f);

    [Header("腰帶")]
    public bool belt = true;
    public Vector3 beltSize = new Vector3(0.38f, 0.07f, 0.26f);
    public Vector3 beltOffset = new Vector3(0f, 0.06f, 0f);

    [Header("護肩")]
    public bool shoulderPads = true;
    public Vector3 padSize = new Vector3(0.16f, 0.10f, 0.20f);
    public Vector3 padOffset = new Vector3(0.02f, 0.02f, 0f);

    [Header("靴子")]
    public bool boots = true;
    public Vector3 bootSize = new Vector3(0.14f, 0.12f, 0.26f);
    public Vector3 bootOffset = new Vector3(0f, -0.02f, 0.03f);

    [Header("頭帶")]
    public bool headband = true;
    public Vector3 headbandSize = new Vector3(0.23f, 0.06f, 0.23f);
    public Vector3 headbandOffset = new Vector3(0f, 0.06f, 0f);

    void Start()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!animator || !animator.isHuman)
        {
            Debug.LogWarning("CharacterOutfit: 找不到 Humanoid 的 Animator，不會產生任何衣著");
            return;
        }

        Build();
    }

    void Build()
    {
        Material trim = trimMaterial ? trimMaterial : clothMaterial;

        if (vest)
            Add(HumanBodyBones.Chest, HumanBodyBones.Spine, PrimitiveType.Cylinder,
                "Vest", vestSize, vestOffset, clothMaterial);

        if (belt)
            Add(HumanBodyBones.Hips, HumanBodyBones.Hips, PrimitiveType.Cylinder,
                "Belt", beltSize, beltOffset, trim);

        if (shoulderPads)
        {
            Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftUpperArm, PrimitiveType.Cube,
                "ShoulderPad_L", padSize, Mirror(padOffset), trim);
            Add(HumanBodyBones.RightUpperArm, HumanBodyBones.RightUpperArm, PrimitiveType.Cube,
                "ShoulderPad_R", padSize, padOffset, trim);
        }

        if (boots)
        {
            Add(HumanBodyBones.LeftFoot, HumanBodyBones.LeftFoot, PrimitiveType.Cube,
                "Boot_L", bootSize, bootOffset, trim);
            Add(HumanBodyBones.RightFoot, HumanBodyBones.RightFoot, PrimitiveType.Cube,
                "Boot_R", bootSize, bootOffset, trim);
        }

        if (headband)
            Add(HumanBodyBones.Head, HumanBodyBones.Head, PrimitiveType.Cylinder,
                "Headband", headbandSize, headbandOffset, trim);
    }

    static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    void Add(HumanBodyBones bone, HumanBodyBones fallback, PrimitiveType type,
             string pieceName, Vector3 size, Vector3 offset, Material mat)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (!t) t = animator.GetBoneTransform(fallback);
        if (!t)
        {
            Debug.LogWarning($"CharacterOutfit: 這個骨架沒有 {bone}，跳過 {pieceName}");
            return;
        }

        var go = GameObject.CreatePrimitive(type);
        go.name = pieceName;

        // 衣服不需要碰撞，而且留著會干擾 CharacterController
        Destroy(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;

        go.transform.SetParent(t, false);

        // 關鍵：先對齊「角色身體」的朝向，而不是骨頭自己的局部軸。
        // Humanoid 骨架每個模型的骨頭軸向都不一樣，直接用 localRotation
        // 會讓上衣躺著或側著
        go.transform.rotation = transform.rotation;
        go.transform.position = t.position + transform.rotation * (offset * scale);

        Vector3 s = size * scale;
        // Unity 的 Cylinder 原始高度是 2 單位，scale.y 是半高
        if (type == PrimitiveType.Cylinder) s.y *= 0.5f;
        go.transform.localScale = Vector3.zero;        // 先歸零再用世界尺寸設定
        SetWorldScale(go.transform, s);
    }

    // 骨頭可能帶著縮放，所以要把父層的縮放除掉，衣服才會是指定的公尺數
    static void SetWorldScale(Transform t, Vector3 world)
    {
        Vector3 parent = t.parent ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            parent.x != 0f ? world.x / parent.x : world.x,
            parent.y != 0f ? world.y / parent.y : world.y,
            parent.z != 0f ? world.z / parent.z : world.z);
    }
}