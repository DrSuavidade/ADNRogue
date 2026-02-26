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
        [Tooltip("Frames de impacto e poeira (Billboard)")]
        public Sprite[] slamFrames;      
        [Tooltip("Frames de cratera/rachadura (Floor)")]
        public Sprite[] craterFrames;    
        public float vfxFPS = 8f;
        public Vector3 slamScale = new Vector3(2.5f, 2.5f, 2.5f);
        public Vector3 craterScale = new Vector3(3.5f, 3.5f, 3.5f);
        [ColorUsage(true, true)] public Color impactColor = Color.white;

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

            // CAMADA 1: CRATERA (Ground Layer)
            if (craterFrames != null && craterFrames.Length > 0)
            {
                SpawnVFXLayer(
                    "Mace_Crater_Floor", 
                    spawnPos, 
                    craterScale, 
                    craterFrames, 
                    vfxFPS * 0.7f, 
                    impactColor, 
                    1.1f, 
                    360f, 
                    0.4f, 
                    false, 
                    null, 
                    Visuals.SpriteSheetAnimator.AnimationMode.Floor,
                    false, // Loop
                    false  // useSpawnScale = false (Aparece instantâneo para impacto)
                );
            }

            // CAMADA 2: IMPACTO SÍSMICO (Billboard Layer)
            // Eleva ligeiramente (0.1f) para evitar Z-fighting com o chão
            if (slamFrames != null && slamFrames.Length > 0)
            {
                SpawnVFXLayer(
                    "Mace_Slam_Impact", 
                    impactPos + Vector3.up * 0.1f, 
                    slamScale, 
                    slamFrames, 
                    vfxFPS, 
                    impactColor * 2f, // Brilho extra (HDR)
                    1.4f, 
                    180f, 
                    0.7f, 
                    true // Pulsação para dar "peso"
                );
            }

            // CAMADA 3: GLOW RESIDUAL (Flash Rápido)
            if (slamFrames != null && slamFrames.Length > 0)
            {
                SpawnVFXLayer(
                    "Mace_Slam_Glow", 
                    impactPos + Vector3.up * 0.2f, 
                    slamScale * 1.5f, 
                    new Sprite[] { slamFrames[0] }, 
                    1f, 
                    impactColor * 0.5f, 
                    1.0f, 
                    0f, 
                    0.2f, 
                    true
                );
            }
        }
    }
}
