// Bullet.cs
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float lifeTime = 3f;
    [HideInInspector] public float damage = 1f;
    [HideInInspector] public float knockbackForce = 0f;  // e.g. 1 → 0.1f, 0.5 → 0.05f
    [HideInInspector] public bool isCrit = false;
    public GameObject impactEffectPrefab;

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
        }


        // 3) Destroy the bullet
        Destroy(transform.root.gameObject);
    }
}
