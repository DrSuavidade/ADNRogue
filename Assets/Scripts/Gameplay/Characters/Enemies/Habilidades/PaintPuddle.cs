using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Progression;
using Geneforge.Gameplay.Visuals;
using Geneforge.Core.Pooling;
using System.Collections;
using Geneforge.Gameplay.Characters.Enemies.Eras.Roman;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintPuddle : MonoBehaviour
    {
        [Header("Settings")]
        public float lifetime = 20f;
        public float slowAmount = -0.6f;
        
        [Header("Hades Style Juice")]
        public bool useGlowLayer = true;
        public float pulseSpeed = 2f;
        public float pulseAmount = 0.05f;

        private float _poisonDps;
        private float _poisonDuration;
        private bool _playerInside = false;
        private GameObject _glowLayer;
        private Vector3 _baseScale;
        private float _timeActive;

        private Coroutine _lifetimeCoroutine;

        private void OnEnable()
        {
            _playerInside = false;
            _timeActive = 0f;
            _isReturningToPool = false;
            // Delay starting the lifetime until Init is called, 
            // but have a fallback in case Init is never called
            if (_lifetimeCoroutine != null) StopCoroutine(_lifetimeCoroutine);
            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(lifetime));
        }

        private bool _isReturningToPool = false;

        private void OnDisable()
        {
            RemoveSlow();
            if (!_isReturningToPool) CleanupGlow();
        }

        private IEnumerator LifetimeRoutine(float delay)
        {
            yield return Geneforge.Core.Utils.WaitCache.Get(delay);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            _isReturningToPool = true;
            CleanupGlow();
            if (PoolManager.Instance != null && GetComponent<PoolIdentifier>() != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }

        private void CleanupGlow()
        {
            if (_glowLayer != null)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Reclaim(_glowLayer);
                else Destroy(_glowLayer);
                _glowLayer = null;
            }
        }

        public void Init(Color color, Sprite[] frames = null, float fps = 10f, Vector3 scale = default, float rotationY = 0f, float poisonDps = 0f, float poisonDuration = 0f, float duration = -1f)
        {
            // Note: frames and fps are kept for signature compatibility but ignored if null.
            // In a full refactor, these would be removed. Given 'trocar tudo', we'll just ignore them.
            if (duration > 0) lifetime = duration;
            if (scale == default) scale = Vector3.one;
            _baseScale = scale;
            _poisonDps = poisonDps;
            _poisonDuration = poisonDuration;

            // Restart lifetime with corrected value
            if (_lifetimeCoroutine != null) StopCoroutine(_lifetimeCoroutine);
            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(lifetime));

            transform.rotation = Quaternion.Euler(90f, rotationY, 0f);
            
            if (frames != null && frames.Length > 0)
            {
                var animator = GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = gameObject.AddComponent<SpriteSheetAnimator>();
                
                animator.useSpawnScale = false;
                animator.usePulse = false;
                animator.scaleMultiplier = Vector3.one;
                animator.tintColor = color;
                animator.useFadeOut = true;
                animator.loop = true;
                animator.fadeStartTime = 0.85f;
                animator.Initialize(frames, fps, SpriteSheetAnimator.AnimationMode.Floor, lifetime);
            }

            if (useGlowLayer && _glowLayer == null)
            {
                // We'll skip glow layer if it relies on sprites being passed in, 
                // or assume it's part of the prefab.
            }

            StopCoroutine("PopInRoutine");
            StartCoroutine(PopInRoutine());
        }

        private void SpawnGlowLayer(Color color, Sprite baseSprite, Vector3 scale)
        {
            var drunkScript = GetComponentInParent<RomanDrunk>();
            GameObject prefab = (drunkScript != null) ? drunkScript.vfxGenericPrefab : null;
            
            if (prefab == null) {
                var painter = FindFirstObjectByType<RomanPainter>();
                if (painter) prefab = painter.vfxGenericPrefab;
            }

            if (PoolManager.Instance != null && prefab != null)
            {
                _glowLayer = PoolManager.Instance.Spawn(prefab, transform.position - Vector3.up * 0.01f, transform.rotation, null);
                _glowLayer.name = "Puddle_Glow_Layer";

                var gAnim = _glowLayer.GetComponent<SpriteSheetAnimator>();
                if (gAnim == null) gAnim = _glowLayer.AddComponent<SpriteSheetAnimator>();
                
                Color gColor = color;
                gColor.a = 0.35f;
                
                gAnim.tintColor = gColor * 2.5f;
                gAnim.useFadeOut = true;
                gAnim.fadeStartTime = 0.85f;
                gAnim.loop = true;
                gAnim.Initialize(new Sprite[] { baseSprite }, 1, SpriteSheetAnimator.AnimationMode.Floor, lifetime);
            }
        }

        private IEnumerator PopInRoutine()
        {
            float t = 0f;
            while (t < 1f)
            {
                // Slowed down pop-in (from 4f to 2f)
                t += Time.deltaTime * 2f; 
                float s = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.05f;
                if (t > 0.8f) s = Mathf.Lerp(s, 1.0f, (t-0.8f)*5f);
                
                // We don't set scale here anymore, we let Update do it using a pop multiplier
                _popScaleMult = s;
                yield return null;
            }
            _popScaleMult = 1f;
        }

        private float _popScaleMult = 0f;

        private void Update()
        {
            _timeActive += Time.deltaTime;
            
            float pulse = 1f + Mathf.Sin(_timeActive * pulseSpeed) * pulseAmount;
            Vector3 finalScale = _baseScale * pulse * _popScaleMult;
            transform.localScale = finalScale;

            if (_glowLayer != null)
            {
                _glowLayer.transform.localScale = finalScale * 1.3f;
                _glowLayer.transform.position = transform.position - Vector3.up * 0.01f;
            }
        }

        private PlayerPoisonStatus _cachedStatus;

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                ApplySlow();
                // Cache immediately on enter
                _cachedStatus = other.GetComponentInParent<PlayerPoisonStatus>();
                if (_cachedStatus == null)
                    _cachedStatus = other.transform.root.gameObject.AddComponent<PlayerPoisonStatus>();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
            {
                if (!_playerInside) ApplySlow();
                
                if (_poisonDps > 0)
                {
                    // Use cached status if possible
                    if (_cachedStatus != null)
                    {
                        _cachedStatus.Apply(_poisonDps, _poisonDuration, Color.green, 0.1f);
                    }
                    else
                    {
                        // Fallback in case of re-parenting or other edge cases
                        ApplyPoison(other);
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                RemoveSlow();
                _cachedStatus = null;
            }
        }

        private bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") || other.gameObject.layer == 3;
        }

        private void ApplySlow()
        {
            if (_playerInside) return;
            var run = RunSession.Instance?.Run;
            if (run != null)
            {
                if (Mathf.Abs(slowAmount) > 0.01f) run.ModifySpeed(slowAmount);
                _playerInside = true;
            }
        }

        private void ApplyPoison(Collider other)
        {
            var pStatus = other.GetComponentInParent<PlayerPoisonStatus>();
            if (pStatus == null) pStatus = other.transform.root.gameObject.AddComponent<PlayerPoisonStatus>();
            pStatus.Apply(_poisonDps, _poisonDuration, Color.green, 0.1f);
        }

        private void RemoveSlow()
        {
            if (!_playerInside) return;
            var run = RunSession.Instance?.Run;
            if (run != null)
            {
                if (Mathf.Abs(slowAmount) > 0.01f) run.ModifySpeed(-slowAmount);
                _playerInside = false;
            }
        }
    }
}



