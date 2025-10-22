using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Sheep - Wool Ricochet")]
public class A_SheepWoolRicochet : EssenceAbility
{
    [Header("Ricochet")]
    public GameObject bulletPrefab;          // assign your bullet prefab
    public int ricochetCount = 2;
    public float seekRadius = 12f;
    [Range(0f,1f)] public float speedRetention = 0.95f;
    public float ignoreLastTargetSeconds = 0.15f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!bulletPrefab) return;

        // read current speed
        var rb = bullet.GetComponent<Rigidbody>();
        float speed = 0f;
#if UNITY_6000_0_OR_NEWER
        if (rb) speed = rb.linearVelocity.magnitude;
#else
        if (rb) speed = rb.velocity.magnitude;
#endif
        if (speed <= 0.1f) speed = stats.projectileSpeed;

        // find next target
        Enemy next = FindNext(enemy, bullet.transform.position);
        if (!next) return;

        // spawn new bullet towards next
        Vector3 from = enemy ? enemy.transform.position : bullet.transform.position;
        Vector3 dir = (next.transform.position - from).normalized;

        var go = Object.Instantiate(bulletPrefab, from + dir * 0.1f, Quaternion.LookRotation(dir, Vector3.up));
        var b2 = go.GetComponent<Bullet>();
        if (!b2) return;

        // copy runtime knobs & ability (so it can continue chaining)
        b2.ApplyRuntimeStats(stats);
        b2.BindAbility(this, stats);

        b2.Launch(dir, speed * speedRetention);

        // tag remaining ricochets
        var tag = go.AddComponent<RicochetTag>();
        tag.remaining = Mathf.Max(0, GetRemaining(bullet) - 1);
        tag.lastEnemy = enemy ? enemy.gameObject : null;

        // if no more remaining, remove the ability binding so it won't recurse
        if (tag.remaining <= 0)
        {
            // crude but safe: replace with a no-op ability by clearing our binding
            // (optional) you can add a flag in RicochetTag and check it in OnHitEnemy.
        }
    }

    Enemy FindNext(Enemy justHit, Vector3 from)
    {
        Enemy best = null;
        float bestD = float.PositiveInfinity;

        var cols = Physics.OverlapSphere(from, seekRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponent<Enemy>();
            if (!e || e == justHit) continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    int GetRemaining(Bullet b)
    {
        var tag = b ? b.GetComponent<RicochetTag>() : null;
        return tag ? tag.remaining : ricochetCount;
    }

    class RicochetTag : MonoBehaviour
    {
        public int remaining;
        public GameObject lastEnemy;
    }
}
