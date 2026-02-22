using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class WinePuddle : MonoBehaviour
    {
        public float lifetime = 5f;
        public float slowAmount = -0.5f; // -50% speed
        public Color puddleColor = new Color(0.5f, 0, 0, 0.7f); // Deep Wine Red

        private bool _playerInside = false;

        public void Init()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var propBlock = new MaterialPropertyBlock();
                propBlock.SetColor("_Color", puddleColor);
                propBlock.SetColor("_BaseColor", puddleColor);
                renderer.SetPropertyBlock(propBlock);
            }

            transform.localScale = Vector3.one * 0.1f;
            // Simple scale up effect instead of LeanTween
            StartCoroutine(ScaleUp());

            Destroy(gameObject, lifetime);
        }

        private System.Collections.IEnumerator ScaleUp()
        {
            float elapsed = 0f;
            float duration = 0.5f;
            Vector3 targetScale = new Vector3(2.5f, 0.1f, 2.5f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, targetScale, elapsed / duration);
                yield return null;
            }
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
                    run.ModifySpeed(-slowAmount); // Restore speed
                    _playerInside = false;
                }
            }
        }

        private void OnDestroy()
        {
            // Ensure speed is restored if puddle disappears while player is inside
            if (_playerInside)
            {
                var run = RunSession.Instance?.Run;
                run?.ModifySpeed(-slowAmount);
            }
        }
    }
}
