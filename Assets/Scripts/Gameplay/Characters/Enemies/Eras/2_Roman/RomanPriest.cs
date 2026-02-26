using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Visuals;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanPriest : RomanEnemyAbilityBase
    {
        [Header("Tactical Settings")]
        public float buffRadius = 25f;
        public LayerMask allyMask = ~0;
        
        [Range(0, 1)] public float healthThresholdForShield = 0.8f; 
        public int allyCountForFury = 3;

        [Header("Buff Rules")]
        public float shieldDuration = 4f; // Duração exata de 4 segundos pedida
        public float furyDuration = 5f;
        public float attackSpeedMultiplier = 1.2f; // Reduzido de 1.4f para 1.2f para ser mais equilibrado

        [Header("Shield Visuals")]
        public GameObject shieldPrefab;
        public Sprite[] shieldAnimationFrames;
        public float shieldFPS = 8f;
        public float shieldScale = 1.5f;
        public float shieldYOffset = 1.0f;
        [ColorUsage(true, true)] public Color shieldColor = Color.white; // Cor do escudo (HDR)

        [Header("Fury Visuals")]
        public GameObject furyPrefab; // Podes usar o mesmo prefab da esfera ou outro
        public Sprite[] furyAnimationFrames; // Os teus 10 frames para o chão
        public float furyFPS = 8f;
        public float furyScale = 2.0f;
        public float furyYOffset = 0.05f; // Quase rente ao chão
        [ColorUsage(true, true)] public Color furyColor = new Color(2.0f, 0.2f, 0.2f, 0.8f); // Vermelho Fúria (HDR) por padrão

        [Header("Attack Indicator Visuals")]
        public GameObject indicatorPrefab; 
        public Sprite[] indicatorAnimationFrames;
        public float indicatorFPS = 8f;
        public float indicatorScale = 2.0f;
        public float indicatorYOffset = 0.02f;
        [ColorUsage(true, true)] public Color indicatorColor = new Color(1.5f, 0.2f, 2.0f, 0.8f); // Roxo Místico (HDR) por padrão

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, buffRadius);
        }

        /// <summary>
        /// Chamado pelo Animator no INÍCIO da animação de suporte/ataque.
        /// Cria o indicador visual no chão com Glow HDR e Fade suave usando o novo sistema.
        /// </summary>
        public void AnimEvent_StartAttackIndicator()
        {
            if (indicatorAnimationFrames == null || indicatorAnimationFrames.Length == 0) return;

            // Aumentamos ligeiramente o offset (0.1f) para evitar clipping com o chão 
            Vector3 spawnPos = transform.position + Vector3.up * 0.1f; 
            
            // USAR AnimationMode.Floor para ficar deitado no chão
            SpawnVFXLayer("Priest_Indicator_Main", spawnPos, Vector3.one * indicatorScale, indicatorAnimationFrames, indicatorFPS, indicatorColor, 1.05f, 0f, 0.7f, true, transform, SpriteSheetAnimator.AnimationMode.Floor);
            
            SpawnVFXLayer("Priest_Indicator_Flash", spawnPos, Vector3.one * indicatorScale * 1.2f, new Sprite[] { indicatorAnimationFrames[0] }, 1f, indicatorColor * 2f, 1.0f, 0f, 0.1f, false, null, SpriteSheetAnimator.AnimationMode.Floor);
        }

        public void AnimEvent_PriestLogic()
        {
            Debug.Log("<color=white><b>[PRIEST]</b> Analisando campo de batalha...</color>");

            var allies = GetValidAllies();
            if (allies.Count == 0) 
            {
                // Diagnóstico extra para ajudar o utilizador no Unity
                var totalInRange = Physics.OverlapSphere(transform.position, buffRadius).Length;
                Debug.LogWarning($"[PRIEST] Ninguém por perto para buff. Detetei {totalInRange} objetos no raio, mas nenhum passou o filtro do Ally Mask.");
                return;
            }

            bool shieldNeeded = false;
            foreach (var a in allies)
            {
                if (a.CurrentHealth < a.MaxHealth * healthThresholdForShield)
                {
                    shieldNeeded = true;
                    break;
                }
            }

            if (shieldNeeded)
            {
                Debug.Log("<color=yellow><b>[PRIEST] ESCUDO ativado por " + shieldDuration + " segundos!</b></color>");
                foreach (var a in allies) StartCoroutine(ApplyShieldRoutine(a));
            }
            else
            {
                Debug.Log("<color=red><b>[PRIEST] FÚRIA ativada!</b></color>");
                foreach (var a in allies) StartCoroutine(ApplyFuryRoutine(a));
            }
        }

        private List<EnemyCore> GetValidAllies()
        {
            var list = new List<EnemyCore>();
            var cols = Physics.OverlapSphere(transform.position, buffRadius, allyMask);
            foreach (var c in cols)
            {
                var ec = c.GetComponentInParent<EnemyCore>();
                
                // Filtros: não nulo, não ser o próprio priest, não estar morto
                if (ec != null && ec != enemy && !ec.IsDead) 
                {
                    // NOVO: Impedir que o Player receba o buff mesmo que tenha EnemyCore ou esteja na LayerMask
                    if (ec.CompareTag("Player")) continue;
                    if (target != null && ec.transform == target) continue;

                    list.Add(ec);
                }
            }
            return list;
        }

        // --- ROTINA DO ESCUDO: ESFERA ANIMADA PROFISSIONAL (LAYERED) ---
        private IEnumerator ApplyShieldRoutine(EnemyCore ally)
        {
            if (ally == null) yield break;
            
            // PREVENIR STACKING
            string coreName = "PriestShieldFX_Core";
            if (ally.transform.Find(coreName) != null) yield break;

            ally.IsInvulnerable = true;

            // 1. CAMADA CORE (A bolha principal em LOOP)
            // Passamos 'true' no último parâmetro para ativar o loop
            GameObject core = SpawnVFXLayer(coreName, ally.transform.position + Vector3.up * shieldYOffset, Vector3.one * shieldScale, shieldAnimationFrames, shieldFPS, shieldColor, 1.05f, 0f, 0.8f, true, ally.transform, SpriteSheetAnimator.AnimationMode.Billboard, true);
            
            // GARANTIA: Se o objeto não foi criado (ex: limite de pool), sai para não travar invulnerabilidade
            if (core == null) 
            {
                ally.IsInvulnerable = false;
                yield break;
            }
            
            // 2. CAMADA "RIPPLE" (Ondas de energia que expandem em intervalos)
            float elapsed = 0f;
            while (elapsed < shieldDuration)
            {
                if (ally == null || ally.IsDead) break;
                
                // Spawn ripple periódico (não é loop, morre sozinho)
                SpawnVFXLayer("PriestShield_Ripple", ally.transform.position + Vector3.up * shieldYOffset, Vector3.one * shieldScale * 0.8f, shieldAnimationFrames, shieldFPS * 1.5f, shieldColor * 1.5f, 1.8f, 180f, 0.4f, false, ally.transform);
                
                yield return new WaitForSeconds(1.0f);
                elapsed += 1.0f;
            }

            if (ally != null)
            {
                ally.IsInvulnerable = false;
            }

            // Limpeza manual do CORE que estava em loop
            if (core != null)
            {
                if (PoolManager.Instance != null && core.GetComponent<PoolIdentifier>() != null)
                    PoolManager.Instance.Reclaim(core);
                else
                    Destroy(core);
            }
        }

        // --- ROTINA DA FÚRIA: ANIMAÇÃO PROFISSIONAL NO CHÃO (LAYERED) ---
        private IEnumerator ApplyFuryRoutine(EnemyCore ally)
        {
            if (ally == null) yield break;
            
            if (ally.transform.Find("PriestFuryFX_Main") != null) yield break;

            if (ally.Animator == null) yield break;

            // NÃO STACKAR: Se a velocidade já for maior que 1.1 (margem de erro), assume que já tem buff
            if (ally.Animator.speed > 1.1f) yield break;

            float oldSpeed = 1f; // Assumindo 1f como base saudável, ou capturar o atual se for garantido único
            ally.Animator.speed = oldSpeed * attackSpeedMultiplier;

            // Offset ligeiramente maior e AnimationMode.Floor
            Vector3 groundPos = ally.transform.position + Vector3.up * 0.1f;

            // 1. CAMADA CHÃO (Glow por baixo) - Modo Floor
            SpawnVFXLayer("PriestFuryFX_Main", groundPos, Vector3.one * furyScale, furyAnimationFrames, furyFPS, furyColor, 1.0f, 0f, 0.7f, true, ally.transform, SpriteSheetAnimator.AnimationMode.Floor);

            // 2. CAMADA BURST (Esfera expandindo no centro do corpo) - Modo Billboard (3D)
            SpawnVFXLayer("PriestFury_Burst", ally.transform.position + Vector3.up * 0.8f, Vector3.one * furyScale * 0.5f, furyAnimationFrames, furyFPS * 0.8f, furyColor * 2f, 2.5f, 0f, 0.3f, false, ally.transform, SpriteSheetAnimator.AnimationMode.Billboard);

            yield return new WaitForSeconds(furyDuration);
            
            if (ally != null && !ally.IsDead && ally.Animator != null)
            {
                // RESET ABSOLUTO: Força a volta para 1.0f para garantir que não fique rápido
                // Só resetamos se o valor atual for exatamente o valor do buff (para não quebrar outros sistemas)
                if (Mathf.Abs(ally.Animator.speed - attackSpeedMultiplier) < 0.05f)
                {
                    ally.Animator.speed = 1.0f;
                }
            }
        }

    }
}
