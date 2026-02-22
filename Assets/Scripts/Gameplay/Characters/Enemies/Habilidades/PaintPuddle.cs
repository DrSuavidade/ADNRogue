using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintPuddle : MonoBehaviour
    {
        public float lifetime = 4f;
        public float slowAmount = -0.3f; // -30% speed (um pouco menos que o Drunk)
        
        private bool _playerInside = false;

        private void Start()
        {
            // Garante que o objeto se destrói mesmo que o Init() falhe por algum motivo
            Destroy(gameObject, lifetime);
        }

        public void Init(Color color)
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

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var run = RunSession.Instance?.Run;
                if (run != null && !_playerInside)
                {
                    run.ModifySpeed(slowAmount);
                    _playerInside = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var run = RunSession.Instance?.Run;
                if (run != null && _playerInside)
                {
                    run.ModifySpeed(-slowAmount);
                    _playerInside = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerInside)
            {
                RunSession.Instance?.Run?.ModifySpeed(-slowAmount);
            }
        }
    }
}
