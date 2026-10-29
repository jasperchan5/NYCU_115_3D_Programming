using System;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 玩家血量。燃燒白鑞（Pewter）時：受到的傷害減半，而且會慢慢回血。
    /// 
    public class PlayerHealth : MonoBehaviour
    {
        public float max = 100f;
        public float current = 100f;
        public MetalReserves reserves;

        /// 變化量（負 = 受傷、正 = 補血），給 HUD 做閃爍效果。
        public event Action<float> Changed;
        public event Action Died;

        public float Fraction => current / max;

        public void Damage(float amount)
        {
            if (reserves != null && reserves.IsBurning(Metal.Pewter)) amount *= 0.5f;
            Set(current - amount);
            if (current <= 0f) Died?.Invoke();
        }

        public void Heal(float amount) => Set(current + amount);

        void Set(float value)
        {
            float before = current;
            current = Mathf.Clamp(value, 0f, max);
            if (!Mathf.Approximately(before, current)) Changed?.Invoke(current - before);
        }

        void Update()
        {
            // 白鑞：每秒回 4 點；這是連續的小變化，不觸發 Changed（避免 HUD 一直閃）
            if (reserves != null && reserves.IsBurning(Metal.Pewter) && current < max)
                current = Mathf.Min(max, current + 4f * Time.deltaTime);
        }
    }
}
