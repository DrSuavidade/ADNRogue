using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Bee - Honeycomb")]
public class A_BeeHoneycomb : EssenceAbility
{
    [Header("Sticky Stacks")]
    public float slowPerStack = 0.07f; // 7% per stack
    public int   maxStacks = 6;
    public float stackDuration = 4f;

    [Header("Root on Cap")]
    public float rootDuration = 1.25f;

    [Header("Puddle")]
    public float puddleRadius = 3f;
    public float puddleDuration = 4f;
    public float puddleSlow = 0.35f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        var st = enemy.GetComponent<StickyStatus>();
        if (!st) st = enemy.gameObject.AddComponent<StickyStatus>();
        st.Apply(this, enemy);
    }

    // --- Status on enemies ---
    public class StickyStatus : MonoBehaviour
    {
        A_BeeHoneycomb def;
        Enemy enemy;
        NavMeshAgent agent;

        int stacks = 0;
        float[] expiry; // ring buffer per stack

        bool rooted; float rootEnd;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void Apply(A_BeeHoneycomb d, Enemy e)
        {
            def = d; enemy = e;
            if (expiry == null || expiry.Length != def.maxStacks) expiry = new float[def.maxStacks];

            // add/refresh a stack
            if (stacks < def.maxStacks) stacks++;
            // put/refresh the newest stack expiry
            expiry[stacks - 1] = Time.time + def.stackDuration;

            RecomputeSlow();

            // reached cap -> root + puddle
            if (stacks >= def.maxStacks)
            {
                StartCoroutine(RootCoroutine(def.rootDuration));
                SpawnPuddle();
                // optionally consume stacks or keep them; here we keep but timers will drop soon
            }
        }

        void Update()
        {
            // expire stacks
            int before = stacks;
            float now = Time.time;
            for (int i = stacks - 1; i >= 0; i--)
            {
                if (expiry[i] <= now)
                {
                    // shift down
                    for (int j = i; j < stacks - 1; j++) expiry[j] = expiry[j + 1];
                    stacks--;
                }
            }
            if (stacks != before) RecomputeSlow();

            // end root
            if (rooted && Time.time >= rootEnd) Unroot();
        }

        void RecomputeSlow()
        {
            if (!agent) return;
            float totalSlow = Mathf.Clamp01(stacks * def.slowPerStack);
            float baseSpeed = agent.speed / Mathf.Max(0.01f, 1f - totalSlow); // deduce base if already slowed
            agent.speed = baseSpeed * (1f - totalSlow);
        }

        IEnumerator RootCoroutine(float dur)
        {
            Root();
            yield return new WaitForSeconds(dur);
            Unroot();
        }

        void Root()
        {
            if (agent) agent.isStopped = true;
            rooted = true; rootEnd = Time.time + def.rootDuration;
        }

        void Unroot()
        {
            rooted = false;
            if (agent) agent.isStopped = false;
        }

        void SpawnPuddle()
        {
            var go = new GameObject("HoneyPuddle");
            go.transform.position = transform.position;
            var rt = go.AddComponent<HoneyPuddleRuntime>();
            rt.radius = def.puddleRadius;
            rt.slow = def.puddleSlow;
            rt.duration = def.puddleDuration;
        }

        void OnDestroy()
        {
            // restore agent if needed
            if (agent) agent.isStopped = false;
        }
    }

    // --- Simple slowing puddle ---
    public class HoneyPuddleRuntime : MonoBehaviour
    {
        public float radius = 3f;
        public float slow = 0.35f;
        public float duration = 4f;

        SphereCollider col;
        void Awake()
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;

            // simple visual (optional): flat disc
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

        void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<NavMeshAgent>();
            if (!agent) return;
            StartCoroutine(ApplySlow(agent));
        }

        IEnumerator ApplySlow(NavMeshAgent agent)
        {
            if (!agent) yield break;
            float baseSpeed = agent.speed;
            agent.speed = baseSpeed * (1f - Mathf.Clamp01(slow));
            yield return new WaitForSeconds(duration);
            if (agent) agent.speed = baseSpeed;
        }
    }
}
