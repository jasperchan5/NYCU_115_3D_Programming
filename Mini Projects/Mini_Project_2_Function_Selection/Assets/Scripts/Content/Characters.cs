using UnityEngine;

namespace FunctionSelection
{
    public enum Headgear { Helmet, Hood, WizardHat }

    /// 可選角色的資料：外觀與能力值。
    [System.Serializable]
    public class CharacterDef
    {
        public string name;
        public string title;
        public string description;
        public Color body, accent, skin;
        public Headgear headgear;
        public float walkSpeed, runSpeed, jumpHeight;
        public float scale;          // 整體大小
        public float bulk;           // 身體粗細

        // 選角畫面上的能力條（0–1）
        public float StatSpeed => Mathf.InverseLerp(5f, 9f, runSpeed);
        public float StatJump => Mathf.InverseLerp(0.8f, 2.2f, jumpHeight);
        public float StatPower => Mathf.InverseLerp(0.8f, 1.4f, bulk);
    }

    public static class CharacterCatalog
    {
        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                name = "Lerk", title = "The Adapt",
                description = "Heavy armour, steady steps. Slow, but hard to push around.",
                body = new Color(0.32f, 0.42f, 0.62f), accent = new Color(0.85f, 0.75f, 0.35f),
                skin = new Color(0.96f, 0.80f, 0.66f), headgear = Headgear.Helmet,
                walkSpeed = 3.0f, runSpeed = 5.5f, jumpHeight = 1.0f, scale = 1.1f, bulk = 1.35f,
            },
            new CharacterDef
            {
                name = "Dean", title = "The Prodigy",
                description = "Light on her feet. The fastest runner and the highest jumper.",
                body = new Color(0.25f, 0.55f, 0.30f), accent = new Color(0.55f, 0.35f, 0.20f),
                skin = new Color(0.90f, 0.72f, 0.58f), headgear = Headgear.Hood,
                walkSpeed = 3.8f, runSpeed = 8.5f, jumpHeight = 2.0f, scale = 0.95f, bulk = 0.9f,
            },
            new CharacterDef
            {
                name = "Haze", title = "The Destiny",
                description = "A wandering scholar. Balanced speed with a magical bounce.",
                body = new Color(0.45f, 0.25f, 0.62f), accent = new Color(0.95f, 0.85f, 0.40f),
                skin = new Color(0.98f, 0.86f, 0.74f), headgear = Headgear.WizardHat,
                walkSpeed = 3.4f, runSpeed = 7.0f, jumpHeight = 1.6f, scale = 1.0f, bulk = 1.05f,
            },
        };

        const string PrefKey = "fs.character";

        public static int SelectedIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, All.Length - 1);
            set { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
        }

        public static CharacterDef Selected => All[SelectedIndex];
    }

    /// 
    /// 用 primitive 組出人形角色：身體、頭、手、腳都掛在關節（pivot）上，方便做程序化動畫。
    /// 原點在腳底。
    /// 
    public static class CharacterBuilder
    {
        public static CharacterRig Build(CharacterDef def, Transform parent, string name = null)
        {
            var root = new GameObject(name ?? def.name).transform;
            root.SetParent(parent, false);

            var model = Geo.Pivot("Model", root, Vector3.zero);
            model.localScale = Vector3.one * def.scale;

            var bodyMat = MaterialLib.Get(def.body, 0.35f);
            var accentMat = MaterialLib.Get(def.accent, 0.6f, 0.3f);
            var skinMat = MaterialLib.Get(def.skin, 0.3f);
            var darkMat = MaterialLib.Get(new Color(0.12f, 0.1f, 0.1f), 0.5f);
            float b = def.bulk;

            // 身體（會隨呼吸上下）
            var torso = Geo.Pivot("Torso", model, new Vector3(0f, 0.95f, 0f));
            Geo.Prim(PrimitiveType.Capsule, torso, new Vector3(0, 0.35f, 0), new Vector3(0.55f * b, 0.45f, 0.38f * b), bodyMat);
            Geo.Prim(PrimitiveType.Cube, torso, new Vector3(0, 0.12f, 0), new Vector3(0.58f * b, 0.08f, 0.42f * b), accentMat, name: "Belt");

            // 頭與臉
            var head = Geo.Pivot("Head", torso, new Vector3(0, 0.95f, 0));
            Geo.Prim(PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.42f, skinMat);
            Geo.Prim(PrimitiveType.Sphere, head, new Vector3(-0.08f, 0.03f, 0.19f), Vector3.one * 0.06f, darkMat, name: "EyeL");
            Geo.Prim(PrimitiveType.Sphere, head, new Vector3(0.08f, 0.03f, 0.19f), Vector3.one * 0.06f, darkMat, name: "EyeR");

            switch (def.headgear)
            {
                case Headgear.Helmet:
                    Geo.Prim(PrimitiveType.Cylinder, head, new Vector3(0, 0.1f, 0), new Vector3(0.46f, 0.12f, 0.46f), accentMat);
                    Geo.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.3f, -0.05f), new Vector3(0.06f, 0.18f, 0.32f), MaterialLib.Get(new Color(0.8f, 0.15f, 0.15f)), name: "Plume");
                    break;
                case Headgear.Hood:
                    Geo.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.06f, -0.05f), new Vector3(0.48f, 0.44f, 0.48f), bodyMat, name: "Hood");
                    Geo.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.05f, -0.3f), new Vector3(0.12f, 0.12f, 0.2f), bodyMat, new Vector3(20, 0, 0), name: "HoodTip");
                    break;
                case Headgear.WizardHat:
                    Geo.Prim(PrimitiveType.Cylinder, head, new Vector3(0, 0.17f, 0), new Vector3(0.62f, 0.02f, 0.62f), bodyMat, name: "Brim");
                    for (int i = 0; i < 4; i++)                       // 越往上越細，疊出尖帽
                    {
                        float w = 0.34f - i * 0.075f;
                        Geo.Prim(PrimitiveType.Cylinder, head, new Vector3(0.02f * i, 0.26f + i * 0.12f, -0.02f * i),
                                 new Vector3(w, 0.07f, w), bodyMat, new Vector3(-6f * i, 0, 4f * i));
                    }
                    Geo.Prim(PrimitiveType.Sphere, head, new Vector3(0.09f, 0.72f, -0.09f), Vector3.one * 0.07f, MaterialLib.Emissive(def.accent, 2f), name: "Star");
                    break;
            }

            // 手臂（關節在肩膀）
            var armL = Geo.Pivot("ArmL", torso, new Vector3(-0.34f * b, 0.6f, 0));
            var armR = Geo.Pivot("ArmR", torso, new Vector3(0.34f * b, 0.6f, 0));
            foreach (var arm in new[] { armL, armR })
            {
                Geo.Prim(PrimitiveType.Capsule, arm, new Vector3(0, -0.28f, 0), new Vector3(0.15f, 0.3f, 0.15f), bodyMat);
                Geo.Prim(PrimitiveType.Sphere, arm, new Vector3(0, -0.6f, 0), Vector3.one * 0.14f, skinMat, name: "Hand");
            }

            // 腿（關節在髖部）
            var legL = Geo.Pivot("LegL", model, new Vector3(-0.14f * b, 0.95f, 0));
            var legR = Geo.Pivot("LegR", model, new Vector3(0.14f * b, 0.95f, 0));
            foreach (var leg in new[] { legL, legR })
            {
                Geo.Prim(PrimitiveType.Capsule, leg, new Vector3(0, -0.45f, 0), new Vector3(0.2f, 0.45f, 0.2f), darkMat);
                Geo.Prim(PrimitiveType.Cube, leg, new Vector3(0, -0.9f, 0.06f), new Vector3(0.2f, 0.1f, 0.3f), accentMat, name: "Boot");
            }

            var rig = root.gameObject.AddComponent<CharacterRig>();
            rig.torso = torso; rig.head = head;
            rig.armL = armL; rig.armR = armR; rig.legL = legL; rig.legR = legR;
            rig.def = def;
            return rig;
        }
    }

    /// 
    /// 程序化走路動畫：依速度擺動手腳、身體上下起伏；靜止時有呼吸動作。
    /// 由 PlayerController / AutoWalker 每幀呼叫 SetLocomotion。
    /// 
    public class CharacterRig : MonoBehaviour
    {
        public Transform torso, head, armL, armR, legL, legR;
        public CharacterDef def;

        float speed01, phase, grounded = 1f;
        float torsoBaseY;
        public float wave;           // >0 時舉手揮動（選角畫面用）

        void Start() => torsoBaseY = torso.localPosition.y;

        /// speed01：0 = 靜止，0.5 = 走，1 = 跑。
        public void SetLocomotion(float speed01, bool isGrounded)
        {
            this.speed01 = Mathf.Lerp(this.speed01, speed01, 1f - Mathf.Exp(-10f * Time.deltaTime));
            grounded = Mathf.Lerp(grounded, isGrounded ? 1f : 0f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            phase += dt * Mathf.Lerp(1.2f, 11f, speed01);           // 越快步頻越高
            float swing = Mathf.Lerp(0f, 50f, speed01) * grounded;
            float s = Mathf.Sin(phase);

            legL.localRotation = Quaternion.Euler(s * swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-s * swing, 0, 0);

            // 在空中：雙腳微彎、手臂張開
            float air = 1f - grounded;
            float armSwing = s * swing * 0.8f;
            armL.localRotation = Quaternion.Euler(-armSwing - 20f * air, 0, -8f - 35f * air);
            armR.localRotation = Quaternion.Euler(armSwing - 20f * air, 0, 8f + 35f * air);

            if (wave > 0f)
            {
                armR.localRotation = Quaternion.Euler(0, 0, 150f + Mathf.Sin(Time.time * 10f) * 20f);
                wave -= dt;
            }

            // 走路時身體上下、靜止時呼吸
            float bob = Mathf.Abs(Mathf.Cos(phase)) * 0.06f * speed01 + Mathf.Sin(Time.time * 2f) * 0.012f * (1f - speed01);
            var p = torso.localPosition;
            torso.localPosition = new Vector3(p.x, torsoBaseY + bob, p.z);
            torso.localRotation = Quaternion.Euler(6f * speed01, Mathf.Sin(phase) * 4f * speed01, 0);
            head.localRotation = Quaternion.Euler(-4f * speed01, 0, Mathf.Sin(Time.time * 1.3f) * 3f * (1f - speed01));
        }
    }
}
