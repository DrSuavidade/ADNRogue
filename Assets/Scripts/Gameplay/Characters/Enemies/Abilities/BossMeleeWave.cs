using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossMeleeWave : MonoBehaviour
    {
        private float _targetRadius;
        private float _duration = 0.5f;
        private float _elapsed = 0f;
        private SpriteRenderer _sr;
        private float _startAlpha = 1.0f;

        public void Init(float radius, int strikeIndex = 1)
        {
            _targetRadius = radius;
            _sr = GetComponent<SpriteRenderer>();

            // Strike 1 & 2: Fast, snappy pulses (0.3s)
            // Strike 3: Slower, more impactful slammed wave (0.6s)
            if (strikeIndex < 3)
            {
                _duration = 0.3f;
                _startAlpha = 0.7f; // Lighter pulses
            }
            else
            {
                _duration = 0.6f;
                _startAlpha = 1.0f; // Stronger slam
            }

            // Force the wave to be flat on the ground
            transform.rotation = Quaternion.Euler(90, 0, 0);
            transform.localScale = Vector3.zero;

            // Cleanup after animation
            Destroy(gameObject, _duration + 0.1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);

            // Expand scale from 0 to full diameter
            float currentScale = progress * (_targetRadius * 2.0f);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            // Fade out alpha starting from strike-specific intensity
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = _startAlpha * (1.0f - progress);
                _sr.color = c;
            }
        }
    }
}
