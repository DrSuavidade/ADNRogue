using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintProjectile : MonoBehaviour
    {
        public float damage;
        public LayerMask hitMask;
        public Color myColor = Color.white; // Guardamos a cor enviada pelo Painter
        public GameObject puddlePrefab;    // Arraste aqui o Prefab da poça

        [HideInInspector] public Sprite[] puddleFrames;
        [HideInInspector] public float puddleFPS = 10f;
        [HideInInspector] public float puddleScale = 2.0f;

        public float visualYawOffset = 90f; // Ajuste no Inspector se a mancha voar de lado

        private float _fixedYaw;
        private Rigidbody _rb;
        private bool _isInitialized;

        public void Init(float dmg, LayerMask mask, float yaw, Color color, bool useGravity)
        {
            damage = dmg;
            hitMask = mask;
            myColor = color;
            _fixedYaw = yaw;
            
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>(); 

            _rb.isKinematic = false;
            _rb.useGravity = useGravity;
            _rb.constraints = RigidbodyConstraints.FreezeRotation; // Impede a física de girar o sprite
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            _isInitialized = true;
            Destroy(gameObject, 5f);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;

            // FORÇA A ORIENTAÇÃO EXACTA (Tipo a seta/disco)
            // Isso garante que ele não vire para os lados nem para cima/baixo
            _rb.MoveRotation(Quaternion.Euler(0, _fixedYaw, 0));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<EnemyCore>() != null) return;

            if (((1 << other.gameObject.layer) & hitMask) != 0)
            {
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health != null) health.ApplyDamage(damage);
                Impact(true);
            }
            else
            {
                Impact(false);
            }
        }

        private void Impact(bool hitPlayer)
        {
            if (puddlePrefab != null)
            {
                // Começamos o raio um pouco acima do ponto de impacto
                Vector3 rayStart = transform.position + Vector3.up * 1.0f;
                Vector3 spawnPos = transform.position;

                // Camada do chão (Geralmente Default ou Environment). 
                // IMPORTANTE: Excluímos a camada do Player (Layer 3 ou 6 geralmente) para o raio passar através dele.
                int floorMask = ~((1 << 3) | (1 << 6) | (1 << 2)); // Ignora Player, Triggers e IgnoreRaycast

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, floorMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + new Vector3(0, 0.02f, 0); 
                }
                else
                {
                    // Fallback: spawn na base do player se não houver chão detectado (ex: Y=0)
                    spawnPos = new Vector3(transform.position.x, 0.05f, transform.position.z);
                }

                GameObject puddle = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                var puddleScript = puddle.GetComponent<PaintPuddle>();
                if (puddleScript != null) 
                {
                    puddleScript.Init(myColor, puddleFrames, puddleFPS, puddleScale);
                }
            }

            Destroy(gameObject);
        }


    }
}
