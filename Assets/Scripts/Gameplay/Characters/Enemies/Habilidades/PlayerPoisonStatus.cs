using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    /// <summary>
    /// Componente que lida com o efeito de veneno no Player,
    /// imitando a lógica da habilidade Frog Toxicity.
    /// </summary>
    public class PlayerPoisonStatus : MonoBehaviour
    {
        private float dps;
        private float expireAt;
        private bool ticking;
        private Color flashColor;
        private float flashDuration;

        public void Apply(float poisonDps, float duration, Color color, float fDuration)
        {
            dps = poisonDps;
            expireAt = Time.time + duration;
            flashColor = color;
            flashDuration = fDuration;

            if (!ticking) 
            {
                StartCoroutine(Tick());
            }
        }

        private IEnumerator Tick()
        {
            ticking = true;
            const float tickInterval = 0.5f;
            var playerHealth = GetComponent<PlayerHealth>();

            while (Time.time < expireAt)
            {
                if (playerHealth != null)
                {
                    // Aplica dano proporcional ao intervalo do tick
                    playerHealth.ApplyDamage(dps * tickInterval, false);
                    
                    // Feedback visual de veneno (opcional/basico)
                    // Poderíamos adicionar um PoisonFlash aqui depois como no Frog
                }

                yield return new WaitForSeconds(tickInterval);
            }
            
            ticking = false;
            Destroy(this);
        }
    }
}
