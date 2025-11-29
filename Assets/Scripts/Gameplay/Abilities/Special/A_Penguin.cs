using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Penguin - Ice Slug")]
public class PenguinIceSlugAbility : EssenceAbility
{
    [Header("Projectile")]
    [Range(0.1f, 1f)] public float speedMultiplier = 0.6f;

    [Header("On Hit")]
    [Range(0f, 1f)] public float slowPercent = 0.4f;
    public float slowDuration = 2.5f;
    [Range(0f, 1f)] public float freezeChance = 0.05f;
    public float freezeDuration = 2f;

    [Tooltip("After a freeze ends, ignore new freezes for this many seconds.")]
    public float freezeCooldown = 0.75f;

    [Header("VFX: Trail")]
    public bool addTrail = true;
    public float trailTime = 0.25f;
    public float trailWidth = 0.08f;
    public Color trailStartColor = new Color(0.8f, 0.95f, 1f, 0.95f);
    public Color trailEndColor   = new Color(0.6f, 0.85f, 1f,  0.00f);

    [Header("VFX: Hit Ring")]
    public bool showHitRing = true;
    public float hitRingRadius = 0.9f;
    public float hitRingDuration = 0.35f;
    public float hitRingWidth = 0.06f;
    public Color hitRingColor = new Color(0.75f, 0.95f, 1f, 0.9f);

    [Header("VFX: Ice Block on Freeze")]
    public bool showIceBlockOnFreeze = true;

    [Tooltip("Drag a transparent material here (URP: Lit Transparent, BIRP: Standard Transparent). If left empty, a fallback will be created at runtime.")]
    public Material iceMaterial;

