using UnityEngine;
using UnityEngine.InputSystem;

namespace FunctionSelection
{
    /// 
    /// 一台攝影機、四種模式，切換時平滑過渡：
    ///   Orbit       主選單背景：繞村莊慢慢旋轉
    ///   Pose        選角畫面：移到指定位置看向角色
    ///   ThirdPerson 遊戲中：滑鼠控制視角、滾輪縮放、避免穿牆
    ///   Cinematic   示範模式：自動繞著角色拍
    /// 選單時 Time.timeScale 可能是 0，所以一律用 unscaledDeltaTime。
    /// 鎔金術（AllomancySession 設定）：
    ///   lookLocked：開著金屬輪盤時，滑鼠交給輪盤，不轉鏡頭
    ///   scrollZoom：遊戲中滾輪用來推 / 拉，所以關掉滾輪縮放
    ///   aiming    ：按住右鍵瞄準時，鏡頭拉近到 2.6 公尺、往右偏成越肩視角、視角從 60° 縮到 45°
    /// 
    public class CameraDirector : MonoBehaviour
    {
        public enum Mode { Orbit, Pose, ThirdPerson, Cinematic }

        public Mode mode = Mode.Orbit;
        public float Yaw { get; private set; }

        // Orbit
        Vector3 orbitCenter;
        float orbitRadius = 40f, orbitHeight = 18f, orbitSpeed = 6f, orbitAngle = 200f;
        // Pose
        Vector3 posePos, poseLook;
        // ThirdPerson / Cinematic
        Transform target;
        float pitch = 15f, distance = 6.5f;
        public float mouseSensitivity = 0.12f;

        // 鎔金術
        public bool lookLocked;
        public bool scrollZoom = true;
        public bool aiming;
        float aimZoom;                              // 0 = 一般鏡頭，1 = 瞄準鏡頭（平滑過渡）
        Camera view;
        float baseFov;

        Vector3 lookPoint;

        public void SetOrbit(Vector3 center, float radius, float height, float degPerSec)
        {
            mode = Mode.Orbit;
            orbitCenter = center; orbitRadius = radius; orbitHeight = height; orbitSpeed = degPerSec;
        }

        public void SetPose(Vector3 pos, Vector3 look)
        {
            mode = Mode.Pose;
            posePos = pos; poseLook = look;
        }

        public void SetThirdPerson(Transform t)
        {
            mode = Mode.ThirdPerson;
            target = t;
            Yaw = t.eulerAngles.y;
            pitch = 15f;
        }

        public void SetCinematic(Transform t)
        {
            mode = Mode.Cinematic;
            target = t;
        }

        void Awake()
        {
            view = GetComponent<Camera>();
            baseFov = view.fieldOfView;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            aimZoom = Mathf.MoveTowards(aimZoom, aiming && mode == Mode.ThirdPerson ? 1f : 0f, dt * 5f);
            view.fieldOfView = Mathf.Lerp(baseFov, 45f, aimZoom);
            Vector3 wantPos, wantLook;
            float follow = 4f;   // 越大越緊跟

            switch (mode)
            {
                case Mode.Orbit:
                    orbitAngle += orbitSpeed * dt;
                    wantPos = orbitCenter + Quaternion.Euler(0, orbitAngle, 0) * new Vector3(0, orbitHeight, -orbitRadius);
                    wantLook = orbitCenter + Vector3.up * 2f;
                    follow = 2f;
                    break;

                case Mode.Pose:
                    wantPos = posePos;
                    wantLook = poseLook;
                    follow = 5f;
                    break;

                case Mode.ThirdPerson:
                    if (target == null) return;
                    ReadMouse();
                    var pivot = target.position + Vector3.up * 1.7f;
                    var rot = Quaternion.Euler(pitch, Yaw, 0);
                    var dir = rot * Vector3.back;
                    float want = Mathf.Lerp(distance, 2.6f, aimZoom);         // 瞄準時拉近
                    float d = want;
                    // 鏡頭和角色之間有東西時往前拉，避免穿牆
                    if (Physics.SphereCast(pivot, 0.3f, dir, out var hit, want, ~0, QueryTriggerInteraction.Ignore)
                        && !hit.transform.IsChildOf(target))
                        d = Mathf.Max(1.2f, hit.distance - 0.2f);
                    var shoulder = rot * Vector3.right * (0.85f * aimZoom);    // 瞄準時往右偏：越肩視角
                    wantPos = pivot + dir * d + shoulder;
                    wantPos.y = Mathf.Max(wantPos.y, target.position.y + 0.3f); // 往上看時鏡頭不鑽進地板
                    // 看的方向完全由 pitch / yaw 決定（沒被地板擋住時，這個點剛好就是 pivot + shoulder）
                    wantLook = wantPos + rot * Vector3.forward * d;
                    follow = 14f;
                    break;

                default: // Cinematic
                    if (target == null) return;
                    float a = Time.unscaledTime * 12f;
                    wantPos = target.position + Quaternion.Euler(0, a, 0) * new Vector3(0, 3.2f, -8f);
                    wantLook = target.position + Vector3.up * 1.3f;
                    follow = 3f;
                    break;
            }

            float k = 1f - Mathf.Exp(-follow * dt);
            transform.position = Vector3.Lerp(transform.position, wantPos, k);
            lookPoint = Vector3.Lerp(lookPoint, wantLook, k);
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        }

        void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (!lookLocked)
            {
                var delta = mouse.delta.ReadValue();
                Yaw += delta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -60f, 65f);   // 可以抬頭瞄準高處的金屬
            }
            if (!scrollZoom) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.6f, 3f, 12f);
        }

        /// 切換模式時直接跳到目標附近（例如剛進遊戲）。
        public void SnapLook(Vector3 look) => lookPoint = look;
    }
}
