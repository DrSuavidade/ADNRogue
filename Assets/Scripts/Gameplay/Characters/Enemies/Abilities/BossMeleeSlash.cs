using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossMeleeSlash : MonoBehaviour
    {
        private float _startAngle;
        private float _endAngle;
        private float _baseEulerY;
        private float _duration = 0.4f;
        private float _elapsed = 0f;
        private SpriteRenderer _sr;

        public void Init(float radius, bool flipDirection, float duration = 0.4f)
        {
            _duration = duration;
            _sr = GetComponent<SpriteRenderer>();

            // 1. Position: 0.5m in front of boss
            transform.localPosition += transform.forward * 0.5f;

            // 2. Rotation Setup: Sweep 120 degrees (-60 to +60 or vice-versa)
            // Capture the boss's current Y orientation as the center (0 degrees) of the arc
            _baseEulerY = transform.eulerAngles.y;
            
            // If flipDirection (Strike 2), go Left to Right (-60 to +60)
            // Else (Strike 1 & 3), go Right to Left (+60 to -60)
            if (flipDirection)
            {
                _startAngle = -60f;
                _endAngle = 60f;
            }
            else
            {
                _startAngle = 60f;
                _endAngle = -60f;
            }

            // Set initial rotation
            UpdateRotation(0);

            // 3. Scale: The "line" length matches the radius
            transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            Destroy(gameObject, _duration + 0.1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);

            UpdateRotation(progress);

            // Fade out
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = 1.0f - progress;
                _sr.color = c;
            }
        }

        private void UpdateRotation(float progress)
        {
            float relativeAngle = Mathf.Lerp(_startAngle, _endAngle, progress);
            // Apply rotation relative to the base orientation at spawn
            transform.rotation = Quaternion.Euler(90, _baseEulerY + relativeAngle, 0);
        }
    }
}
