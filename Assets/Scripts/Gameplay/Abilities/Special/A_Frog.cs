using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Frog - Toxicity")]
public class A_FrogToxicity : EssenceAbility
{
    [Header("Poison")]
    public float poisonDps = 1.0f;
    public float poisonDuration = 4f;
    public int   maxStacks = 8;

    [Header("Death puddle")]
    [Range(0f,1f)] public float puddleChance = 0.05f;
    public float puddleRadius = 3.5f;
    public float puddleDuration = 4f;
    public float puddleDps = 0.8f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        var p = enemy.GetComponent<PoisonStatus>();
        if (!p) p = enemy.gameObject.AddComponent<PoisonStatus>();
        p.Apply(this);
    }

    // --- Poison state on enemies ---
    public class PoisonStatus : MonoBehaviour
    {
        A_FrogToxicity def;
        int stacks;
        float expireAt;
        bool ticking;
        bool expiredNaturally;

        public void Apply(A_FrogToxicity d)
        {
            def = d;
            stacks = Mathf.Min(def.maxStacks, stacks + 1);
            expireAt = Time.time + def.poisonDuration;

            if (!ticking) StartCoroutine(Tick());
        }

        IEnumerator Tick()
        {
            ticking = true;
            while (Time.time < expireAt)
            {
                var e = GetComponent<Enemy>();
                if (e) e.TakeDamage((def.poisonDps * stacks) * 0.5f, false); // 0.5s tick
                yield return new WaitForSeconds(0.5f);
            }
            expiredNaturally = true;
            Destroy(this);
        }

        void OnDestroy()
        {
            if (expiredNaturally) return; // ended by expiry, not death
            // Enemy object destroyed while poison active -> death occurred
            if (Random.value <= def.puddleChance)
            {
                SpawnPuddle(transform.position);
            }
        }

        void SpawnPuddle(Vector3 at)
        {
            var go = new GameObject("ToxicPuddle");
            go.transform.position = at;
            var rt = go.AddComponent<ToxicPuddleRuntime>();
            rt.radius = def.puddleRadius;
            rt.duration = def.puddleDuration;
            rt.dps = def.puddleDps;
        }
    }

    // --- Puddle poisons any enemy inside ---
    public class ToxicPuddleRuntime : MonoBehaviour
    {
        public float radius = 3f;
        public float duration = 4f;
        public float dps = 0.8f;

        SphereCollider col;

        void Awake()
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;

            // optional ring visual
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.widthMultiplier = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Vector3[] pts = new Vector3[lr.positionCount];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            }
            lr.SetPositions(pts);

            Destroy(gameObject, duration);
        }

        void OnTriggerStay(Collider other)
        {
            var e = other.GetComponent<Enemy>();
            if (!e) return;
            e.TakeDamage(dps * Time.deltaTime, false);
        }
    }
}
