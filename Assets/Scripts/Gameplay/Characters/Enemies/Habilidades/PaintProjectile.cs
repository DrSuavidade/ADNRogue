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

        private void Start()
        {
            // Destrói o balde/tiro após 5 segundos para não poluir o mapa ou criar paredes invisíveis
            Destroy(gameObject, 5f);
        }

        private void Update()
        {
            var rb = GetComponent<Rigidbody>();
            // Profissional: Projéteis que não usam gravidade (como o jato de tinta) devem olhar para onde vão.
            // Os baldes usam gravidade e torque aleatório, então ignoramos para não estragar a física.
            if (rb != null && !rb.useGravity && rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 1. Ignorar amigos
            if (other.GetComponentInParent<EnemyCore>() != null) return;

            // 2. Tentar dar dano ao Player
            if (((1 << other.gameObject.layer) & hitMask) != 0)
            {
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health != null) health.ApplyDamage(damage);
                Impact(true);
            }
            // 3. Qualquer outra coisa (Chão, Parede)
            else
            {
                Impact(false);
            }
        }

        private void Impact(bool hitPlayer)
        {
            if (puddlePrefab != null)
            {
                Vector3 spawnPos = transform.position;

                // Raycast melhorado: ignora Triggers (outras poças/projéteis) e procura o chão real
                if (Physics.Raycast(transform.position + Vector3.up * 2.0f, Vector3.down, out RaycastHit hit, 20f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + new Vector3(0, 0.02f, 0); 
                }
                else
                {
                    // Se falhar, tenta usar a posição atual mas baixa (Y=0 ou próximo do pivot do inimigo)
                    spawnPos = new Vector3(transform.position.x, 0.02f, transform.position.z);
                }

                GameObject puddle = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                var puddleScript = puddle.GetComponent<PaintPuddle>();
                if (puddleScript != null) puddleScript.Init(myColor);
            }

            Destroy(gameObject);
        }
    }
}
