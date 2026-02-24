using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class WinePuddle : MonoBehaviour
    {
        public float lifetime = 5f;
        public Color puddleColor = new Color(0.5f, 0, 0, 0.7f); // Deep Wine Red

        private float _poisonDps;
        private float _poisonDuration;
        private bool _playerInside = false;

        public void Init(Sprite[] frames = null, float fps = 10f, float scale = 2.5f, float dps = 2f, float duration = 3f)
        {
            _poisonDps = dps;
            _poisonDuration = duration;

            Debug.Log($"<color=purple>[WINE]</color> Inicializando poça com POISON. DPS: {dps}, Duração: {duration}");

            if (frames != null && frames.Length > 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (r != null && !(r is SpriteRenderer)) r.enabled = false;
                }

                var animator = GetComponent<Visuals.SpriteSheetAnimator>();
                if (animator == null) animator = gameObject.AddComponent<Visuals.SpriteSheetAnimator>();
                
                animator.Initialize(frames, fps, Visuals.SpriteSheetAnimator.AnimationMode.Floor);
                transform.localScale = Vector3.one * scale;

                var sr = animator.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) 
                {
                    sr.color = puddleColor;
                    sr.sortingOrder = 5;
                }
            }
            else
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
                StartCoroutine(ScaleUp(scale));
            }

            Destroy(gameObject, lifetime);
        }

        private System.Collections.IEnumerator ScaleUp(float targetScaleVal)
        {
            float elapsed = 0f;
            float duration = 0.5f;
            Vector3 targetScale = new Vector3(targetScaleVal, 0.1f, targetScaleVal);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, targetScale, elapsed / duration);
                yield return null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                ApplyPoison(other);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
            {
                // No caso do poison, aplicamos continuamente enquanto estiver na poça 
                // para renovar a duração
                ApplyPoison(other);
            }
        }

        private bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") || 
                   other.GetComponentInParent<PlayerHealth>() != null || 
                   other.gameObject.layer == 3;
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
    }
}
