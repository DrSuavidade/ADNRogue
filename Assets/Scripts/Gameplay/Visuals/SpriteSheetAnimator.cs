using UnityEngine;
using System.Collections;

namespace Geneforge.Gameplay.Visuals
{
    /// <summary>
    /// Professional frame-based animator with procedural juice (scaling, pulse, flash).
    /// </summary>
    public class SpriteSheetAnimator : MonoBehaviour
    {
        public enum AnimationMode { Billboard, Floor, Horizontal }
        
        [Header("Settings")]
        public AnimationMode mode = AnimationMode.Billboard;
        public bool loop = true;
        
        [Header("Juice (Professional Polish)")]
        public bool useSpawnScale = true;
        public bool usePulse = false;
        [ColorUsage(true, true)] public Color tintColor = Color.white;
        
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps;
        private float _timer;
        private int _currentIndex;
        private Vector3 _baseScale;
        private MaterialPropertyBlock _propBlock;

        public void Initialize(Sprite[] frames, float fps, AnimationMode animationMode)
        {
            _sr = GetComponent<SpriteRenderer>();
            
            if (_sr == null)
            {
                GameObject child = new GameObject("SpriteAnimation");
                child.transform.SetParent(this.transform, false);
                _sr = child.AddComponent<SpriteRenderer>();
            }
            
            _frames = frames;
            _fps = fps;
            mode = animationMode;
            _timer = 0f;
            _currentIndex = 0;
            _baseScale = transform.localScale;

            if (_frames != null && _frames.Length > 0 && _sr != null)
            {
                _sr.sprite = _frames[0];
                ApplyJuiceTint(tintColor);
            }

            if (useSpawnScale)
            {
                StopAllCoroutines();
                StartCoroutine(SpawnScaleRoutine());
            }
        }

        public void SetTintColor(Color color)
        {
            tintColor = color;
            ApplyJuiceTint(color);
        }

        private void ApplyJuiceTint(Color color)
        {
            if (_sr == null) return;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            
            _sr.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_Color", color);
            _propBlock.SetColor("_BaseColor", color); 
            _sr.SetPropertyBlock(_propBlock);
        }

        private IEnumerator SpawnScaleRoutine()
        {
            transform.localScale = Vector3.zero;
            float t = 0;
            while (t < 1.0f)
            {
                t += Time.deltaTime * 5f; // Velocidade do surgimento
                // Curva de "Overshoot" (escala passa um pouco e volta)
                float s = -4 * t * t + 4 * t; 
                float bounce = Mathf.Sin(t * Mathf.PI * 1.25f);
                transform.localScale = _baseScale * Mathf.Lerp(0, 1.1f, t);
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;

            // Animação de Frames
            _timer += Time.deltaTime;
            float frameDuration = 1f / _fps;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                if (!loop && _currentIndex >= _frames.Length - 1)
                {
                    // Mantém o último frame ou desativa
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % _frames.Length;
                    _sr.sprite = _frames[_currentIndex];
                }
            }

            // Animação Procedural (Pulse)
            if (usePulse)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.05f;
                transform.localScale = _baseScale * pulse;
            }
        }

        private void LateUpdate()
        {
            if (mode == AnimationMode.Billboard)
            {
                if (Camera.main != null)
                {
                    transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                     Camera.main.transform.rotation * Vector3.up);
                }
            }
            else if (mode == AnimationMode.Floor)
            {
                transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
            }
            else if (mode == AnimationMode.Horizontal)
            {
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>
        /// Flash branco rápido para dar impacto (Impact Juice).
        /// </summary>
        public void Flash(float duration = 0.1f)
        {
            StartCoroutine(FlashRoutine(duration));
        }

        private IEnumerator FlashRoutine(float duration)
        {
            ApplyJuiceTint(Color.white * 10f); // HDR White
            yield return new WaitForSeconds(duration);
            ApplyJuiceTint(tintColor);
        }
    }
}

