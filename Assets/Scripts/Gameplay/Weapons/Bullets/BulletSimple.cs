using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Weapons.Bullets
{
public class BulletSimple : MonoBehaviour
{
    [Header("Configuração")]
    public float speed = 25f;         // velocidade da bala
    public float damage = 10f;        // dano que causa ao Player
    public float lifeTime = 3f;       // tempo até ser destruída

    [Header("Referências")]
    private Rigidbody rb;

    PoolIdentifier poolId;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        poolId = GetComponent<PoolIdentifier>();
        if (rb == null)
        {
            Debug.LogError("A bala precisa de um Rigidbody!");
            return;
        }

        rb.useGravity = false;
        rb.isKinematic = false;

        // Mover para a frente
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = transform.forward * speed;
#else
        rb.velocity = transform.forward * speed;
#endif

        // Auto-destruição
        Invoke(nameof(Despawn), lifeTime);
    }

    void Despawn()
    {
        CancelInvoke(nameof(Despawn));

        if (poolId != null && PoolManager.Instance != null)
            PoolManager.Instance.Reclaim(gameObject);
        else
            Destroy(gameObject);
    }


    void OnTriggerEnter(Collider other)
    {
        // Aplica dano ao player
        PlayerHealth ph = other.GetComponent<PlayerHealth>();

        if (ph != null)
        {
            ph.ApplyDamage(damage);
            Despawn();
            return;
        }

        // destruir se bater noutro objeto físico (paredes, chão, etc.)
        if (!other.isTrigger)
            Despawn();
    }
}
 }
