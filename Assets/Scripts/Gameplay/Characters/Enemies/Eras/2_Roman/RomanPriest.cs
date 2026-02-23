using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Visuals;

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
        public float attackSpeedMultiplier = 1.4f;

        [Header("Shield Visuals")]
        public GameObject shieldPrefab;
        public Sprite[] shieldAnimationFrames;
        public float shieldFPS = 12f;
        public float shieldScale = 1.5f;
        public float shieldYOffset = 1.0f;

        [Header("Fury Visuals")]
        public GameObject furyPrefab; // Podes usar o mesmo prefab da esfera ou outro
        public Sprite[] furyAnimationFrames; // Os teus 10 frames para o chão
        public float furyFPS = 12f;
        public float furyScale = 2.0f;
        public float furyYOffset = 0.05f; // Quase rente ao chão

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, buffRadius);
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

        // --- ROTINA DO ESCUDO: ESFERA ANIMADA ---
        private IEnumerator ApplyShieldRoutine(EnemyCore target)
        {
            if (target == null) yield break;
            
            // PREVENIR STACKING: Se já tiver escudo, não aplicamos outro
            if (target.transform.Find("PriestShieldFX") != null) yield break;

            target.IsInvulnerable = true;

            GameObject shieldObj = null;
            if (shieldPrefab != null)
            {
                Vector3 spawnPos = target.transform.position + Vector3.up * shieldYOffset;
                shieldObj = Instantiate(shieldPrefab, spawnPos, Quaternion.identity, target.transform);
                shieldObj.name = "PriestShieldFX"; // Nome fixo para detecção
                shieldObj.transform.localScale = Vector3.one * shieldScale;

                var animator = shieldObj.GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = shieldObj.AddComponent<SpriteSheetAnimator>();
                animator.Initialize(shieldAnimationFrames, shieldFPS, SpriteSheetAnimator.AnimationMode.Billboard);
            }

            yield return new WaitForSeconds(shieldDuration);

            if (target != null)
            {
                target.IsInvulnerable = false;
            }
            
            if (shieldObj != null) 
            {
                Destroy(shieldObj);
            }
        }

        // --- ROTINA DA FÚRIA: ANIMAÇÃO NO CHÃO ---
        private IEnumerator ApplyFuryRoutine(EnemyCore target)
        {
            if (target == null) yield break;
            
            // PREVENIR STACKING: Se já estiver em fúria, não somamos stats
            if (target.transform.Find("PriestFuryFX") != null) yield break;

            float oldSpeed = target.Animator != null ? target.Animator.speed : 1f;
            if (target.Animator != null) target.Animator.speed = oldSpeed * attackSpeedMultiplier;

            GameObject furyObj = null;
            if (furyPrefab != null)
            {
                Vector3 spawnPos = target.transform.position + Vector3.up * furyYOffset;
                furyObj = Instantiate(furyPrefab, spawnPos, Quaternion.identity, target.transform);
                furyObj.name = "PriestFuryFX"; // Nome fixo para detecção
                furyObj.transform.localScale = Vector3.one * furyScale;

                var animator = furyObj.GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = furyObj.AddComponent<SpriteSheetAnimator>();
                animator.Initialize(furyAnimationFrames, furyFPS, SpriteSheetAnimator.AnimationMode.Floor);
            }

            yield return new WaitForSeconds(furyDuration);
            
            if (target != null && target.Animator != null)
            {
                target.Animator.speed = oldSpeed;
            }

            if (furyObj != null)
            {
                Destroy(furyObj);
            }
        }

    }
}
