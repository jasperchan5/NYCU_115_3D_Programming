using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public Transform spawnPoint;
    public BoundaryGenerator boundary;
    public Vector3 fallbackPosition = new Vector3(0f, 1f, 0f);

    [Header("Placement")]
    // 打開的話會把出生點推到平台外；預設關閉，直接生在平台正上方往下掉
    public bool avoidPlatform = false;
    public float platformClearRadius = 5.5f;
    public float groundOffset = 0.05f;   // 腳底離地面留一點縫
    public float dropHeight = 0.5f;      // 在落點上方多高處生成，讓角色自己掉下來

    public GameObject Player { get; private set; }

    public GameObject Spawn()
    {
        Vector3 pos = spawnPoint ? spawnPoint.position : fallbackPosition;
        Quaternion rot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;

        if (avoidPlatform) pos = PushOutOfPlatform(pos);
        pos = ClampToBoundary(pos);

        Player = Instantiate(playerPrefab, pos, rot);
        Player.name = "Player";

        pos = DropToGround(Player, pos);

        var camCtrl = Camera.main ? Camera.main.GetComponent<CameraController>() : null;
        if (camCtrl) camCtrl.target = Player.transform;

        var move = Player.GetComponent<PlayerMovement>();
        if (move && Camera.main)
        {
            move.cam = Camera.main.transform;
            move.respawnPoint = pos;      // 落地後的位置才是重生點
        }

        return Player;
    }

    Vector3 ClampToBoundary(Vector3 pos)
    {
        if (!boundary) return pos;

        pos.x = Mathf.Clamp(pos.x, boundary.MinX + 1f, boundary.MaxX - 1f);
        pos.z = Mathf.Clamp(pos.z, boundary.MinZ + 1f, boundary.MaxZ - 1f);
        return pos;
    }

    Vector3 PushOutOfPlatform(Vector3 pos)
    {
        Vector2 flat = new Vector2(pos.x, pos.z);
        if (flat.magnitude >= platformClearRadius) return pos;

        // 剛好在正中心就沒有方向可推，預設往 -Z 退
        Vector2 dir = flat.sqrMagnitude < 0.01f ? Vector2.down : flat.normalized;
        dir *= platformClearRadius;

        pos.x = dir.x;
        pos.z = dir.y;
        return pos;
    }

    // 從高處往下打一條射線找真正的地面，再把膠囊的腳底對齊上去。
    // 直接用寫死的 y 值很容易讓膠囊一出生就埋在地板或平台裡，
    // CharacterController 處理初始穿透時可能把角色往下推出去。
    Vector3 DropToGround(GameObject player, Vector3 pos)
    {
        // 地板和牆是這一幀才生成的，要先讓物理系統更新內部狀態，
        // 否則射線會打空
        Physics.SyncTransforms();

        var cc = player.GetComponent<CharacterController>();

        // 膠囊底部相對於物件軸心的位移
        float feetOffset = cc ? cc.center.y - cc.height * 0.5f : 0f;

        Vector3 from = new Vector3(pos.x, 100f, pos.z);
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 300f,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            // 落在表面正上方一點，由重力把角色帶下來，
            // 避免膠囊一出生就跟平台方塊互相穿透
            pos.y = hit.point.y - feetOffset + groundOffset + dropHeight;
        }
        else
        {
            Debug.LogWarning($"PlayerSpawner: 在 ({pos.x:F1}, {pos.z:F1}) 下方找不到地面，" +
                             $"沿用設定的高度 {pos.y:F2}");
        }

        // CharacterController 啟用時會覆寫 transform.position，必須先關掉
        if (cc) cc.enabled = false;
        player.transform.position = pos;
        if (cc) cc.enabled = true;

        return pos;
    }
}