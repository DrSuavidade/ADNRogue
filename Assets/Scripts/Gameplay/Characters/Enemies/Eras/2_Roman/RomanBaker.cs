using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Enemies.Habilidades;
using Geneforge.Gameplay.Visuals;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanBaker : RomanEnemyAbilityBase
    {
        [Header("References")]
        public GameObject weaponObject; 
        
        [Header("Melee (Espátula)")]
        public float damage = 10f;
        public float hitRange = 1.8f;

        [Header("Baker Roulette (🥐🍞🥖🥖)")]
        [Tooltip("Renderer onde os pães vão rodar sobre a cabeça do baker.")]
        public SpriteRenderer rouletteIndicator;
        [Tooltip("Sprites dos 4 itens: 0=Croissant, 1=Pão Bolorento, 2=Pão Dourado, 3=Pão Normal")]
        public Sprite[] itemSprites; 
        public float rouletteDuration = 3f;
        public float initialSpinInterval = 0.08f;
        public float indicatorScale = 1.0f; 
        public float abilityRadius = 6f; 

        [Header("Explosion VFX (Normal Bread)")]
        public Sprite[] explosionFrames;
        public float explosionFPS = 8f;
        public Vector3 explosionScale = new Vector3(3f, 3f, 3f);
        public float explosionRotationY = 0f;

        [Header("Effects Visuals (Color)")]
        public Color croissantColor = new Color(1f, 0.8f, 0.4f); 
        public Color moldyColor = new Color(0.4f, 0.7f, 0.2f);    
        public Color goldenColor = new Color(1f, 0.9f, 0.0f);    
        public Color normalColor = new Color(1f, 0.5f, 0.2f);    

        private int _chosenIndex = 0;
        private bool _isSpinning = false;

        protected override void Awake()
        {
            base.Awake();
            
            // GARANTIR ARMA ATIVA: Resolve o problema dela nascer sem arma
            if (weaponObject != null) weaponObject.SetActive(true);

            if (rouletteIndicator) rouletteIndicator.gameObject.SetActive(false);
        }

        // Chamado no frame de impacto da animação básica de espátula
        public void AnimEvent_SpatulaHit()
        {
            DealDamageToPlayer(damage, hitRange);
        }

        /// <summary>
        /// Chamado via Animation Event no início da animação de "Especial"
        /// </summary>
        public void AnimEvent_StartRoulette()
        {
            if (_isSpinning) return;
            StartCoroutine(RouletteRoutine());
        }

        private IEnumerator RouletteRoutine()
        {
            if (itemSprites == null || itemSprites.Length < 4 || rouletteIndicator == null)
            {
                Debug.LogWarning("[BAKER] Roulette setup incompleto!");
                yield break;
            }

            _isSpinning = true;
            rouletteIndicator.gameObject.SetActive(true);
            rouletteIndicator.transform.localScale = Vector3.zero;
            rouletteIndicator.color = Color.white;

            // Pop-up visual
            float spawnT = 0;
            while(spawnT < 1f) {
                spawnT += Time.deltaTime * 5f;
                // Aplica o indicatorScale aqui na animação inicial
                rouletteIndicator.transform.localScale = Vector3.one * Mathf.Lerp(0, indicatorScale * 1.25f, spawnT);
                yield return null;
            }
            rouletteIndicator.transform.localScale = Vector3.one * indicatorScale;

            float elapsed = 0f;
            float currentInterval = initialSpinInterval;
            int visualIndex = Random.Range(0, 4);

            // ROTAÇÃO
            while (elapsed < rouletteDuration)
            {
                visualIndex = (visualIndex + 1) % itemSprites.Length;
                rouletteIndicator.sprite = itemSprites[visualIndex];
                
                if (elapsed > rouletteDuration * 0.6f)
                {
                    float t = (elapsed - rouletteDuration * 0.6f) / (rouletteDuration * 0.4f);
                    currentInterval = Mathf.Lerp(initialSpinInterval, 0.35f, t);
                }

                yield return new WaitForSeconds(currentInterval);
                elapsed += currentInterval;
            }

            _chosenIndex = visualIndex;
            
            // Flash de confirmação
            for (int i = 0; i < 3; i++)
            {
                rouletteIndicator.color = Color.yellow * 2f;
                yield return new WaitForSeconds(0.1f);
                rouletteIndicator.color = Color.white;
                yield return new WaitForSeconds(0.1f);
            }

            // EFEITO DE ÁREA (AoE) - O Baker "explode" o efeito
            ExecuteAreaEffect(_chosenIndex);

            yield return new WaitForSeconds(1.0f);
            
            float fadeT = 1f;
            while(fadeT > 0) {
                fadeT -= Time.deltaTime * 4f;
                rouletteIndicator.color = new Color(1, 1, 1, fadeT);
                yield return null;
            }

            rouletteIndicator.gameObject.SetActive(false);
            _isSpinning = false;
        }

        private void ExecuteAreaEffect(int index)
        {
            bool playerInRange = IsPlayerInRange(abilityRadius);
            
            string itemName = itemSprites[index].name;
            Debug.Log($"<color=white><b>[BAKER]</b> Servindo: {itemName}!</color>");

            switch (index)
            {
                case 0: // 🥐 Croissant (Seco) -> DANO BASE + SLOW FORTE
                    if (playerInRange) 
                    {
                        playerHealth.ApplyDamage(10f); // Adicionado 10 de dano base
                        ApplySlowToPlayer(-0.6f, 3.5f);
                    }
                    ShowVisualBlast(croissantColor);
                    break;

                case 1: // 🍞 Pão Bolorento (Esporos) -> VENENO (Dano contínuo)
                    if (playerInRange) ApplyPoisonToPlayer(6f, 5f);
                    ShowVisualBlast(moldyColor);
                    break;

                case 2: // 🥖 Pão Dourado (Ouro) -> HEAL BAKER + DANO BASE + SLOW LEVE PLAYER
                    enemy.Heal(enemy.MaxHealth * 0.20f); 
                    if (playerInRange) 
                    {
                        playerHealth.ApplyDamage(10f); // Adicionado 10 de dano base
                        ApplySlowToPlayer(-0.3f, 5f);
                    }
                    ShowVisualBlast(goldenColor);
                    break;

                case 3: // 🥖 Pão Normal (Quente) -> DANO ALTO (EXPLOSÃO)
                    if (playerInRange) 
                    {
                        playerHealth.ApplyDamage(25f); 
                    }
                    SpawnExplosionVFX();
                    ShowVisualBlast(normalColor);
                    break;
            }
        }

        private void SpawnExplosionVFX()
        {
            if (explosionFrames == null || explosionFrames.Length == 0) return;

            // Posição central da explosão (levemente acima do chão para parecer volumosa)
            Vector3 spawnPos = (target != null) ? target.position + Vector3.up * 0.5f : transform.position + Vector3.up * 0.5f;

            // 1. CAMADA CORE (A explosão principal)
            SpawnVFXLayer("Baker_Explosion_Core", spawnPos, explosionScale, explosionFrames, explosionFPS, normalColor * 4f, 1.2f, 180f);

            // 2. CAMADA "ESFERA" (O Blast que expande e cobre o 3D)
            SpawnVFXLayer("Baker_Explosion_Sphere", spawnPos, explosionScale * 0.5f, explosionFrames, explosionFPS * 0.8f, normalColor * 2f, 2.5f, 0f, 0.4f);

            // 3. CAMADA GLOW (Brilho residual)
            SpawnVFXLayer("Baker_Explosion_Glow", spawnPos, explosionScale * 1.5f, new Sprite[] { explosionFrames[0] }, 1f, normalColor * 0.5f, 1.0f, 0f, 0.2f, true);
        }



        private void ApplySlowToPlayer(float amount, float duration)
        {
            if (target == null) return;
            var slow = target.GetComponent<PlayerSlowStatus>();
            if (slow == null) slow = target.gameObject.AddComponent<PlayerSlowStatus>();
            slow.Apply(amount, duration);
        }

        private void ApplyPoisonToPlayer(float dps, float duration)
        {
            if (target == null) return;
            var poison = target.GetComponent<PlayerPoisonStatus>();
            if (poison == null) poison = target.gameObject.AddComponent<PlayerPoisonStatus>();
            poison.Apply(dps, duration, Color.green, 0.1f);
        }

        private void ShowVisualBlast(Color color)
        {
            // Aqui podias instanciar um prefab de explosão circular. 
            // Como pedido para não inventar, usaremos um Gizmo ou um Flash no Baker se ele tiver Flash logic.
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>[BAKER]</b> EXPLOSÃO DE ÁREA ({abilityRadius}m)</color>");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, abilityRadius);
        }
    }
}

