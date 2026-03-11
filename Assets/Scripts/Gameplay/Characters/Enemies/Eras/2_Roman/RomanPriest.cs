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
        public float shieldYOffset = 1.0f;
        public float shieldScaleMult = 1.0f;

        [Header("Fury Visuals")]
        public GameObject furyPrefab;
        public float furyYOffset = 0.05f;
        public float furyScaleMult = 1.0f;

        [Header("Attack Indicator Visuals")]
        public GameObject indicatorPrefab;
        public float indicatorYOffset = 0.02f;
        public float indicatorScaleMult = 1.0f;

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
            if (indicatorPrefab == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * indicatorYOffset; 
            SpawnVFX(indicatorPrefab, spawnPos, Quaternion.identity, transform, indicatorScaleMult);
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

            // 1. CAMADA CORE (A bolha principal em LOOP - assume que o prefab já tem o loop)
            GameObject vfx = SpawnVFX(shieldPrefab, ally.transform.position + Vector3.up * shieldYOffset, Quaternion.identity, ally.transform, shieldScaleMult);
            
            if (vfx == null) 
            {
                ally.IsInvulnerable = false;
                yield break;
            }
            
            yield return Geneforge.Core.Utils.WaitCache.Get(shieldDuration);

            if (ally != null)
            {
                ally.IsInvulnerable = false;
            }

            if (vfx != null)
            {
                if (PoolManager.Instance != null && vfx.GetComponent<PoolIdentifier>() != null)
                    PoolManager.Instance.Reclaim(vfx);
                else
                    Destroy(vfx);
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

            // USAR MODO FLOOR E BILLBOARD BASEADO NO PREFAB AGORA
            Vector3 groundPos = ally.transform.position + Vector3.up * furyYOffset;

            SpawnVFX(furyPrefab, groundPos, Quaternion.identity, ally.transform, furyScaleMult);

            yield return Geneforge.Core.Utils.WaitCache.Get(furyDuration);
            
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
