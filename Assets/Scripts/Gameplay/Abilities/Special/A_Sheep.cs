using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Sheep - Wool Ricochet")]
public class A_SheepWoolRicochet : EssenceAbility
{
    [Header("Ricochet")]
    public int   ricochetCount = 2;        // redirects after first hit (2 -> ends on 3rd enemy)
    public float seekRadius    = 14f;      // search radius for next enemy
    [Range(0f,1f)] public float speedRetention = 0.95f;
    public float ignoreLastTargetSeconds = 0.15f;  // don't bounce back immediately
    public float forwardNudge = 0.18f;             // step out of collider before redirect

    [Header("Steer lock")]
    [Tooltip("Hold redirected heading briefly so homing/bounce can't override it.")]
    public float steerLockDuration = 0.08f;

    [Header("Safety")]
    public bool ensureMinPierce = true; // guarantee bullet survives hits while ricocheting

    public override void OnAboutToFire(WeaponStats active)
    {
        if (!ensureMinPierce || active == null) return;

        // For N ricochets (redirects), the bullet must pass through N prior enemies.
        // We END on the final enemy ourselves, so min pierce is N (not N+1).
        int needed = Mathf.Max(0, ricochetCount);
        if (active.pierceCount < needed) active.pierceCount = needed;
    }

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        if (!bullet) return;
        var rt = bullet.GetComponent<RicochetRuntime>();
        if (!rt) rt = bullet.gameObject.AddComponent<RicochetRuntime>();

        rt.remaining       = ricochetCount;
        rt.seekRadius      = seekRadius;
        rt.speedRetention  = Mathf.Clamp01(speedRetention);
        rt.ignoreSecs      = Mathf.Max(0f, ignoreLastTargetSeconds);
        rt.forwardNudge    = Mathf.Max(0f, forwardNudge);
        rt.steerLockSecs   = Mathf.Max(0f, steerLockDuration);
        rt.lastEnemyGO     = null;
        rt.ignoreUntilTime = -999f;

        if (rt.visited == null) rt.visited = new HashSet<Enemy>();
        else rt.visited.Clear();
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!bullet || !enemy) return;

        var rt = bullet.GetComponent<RicochetRuntime>();
        if (rt == null) return;

        // Mark current as visited so we never bounce back here later
        if (rt.visited != null) rt.visited.Add(enemy);

        // If we've used all ricochets, END here (destroy bullet on this enemy)
        if (rt.remaining <= 0)
        {
            Object.Destroy(bullet.gameObject);
            return;
        }

        Vector3 origin = enemy.transform.position;

        // Find nearest NEW target in radius (exclude visited + recent last)
        Enemy next = FindNextTarget(enemy, origin, rt);
        if (!next)
        {
            // No valid next target -> end on this enemy
            Object.Destroy(bullet.gameObject);
            return;
        }

        // Current speed (fallback to stats)
        var rb = bullet.GetComponent<Rigidbody>();
        float speed =
#if UNITY_6000_0_OR_NEWER
            (rb ? rb.linearVelocity.magnitude : 0f);
#else
            (rb ? rb.velocity.magnitude : 0f);
#endif
        if (speed <= 0.1f) speed = stats.projectileSpeed;

        // Redirect toward chosen target (planar for readability; remove .y=0 to use full 3D)
        Vector3 to = next.transform.position - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-6f) { Object.Destroy(bullet.gameObject); return; }
        Vector3 dir = to.normalized;

        bullet.transform.position = origin + dir * rt.forwardNudge;
        bullet.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        if (rb)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = dir * (speed * rt.speedRetention);
            rb.angularVelocity = Vector3.zero;
#else
            rb.velocity = dir * (speed * rt.speedRetention);
            rb.angularVelocity = Vector3.zero;
#endif
        }
        else
        {
            bullet.Launch(dir, speed * rt.speedRetention);
        }

        // Briefly “pin” the heading so homing/bounce doesn’t immediately undo the redirect
        if (rt.steerLockSecs > 0f) TemporarySteerLock.Begin(bullet.gameObject, dir, rt.steerLockSecs);

        // Ignore the enemy we just hit for a short window & step remaining
        rt.remaining--;
        rt.lastEnemyGO     = enemy.gameObject;
        rt.ignoreUntilTime = Time.time + rt.ignoreSecs;
        rt.ApplyTemporaryIgnore(bullet, enemy, rt.ignoreSecs);
    }

    Enemy FindNextTarget(Enemy justHit, Vector3 from, RicochetRuntime rt)
    {
        Enemy best = null;
        float bestSqr = float.PositiveInfinity;

        var cols = Physics.OverlapSphere(from, rt.seekRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponent<Enemy>();
            if (!e) continue;
            if (e == justHit) continue;

            if (rt.lastEnemyGO == e.gameObject && Time.time < rt.ignoreUntilTime) continue;
            if (rt.visited != null && rt.visited.Contains(e)) continue;

            float d2 = (e.transform.position - from).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = e; }
        }
        return best;
    }

    // Per-bullet runtime + helpers
    class RicochetRuntime : MonoBehaviour
    {
        public int   remaining;
        public float seekRadius;
        public float speedRetention;
        public float ignoreSecs;
        public float forwardNudge;
        public float steerLockSecs;

        public GameObject lastEnemyGO;
        public float      ignoreUntilTime;

        public HashSet<Enemy> visited;

        public void ApplyTemporaryIgnore(Bullet b, Enemy e, float seconds)
        {
            if (!b || !e) return;
            var bulletCols = b.GetComponentsInChildren<Collider>();
            var enemyCols  = e.GetComponentsInChildren<Collider>();
            if (bulletCols == null || enemyCols == null) return;

            for (int i = 0; i < bulletCols.Length; i++)
                for (int j = 0; j < enemyCols.Length; j++)
                    if (bulletCols[i] && enemyCols[j])
                        Physics.IgnoreCollision(bulletCols[i], enemyCols[j], true);

            if (seconds > 0f) StartCoroutine(UndoIgnoresAfter(seconds, bulletCols, enemyCols));
        }

        System.Collections.IEnumerator UndoIgnoresAfter(float delay, Collider[] bullets, Collider[] enemies)
        {
            yield return new WaitForSeconds(delay);
            if (bullets == null || enemies == null) yield break;

            for (int i = 0; i < bullets.Length; i++)
                for (int j = 0; j < enemies.Length; j++)
                    if (bullets[i] && enemies[j])
                        Physics.IgnoreCollision(bullets[i], enemies[j], false);
        }
    }

    // Holds the redirected heading briefly
    class TemporarySteerLock : MonoBehaviour
    {
        Rigidbody rb;
        Vector3 keepDir;
        float until;

        public static void Begin(GameObject go, Vector3 dir, float seconds)
        {
            var t = go.GetComponent<TemporarySteerLock>();
            if (!t) t = go.AddComponent<TemporarySteerLock>();
            t.keepDir = dir.normalized;
            t.until = Time.time + Mathf.Max(0.01f, seconds);
            t.rb = go.GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (Time.time >= until) { Destroy(this); return; }
            if (!rb) return;

#if UNITY_6000_0_OR_NEWER
            float spd = rb.linearVelocity.magnitude;
            rb.linearVelocity = keepDir * spd;
#else
            float spd = rb.velocity.magnitude;
            rb.velocity = keepDir * spd;
#endif
            transform.forward = keepDir;
        }
    }
}
