// not implemented

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
    [Range(0f, 1f)] public float fishChance = 0.02f;   // 2% chance to be a fish
    public float fishScale = 1.2f;

    [Header("Shark Bite")]
    public float telegraphDelay = 2.0f;
    public float biteRadius = 4.5f;
    public float biteDamageFactor = 5f;     // 5x fish bullet damage
    public float biteKnockback = 12f;

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
    public Vector2 breachSize = new Vector2(1.2f, 3.2f); // x = fin width, y = height

    // Mark bullets as "fish" at spawn time
    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        if (!bullet) return;
        if (Random.value > fishChance) return;

        // Tag this bullet as a fish
        var tag = bullet.gameObject.AddComponent<FishTag>();
        tag.owner = this;
        tag.baseDamage = Mathf.Max(0f, bullet.damage);

        // Light visual cue (optional): scale a bit & tint trail/renderer if present
        bullet.transform.localScale *= fishScale;

        var tr = bullet.GetComponentInChildren<TrailRenderer>();
        if (tr) { var c = new Color(0.2f, 0.8f, 1f, 1f); tr.startColor = c; tr.endColor = c; }

        var mr = bullet.GetComponentInChildren<Renderer>();
        if (mr && mr.material && mr.material.HasProperty("_Color"))
            mr.material.color = new Color(0.2f, 0.7f, 1f, 1f);
    }

    // When a fish bullet hits any enemy, schedule the shark breach
    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!bullet || !enemy) return;
        var fish = bullet.GetComponent<FishTag>();
        if (fish == null || fish.triggered) return;

        fish.triggered = true;

        // Lock-in ground position at impact
        Vector3 pos = enemy.transform.position;
        Vector3 ground = ProjectToGround(pos);

        bullet.StartCoroutine(SharkSequence(ground, fish.baseDamage));
    }

    Vector3 ProjectToGround(Vector3 at)
    {
        // Try raycast to ground; otherwise, keep y as-is
        Vector3 origin = at + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return at;
    }

    IEnumerator SharkSequence(Vector3 atGround, float fishDamage)
    {
        // Telegraph
        GameObject tele = null;
        if (showTelegraph)
            tele = CreateTelegraph(atGround, biteRadius, telegraphColor, telegraphWidth, telegraphSegments);

        // Wait the delay
        yield return new WaitForSeconds(Mathf.Max(0.01f, telegraphDelay));

        // Cleanup telegraph
        if (tele) Object.Destroy(tele);

        // Do the breach VFX + damage/knockback
        SpawnBreachVFX(atGround);
        ApplyBite(atGround, fishDamage);
    }

    void ApplyBite(Vector3 center, float fishDamage)
    {
        float dmg = Mathf.Max(0f, fishDamage) * Mathf.Max(0f, biteDamageFactor);

        var hits = Physics.OverlapSphere(center, biteRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var e = hits[i].GetComponent<Enemy>();
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
        lr.positionCount = Mathf.Max(8, segs);

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
        // Simple “fin” breach: a vertical quad rising and falling
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

        // Face camera for readability
        var cam = Camera.main;
        if (cam) root.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);

        quad.transform.localScale = new Vector3(breachSize.x, 0.01f, 1f); // start flat
        root.AddComponent<BreachRunner>().Init(quad.transform, breachSize, breachRiseTime, breachHoldTime, breachFallTime);
    }

    // --- helpers ---
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

            // Hold
            if (hold > 0f) yield return new WaitForSeconds(hold);

            // Fall
            t = 0f;
            while (t < fall)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                if (fin) fin.localScale = new Vector3(size.x, Mathf.Lerp(size.y, 0.01f, k), 1f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
