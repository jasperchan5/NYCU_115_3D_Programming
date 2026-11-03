using UnityEngine;

namespace Allomancy
{
    /// 金屬藥瓶：碰到就補充所有金屬 40%，20 秒後重新出現。
    public class MetalVial : MonoBehaviour
    {
        public MetalReserves reserves;
        public Transform player;
        Vector3 basePos;
        float respawn;
        GameObject body;
        public bool Available => body.activeSelf;

        public static MetalVial Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("MetalVial");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var v = go.AddComponent<MetalVial>();
            v.basePos = pos;
            v.body = new GameObject("Body");
            v.body.transform.SetParent(go.transform, false);
            Build.Prim(PrimitiveType.Capsule, v.body.transform, Vector3.zero, new Vector3(0.22f, 0.25f, 0.22f), Build.Glow(new Color(0.7f, 0.85f, 1f)));
            Build.Prim(PrimitiveType.Cylinder, v.body.transform, new Vector3(0, 0.3f, 0), new Vector3(0.1f, 0.06f, 0.1f), Build.Mat(new Color(0.4f, 0.3f, 0.2f)));
            return v;
        }

        void Update()
        {
            if (respawn > 0f)
            {
                respawn -= Time.deltaTime;
                if (respawn <= 0f) body.SetActive(true);
                return;
            }
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
            transform.Rotate(0, 60f * Time.deltaTime, 0);
            if ((player.position + Vector3.up - transform.position).sqrMagnitude < 1.3f * 1.3f)
            {
                reserves.Refill(0.4f);
                SoundFx.Play(SoundFx.Kind.Pickup);
                body.SetActive(false);
                respawn = 20f;
            }
        }
    }
}
