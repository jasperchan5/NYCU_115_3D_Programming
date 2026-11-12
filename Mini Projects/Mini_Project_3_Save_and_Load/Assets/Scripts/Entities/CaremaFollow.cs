using UnityEngine;
using UnityEngine.InputSystem;

namespace MP3_Save
{
    /// 第三人稱跟隨鏡頭：掛在 Main Camera 上。
    /// 按住滑鼠右鍵拖曳 = 繞著角色轉；滾輪 = 拉近拉遠。
    /// 鏡頭和角色之間有牆時會自動拉近，避免穿牆。
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;                 // 拖入 Player
        [SerializeField] float height = 1.5f;              // 看角色的哪個高度（大約胸口到頭）

        [Header("距離")]
        [SerializeField] float distance = 5f;
        [SerializeField] float minDistance = 2f;
        [SerializeField] float maxDistance = 12f;
        [SerializeField] float zoomSpeed = 0.01f;

        [Header("旋轉")]
        [SerializeField] float yaw = 0f;                   // 水平角度
        [SerializeField] float pitch = 20f;                // 俯角
        [SerializeField] float minPitch = -10f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float mouseSensitivity = 0.2f;

        [Header("平滑")]
        [SerializeField] float followSmooth = 10f;
        [SerializeField] float snapDistance = 5f;          // 角色瞬移（例如讀檔）超過這個距離就直接跳過去

        [Header("防穿牆")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.3f;

        Vector3 focus;

        void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<PlayerMovement>();
                if (player != null) target = player.transform;
            }
            if (target != null)
            {
                focus = target.position + Vector3.up * height;
                yaw = target.eulerAngles.y;
            }
        }

        void LateUpdate()   // 在角色移動（Update）之後才更新鏡頭，畫面不會抖
        {
            if (target == null) return;

            // 1. 滑鼠輸入
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    yaw += d.x * mouseSensitivity;
                    pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, minPitch, maxPitch);
                }
                float scroll = mouse.scroll.ReadValue().y;
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
            }

            // 2. 跟隨的焦點（平滑；瞬移就直接跳）
            Vector3 desiredFocus = target.position + Vector3.up * height;
            if (Vector3.Distance(focus, desiredFocus) > snapDistance) focus = desiredFocus;
            else focus = Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

            // 3. 依角度算出鏡頭位置，中間有東西擋住就拉近
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;
            float d2 = distance;
            if (Physics.SphereCast(focus, collisionRadius, back, out RaycastHit hit, distance,
                                   collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
            {
                d2 = Mathf.Max(hit.distance, 0.5f);
            }

            transform.position = focus + back * d2;
            transform.rotation = rot;
        }
    }
}