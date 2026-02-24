using UnityEngine;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricPoisonKid : PrehistoricEnemyAbilityBase
    {
        [Header("Poison Dart")]
        public GameObject dartPrefab;
        public Transform shootOrigin;
        public float dartSpeed = 24f;
        public float hitDamage = 4f;
        public float dotDamagePerSecond = 2f;
        public float dotDuration = 4f;

        [Tooltip("What layers the poison dart can affect (usually Player).")]
        public LayerMask hitMask;


        public void AnimEvent_ShootDart()
        {
            if (!dartPrefab || !shootOrigin || !target) return;

            // Garantir que a rotação inicial seja horizontal na direção do alvo
            Vector3 targetDir = (target.position - shootOrigin.position);
            targetDir.y = 0;
            if (targetDir == Vector3.zero) targetDir = shootOrigin.forward;
            Quaternion horizontalRot = Quaternion.LookRotation(targetDir);

            var obj = Object.Instantiate(dartPrefab, shootOrigin.position, horizontalRot);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                // Freeze rotation via code to ensure physics don't spin the arrow
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                Vector3 dir = (target.position - shootOrigin.position).normalized;
                rb.linearVelocity = dir * dartSpeed;
            }

            var proj = obj.GetComponent<PrehistoricPoisonDartProjectile>();
            if (!proj) proj = obj.AddComponent<PrehistoricPoisonDartProjectile>();
            proj.Init(hitDamage, dotDamagePerSecond, dotDuration, hitMask);
        }
    }

    public class PrehistoricPoisonDartProjectile : MonoBehaviour
    {
        float hitDamage;
        float dotDps;
        float dotDuration;
        LayerMask hitMask;

        public void Init(float hit, float dps, float duration, LayerMask mask)
        {
            hitDamage = hit;
            dotDps = dps;
            dotDuration = duration;
            hitMask = mask;
            Destroy(gameObject, 8f);
        }

        void Update()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                Vector3 horizontalVel = rb.linearVelocity;
                horizontalVel.y = 0; // Forçamos a ser horizontal

                if (horizontalVel != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(horizontalVel);
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Layer filter first
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<Player.PlayerHealth>();
            if (!hp)
            {
                Destroy(gameObject);
                return;
            }

            hp.ApplyDamage(hitDamage);
            hp.StartCoroutine(ApplyDot(hp));

            Destroy(gameObject);
        }

        System.Collections.IEnumerator ApplyDot(Geneforge.Gameplay.Characters.Player.PlayerHealth hp)
        {
            float t = 0f;
            while (t < dotDuration && hp != null)
            {
                hp.ApplyDamage(dotDps * Time.deltaTime, false);
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
