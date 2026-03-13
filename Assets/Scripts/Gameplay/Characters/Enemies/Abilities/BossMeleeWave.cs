using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossMeleeWave : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool useInternalLifeCycle = false; 
        [SerializeField] private float speedMultiplier = 0.4f; // Expansão lenta e pesada para ser fácil de ler

        private float _targetRadius;
        private float _duration = 0.5f; 
        private float _elapsed = 0f;
        private SpriteRenderer _sr;
        private float _startAlpha = 1.0f;
        private float _damage;
        private bool _hasDealtDamage = false;
        private Animator _anim;
        private UnityEngine.VFX.VisualEffect _vfx;

        public void Init(float radius, int strikeIndex = 1, float speedMult = 1.0f, float damage = 10f)
        {
            float finalSpeed = speedMultiplier * speedMult;
            _targetRadius = radius;
            _damage = damage;
            _sr = GetComponent<SpriteRenderer>();
            _anim = GetComponentInChildren<Animator>();
            _vfx = GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            var ps = GetComponentInChildren<ParticleSystem>();

            if (_vfx != null || _anim != null || ps != null) useInternalLifeCycle = true;

            if (ps != null)
            {
                var main = ps.main;
                main.simulationSpeed *= finalSpeed;
                _duration = main.duration / finalSpeed;
            }
            else
            {
                // Ondas super snappies para obrigar a reação rápida
                _duration = (strikeIndex == 3 ? 0.4f : 0.25f) / finalSpeed;
                _startAlpha = strikeIndex == 3 ? 1.0f : 0.8f;
            }

            if (!useInternalLifeCycle)
            {
                transform.rotation = Quaternion.Euler(90, 0, 0);
                transform.localScale = Vector3.zero;
            }
            else if (_vfx != null)
            {
                _vfx.Play();
            }

            Destroy(gameObject, _duration + 0.1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);

            if (!useInternalLifeCycle)
            {
                float currentScale = progress * (_targetRadius * 2.0f);
                transform.localScale = new Vector3(currentScale, currentScale, currentScale);
            }

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = _startAlpha * (1.0f - progress);
                _sr.color = c;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasDealtDamage) return;

            if (other.CompareTag("Player"))
            {
                var health = other.GetComponent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
                if (health != null)
                {
                    health.ApplyDamage(_damage);
                    _hasDealtDamage = true; // Só leva dano uma vez por cada onda
                }
            }
        }
    }
}
