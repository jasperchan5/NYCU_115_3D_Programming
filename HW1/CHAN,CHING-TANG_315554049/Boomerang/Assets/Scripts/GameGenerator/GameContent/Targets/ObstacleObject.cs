using UnityEngine;

// 障礙物被武器擊中時的反應。刻意跟 TargetObject 長得一樣，
// 差別只在於障礙物不計分、不消失——題目要求「Cannot be destroyed by boomerangs」。
public class ObstacleObject : MonoBehaviour
{
    [Header("Hit Response")]
    public float knockForce = 6f;
    public float knockUpForce = 2f;

    [Header("Audio")]
    public AudioClip hitSound;

    public void OnHitByBoomerang(Vector3 fromPosition)
    {
        if (hitSound) AudioSource.PlayClipAtPoint(hitSound, transform.position);

        var rb = GetComponent<Rigidbody>();
        if (rb && !rb.isKinematic)
        {
            Vector3 dir = (transform.position - fromPosition).normalized;
            rb.AddForce(dir * knockForce + Vector3.up * knockUpForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * knockForce, ForceMode.Impulse);
        }
    }
}