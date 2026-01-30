using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.Abilities;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    // Keeping these for backwards compatibility with existing prefabs until they are updated to use SimpleProjectile.
    
    public class PrehistoricFireProjectile : MonoBehaviour, IProjectile
    {
        float damage;
        LayerMask hitMask;

        public void Initialize(float damage, float speed, Vector3 direction, LayerMask mask, float arcHeight = 0f)
        {
             Init(damage, mask);
             // Physics handled by Rigidbody usually for these old prefabs
             var rb = GetComponent<Rigidbody>();
             if(rb) 
             {
                 rb.linearVelocity = direction * speed;
                 rb.useGravity = arcHeight > 0;
             }
        }

        public void Init(float dmg, LayerMask mask)
        {
            damage = dmg;
            hitMask = mask;
            Destroy(gameObject, 6f);
        }

        void OnTriggerEnter(Collider other)
        {
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;

            var hp = other.GetComponent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
            if (hp != null) hp.ApplyDamage(damage);
            
            // Also check parent
            if (hp == null)
            {
                hp = other.GetComponentInParent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
                if (hp != null) hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }

    public class PrehistoricSpearProjectile : MonoBehaviour, IProjectile
    {
        float damage;
        LayerMask hitMask;

        public void Initialize(float damage, float speed, Vector3 direction, LayerMask mask, float arcHeight = 0f)
        {
             Init(damage, mask);
             var rb = GetComponent<Rigidbody>();
             if(rb)
             { 
                 rb.linearVelocity = direction * speed;
                 rb.useGravity = arcHeight > 0;
             }
        }

        public void Init(float dmg, LayerMask mask)
        {
            damage = dmg;
            hitMask = mask;
            Destroy(gameObject, 8f);
        }

        void OnTriggerEnter(Collider other)
        {
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;

            var hp = other.GetComponentInParent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
            if (hp != null) hp.ApplyDamage(damage);

            Destroy(gameObject);
        }
    }
}
