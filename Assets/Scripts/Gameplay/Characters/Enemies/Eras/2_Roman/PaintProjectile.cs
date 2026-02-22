using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
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

                // Se batermos em algo, tentamos encontrar o chão exato abaixo com um raio
                if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f))
                {
                    spawnPos = hit.point + new Vector3(0, 0.05f, 0); // Altura de segurança para ser visível
                }
                else
                {
                    spawnPos = transform.position + new Vector3(0, 0.05f, 0);
                }

                GameObject puddle = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                var puddleScript = puddle.GetComponent<PaintPuddle>();
                if (puddleScript != null) puddleScript.Init(myColor);
            }

            Destroy(gameObject);
        }
    }
}
