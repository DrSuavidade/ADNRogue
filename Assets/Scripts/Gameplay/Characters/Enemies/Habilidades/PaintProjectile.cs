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

        private float _fixedX;
        private float _fixedZ;
        private Rigidbody _rb;
        private bool _isInitialized;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            
            // Guardamos a rotação horizontal original do prefab
            _fixedX = transform.eulerAngles.x;
            _fixedZ = transform.eulerAngles.z;

            if (_rb != null)
            {
                // Travamos X e Z para ele não "capotar" no ar, tal como o disco
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            _isInitialized = true;
            Destroy(gameObject, 5f);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;

            // Se for um projétil sem gravidade (Jato de tinta), mantemos a orientação horizontal
            if (!_rb.useGravity && _rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                // Calculamos para onde ele deve olhar no eixo Y (Yaw)
                Quaternion targetRot = Quaternion.LookRotation(_rb.linearVelocity);
                
                // Aplicamos a rotação mantendo o X e Z originais (o "deitado" do prefab)
                _rb.MoveRotation(Quaternion.Euler(_fixedX, targetRot.eulerAngles.y, _fixedZ));
            }
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
