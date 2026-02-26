using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;
using Geneforge.Gameplay.Visuals;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintPuddle : MonoBehaviour
    {
        public float lifetime = 8f;
        public float slowAmount = -0.6f; // Aumentado para 60% para ser bem percetível
        
        private float _poisonDps;
        private float _poisonDuration;
        private bool _playerInside = false;

        private void OnEnable()
        {
            _playerInside = false; // Reset state for pooling
            StopAllCoroutines();
            StartCoroutine(LifetimeRoutine(lifetime));
        }

        private void OnDisable()
        {
            RemoveSlow(); // Garante que o slow é removido se o objeto sumir
        }

        private System.Collections.IEnumerator LifetimeRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null && GetComponent<Geneforge.Core.Pooling.PoolIdentifier>() != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }

        public void Init(Color color, Sprite[] frames = null, float fps = 10f, Vector3 scale = default, float rotationY = 0f, float poisonDps = 0f, float poisonDuration = 0f)
        {
            if (scale == default) scale = Vector3.one;
            _poisonDps = poisonDps;
            _poisonDuration = poisonDuration;

            // Define a rotação (90 no X para deitar no chão, e a rotação custom no Y)
            transform.rotation = Quaternion.Euler(90f, rotationY, 0f);

            if (frames != null && frames.Length > 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                    if (!(r is SpriteRenderer)) r.enabled = false;

                var animator = GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = gameObject.AddComponent<SpriteSheetAnimator>();
                
                transform.localScale = scale;

                // Configuração Profissional (Simulando uma partícula de poça)
                animator.tintColor = color;
                animator.useSpawnScale = true; 
                animator.useFadeOut = true;
                animator.loop = false;
                animator.fadeStartTime = 0.8f;
                // Faz a poça "espalhar" ligeiramente enquanto dura
                animator.scaleMultiplier = new Vector3(1.1f, 1.1f, 1f); 
                
                animator.Initialize(frames, fps, SpriteSheetAnimator.AnimationMode.Floor, lifetime);
                
                // IMPACTO: Flash de luz quando a tinta atinge o chão
                animator.Flash(0.15f); 
            }
            else
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    var propBlock = new MaterialPropertyBlock();
                    propBlock.SetColor("_Color", color);
                    propBlock.SetColor("_BaseColor", color);
                    renderer.SetPropertyBlock(propBlock);
                }
                transform.localScale = scale;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                ApplySlow();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
            {
                if (!_playerInside) ApplySlow();
                if (_poisonDps > 0) ApplyPoison(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                RemoveSlow();
            }
        }

        private bool IsPlayer(Collider other)
        {
            // Verificação tripla para não falhar: Tag OR Componente OR Layer
            return other.CompareTag("Player") || 
                   other.GetComponentInParent<PlayerHealth>() != null || 
                   other.gameObject.layer == 3; // Layer 3 é geralmente Player
        }


        private void ApplySlow()
        {
            if (_playerInside) return;
            var run = RunSession.Instance?.Run;
            if (run != null)
            {
                // Só aplica se houver um valor de slow real
                if (Mathf.Abs(slowAmount) > 0.01f)
                {
                    run.ModifySpeed(slowAmount);
                    Debug.Log("<color=orange><b>[PUDDLE]</b> Player ENTROU na tinta! Slow aplicado.</color>");
                }
                
                _playerInside = true;
            }
        }

        private void ApplyPoison(Collider other)
        {
            var pStatus = other.GetComponentInParent<PlayerPoisonStatus>();
            if (pStatus == null) 
            {
                pStatus = other.transform.root.gameObject.AddComponent<PlayerPoisonStatus>();
            }
            
            pStatus.Apply(_poisonDps, _poisonDuration, Color.green, 0.1f);
        }

        private void RemoveSlow()
        {
            if (!_playerInside) return;
            var run = RunSession.Instance?.Run;
            if (run != null)
            {
                if (Mathf.Abs(slowAmount) > 0.01f)
                {
                    run.ModifySpeed(-slowAmount);
                    Debug.Log("<color=green><b>[PUDDLE]</b> Player SAIU da tinta! Velocidade restaurada.</color>");
                }
                _playerInside = false;
            }
        }

        private void OnDestroy()
        {
            RemoveSlow();
        }
    }
}



