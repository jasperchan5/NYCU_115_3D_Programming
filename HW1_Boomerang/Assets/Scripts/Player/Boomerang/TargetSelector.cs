using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TargetSelector : MonoBehaviour
{
    [Header("Picking")]
    public float maxRange = 50f;
    public LayerMask pickableLayers = ~0;   // 預設打到所有層
    public string validTag = "Target";

    [Header("Audio")]
    public AudioClip errorSound;

    [Header("Refs")]
    public Throw thrower;

    AudioSource audioSrc;
    Camera cam;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (!thrower) thrower = GetComponent<Throw>();
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        cam = Camera.main;
        if (!cam) return;

        Ray ray = BuildRay();

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, pickableLayers))
        {
            PlayError();
            return;
        }

        var target = hit.transform.GetComponentInParent<TargetObject>();
        if (target)
            thrower.TryThrow(target.transform);
        else
            PlayError();
    }

    Ray BuildRay()
    {
        // 第一人稱鏡頭鎖定滑鼠時，游標在螢幕中心
        if (Cursor.lockState == CursorLockMode.Locked)
            return cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        return cam.ScreenPointToRay(Input.mousePosition);
    }

    void PlayError()
    {
        if (errorSound) audioSrc.PlayOneShot(errorSound);
        else Debug.Log("Invalid target");
    }
}