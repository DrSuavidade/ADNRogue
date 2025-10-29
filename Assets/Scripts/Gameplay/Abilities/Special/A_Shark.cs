// Assets/Scripts/Abilities/A_Shark.cs
using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Shark - Fish Proc")]
public class A_SharkFishProc : EssenceAbility
{
    [Header("Fish Proc")]
    [Range(0f, 1f)] public float fishChance = 0.02f;   // 2% chance a bullet is a "fish"
    public float fishScale = 1.2f;

    [Header("Shark Bite")]
    public float telegraphDelay = 1.6f;
    public float biteRadius = 6.0f;        // bigger than Elephant
    public float biteDamageFactor = 6f;    // x fish bullet damage
    public float biteKnockback = 14f;      // stronger knockback

    [Header("Cooldown")]
    public float cooldownSeconds = 30f;    // absolute cooldown

    [Header("Telegraph VFX")]
    public bool showTelegraph = true;
    public Color telegraphColor = new Color(0.1f, 0.6f, 1f, 0.9f);
    public float telegraphWidth = 0.06f;
    public int   telegraphSegments = 64;

    [Header("Breach VFX")]
    public Color breachColor = new Color(0.1f, 0.4f, 0.9f, 0.95f);
    public float breachRiseTime = 0.25f;
    public float breachHoldTime = 0.10f;
    public float breachFallTime = 0.20f;
    public Vector2 breachSize = new Vector2(1.6f, 3.6f); // x = fin width, y = height

    // ---- internal cooldown + runner host ----
    static float s_nextAllowedTime = 0f;
    static SharkRunnerHost s_host;

    static SharkRunnerHost Host
    {
        get
        {
            if (s_host != null) return s_host;
            var go = new GameObject("[SharkRunnerHost]");
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<SharkRunnerHost>();
            return s_host;
        }
    }

    // Tag bullets as "fish" at spawn time
    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        if (!bullet) return;
        if (Random.value > fishChance) return;

        var tag = bullet.gameObject.AddComponent<FishTag>();
        tag.owner = this;
        tag.baseDamage = Mathf.Max(0f, bullet.damage);

        // Small visual cue that this projectile is a fish
        bullet.transform.localScale *= fishScale;

        var tr = bullet.GetComponentInChildren<TrailRenderer>();
        if (tr) { var c = new Color(0.2f, 0.8f, 1f, 1f); tr.startColor = c; tr.endColor = c; }

        var mr = bullet.GetComponentInChildren<Renderer>();
        if (mr && mr.material && mr.material.HasProperty("_Color"))
            mr.material.color = new Color(0.2f, 0.7f, 1f, 1f);
    }

    // When a fish bullet hits an enemy, schedule the shark breach (if cooldown allows)
    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!bullet || !enemy) return;

        var fish = bullet.GetComponent<FishTag>();
        if (fish == null || fish.triggered) return;

        // Absolute cooldown gate
        if (Time.time < s_nextAllowedTime) return;
        s_nextAllowedTime = Time.time + Mathf.Max(0f, cooldownSeconds);

        fish.triggered = true;

        // Lock ground position at the moment of impact
        Vector3 pos = enemy.transform.position;
        Vector3 ground = ProjectToGround(pos);

        // IMPORTANT: run the sequence on a persistent host (not on the bullet),
        // otherwise it dies when the bullet is destroyed. (Bullets manage their
        // own lifetimes/coroutines.)  :contentReference[oaicite:2]{index=2}
        Host.Run(SharkSequence(ground, fish.baseDamage));
    }

    Vector3 ProjectToGround(Vector3 at)
    {
        Vector3 origin = at + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return at;
    }

    IEnumerator SharkSequence(Vector3 atGround, float fishDamage)
    {
        GameObject tele = null;
        if (showTelegraph)
            tele = CreateTelegraph(atGround, biteRadius, telegraphColor, telegraphWidth, telegraphSegments);

        yield return new WaitForSeconds(Mathf.Max(0.01f, telegraphDelay));

        if (tele) Object.Destroy(tele);

        SpawnBreachVFX(atGround);
        ApplyBite(atGround, fishDamage);
    }

    void ApplyBite(Vector3 center, float fishDamage)
    {
        // Damage scales from the original fish-bullet’s damage
        float dmg = Mathf.Max(0f, fishDamage) * Mathf.Max(0f, biteDamageFactor);

        var cols = Physics.OverlapSphere(center, biteRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponent<Enemy>();
            if (!e) continue;

            e.TakeDamage(dmg, false);

            if (biteKnockback > 0f)
            {
                Vector3 dir = (e.transform.position - center); dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                    e.ApplyKnockback(dir.normalized, biteKnockback);
            }
        }
    }

    GameObject CreateTelegraph(Vector3 center, float radius, Color col, float width, int segs)
    {
        var go = new GameObject("Shark_Telegraph");
        go.transform.position = new Vector3(center.x, center.y + 0.02f, center.z);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; lr.loop = true; lr.alignment = LineAlignment.View;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.widthMultiplier = Mathf.Max(0.01f, width);
        lr.startColor = col; lr.endColor = col;
        lr.positionCount = Mathf.Max(16, segs);

        var pts = new Vector3[lr.positionCount];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = (i / (float)pts.Length) * Mathf.PI * 2f;
            pts[i] = new Vector3(center.x + Mathf.Cos(t) * radius, center.y + 0.02f, center.z + Mathf.Sin(t) * radius);
        }
        lr.SetPositions(pts);

        return go;
    }

    void SpawnBreachVFX(Vector3 at)
    {
        var root = new GameObject("Shark_BreachVFX");
        root.transform.position = at;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Fin";
        quad.transform.SetParent(root.transform, false);
        var mr = quad.GetComponent<MeshRenderer>();
        if (mr)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = breachColor;
            mr.material = m;
        }
        var col = quad.GetComponent<Collider>(); if (col) Object.Destroy(col);

        var cam = Camera.main;
        if (cam) root.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);

        quad.transform.localScale = new Vector3(breachSize.x, 0.01f, 1f);
        root.AddComponent<BreachRunner>().Init(quad.transform, breachSize, breachRiseTime, breachHoldTime, breachFallTime);
    }

    // --- helpers & runner types ---
    class FishTag : MonoBehaviour
    {
        public A_SharkFishProc owner;
        public float baseDamage;
        public bool triggered;
    }

    class BreachRunner : MonoBehaviour
    {
        Transform fin;
        Vector2 size;
        float rise, hold, fall;

        public void Init(Transform finTf, Vector2 targetSize, float riseT, float holdT, float fallT)
        {
            fin  = finTf; size = targetSize;
            rise = Mathf.Max(0.01f, riseT);
            hold = Mathf.Max(0f, holdT);
            fall = Mathf.Max(0.01f, fallT);
        }

        IEnumerator Start()
        {
            // Rise
            float t = 0f;
            while (t < rise)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rise);
                if (fin) fin.localScale = new Vector3(size.x, Mathf.Lerp(0.01f, size.y, k), 1f);
                yield return null;
            }

            if (hold > 0f) yield return new WaitForSeconds(hold);

            // Fall
            float t2 = 0f;
            while (t2 < fall)
            {
                t2 += Time.deltaTime;
                float k = Mathf.Clamp01(t2 / fall);
                if (fin) fin.localScale = new Vector3(size.x, Mathf.Lerp(size.y, 0.01f, k), 1f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }

    class SharkRunnerHost : MonoBehaviour
    {
        public void Run(IEnumerator routine) { StartCoroutine(routine); }
    }
}
