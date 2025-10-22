using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Chain Lightning")]
public class ChainLightningAbility : EssenceAbility
{
    [Header("Chain")]
    public int maxJumps = 3;
    public float radius = 6f;
    public float jumpDelay = 0.06f;
    [Range(0f, 1f)] public float damageFactorPerJump = 0.7f;
    
    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Chain/MaxJumps":
                    if (u.kind == ModifierKind.Add) maxJumps += Mathf.RoundToInt(u.value);
                    else maxJumps = Mathf.RoundToInt(maxJumps * u.value);
                    break;

                case "Chain/Radius":
                    if (u.kind == ModifierKind.Add) radius += u.value;
                    else radius *= Mathf.Max(0f, u.value);
                    break;

                case "Chain/JumpDelay":
                    if (u.kind == ModifierKind.Add) jumpDelay = Mathf.Max(0f, jumpDelay + u.value);
                    else jumpDelay = Mathf.Max(0f, jumpDelay * u.value);
                    break;

                case "Chain/DamageFactorPerJump":
                    if (u.kind == ModifierKind.Add) damageFactorPerJump = Mathf.Clamp01(damageFactorPerJump + u.value);
                    else damageFactorPerJump = Mathf.Clamp01(damageFactorPerJump * u.value);
                    break;
            }
        }
    }

    public override void OnHitEnemy(Bullet bullet, Enemy first, WeaponStats stats)
    {
        bullet.StartCoroutine(ChainRoutine(first, stats));
    }

    IEnumerator ChainRoutine(Enemy start, WeaponStats stats)
    {
        var current = start.transform;
        var visited = new System.Collections.Generic.HashSet<Enemy>();
        visited.Add(start);

        float dmg = stats.damage;
        for (int i = 0; i < maxJumps; i++)
        {
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

            visited.Add(next);
            next.TakeDamage(dmg * damageFactorPerJump, false);
            dmg *= damageFactorPerJump;
            current = next.transform;
            yield return new WaitForSeconds(jumpDelay);
        }
    }
}
