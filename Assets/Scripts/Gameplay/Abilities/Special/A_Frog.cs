using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Frog - Toxicity")]
public class A_FrogToxicity : EssenceAbility
{
    [Header("Poison")]
    public float poisonDps = 1.0f;     // DPS applied while poisoned
    public float poisonDuration = 4f;  // refreshed on each hit

    [Header("Death puddle")]
    [Range(0f,1f)] public float puddleChance = 0.05f;
    public float puddleRadius   = 3.5f;
    public float puddleDuration = 4f;
    public float puddleDps      = 0.8f;

    [Header("VFX")]
    public Color poisonFlashColor    = new Color(0f, 0.85f, 0f, 1f); // green
    public float poisonFlashDuration = 0.05f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy) return;

        // Apply or refresh poison (no stacks)
        var p = enemy.GetComponent<PoisonStatus>();
        if (!p) p = enemy.gameObject.AddComponent<PoisonStatus>();
        p.Apply(this);
    }

    // ---------------- Poison state on enemies (no stacks, refresh on hit) ----------------
    public class PoisonStatus : MonoBehaviour
    {
        A_FrogToxicity def;
        float expireAt;
        bool ticking;
        bool expiredNaturally;

        public void Apply(A_FrogToxicity d)
        {
            def = d;
            expireAt = Time.time + def.poisonDuration;

            if (!ticking) StartCoroutine(Tick());
        }

        IEnumerator Tick()
        {
            ticking = true;
            const float tickInterval = 0.5f;
            while (Time.time < expireAt)
            {
                var e = GetComponent<Enemy>();
                if (e)
                {
                    // Damage tick (DPS scaled by tick length)
                    e.TakeDamage(def.poisonDps * tickInterval, false);

                    // Green flash VFX (distinct from Tiger's red)
                    var flash = e.GetComponent<PoisonFlash>();
                    if (!flash) flash = e.gameObject.AddComponent<PoisonFlash>();
                    flash.Trigger(def.poisonFlashDuration, def.poisonFlashColor);
                }

                yield return new WaitForSeconds(tickInterval);
            }
            expiredNaturally = true;
            ticking = false;
            Destroy(this); // ends poison & restores tint via PoisonFlash lifetime
        }

        void OnDestroy()
        {
            if (expiredNaturally) return; // ended due to expiry, not death
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
            rt.radius   = def.puddleRadius;
            rt.duration = def.puddleDuration;
            rt.dps      = def.puddleDps;
        }
    }

    // ---------------- Toxic puddle poisons any enemy inside (flat DPS over time) ----------------
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

            // Simple green ring visual
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true;
            lr.positionCount = 48; lr.widthMultiplier = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            var ringColor = new Color(0f, 0.9f, 0.2f, 0.85f);
            lr.startColor = ringColor; lr.endColor = ringColor;

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

    // ---------------- Tiny helper: flashes renderers green briefly, then restores ----------------
    class PoisonFlash : MonoBehaviour
    {
        static readonly int _ColorID     = Shader.PropertyToID("_Color");
        static readonly int _BaseColorID = Shader.PropertyToID("_BaseColor");

        Coroutine co;

        public void Trigger(float duration, Color flashColor)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Flash(duration, flashColor));
        }

        IEnumerator Flash(float duration, Color flashColor)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) yield break;

            // Snapshot current colors per material, restore after
            var originals = new System.Collections.Generic.List<(Material mat, Color col, int prop)>();
            foreach (var r in rends)
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m) continue;
                    int prop = m.HasProperty(_BaseColorID) ? _BaseColorID :
                               (m.HasProperty(_ColorID) ? _ColorID : -1);
                    if (prop < 0) continue;

                    originals.Add((m, m.GetColor(prop), prop));
                    m.SetColor(prop, flashColor);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

            foreach (var tuple in originals)
                if (tuple.mat) tuple.mat.SetColor(tuple.prop, tuple.col);

            co = null;
        }

        void OnDisable() { if (co != null) { StopCoroutine(co); co = null; } }
        void OnDestroy() { if (co != null) { StopCoroutine(co); co = null; } }
    }
}
