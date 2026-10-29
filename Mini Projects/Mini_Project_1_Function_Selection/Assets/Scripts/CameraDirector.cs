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

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
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
                    var dir = Quaternion.Euler(pitch, Yaw, 0) * Vector3.back;
                    float d = distance;
                    // 鏡頭和角色之間有東西時往前拉，避免穿牆
                    if (Physics.SphereCast(pivot, 0.3f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
                        && !hit.transform.IsChildOf(target))
                        d = Mathf.Max(1.2f, hit.distance - 0.2f);
                    wantPos = pivot + dir * d;
                    wantLook = pivot;
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
            var delta = mouse.delta.ReadValue();
            Yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -20f, 65f);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.6f, 3f, 12f);
        }

        /// 切換模式時直接跳到目標附近（例如剛進遊戲）。
        public void SnapLook(Vector3 look) => lookPoint = look;
    }
}
