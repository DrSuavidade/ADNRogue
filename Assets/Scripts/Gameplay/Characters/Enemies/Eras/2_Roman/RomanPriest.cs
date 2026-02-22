using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;

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
                if (ec != null && ec != enemy && !ec.IsDead) list.Add(ec);
            }
            return list;
        }

        // --- ROTINA DO ESCUDO: PISCAR AMARELO ---
        private IEnumerator ApplyShieldRoutine(EnemyCore target)
        {
            if (target == null) yield break;
            
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var propBlock = new MaterialPropertyBlock();
            float elapsed = 0f;
            float blinkSpeed = 0.15f; // Velocidade do piscar

            while (elapsed < shieldDuration)
            {
                if (target == null || target.IsDead) break;

                // Lógica de piscar
                bool on = (Mathf.FloorToInt(elapsed / blinkSpeed) % 2 == 0);
                
                // Amarelo bem brilhante (HDR) quando ligado, Original (Branco) quando desligado
                Color blinkColor = on ? Color.yellow * 2.5f : Color.white;
                
                propBlock.SetColor("_Color", blinkColor);
                propBlock.SetColor("_BaseColor", blinkColor);
                propBlock.SetColor("_EmissionColor", blinkColor * 0.6f);

                foreach (var r in renderers) {
                    if (r) r.SetPropertyBlock(propBlock);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Garante que volta ao normal no fim
            foreach (var r in renderers) {
                if (r) r.SetPropertyBlock(null);
            }
        }

        private IEnumerator ApplyFuryRoutine(EnemyCore target)
        {
            if (target == null) yield break;
            
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var propBlock = new MaterialPropertyBlock();
            float oldSpeed = target.Animator != null ? target.Animator.speed : 1f;
            
            if (target.Animator != null) target.Animator.speed = oldSpeed * attackSpeedMultiplier;

            // Fúria é um vermelho constante (não pisca) para diferenciar do escudo
            Color furyColor = new Color(1f, 0.2f, 0.2f) * 1.5f;
            propBlock.SetColor("_Color", furyColor);
            propBlock.SetColor("_BaseColor", furyColor);
            propBlock.SetColor("_EmissionColor", furyColor * 0.4f);

            foreach (var r in renderers) {
                if (r) r.SetPropertyBlock(propBlock);
            }

            yield return new WaitForSeconds(furyDuration);
            
            if (target != null)
            {
                if (target.Animator != null) target.Animator.speed = oldSpeed;
                foreach (var r in renderers) {
                    if (r) r.SetPropertyBlock(null);
                }
            }
        }
    }
}
