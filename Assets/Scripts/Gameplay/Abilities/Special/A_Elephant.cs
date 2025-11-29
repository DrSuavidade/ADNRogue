using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Elephant - Stampede Shot")]
public class A_ElephantStampede : EssenceAbility
{
    [Header("Projectile")]
    public float sizeMult = 1.8f;

    [Header("Impact AoE")]
    public float aoeRadius = 4.5f;
    public float aoeDamageFactor = 0.8f;   // x bullet damage to others
    public float knockback = 10f;

    [Header("VFX: AoE Ring")]
    public bool  showAoeRing   = true;
    public float ringDuration  = 0.35f;
    public float ringWidth     = 0.08f;
    public int   ringSegments  = 64;
    public Color ringColor     = new Color(1f, 0.85f, 0.2f, 0.9f); // warm stomp color

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        bullet.transform.localScale *= sizeMult;
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        Vector3 center = enemy ? enemy.transform.position : bullet.transform.position;

        // VFX ring (parallel to ground, short-lived)
        if (showAoeRing)
        {
            int layer = enemy ? enemy.gameObject.layer : bullet.gameObject.layer;
            SpawnRing(center, aoeRadius, ringDuration, ringWidth, ringSegments, ringColor, layer);
        }

        // Gameplay AoE
        var cols = Physics.OverlapSphere(center, aoeRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponent<Enemy>();
            if (!e || e == enemy) continue;

            e.TakeDamage(stats.damage * aoeDamageFactor, false);

            if (knockback > 0f)
            {
                Vector3 dir = (e.transform.position - center); dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) e.ApplyKnockback(dir.normalized, knockback);
            }
        }
    }

    // ---------- Simple expanding ring ----------
    static void SpawnRing(Vector3 center, float targetRadius, float life, float width, int segments, Color color, int layer)
    {
        var go = new GameObject("Elephant_AoE_Ring");
        go.layer = layer;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Mathf.Max(16, segments);
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor   = color;

        go.AddComponent<RingExpander>().Init(center, targetRadius, life, lr);
    }

    class RingExpander : MonoBehaviour
    {
        Vector3 center;
        float targetR, life, t;
        LineRenderer lr;

        public void Init(Vector3 c, float r, float l, LineRenderer line)
        {
            center = c;
            targetR = Mathf.Max(0.01f, r);
            life = Mathf.Max(0.05f, l);
            lr = line;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            float r = Mathf.Lerp(0f, targetR, k);

            // keep ring parallel to ground at the impact height
            int n = lr.positionCount;
            float y = center.y + 0.02f; // slight lift to avoid Z-fighting
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, y, center.z + Mathf.Sin(a) * r));
            }

            // fade out
            var c0 = lr.startColor;
            var c1 = lr.endColor;
            float alpha = (1f - k) * c0.a;
            c0.a = alpha; c1.a = alpha;
            lr.startColor = c0; lr.endColor = c1;

            if (k >= 1f) Destroy(gameObject);
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
                case "Elephant/SizeMult":
                    sizeMult = Mathf.Max(0.1f, ApplyNumeric(sizeMult, u));
                    break;

                case "Elephant/AoeRadius":
                    aoeRadius = Mathf.Max(0f, ApplyNumeric(aoeRadius, u));
                    break;

                case "Elephant/AoeDamageFactor":
                    aoeDamageFactor = Mathf.Max(0f, ApplyNumeric(aoeDamageFactor, u));
                    break;

                case "Elephant/Knockback":
                    knockback = Mathf.Max(0f, ApplyNumeric(knockback, u));
                    break;

            }
        }
    }

}
