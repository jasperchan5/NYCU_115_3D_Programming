using UnityEngine;

namespace Allomancy
{
    /// 
    /// 敵人的鎔金術：這個敵人「正在燒哪些金屬」，以及他是不是「搜尋者」。
    ///
    /// 規則（遊戲版本，跟原作不完全一樣）：
    ///   紅銅：只遮蔽自己。敵人燒紅銅 → 玩家用青銅看不到他燒什麼（小輪盤全暗）；
    ///         玩家燒紅銅 → 搜尋者感覺不到玩家在燒金屬。
    ///   搜尋者（青銅色頭盔，燒青銅）：玩家「有燒金屬、但沒燒紅銅」時，
    ///         只要在 SeekerRange 以內，就算隔著牆也會進入警戒、直接追過來。
    /// 
    public class EnemyAllomancy : MonoBehaviour
    {
        public readonly bool[] burning = new bool[MetalInfo.Count];
        public bool isSeeker;
        public MetalReserves playerReserves;

        public const float SeekerRange = 40f;      // 搜尋者感應的距離（不管有沒有障礙物）
        const float AlertTime = 3f;                // 玩家停止燒金屬後，警戒還會持續幾秒
        float alertTimer;

        /// 警戒中：Enemy 會無視追擊距離直接追玩家。
        public bool Alerted => alertTimer > 0f;

        /// 燒紅銅：其他人（玩家的青銅）看不到他在燒什麼。
        public bool Hidden => burning[(int)Metal.Copper];

        /// 
        /// 四種敵人輪流出現（role = 0~3）：
        ///   0 搜尋者：青銅   1 打手：白鑞   2 煙陣：只燒紅銅（用青銅看是全暗的）   3 錫眼：錫
        /// 
        public static EnemyAllomancy Setup(Enemy enemy, MetalReserves playerReserves, int role)
        {
            var a = enemy.gameObject.AddComponent<EnemyAllomancy>();
            a.playerReserves = playerReserves;
            switch (role % 4)
            {
                case 0: a.isSeeker = true; a.burning[(int)Metal.Bronze] = true; break;
                case 1: a.burning[(int)Metal.Pewter] = true; break;
                case 2: a.burning[(int)Metal.Copper] = true; break;
                default: a.burning[(int)Metal.Tin] = true; break;
            }

            // 搜尋者：頭盔換成青銅色（金屬感：smoothness 0.7、metallic 0.9）
            if (a.isSeeker)
            {
                var helmet = enemy.transform.Find("Helmet");
                if (helmet != null)
                {
                    helmet.GetComponent<Renderer>().sharedMaterial = Build.Mat(MetalInfo.Colors[(int)Metal.Bronze], 0.7f, 0.9f);
                    helmet.GetComponent<MetalObject>().displayName = "Seeker's bronze helmet";
                }
            }
            return a;
        }

        /// 玩家正在「發出鎔金脈動」= 有燒任何金屬，而且沒有燒紅銅。
        public static bool PlayerPulsing(MetalReserves r)
        {
            if (r.IsBurning(Metal.Copper)) return false;      // 紅銅雲把脈動藏起來
            for (int i = 0; i < MetalInfo.Count; i++)
                if (r.burning[i]) return true;
            return false;
        }

        void Update()
        {
            alertTimer -= Time.deltaTime;
            if (!isSeeker || playerReserves == null) return;

            // 不做射線檢查 → 隔著障礙物也感覺得到
            float dist = Vector3.Distance(transform.position, playerReserves.transform.position);
            if (dist < SeekerRange && PlayerPulsing(playerReserves)) alertTimer = AlertTime;
        }
    }
}
