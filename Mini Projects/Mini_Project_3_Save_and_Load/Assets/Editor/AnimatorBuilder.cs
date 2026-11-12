// 放在 Assets/Editor/ 底下（資料夾名稱一定要叫 Editor）
// 選單：Tools > MP3 > Build Animators
// 1. 設定 person_animated.fbx / dragon_animated.fbx 的匯入選項（Generic、迴圈、clip 名稱、龍的縮放）
// 2. 產生 Assets/Animators/Player.controller 和 Dragon.controller
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatorBuilder
{
    const string OutDir = "Assets/Animators";

    // 角色腳本裡的 walkSpeed / runSpeed，Blend Tree 的門檻要跟它一致
    const float PlayerWalkSpeed = 2f;
    const float PlayerRunSpeed = 5f;
    const float DragonScale = 0.06f;   // 龍原始尺寸很大，這裡縮小；之後可在 FBX 的 Model 分頁改 Scale Factor

    [MenuItem("Tools/MP3/Build Animators")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutDir);

        string personPath = FindModel("person_animated");
        string dragonPath = FindModel("dragon_animated");
        if (personPath == null || dragonPath == null)
        {
            EditorUtility.DisplayDialog("MP3", "找不到 person_animated.fbx 或 dragon_animated.fbx，請先把它們放進 Assets。", "OK");
            return;
        }

        ConfigureModel(personPath, new[] { "Idle", "Walk", "Run" }, 1f);
        ConfigureModel(dragonPath, new[] { "Idle", "Walk", "Sniff" }, DragonScale);

        BuildPlayer(personPath);
        BuildDragon(dragonPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MP3] Animators built in " + OutDir);
    }

    static string FindModel(string name)
    {
        return AssetDatabase.FindAssets(name + " t:Model")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
    }

    // Blender 匯出的 take 名稱像 "Armature|Idle"，這裡改成 "Idle" 並設定迴圈
    static void ConfigureModel(string path, string[] loopClips, float scale)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.globalScale = scale;

        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            string shortName = c.takeName.Split('|').Last();
            c.name = shortName;
            bool loop = loopClips.Contains(shortName);
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = true;     // Bake Into Pose：動畫不帶位移，位移交給程式
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimationClip Clip(string modelPath, string name)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogError($"[MP3] {modelPath} 裡找不到 clip：{name}");
        return clip;
    }

    // ---------- 人：Locomotion(Blend Tree: Idle/Walk/Run) + Jump ----------
    static void BuildPlayer(string modelPath)
    {
        string path = OutDir + "/Player.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter(new AnimatorControllerParameter
            { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;

        var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip(modelPath, "Idle"), 0f);
        tree.AddChild(Clip(modelPath, "Walk"), PlayerWalkSpeed);
        tree.AddChild(Clip(modelPath, "Run"), PlayerRunSpeed);
        sm.defaultState = loco;

        var jump = sm.AddState("Jump");
        jump.motion = Clip(modelPath, "Jump");

        // Locomotion → Jump：收到 Jump 立刻切
        var toJump = loco.AddTransition(jump);
        toJump.hasExitTime = false;
        toJump.duration = 0.05f;
        toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // Jump → Locomotion：播到一半以後、而且著地才回來（避免起跳那幀還算著地就馬上切回）
        var toLoco = jump.AddTransition(loco);
        toLoco.hasExitTime = true;
        toLoco.exitTime = 0.5f;
        toLoco.duration = 0.15f;
        toLoco.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        EditorUtility.SetDirty(ac);
    }

    // ---------- 龍：Idle / Walk / Sniff ----------
    static void BuildDragon(string modelPath)
    {
        string path = OutDir + "/Dragon.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);   // NavMeshAgent 的實際速度
        ac.AddParameter("Sniff", AnimatorControllerParameterType.Bool);
        ac.AddParameter(new AnimatorControllerParameter
            { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });  // 走路動畫播放倍率

        var sm = ac.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = Clip(modelPath, "Idle");
        var walk = sm.AddState("Walk");
        walk.motion = Clip(modelPath, "Walk");
        walk.speedParameterActive = true;      // 走得快的龍，腳步也播得快
        walk.speedParameter = "AnimSpeed";
        var sniff = sm.AddState("Sniff");
        sniff.motion = Clip(modelPath, "Sniff");
        sm.defaultState = idle;

        Link(idle, walk, 0.25f, ("Speed", AnimatorConditionMode.Greater, 0.1f));
        Link(walk, idle, 0.25f, ("Speed", AnimatorConditionMode.Less, 0.1f));
        Link(idle, sniff, 0.3f, ("Sniff", AnimatorConditionMode.If, 0f));
        Link(sniff, idle, 0.3f, ("Sniff", AnimatorConditionMode.IfNot, 0f));

        EditorUtility.SetDirty(ac);
    }

    static void Link(AnimatorState from, AnimatorState to, float duration,
                     (string param, AnimatorConditionMode mode, float threshold) cond)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        t.AddCondition(cond.mode, cond.threshold, cond.param);
    }
}
#endif