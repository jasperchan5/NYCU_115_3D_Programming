using UnityEngine;

namespace FunctionSelection
{
    /// 
    /// 用程式合成音效與背景音樂（不需要任何音訊檔）。
    /// 
    public static class AudioSynth
    {
        const int Rate = 22050;

        /// 按鈕點擊：短促的高音。
        public static AudioClip Click() => Tone("click", 0.07f, t => 1320f, 0.35f);

        /// 游標移到按鈕上：更輕的提示音。
        public static AudioClip Hover() => Tone("hover", 0.04f, t => 880f, 0.15f);

        /// 撿到寶石：往上滑的音。
        public static AudioClip Collect() => Tone("collect", 0.25f, t => Mathf.Lerp(660f, 1760f, t), 0.35f);

        /// 被怪物碰到：往下滑的低音。
        public static AudioClip Hurt() => Tone("hurt", 0.3f, t => Mathf.Lerp(330f, 110f, t), 0.45f, square: true);

        static AudioClip Tone(string name, float seconds, System.Func<float, float> freqAt, float volume, bool square = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                phase += 2f * Mathf.PI * freqAt(u) / Rate;
                float s = Mathf.Sin(phase);
                if (square) s = Mathf.Sign(s) * 0.6f;
                float env = Mathf.Min(1f, u * 40f) * (1f - u) * (1f - u);   // 快速起音、自然衰減
                data[i] = s * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// 
        /// 16 秒可無縫循環的背景音樂：和弦鋪底 + 五聲音階琶音。
        /// 
        public static AudioClip Music()
        {
            const float bpm = 90f, beat = 60f / bpm;
            const int bars = 6;
            int n = Mathf.CeilToInt(bars * 4 * beat * Rate);
            var data = new float[n];

            // 每小節一個和弦（根音 Hz）：C – Am – F – G – C – G
            float[] roots = { 130.81f, 110.00f, 87.31f, 98.00f, 130.81f, 98.00f };
            int[][] chords =
            {
                new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 0, 4, 7 },
                new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 },
            };
            int[] arp = { 12, 16, 19, 24, 19, 16, 12, 7 };

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int bar = Mathf.Min((int)(t / (4 * beat)), bars - 1);
                float root = roots[bar];
                float s = 0f;

                // 和弦鋪底：三個音的柔和正弦
                foreach (int semi in chords[bar])
                {
                    float f = root * Mathf.Pow(2f, semi / 12f);
                    s += 0.05f * Mathf.Sin(2f * Mathf.PI * f * t);
                }

                // 八分音符琶音
                float eighth = beat / 2f;
                int step = (int)(t / eighth);
                float local = t - step * eighth;
                int semiArp = arp[step % arp.Length];
                if (chords[bar][1] == 3 && semiArp % 12 == 4) semiArp -= 1;   // 小三和弦用小三度
                float af = root * 2f * Mathf.Pow(2f, semiArp / 12f);
                float aEnv = Mathf.Exp(-local * 6f);
                s += 0.06f * aEnv * Mathf.Sin(2f * Mathf.PI * af * t);

                data[i] = s;
            }

            // 頭尾淡入淡出，循環時不會爆音
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                data[i] *= g;
                data[n - 1 - i] *= g;
            }

            var clip = AudioClip.Create("music", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
