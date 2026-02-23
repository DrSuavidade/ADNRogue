using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;
using Geneforge.Gameplay.Visuals;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintPuddle : MonoBehaviour
    {
        public float lifetime = 4f;
        public float slowAmount = -0.6f; // Aumentado para 60% para ser bem percetível
        
        private bool _playerInside = false;

        private void Start()
        {
            Debug.Log($"<color=cyan>[PUDDLE]</color> Poça de tinta criada em {transform.position}");
            Destroy(gameObject, lifetime);
        }

        public void Init(Color color, Sprite[] frames = null, float fps = 10f, float scale = 2f)
        {
            if (frames != null && frames.Length > 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (!(r is SpriteRenderer)) r.enabled = false;
                }

                var animator = GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = gameObject.AddComponent<SpriteSheetAnimator>();
                
                animator.Initialize(frames, fps, SpriteSheetAnimator.AnimationMode.Floor);
                
                transform.localScale = Vector3.one * scale;

                var sr = animator.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = color;
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
                transform.localScale = Vector3.one;
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
            if (!_playerInside && IsPlayer(other))
            {
                ApplySlow();
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
                run.ModifySpeed(slowAmount);
                _playerInside = true;
                Debug.Log("<color=orange><b>[PUDDLE]</b> Player ENTROU na tinta! Slow aplicado.</color>");
            }
        }

        private void RemoveSlow()
        {
            if (!_playerInside) return;
            var run = RunSession.Instance?.Run;
            if (run != null)
            {
                run.ModifySpeed(-slowAmount);
                _playerInside = false;
                Debug.Log("<color=green><b>[PUDDLE]</b> Player SAIU da tinta! Velocidade restaurada.</color>");
            }
        }

        private void OnDestroy()
        {
            RemoveSlow();
        }
    }
}



