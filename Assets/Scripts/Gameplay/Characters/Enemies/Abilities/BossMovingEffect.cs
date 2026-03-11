using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossMovingEffect : MonoBehaviour
    {
        [Header("Movement")]
        public float speed = 12f;
        public float duration = 0.7f;
        
        [Header("Orientation")]
        [Tooltip("Se for um Sprite ou efeito que fica no chão, deixe marcado. Se for um modelo 3D 'em pé', desmarque.")]
        public bool flatOnGround = true;
        
        [Tooltip("Use para corrigir a rotação se o visual sair de lado (ex: Y = 90).")]
        public Vector3 rotationOffset = Vector3.zero;

        private Vector3 _moveDir;
        private bool _isInitialized = false;

        public void Init(Vector3 direction, float? s = null, float? d = null)
        {
            // Calculamos a direção final e ignoramos o Y para não voar nem enterrar no chão
            _moveDir = new Vector3(direction.x, 0, direction.z).normalized;
            if (_moveDir.sqrMagnitude < 0.01f) _moveDir = transform.forward;

            if (s.HasValue) speed = s.Value;
            if (d.HasValue) duration = d.Value;

            // Resolve o problema de 'vai sempre no mesmo sentido' e 'deformado'
            if (_moveDir != Vector3.zero)
            {
                if (flatOnGround)
                {
                    // Alinhamento para efeitos 2D de chão (como ondas de impacto)
                    float angle = Mathf.Atan2(_moveDir.x, _moveDir.z) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(90 + rotationOffset.x, angle + rotationOffset.y, rotationOffset.z);
                }
                else
                {
                    // Alinhamento para projéteis 3D (Z para frente)
                    transform.rotation = Quaternion.LookRotation(_moveDir) * Quaternion.Euler(rotationOffset);
                }
            }

            _isInitialized = true;
            
            // Debug para você ver a seta no Unity Editor
            Debug.DrawRay(transform.position, _moveDir * 3f, Color.red, 2f);
        }

        private void Update()
        {
            if (!_isInitialized) return;
            
            // Move na direção calculada no Init, independente de como o Boss rodar depois
            transform.position += _moveDir * speed * Time.deltaTime;
        }
    }
}
