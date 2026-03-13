using UnityEngine;
using UnityEngine.VFX; // Adicionado para suporte ao VFX Graph


namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossMeleeSlash : MonoBehaviour
    {
        private float _startAngle;
        private float _endAngle;
        private float _baseEulerY;
        private float _duration = 0.8f; // Mais lento para favorecer dodge
        private float _elapsed = 0f;
        private SpriteRenderer _sr;
        private VisualEffect _vfx;
        private float _damage;
        private bool _hasDealtDamage = false;

        public void Init(float radius, bool flipDirection, float damage = 10f, float duration = 0.8f)
        {
            _duration = duration;
            _damage = damage;
            _sr = GetComponent<SpriteRenderer>();
            _vfx = GetComponentInChildren<VisualEffect>();

            // 1. Centralizar e resetar o VFX
            if (_vfx != null)
            {
                _vfx.transform.localPosition = Vector3.zero;
                _vfx.transform.localRotation = Quaternion.identity; 
                _vfx.Play();
            }

            // 2. Posicionamento
            transform.localPosition += transform.forward * 0.5f;
            Vector3 worldPos = transform.position;
            worldPos.y = 0.1f; 
            transform.position = worldPos;

            // 3. Rotação (Sweep de 120 graus)
            _baseEulerY = transform.eulerAngles.y;
            
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

            UpdateRotation(0);
            Destroy(gameObject, _duration + 0.2f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);

            UpdateRotation(progress);

            // Sincroniza o progresso visual do VFX Graph (visto no screenshot)
            if (_vfx != null)
            {
                _vfx.SetFloat("SlashProgress", progress);
            }

            // Mantém a altura fixa
            Vector3 pos = transform.position;
            pos.y = 0.1f;
            transform.position = pos;
        }

        private void UpdateRotation(float progress)
        {
            float relativeAngle = Mathf.Lerp(_startAngle, _endAngle, progress);
            transform.rotation = Quaternion.Euler(0, _baseEulerY + relativeAngle, 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Se você quiser que o dano aconteça apenas uma vez por ataque
            if (_hasDealtDamage) return;

            // Altere para a tag do seu Player ou componente de vida
            if (other.CompareTag("Player"))
            {
                var health = other.GetComponent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
                if (health != null)
                {
                    health.ApplyDamage(_damage);
                    _hasDealtDamage = true;
                }
            }
        }
    }
}
