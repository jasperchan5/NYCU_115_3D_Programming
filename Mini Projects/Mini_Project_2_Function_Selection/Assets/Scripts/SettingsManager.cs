using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FunctionSelection
{
    /// 
    /// 遊戲設定：解析度、全螢幕、畫質、主音量、音樂音量、亮度、Gamma。
    /// 全部存在 PlayerPrefs，下次開遊戲會自動套用。
    /// 亮度與 Gamma 透過 URP 後製（Color Adjustments / Lift Gamma Gain）即時調整。
    /// 
    public static class SettingsManager
    {
        const string KRes = "fs.res", KFull = "fs.full", KQual = "fs.quality", KMaster = "fs.master",
                     KMusic = "fs.music", KBright = "fs.bright", KGamma = "fs.gamma";

        static ColorAdjustments colorAdjust;
        static LiftGammaGain liftGammaGain;
        static List<Resolution> resolutions;

        public static AudioSource Music;   // 由 GameApp 指定

        public static void Init(Camera cam)
        {
            // 讓攝影機開啟後製，再建立一個優先權最高的全域 Volume
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            var go = new GameObject("SettingsVolume");
            Object.DontDestroyOnLoad(go);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjust = profile.Add<ColorAdjustments>(true);
            liftGammaGain = profile.Add<LiftGammaGain>(true);
            vol.sharedProfile = profile;

            ApplyAll();
        }

        // ---------------- 解析度 ----------------

        public static List<Resolution> Resolutions
        {
            get
            {
                if (resolutions == null)
                {
                    resolutions = Screen.resolutions
                        .GroupBy(r => (r.width, r.height))            // 同解析度不同更新率只留一個
                        .Select(g => g.First())
                        .OrderBy(r => r.width).ThenBy(r => r.height)
                        .ToList();
                    if (resolutions.Count == 0)
                        resolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
                }
                return resolutions;
            }
        }

        public static List<string> ResolutionLabels => Resolutions.Select(r => $"{r.width} x {r.height}").ToList();

        public static int ResolutionIndex
        {
            get
            {
                int saved = PlayerPrefs.GetInt(KRes, -1);
                if (saved >= 0 && saved < Resolutions.Count) return saved;
                int cur = Resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
                return cur >= 0 ? cur : Resolutions.Count - 1;
            }
            set { PlayerPrefs.SetInt(KRes, value); ApplyScreen(); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KFull, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(KFull, value ? 1 : 0); ApplyScreen(); }
        }

        static void ApplyScreen()
        {
            var r = Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Count - 1)];
            // 在 Editor 的 Game 視窗內不會生效，要在 Build 出來的執行檔裡才看得到
            Screen.SetResolution(r.width, r.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.Save();
        }

        // ---------------- 畫質 ----------------

        public static List<string> QualityLabels => QualitySettings.names.ToList();

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KQual, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set { PlayerPrefs.SetInt(KQual, value); QualitySettings.SetQualityLevel(value, true); PlayerPrefs.Save(); }
        }

        // ---------------- 音量 ----------------

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KMaster, 0.8f);
            set { PlayerPrefs.SetFloat(KMaster, value); AudioListener.volume = value; }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KMusic, 0.6f);
            set { PlayerPrefs.SetFloat(KMusic, value); if (Music != null) Music.volume = value; }
        }

        // ---------------- 亮度與 Gamma ----------------

        /// 亮度：曝光補償（EV），-2 到 +2。
        public static float Brightness
        {
            get => PlayerPrefs.GetFloat(KBright, 0f);
            set { PlayerPrefs.SetFloat(KBright, value); if (colorAdjust != null) colorAdjust.postExposure.Override(value); }
        }

        /// Gamma：-0.5（暗部更暗）到 +0.5（暗部更亮），0 為不調整。
        public static float Gamma
        {
            get => PlayerPrefs.GetFloat(KGamma, 0f);
            set { PlayerPrefs.SetFloat(KGamma, value); if (liftGammaGain != null) liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, value)); }
        }

        public static void ApplyAll()
        {
            AudioListener.volume = MasterVolume;
            if (Music != null) Music.volume = MusicVolume;
            Brightness = Brightness;
            Gamma = Gamma;
            QualitySettings.SetQualityLevel(Quality, true);
            // 解析度只有在玩家改過時才套用，避免第一次開啟就改變視窗大小
            if (PlayerPrefs.HasKey(KRes) || PlayerPrefs.HasKey(KFull)) ApplyScreen();
        }

        public static void ResetDefaults()
        {
            foreach (var k in new[] { KMaster, KMusic, KBright, KGamma }) PlayerPrefs.DeleteKey(k);
            ApplyAll();
            PlayerPrefs.Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
