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
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) bullet.StartCoroutine(ApplySlow(agent, slowPercent, slowDuration));
        if (Random.value < freezeChance && agent != null) bullet.StartCoroutine(ApplyFreeze(agent, freezeDuration));
    }

    IEnumerator ApplySlow(NavMeshAgent agent, float pct, float dur)
    {
        if (agent == null) yield break;
        float original = agent.speed;
        agent.speed = Mathf.Max(0f, original * (1f - Mathf.Clamp01(pct)));
        yield return new WaitForSeconds(dur);
        if (agent != null) agent.speed = original;
    }

    IEnumerator ApplyFreeze(NavMeshAgent agent, float dur)
    {
        if (agent == null) yield break;
        bool prevStopped = agent.isStopped;
        agent.isStopped = true;
        yield return new WaitForSeconds(dur);
        if (agent != null) agent.isStopped = prevStopped;
    }
}
