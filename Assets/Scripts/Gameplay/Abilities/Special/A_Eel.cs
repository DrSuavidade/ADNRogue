using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Eel - Chain Lightning")]
public class ChainLightningAbility : EssenceAbility
{
    [Header("Chain")]
    public int   maxJumps = 3;
    public float radius   = 6f;
    [Tooltip("Seconds of visible travel between enemies.")]
    public float jumpDelay = 0.5f;
    [Range(0f, 1f)] public float damageFactorPerJump = 0.7f;

    [Header("VFX")]
    public bool  showVFX     = true;
    public float lineWidth   = 0.06f;
    public Color lineColor   = new Color(0.7f, 0.9f, 1f, 0.95f);

    public override void OnHitEnemy(Bullet bullet, Enemy first, WeaponStats stats)
    {
        if (first != null) first.StartCoroutine(ChainRoutine(first, stats));
    }

    IEnumerator ChainRoutine(Enemy start, WeaponStats stats)
    {
        Transform current = start.transform;
        var visited = new System.Collections.Generic.HashSet<Enemy>();
        if (start != null) visited.Add(start);

        float baseDamage = stats.damage;

        for (int i = 0; i < maxJumps; i++)
        {
            // Find nearest new enemy within radius
            Collider[] hits = Physics.OverlapSphere(current.position, radius, ~0, QueryTriggerInteraction.Ignore);
            Enemy next = null; float best = Mathf.Infinity;
            for (int h = 0; h < hits.Length; h++)
            {
                var e = hits[h].GetComponent<Enemy>();
                if (e == null || visited.Contains(e)) continue;
                float d = (e.transform.position - current.position).sqrMagnitude;
                if (d < best) { best = d; next = e; }
            }
            if (next == null) yield break;

            // VFX: stretch a line from current to next over 'jumpDelay'
            if (showVFX)
                StretchLine(current, next.transform, jumpDelay, lineWidth, lineColor, current.gameObject.layer);

            // Wait for the travel time, then apply damage to the next enemy
            yield return new WaitForSeconds(jumpDelay);

            if (next != null)
            {
                float dmgThisHop = baseDamage * damageFactorPerJump;
                next.TakeDamage(dmgThisHop, false);
                baseDamage = dmgThisHop;   // subsequent hops decay further
                visited.Add(next);
                current = next.transform;  // chain continues from the new enemy
            }
            else
            {
                yield break; // target vanished mid-flight
            }
        }
    }

    // --- Simple stretch-line VFX (2-point LineRenderer that grows toward the target) ---
    void StretchLine(Transform from, Transform to, float duration, float width, Color color, int layer)
    {
        if (from == null || to == null) return;

        var go = new GameObject("Eel_ChainLightning_VFX");
        go.layer = layer;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor   = color;

        // Runner that animates then self-destroys
        var runner = go.AddComponent<StretchLineRunner>();
        runner.Init(from, to, duration, lr);
    }

    class StretchLineRunner : MonoBehaviour
    {
        Transform from, to;
        float duration, t;
        LineRenderer lr;

        public void Init(Transform f, Transform tt, float dur, LineRenderer line)
        {
            from = f; to = tt; duration = Mathf.Max(0.01f, dur); lr = line;
        }

        void Update()
        {
            if (from == null || to == null || lr == null) { Destroy(gameObject); return; }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            Vector3 a = from.position;
            Vector3 b = to.position;

            // start anchored at 'from'; end grows toward 'to'
            lr.SetPosition(0, a);
            lr.SetPosition(1, Vector3.Lerp(a, b, k));

            // Optional slight fade-in/out (looks nice with short durations)
            var c0 = lr.startColor; var c1 = lr.endColor;
            float fade = 1f; // keep solid; change to Mathf.Sin(k * Mathf.PI) for a pulse
            c0.a = c0.a * fade; c1.a = c1.a * fade;
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
                case "Chain/MaxJumps":
                {
                    float v = ApplyNumeric(maxJumps, u);
                    maxJumps = Mathf.Clamp(Mathf.RoundToInt(v), 0, 32);
                    break;
                }

                case "Chain/Radius":
                    radius = Mathf.Max(0f, ApplyNumeric(radius, u));
                    break;

                case "Chain/JumpDelay":
                    jumpDelay = Mathf.Max(0f, ApplyNumeric(jumpDelay, u));
                    break;

                case "Chain/DamageFactorPerJump":
                    damageFactorPerJump = Mathf.Clamp01(ApplyNumeric(damageFactorPerJump, u));
                    break;
            }
        }
    }

}
