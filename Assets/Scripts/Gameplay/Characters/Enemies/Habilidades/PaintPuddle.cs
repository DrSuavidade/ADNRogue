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
        
        private float _poisonDps;
        private float _poisonDuration;
        private bool _playerInside = false;

        private void Start()
        {
            Debug.Log($"<color=cyan>[PUDDLE]</color> Poça de tinta criada em {transform.position}");
            Destroy(gameObject, lifetime);
        }

        public void Init(Color color, Sprite[] frames = null, float fps = 10f, float scale = 2f, float poisonDps = 0f, float poisonDuration = 0f)
        {
            _poisonDps = poisonDps;
            _poisonDuration = poisonDuration;
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



