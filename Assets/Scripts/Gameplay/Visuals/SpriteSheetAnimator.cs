using UnityEngine;
using System.Collections;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Visuals
{
    /// <summary>
    /// Professional frame-based animator with procedural juice (scaling, pulse, flash).
    /// Suporta Pooling automático (Auto-Reclaim).
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
        public bool useFadeOut = true;
        public float fadeStartTime = 0.7f; // % of life when fade starts
        public Vector3 scaleMultiplier = Vector3.one;
        public float randomRotationRange = 0f;
        [ColorUsage(true, true)] public Color tintColor = Color.white;
        
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps;
        private float _timer;
        private int _currentIndex;
        private Vector3 _baseScale;
        private MaterialPropertyBlock _propBlock;
        private float _normalizedLife = 0f;
        private float _totalElapsed = 0f; // Tempo total desde o spawn real
        private Coroutine _reclaimCooldown;
        private PoolIdentifier _poolId;
        private static Camera _mainCam;

        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
            {
                // Try to find in children first (in case of a complex prefab)
                _sr = GetComponentInChildren<SpriteRenderer>();

                if (_sr == null)
                {
                    // Conflict detection: MeshFilter and SpriteRenderer cannot coexist on the same GameObject in many Unity setups.
                    if (GetComponent<MeshFilter>() != null || GetComponent<MeshRenderer>() != null)
                    {
                        GameObject visualChild = new GameObject("SpriteSheet_Visual");
                        visualChild.transform.SetParent(transform, false);
                        _sr = visualChild.AddComponent<SpriteRenderer>();
                        Debug.Log($"[SpriteSheetAnimator] Mesh conflict on {name}. Created child for SpriteRenderer.");
                    }
                    else
                    {
                        _sr = gameObject.AddComponent<SpriteRenderer>();
                    }
                }
            }
            
            _poolId = GetComponent<PoolIdentifier>();
            if (_mainCam == null) _mainCam = Camera.main;
        }

        private float _overriddenDuration = -1f;

        public void Initialize(Sprite[] frames, float fps, AnimationMode animationMode, float customDuration = -1f)
        {
            // Reset state for pooling
            _frames = frames;
            _fps = fps;
            mode = animationMode;
            _overriddenDuration = customDuration;
            _timer = 0f;
            _currentIndex = 0;
            _normalizedLife = 0f;
            _totalElapsed = 0f;
            _baseScale = transform.localScale;

            if (_frames != null && _frames.Length > 0 && _sr != null)
            {
                _sr.sprite = _frames[0];
                ApplyJuiceTint(tintColor);
            }

            if (randomRotationRange > 0)
            {
                transform.rotation *= Quaternion.Euler(0, 0, Random.Range(-randomRotationRange, randomRotationRange));
            }

            // AUTO-RECLAIM LOGIC
            if (!loop)
            {
                float duration = _overriddenDuration > 0 ? _overriddenDuration : (frames != null ? frames.Length / (fps > 0 ? fps : 10f) : 1f);
                if (frames != null && frames.Length == 1 && _overriddenDuration <= 0) duration = 1.0f;
                
                if (_reclaimCooldown != null) StopCoroutine(_reclaimCooldown);
                _reclaimCooldown = StartCoroutine(AutoReclaimRoutine(duration + 0.2f));
            }
        }

        private IEnumerator AutoReclaimRoutine(float delay)
        {
            yield return Geneforge.Core.Utils.WaitCache.Get(delay);
            
            if (PoolManager.Instance != null && _poolId != null)
            {
                PoolManager.Instance.Reclaim(gameObject);
            }
            else if (!loop)
            {
                Destroy(gameObject);
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
            _propBlock.SetColor(ColorProp, color);
            _propBlock.SetColor(BaseColorProp, color); 
            _sr.SetPropertyBlock(_propBlock);
        }


        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;

            // Frame Animation
            _timer += Time.deltaTime;
            float frameDuration = 1f / (_fps > 0 ? _fps : 1f);
            float totalDuration = _overriddenDuration > 0 ? _overriddenDuration : (_frames.Length * frameDuration);
            
            float timeStep = Time.deltaTime / (totalDuration > 0 ? totalDuration : 1f);
            _normalizedLife += timeStep;
            _totalElapsed += Time.deltaTime;

            if (loop) _normalizedLife %= 1f;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                if (!loop && _currentIndex >= _frames.Length - 1)
                {
                    // Stay on last frame
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % _frames.Length;
                    _sr.sprite = _frames[_currentIndex];
                }
            }

            // Professional Fade Out
            if (useFadeOut && _normalizedLife > fadeStartTime)
            {
                float fadeT = (_normalizedLife - fadeStartTime) / (1f - fadeStartTime);
                Color c = tintColor;
                c.a *= (1f - fadeT);
                ApplyJuiceTint(c);
            }

            // Procedural Animation (Scaling Over Life)
            float scaleMod = 1f;
            if (usePulse)
            {
                scaleMod = 1f + Mathf.Sin(_totalElapsed * 5f) * 0.03f;
            }
            
            float spawnScaleMult = 1f;
            if (useSpawnScale && _totalElapsed < 0.3f)
            {
                spawnScaleMult = Mathf.Sin((_totalElapsed / 0.3f) * Mathf.PI * 0.5f);
            }

            Vector3 finalScale = Vector3.Scale(_baseScale, Vector3.Lerp(Vector3.one, scaleMultiplier, _normalizedLife)) * scaleMod * spawnScaleMult;
            transform.localScale = finalScale;
        }

        private void LateUpdate()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            if (mode == AnimationMode.Billboard)
            {
                transform.LookAt(transform.position + _mainCam.transform.rotation * Vector3.forward,
                                 _mainCam.transform.rotation * Vector3.up);
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


