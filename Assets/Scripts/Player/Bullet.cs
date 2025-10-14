// Bullet.cs
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float lifeTime = 3f;
    [HideInInspector] public float damage = 1f;
    [HideInInspector] public float knockbackForce = 0f;  // e.g. 1 → 0.1f, 0.5 → 0.05f
    [HideInInspector] public bool isCrit = false;
    public GameObject impactEffectPrefab;

	// Chain lightning settings (on-hit)
	[Header("Chain Lightning (On Hit)")]
	[Tooltip("Enable chain lightning when this projectile hits any enemy")]
	public bool enableChainLightning = true;
	[Tooltip("Radius to hit other enemies after any hit")]
	public float chainLightningRadius = 6f;
	[Tooltip("If true, kills all enemies in radius. If false, deals fixed damage")]
	public bool chainLightningKillAll = true;
	[Tooltip("Damage dealt to each chained enemy when not killing all")]
	public float chainLightningDamage = 10f;
	[Tooltip("Optional VFX spawned at the hit enemy (and optionally at each chained target)")]
	public GameObject chainLightningEffectPrefab;

    void Awake()
    {
        Destroy(transform.root.gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 1) Impact VFX (unchanged)
        if (impactEffectPrefab != null)
        {
            ContactPoint cp = collision.contacts[0];
            GameObject fx = Instantiate(
                impactEffectPrefab,
                cp.point,
                Quaternion.LookRotation(cp.normal)
            );
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                Destroy(fx, main.duration + main.startLifetime.constantMax);
            }
            else Destroy(fx, 2f);
        }

        // 2) Damage + simple trajectory‐based knockback
		var enemy = collision.collider.GetComponent<Enemy>();
		if (enemy != null)
		{
			// Pass the crit flag along
			enemy.TakeDamage(damage, isCrit);
			if (knockbackForce > 0f)
			{
				Vector3 dir = transform.forward;
				dir.y = 0f;
				enemy.ApplyKnockback(dir, knockbackForce);
			}

			// Trigger chain lightning on any hit (not just kills)
			if (enableChainLightning)
			{
				TriggerChainLightning(enemy.transform.position, enemy);
			}
		}


        // 3) Destroy the bullet
        Destroy(transform.root.gameObject);
    }

	void TriggerChainLightning(Vector3 origin, Enemy hitEnemy)
	{
		// VFX at the hit origin
		if (chainLightningEffectPrefab != null)
		{
			var fx = Instantiate(chainLightningEffectPrefab, origin, Quaternion.identity);
			var ps = fx.GetComponent<ParticleSystem>();
			if (ps != null)
			{
				var main = ps.main;
				Destroy(fx, main.duration + main.startLifetime.constantMax);
			}
			else Destroy(fx, 2f);
		}

		Collider[] hits = Physics.OverlapSphere(origin, chainLightningRadius);
		for (int i = 0; i < hits.Length; i++)
		{
			var otherEnemy = hits[i].GetComponent<Enemy>();
			if (otherEnemy == null || otherEnemy == hitEnemy) continue;

			// Apply damage or guaranteed kill
			if (chainLightningKillAll)
			{
				otherEnemy.TakeDamage(otherEnemy.MaxHealth);
			}
			else
			{
				otherEnemy.TakeDamage(chainLightningDamage);
			}

			// Optional VFX at each chained target
			if (chainLightningEffectPrefab != null)
			{
				var fx = Instantiate(chainLightningEffectPrefab, otherEnemy.transform.position, Quaternion.identity);
				var ps = fx.GetComponent<ParticleSystem>();
				if (ps != null)
				{
					var main = ps.main;
					Destroy(fx, main.duration + main.startLifetime.constantMax);
				}
				else Destroy(fx, 2f);
			}
		}
	}
}
