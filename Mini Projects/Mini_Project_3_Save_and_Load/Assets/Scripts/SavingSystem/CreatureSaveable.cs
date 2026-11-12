using System;
using UnityEngine;
using UnityEngine.AI;

namespace MP3_Save
{
    /// 掛在每隻龍的根物件上（和 NavMeshAgent、DragonWander 同一個物件）。
    public class CreatureSaveable : MonoBehaviour, ISaveable
    {
        DragonWander wander;
        NavMeshAgent agent;
        Animator animator;

        public string Id => wander.Id;

        void Awake()
        {
            wander = GetComponent<DragonWander>();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            if (wander == null) Debug.LogError("[CreatureSaveable] 找不到 DragonWander", this);
            if (animator == null) Debug.LogError("[CreatureSaveable] 子物件裡找不到 Animator", this);
        }

        // 場景 → data：每隻龍各加一筆到 creatures
        public void CaptureState(GameSaveData data)
        {
            if (data.creatures.Exists(c => c.id == Id))
                Debug.LogWarning($"[CreatureSaveable] id 重複：{Id}，讀檔時只會還原其中一隻", this);

            var info = animator.GetCurrentAnimatorStateInfo(0);
            data.creatures.Add(new CreatureState
            {
                id = Id,
                position = transform.position,
                rotation = transform.rotation,
                speed = wander.CurrentSpeed,
                action = wander.CurrentAction,
                moveSpeed = wander.MoveSpeed,
                aiState = wander.State.ToString(),
                stateTimer = wander.StateTimer,
                wanderTarget = wander.WanderTarget,
                animStateHash = info.fullPathHash,
                animNormalizedTime = info.normalizedTime % 1f,
            });
        }

        // data → 場景：用 id 找回自己那一筆
        public void RestoreState(GameSaveData data)
        {
            var c = data.creatures.Find(x => x.id == Id);
            if (c == null) return;

            // NavMeshAgent 會控制位置，直接改 transform 沒用，要用 Warp
            agent.Warp(c.position);
            transform.rotation = c.rotation;

            var s = Enum.TryParse(c.aiState, out DragonWander.AIState parsed) ? parsed : DragonWander.AIState.Idle;
            wander.RestoreAI(s, c.stateTimer, c.wanderTarget, c.moveSpeed, c.speed);

            animator.Play(c.animStateHash, 0, c.animNormalizedTime);
            animator.Update(0f);
        }
    }
}