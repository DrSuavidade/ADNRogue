using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanMaceMaster : RomanEnemyAbilityBase
    {
        [Header("Mace Slam Logic")]
        public float slamDamage = 20f;
        public float slamRadius = 2.4f;

        [Header("Slam VFX (Professional)")]
        [Tooltip("Prefab de impacto e poeira (ex: Particle System)")]
        public GameObject slamPrefab;
        [Tooltip("Prefab de cratera/rachadura")]
        public GameObject craterPrefab;
        public float slamScaleMult = 1f;
        public float craterScaleMult = 1f;

        /// <summary>
        /// Evento Profissional de Impacto: Aplica dano e spawna camadas de VFX.
        /// Deve ser colocado no frame exato onde a Mace atinge o chão.
        /// </summary>
        public void AnimEvent_MaceSlam()
        {
            // 1. Dano em área centrada na posição do inimigo (ou player se preferir, mas Slam costuma ser local)
            DealDamageToPlayer(slamDamage, slamRadius);

            // 2. Spawn dos Efeitos em Camadas
            SpawnProfessionalSlamVFX();
        }

        private void SpawnProfessionalSlamVFX()
        {
            Vector3 impactPos = transform.position;

            // 1. GARANTIR POSIÇÃO NO CHÃO (Raycast)
            Vector3 spawnPos = transform.position;
            int floorMask = ~((1 << 3) | (1 << 2) | (1 << 6)); // Ignora Player, Triggers, etc
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 10f, floorMask))
            {
                spawnPos = hit.point + Vector3.up * 0.05f;
            }
            else
            {
                spawnPos.y = 0.05f; // Fallback para altura mínima do chão
            }

            // CAMADA 1: CRATERA
            if (craterPrefab != null)
            {
                SpawnVFX(craterPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360f), 0), null, craterScaleMult);
            }

            // CAMADA 2: IMPACTO SÍSMICO
            if (slamPrefab != null)
            {
                SpawnVFX(slamPrefab, impactPos + Vector3.up * 0.1f, Quaternion.identity, null, slamScaleMult);
            }
        }
    }
}
