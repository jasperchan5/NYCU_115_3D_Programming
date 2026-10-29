using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace FunctionSelection
{
    /// 
    /// 示範影片：
    ///   1. 有 StreamingAssets/demo.mp4 → 用 VideoPlayer 全螢幕播放，播完自動回主選單
    ///   2. 沒有影片（或影片無法播放）→ 播放「即時示範」：角色自動在村莊裡導覽，攝影機繞著拍
    /// 
    public class DemoDirector : MonoBehaviour
    {
        public static string VideoPath => Path.Combine(Application.streamingAssetsPath, "demo.mp4");
        public static bool HasVideo => File.Exists(VideoPath);

        public bool PlayingVideo { get; private set; }
        public event Action Finished;

        VideoPlayer vp;
        CharacterRig walker;
        Camera cam;
        CameraDirector director;
        LevelInfo level;

        public void Begin(Camera cam, CameraDirector director, LevelInfo level)
        {
            this.cam = cam;
            this.director = director;
            this.level = level;
            if (HasVideo) PlayVideo();
            else PlayLive();
        }

        void PlayVideo()
        {
            PlayingVideo = true;
            vp = cam.gameObject.GetComponent<VideoPlayer>();
            if (vp == null) vp = cam.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = VideoPath;
            vp.renderMode = VideoRenderMode.CameraNearPlane;   // 蓋在 3D 畫面上，UI 仍在最上層
            vp.targetCamera = cam;
            vp.aspectRatio = VideoAspectRatio.FitInside;
            vp.isLooping = false;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.SetDirectAudioVolume(0, SettingsManager.MasterVolume);
            vp.loopPointReached += OnVideoEnd;
            vp.errorReceived += OnVideoError;
            vp.enabled = true;
            vp.Play();
        }

        void OnVideoEnd(VideoPlayer source) => Finished?.Invoke();

        void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[Demo] 影片無法播放，改用即時示範：{message}");
            StopVideo();
            PlayLive();
        }

        void PlayLive()
        {
            PlayingVideo = false;
            walker = CharacterBuilder.Build(CharacterCatalog.Selected, level.root, "DemoWalker");
            walker.transform.position = level.tourPoints[0];
            var auto = walker.gameObject.AddComponent<AutoWalker>();
            auto.points = level.tourPoints;
            auto.def = CharacterCatalog.Selected;
            director.SetCinematic(walker.transform);
        }

        void StopVideo()
        {
            if (vp == null) return;
            vp.loopPointReached -= OnVideoEnd;
            vp.errorReceived -= OnVideoError;
            vp.Stop();
            vp.enabled = false;
        }

        public void End()
        {
            StopVideo();
            if (walker != null) Destroy(walker.gameObject);
            walker = null;
            PlayingVideo = false;
        }
    }

    /// 
    /// 自動導覽：沿著路線點走，轉向平滑；走一段會小跑一段。
    /// 
    public class AutoWalker : MonoBehaviour
    {
        public List<Vector3> points;
        public CharacterDef def;
        int index = 1;
        CharacterRig rig;
        float runTimer;

        void Start() => rig = GetComponent<CharacterRig>();

        void Update()
        {
            if (points == null || points.Count < 2) return;
            float dt = Time.deltaTime;
            runTimer += dt;
            bool run = Mathf.Repeat(runTimer, 10f) > 7f;          // 每 10 秒跑 3 秒
            float speed = run ? def.runSpeed : def.walkSpeed;

            var target = points[index];
            var to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.4f)
            {
                index = (index + 1) % points.Count;
                return;
            }

            var want = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-5f * dt));
            // 沿著目前朝向前進，轉彎時自然畫弧線，不會瞬間變向
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
            transform.position += transform.forward * speed * facing * dt;
            rig.SetLocomotion(speed * facing / def.runSpeed, true);
        }
    }
}
