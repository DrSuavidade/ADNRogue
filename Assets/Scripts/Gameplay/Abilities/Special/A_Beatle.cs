using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Beetle - Dung Roller")]
public class A_BeetleDungRoller : EssenceAbility
{
    [Header("Growth")]
    public float growthPerMeter = 0.15f;   // +15% size per meter
    public float maxSizeMult = 2.5f;       // cap
    public float damagePerSizeMult = 0.6f; // extra damage = (sizeMult-1)*this * baseDamage

    [Header("Burst (optional)")]
    public bool burstOnBigHit = true;
    public float burstThreshold = 2.0f;    // trigger if size >= this
    public float burstRadius = 3.5f;
    public float burstSlowPercent = 0.4f;
    public float burstDuration = 2.0f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        var rt = bullet.gameObject.AddComponent<DungRuntime>();
        rt.baseDamage = stats.damage;
        rt.growthPerMeter = growthPerMeter;
        rt.maxSizeMult = maxSizeMult;
        rt.damagePerSizeMult = damagePerSizeMult;
        rt.burstOnBigHit = burstOnBigHit;
        rt.burstThreshold = burstThreshold;
        rt.burstRadius = burstRadius;
        rt.burstSlowPercent = burstSlowPercent;
        rt.burstDuration = burstDuration;
    }

    class DungRuntime : MonoBehaviour
    {
        public float baseDamage, growthPerMeter, maxSizeMult, damagePerSizeMult;
        public bool burstOnBigHit; public float burstThreshold, burstRadius, burstSlowPercent, burstDuration;

        Bullet b; Vector3 lastPos; float dist; float sizeMult = 1f;

        void Awake() { b = GetComponent<Bullet>(); lastPos = transform.position; }

        void Update()
        {
            Vector3 p = transform.position;
            dist += (p - lastPos).magnitude;
            lastPos = p;

            float targetSize = Mathf.Clamp(1f + dist * growthPerMeter, 1f, maxSizeMult);
            if (!Mathf.Approximately(targetSize, sizeMult))
            {
                sizeMult = targetSize;
                transform.localScale = Vector3.one * sizeMult;
                if (b) b.damage = baseDamage * (1f + (sizeMult - 1f) * damagePerSizeMult);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (!burstOnBigHit || sizeMult < burstThreshold) return;

            Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            var cols = Physics.OverlapSphere(at, burstRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var e = cols[i].GetComponent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
                if (!e) continue;
                // simple slow via NavMeshAgent if present
                var agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent) StartCoroutine(Slow(agent, burstSlowPercent, burstDuration));
            }
        }

        System.Collections.IEnumerator Slow(UnityEngine.AI.NavMeshAgent agent, float pct, float dur)
        {
            if (!agent) yield break;
            float baseSpd = agent.speed;
            agent.speed = Mathf.Max(0f, baseSpd * (1f - Mathf.Clamp01(pct)));
            yield return new WaitForSeconds(dur);
            if (agent) agent.speed = baseSpd;
        }
    }
}
