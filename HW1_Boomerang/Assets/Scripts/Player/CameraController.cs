using UnityEngine;

public class CameraController : MonoBehaviour
{
    public enum Mode { FirstPerson, ThirdPerson }

    [Header("Target")]
    public Transform target;
    public float eyeHeight = 1.6f;      // 第一人稱：眼睛高度
    public float focusHeight = 1.4f;    // 第三人稱：看向的高度

    [Header("Rotation")]
    public float sensitivity = 3f;
    public float minPitch = -60f;
    public float maxPitch = 70f;

    [Header("Third Person")]
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float zoomSpeed = 3f;

    [Header("Mode")]
    public Mode mode = Mode.ThirdPerson;
    public KeyCode toggleKey = KeyCode.V;

    [Header("Cursor")]
    // 按住不放不行，要用切換的：游標放出來之後滑鼠要能移到視窗外
    public KeyCode cursorToggleKey = KeyCode.Tab;

    float yaw, pitch;
    bool cursorFree;

    Renderer[] bodyRenderers;
    Transform cachedTarget;

    public bool CursorFree => cursorFree;

    void Start()
    {
        ApplyCursor();
        yaw = target ? target.eulerAngles.y : 0f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        if (Input.GetKeyDown(cursorToggleKey))
        {
            cursorFree = !cursorFree;
            ApplyCursor();
        }

        if (Input.GetKeyDown(toggleKey))
            mode = mode == Mode.FirstPerson ? Mode.ThirdPerson : Mode.FirstPerson;

        // 滾輪調整距離（第三人稱）
        if (mode == Mode.ThirdPerson)
        {
            distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * 10f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void ApplyCursor()
    {
        Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursorFree;
    }

    // 切出視窗再切回來時 Unity 會自己解除鎖定，要重新套用一次
    void OnApplicationFocus(bool focused)
    {
        if (focused) ApplyCursor();
    }

    void CacheRenderers()
    {
        if (target == cachedTarget) return;   // 沒換人就不用重抓
        cachedTarget = target;
        bodyRenderers = target ? target.GetComponentsInChildren<Renderer>() : null;
    }

    void ApplyMode()
    {
        if (bodyRenderers == null) return;
        bool show = mode == Mode.ThirdPerson;
        foreach (var r in bodyRenderers)
            if (r) r.enabled = show;
    }

    void LateUpdate()
    {
        if (!target) return;

        CacheRenderers();
        ApplyMode();

        // 游標放出來時不要轉鏡頭，否則滑鼠一動畫面就跟著甩
        if (!cursorFree)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        if (mode == Mode.FirstPerson)
        {
            transform.position = target.position + Vector3.up * eyeHeight;
            transform.rotation = rot;
        }
        else
        {
            Vector3 focus = target.position + Vector3.up * focusHeight;
            transform.position = focus - rot * Vector3.forward * distance;
            transform.rotation = rot;
        }
    }
}