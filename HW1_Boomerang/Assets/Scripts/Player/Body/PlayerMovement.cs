using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float normalSpeed = 4f;
    public float fastSpeed = 9f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    [Range(0f, 1f)] public float airControl = 0.2f;

    [Header("Refs")]
    public Transform cam;
    Throw thrower;

    [Header("Respawn")]
    public Vector3 respawnPoint = new Vector3(0f, 2f, -10f);
    public float fallThreshold = -10f;

    [Header("Push")]
    public float pushPower = 2f;

    CharacterController cc;
    Animator anim;

    Vector3 horizontalVelocity;   // 水平速度，跳躍時保留
    float verticalVelocity;
    bool isFast;

    public bool IsFast => isFast;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        thrower = GetComponent<Throw>();
        if (!cam && Camera.main) cam = Camera.main.transform;
    }

    void Update()
    {
        // SPACE 切換速度
        if (Input.GetKeyDown(KeyCode.Space)) isFast = !isFast;
        float speed = isFast ? fastSpeed : normalSpeed;

        // 角色朝向 = 鏡頭的水平朝向
        Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (camForward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(camForward);

        // W/S for movement, locked while throwing/catching
        bool locked = thrower && thrower.IsLocked;
        float v = locked ? 0f : Input.GetAxisRaw("Vertical");
        Vector3 desired = transform.forward * v * speed;


        if (cc.isGrounded)
        {
            horizontalVelocity = desired;

            if (verticalVelocity < 0f) verticalVelocity = -2f;

            // F 跳躍：保留當下水平速度形成拋物線
            if (Input.GetKeyDown(KeyCode.F))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            // 空中只有有限的控制力
            horizontalVelocity = Vector3.Lerp(
                horizontalVelocity, desired, airControl * Time.deltaTime * 10f);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        cc.Move(motion * Time.deltaTime);

        UpdateAnimator(v, speed);
        if (transform.position.y < fallThreshold) Respawn();
    }

    void UpdateAnimator(float v, float speed)
    {
        if (!anim) return;
        anim.SetFloat("Speed", v * speed, 0.1f, Time.deltaTime);
        anim.SetBool("Grounded", cc.isGrounded);
    }

    public void Respawn()
    {
        // Character Controller 啟用時會覆寫 transform.position，必須先關掉
        cc.enabled = false;
        transform.position = respawnPoint;
        transform.rotation = Quaternion.identity;
        cc.enabled = true;

        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.collider.attachedRigidbody;
        if (!rb || rb.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;      // 別把踩到的東西往下壓

        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        rb.AddForce(dir * pushPower, ForceMode.Impulse);
    }
}