    public Vector3 iceBlockScale = new Vector3(1.0f, 1.6f, 1.0f);
    public Color iceBlockColor = new Color(0.6f, 0.9f, 1f, 0.35f);

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        var rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity *= speedMultiplier;
#else
            rb.velocity *= speedMultiplier;
#endif
        }

        if (addTrail)
        {
            var tr = bullet.gameObject.AddComponent<TrailRenderer>();
            tr.time = trailTime;
            tr.widthMultiplier = trailWidth;
            tr.minVertexDistance = 0.02f;
            tr.material = new Material(Shader.Find("Sprites/Default"));

            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(trailStartColor, 0f),
                    new GradientColorKey(trailEndColor,   1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(trailStartColor.a, 0f),
                    new GradientAlphaKey(trailEndColor.a,   1f)
                }
            );
            tr.colorGradient = grad;
        }
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy) return;

        if (showHitRing) SpawnHitRing(enemy.transform, hitRingRadius, hitRingWidth, hitRingColor, hitRingDuration);

        // Optional slow if the enemy uses a NavMeshAgent
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) enemy.StartCoroutine(ApplySlow(agent, slowPercent, slowDuration));

        // Freeze (with cooldown + no refresh while active)
        if (Random.value < Mathf.Clamp01(freezeChance))
        {
            var st = enemy.GetComponent<FreezeStatus>();
            if (!st) st = enemy.gameObject.AddComponent<FreezeStatus>();
            st.Begin(this, enemy, freezeDuration, freezeCooldown);
        }
    }

    IEnumerator ApplySlow(NavMeshAgent agent, float pct, float dur)
    {
        if (agent == null) yield break;
        float original = agent.speed;
        agent.speed = Mathf.Max(0f, original * (1f - Mathf.Clamp01(pct)));
        yield return new WaitForSeconds(dur);
        if (agent != null) agent.speed = original;
    }

    // --- Runtime freeze status attached to enemies ---
    public class FreezeStatus : MonoBehaviour
    {
        PenguinIceSlugAbility def;
        Enemy enemy;

        // State
        bool active;
        float cooldownUntil;

        // Agent freeze
        NavMeshAgent agent;
        bool agentPrevStopped;

        // Rigidbody freeze
        Rigidbody rb;
        RigidbodyConstraints rbPrevConstraints;
        bool rbPrevKinematic;
        Vector3 rbPrevVel, rbPrevAngVel;

        // AI freeze (disable a behaviour named "EnemyAI" if present)
        Behaviour ai;
        bool aiPrevEnabled;

        // Animator optional
        Animator anim;
        float animPrevSpeed;

        // VFX
        GameObject ice;

        public void Begin(PenguinIceSlugAbility ability, Enemy e, float duration, float cooldown)
        {
            def = ability; enemy = e;

            if (Time.time < cooldownUntil) return; // still on cooldown
            if (active) return;                    // already frozen -> ignore

            StartCoroutine(DoFreeze(duration, cooldown));
        }

        IEnumerator DoFreeze(float dur, float cooldown)
        {
            active = true;

            // Components
            agent = GetComponent<NavMeshAgent>();
            rb    = GetComponent<Rigidbody>();
            anim  = GetComponentInChildren<Animator>();
            ai    = GetComponent("EnemyAI") as Behaviour;

            // AI off
            if (ai != null) { aiPrevEnabled = ai.enabled; ai.enabled = false; }

            // Agent stop
            if (agent != null) { agentPrevStopped = agent.isStopped; agent.isStopped = true; }

            // Rigidbody lock
            if (rb != null)
            {
                rbPrevKinematic   = rb.isKinematic;
                rbPrevConstraints = rb.constraints;
                rbPrevVel         = rb.linearVelocity;
                rbPrevAngVel      = rb.angularVelocity;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            // Animator pause
            if (anim != null) { animPrevSpeed = anim.speed; anim.speed = 0f; }

            // Ice VFX
            if (def.showIceBlockOnFreeze && enemy != null)
                ice = CreateIceBlock(def, enemy.transform, def.iceBlockScale, def.iceBlockColor);

            yield return new WaitForSeconds(dur);

            // End freeze
            if (agent != null) agent.isStopped = agentPrevStopped;
            if (rb != null)
            {
                rb.isKinematic  = rbPrevKinematic;
                rb.constraints  = rbPrevConstraints;
                rb.linearVelocity     = rbPrevVel;
                rb.angularVelocity = rbPrevAngVel;
            }
            if (ai != null) ai.enabled = aiPrevEnabled;
            if (anim != null) anim.speed = animPrevSpeed;
            if (ice) Destroy(ice);

            active = false;
            cooldownUntil = Time.time + Mathf.Max(0f, cooldown);
        }
    }

    // --- VFX helpers ---
    static void SpawnHitRing(Transform enemy, float radius, float width, Color color, float life)
    {
        var go = new GameObject("Penguin_HitRing_VFX");
        go.transform.SetParent(enemy, false);
        go.transform.localPosition = Vector3.zero; // at feet
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 48;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;

        Vector3[] pts = new Vector3[lr.positionCount];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = (i / (float)pts.Length) * Mathf.PI * 2f;
            pts[i] = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
        }
        lr.SetPositions(pts);

        go.AddComponent<FadeAndDie>().Init(lr, color, Mathf.Max(0.05f, life));
    }

    static GameObject CreateIceBlock(PenguinIceSlugAbility def, Transform enemy, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Penguin_IceBlock_VFX";
        Object.Destroy(go.GetComponent<Collider>()); // visual only
        go.transform.SetParent(enemy, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;

        var r = go.GetComponent<Renderer>();

        // Use provided material if set, else try to create a sensible fallback
        Material mat = def.iceMaterial;
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
        }

        // Set color on common properties (_BaseColor for URP/Lit, _Color for Standard)
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.color = color;

        if (r) r.material = mat;

        return go;
    }

    class FadeAndDie : MonoBehaviour
    {
        LineRenderer lr;
        Color baseCol;
        float life;
        float t;

        public void Init(LineRenderer _lr, Color c, float _life)
        {
            lr = _lr; baseCol = c; life = _life;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / life);
            var c = baseCol; c.a *= k;
            if (lr) { lr.startColor = c; lr.endColor = c; }
            if (t >= life) Destroy(gameObject);
        }
    }
    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Penguin/slowPercent":
                    slowPercent = Mathf.Clamp01(ApplyNumeric(slowPercent, u));
                    break;

                case "Penguin/SlowDuration":
                    slowDuration = Mathf.Max(0.1f, ApplyNumeric(slowDuration, u));
                    break;

                case "Penguin/freezeDuration":
                    freezeDuration = Mathf.Max(0.1f, ApplyNumeric(freezeDuration, u));
                    break;

                case "Penguin/freezeCooldown":
                    freezeCooldown = Mathf.Max(0.1f, ApplyNumeric(freezeCooldown, u));
                    break;
            }
        }
    }

}
