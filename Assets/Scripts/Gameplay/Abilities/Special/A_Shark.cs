using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Shark - Feeding Frenzy")]
public class A_SharkFeedingFrenzy : EssenceAbility
{
    [Header("Rage")]
    [Range(0f,1f)] public float rageChance = 0.05f;
    public float rageDuration = 5f;
    public float rageSpeedMult = 1.75f;        // agent speed multiplier while raging
    public float retargetEvery = 0.5f;         // choose a new random point this often
    public float roamRadius = 6f;              // random roam radius around current pos

    [Header("Shark Bite AoE")]
    public float biteDelay = 0.15f;            // small anticipation
    public float biteRadius = 4.5f;
    public float biteDamageFactor = 1.35f;     // x bullet damage
    public float biteKnockback = 8f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (enemy == null) return;
        if (Random.value > rageChance) return;

        // Attach/enforce unique status per enemy
        var s = enemy.GetComponent<SailorsRageStatus>();
        if (!s) s = enemy.gameObject.AddComponent<SailorsRageStatus>();
        s.Begin(this, enemy, stats.damage); // pass damage so bite scales
    }

    // --- Runtime status attached to enemies ---
    // Destroys itself when duration ends. If destroyed because the enemy died while active,
    // it triggers the shark AoE.
    public class SailorsRageStatus : MonoBehaviour
    {
        A_SharkFeedingFrenzy def;
        Enemy enemy;
        float baseAgentSpeed;
        NavMeshAgent agent;

        float endAt;
        bool expiredNaturally;
        float storedBulletDamage;

        Coroutine roamCoro;

        public void Begin(A_SharkFeedingFrenzy d, Enemy e, float dmg)
        {
            def = d; enemy = e; storedBulletDamage = dmg;

            agent = e.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                baseAgentSpeed = agent.speed;
                agent.speed = baseAgentSpeed * def.rageSpeedMult;
            }

            endAt = Time.time + def.rageDuration;
            if (roamCoro != null) StopCoroutine(roamCoro);
            roamCoro = StartCoroutine(RoamLoop());
        }

        IEnumerator RoamLoop()
        {
            var wait = new WaitForSeconds(def.retargetEvery);
            while (Time.time < endAt && enemy != null)
            {
                // choose a random point nearby
                Vector3 center = enemy.transform.position;
                Vector2 r2 = Random.insideUnitCircle * def.roamRadius;
                Vector3 dest = center + new Vector3(r2.x, 0f, r2.y);

                if (agent != null)
                {
                    if (agent.isOnNavMesh) agent.SetDestination(dest);
                }
                else
                {
                    // fallback: simple move
                    var rb = enemy.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 dir = (dest - enemy.transform.position).normalized;
                        rb.AddForce(dir * 5f, ForceMode.Acceleration);
                    }
                    else
                    {
                        enemy.transform.position = Vector3.MoveTowards(enemy.transform.position, dest, 3f * def.retargetEvery);
                    }
                }
                yield return wait;
            }

            // natural expiry
            expiredNaturally = true;
            Destroy(this);
        }

        void OnDestroy()
        {
            // Restore agent speed
            if (agent != null) agent.speed = baseAgentSpeed;

            // If we expired naturally -> no shark.
            if (expiredNaturally) return;

            // If enemy object was destroyed WHILE status still active -> interpret as death.
            Vector3 pos = transform.position;
            if (def != null) StartCoroutine(DoSharkBite(pos));
        }

        IEnumerator DoSharkBite(Vector3 at)
        {
            yield return new WaitForSeconds(def.biteDelay);

            float dmg = storedBulletDamage * def.biteDamageFactor;
            var cols = Physics.OverlapSphere(at, def.biteRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var e = cols[i].GetComponent<Enemy>();
                if (e == null) continue;
                e.TakeDamage(dmg, false);

                if (def.biteKnockback > 0f)
                {
                    Vector3 dir = (e.transform.position - at); dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        e.ApplyKnockback(dir.normalized, def.biteKnockback);
                }
            }
            // (Optional) spawn VFX here
        }
    }
}
