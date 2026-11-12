using UnityEngine;

namespace MP3_Save
{
    public class PlayerSaveable : MonoBehaviour, ISaveable
    {
        public string Id => "player";

        CharacterController controller;
        Animator animator;
        PlayerMovement movement;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            movement = GetComponent<PlayerMovement>();
        }

        // 場景 → data
        public void CaptureState(GameSaveData data)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);

            data.player = new PlayerState
            {
                position = transform.position,
                rotation = transform.rotation,
                speed = movement.CurrentSpeed,
                action = movement.CurrentAction,
                verticalVelocity = movement.VerticalVelocity,
                animStateHash = info.fullPathHash,
                animNormalizedTime = info.normalizedTime % 1f,
            };
        }

        // data → 場景
        public void RestoreState(GameSaveData data)
        {
            var p = data.player;          // 取出存檔裡的那筆
            if (p == null) return;

            // CharacterController 開著時會把位置拉回去，所以先關掉再設定
            controller.enabled = false;
            transform.position = p.position;
            transform.rotation = p.rotation;
            controller.enabled = true;

            // 速度、垂直速度、Speed/Grounded 參數先設好，再播動畫
            movement.RestoreMotion(p.speed, p.verticalVelocity, p.action != "Jump");

            // 從存檔當下的狀態、同一個時間點繼續播
            animator.Play(p.animStateHash, 0, p.animNormalizedTime);
            animator.Update(0f);          // 這一幀就套用姿勢，不會閃一下舊動作
        }
    }
}