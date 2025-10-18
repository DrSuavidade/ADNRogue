using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SpearProjectile : MonoBehaviour
{
    [Header("Flight")]
    public float lifeSeconds = 8f;
    public bool alignToVelocity = true;

    [Header("Damage")]
    public int damage = 20;
    public LayerMask hittable = ~0;

    Rigidbody rb;
    Collider col;
    bool launched;
    float alive;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.isKinematic = true;        // começa presa à mão
        col.enabled = false;          // sem colisão enquanto na mão
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void Launch(Vector3 velocity, Collider[] ignoreOwner = null)
    {
        launched = true;
        rb.isKinematic = false;
        col.enabled = true;
        rb.linearVelocity = velocity;
        rb.useGravity = true;

        if (ignoreOwner != null)
            foreach (var c in ignoreOwner)
                if (c) Physics.IgnoreCollision(col, c, true);
    }

    void Update()
    {
        if (!launched) return;

        alive += Time.deltaTime;
        if (alive >= lifeSeconds) Destroy(gameObject);

        if (alignToVelocity && rb.linearVelocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
    }

    void OnCollisionEnter(Collision other)
    {
        if (!launched) return;

        // “crava” a lança
        rb.isKinematic = true;
        rb.useGravity = false;
        col.enabled = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Parent ao objeto atingido para ficar presa (se quiseres)
        transform.SetParent(other.transform, true);

        // TODO: aplicar dano se tiver um componente de vida
        // other.gameObject.GetComponent<Health>()?.Take(damage);

        // destruir depois de alguns segundos
        Destroy(gameObject, 10f);
    }
}
