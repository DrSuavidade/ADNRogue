using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Pooling;
using System.Collections;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    /// <summary>
    /// Base partilhada para as habilidades dos inimigos Roman.
    /// Faz cache do EnemyCore, Transform próprio, alvo (player) e PlayerHealth.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class RomanEnemyAbilityBase : MonoBehaviour
    {
        [Header("VFX Pooling")]
        [Tooltip("Arraste aqui o prefab 'VFX_Generic_Poolable'")]
        public GameObject vfxGenericPrefab;

        protected EnemyCore enemy;
        protected Transform self;
        protected Transform target;
        protected PlayerHealth playerHealth;

        protected virtual void Awake()
        {
            enemy = GetComponent<EnemyCore>();
            self = transform;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        protected bool IsPlayerInRange(float range)
        {
            if (!target) return false;

            Vector3 a = self.position;
            Vector3 b = target.position;
            a.y = b.y = 0f;

            return Vector3.Distance(a, b) <= range;
        }

        /// <summary>
        /// Helper simples: se o player estiver em range, aplica dano directo.
        /// </summary>
        protected void DealDamageToPlayer(float damage, float range)
        {
            if (playerHealth == null || !IsPlayerInRange(range)) return;
            playerHealth.ApplyDamage(damage);
        }

        /// <summary>
        /// Wrapper público para o SpawnVFXLayer.
        /// </summary>
        public GameObject SpawnVFXLayer_Public(string vfxName, Vector3 pos, Vector3 scale, Sprite[] frames, float fps, Color color, float scaleMult = 1f, float rotationRange = 0f, float fadeStart = 0.7f, bool pulse = false, Transform parent = null, Visuals.SpriteSheetAnimator.AnimationMode animationMode = Visuals.SpriteSheetAnimator.AnimationMode.Billboard, bool loop = false)
        {
            return SpawnVFXLayer(vfxName, pos, scale, frames, fps, color, scaleMult, rotationRange, fadeStart, pulse, parent, animationMode, loop);
        }

        /// <summary>
        /// Cria uma camada de VFX profissional usando pooling e animação procedural.
        /// </summary>
        protected GameObject SpawnVFXLayer(string vfxName, Vector3 pos, Vector3 scale, Sprite[] frames, float fps, Color color, float scaleMult = 1f, float rotationRange = 0f, float fadeStart = 0.7f, bool pulse = false, Transform parent = null, Visuals.SpriteSheetAnimator.AnimationMode animationMode = Visuals.SpriteSheetAnimator.AnimationMode.Billboard, bool loop = false, bool useSpawnScale = true)
        {
            if (frames == null || frames.Length == 0) return null;

            GameObject vfx = null;

            if (PoolManager.Instance != null && vfxGenericPrefab != null)
            {
                vfx = PoolManager.Instance.Spawn(vfxGenericPrefab, pos, Quaternion.identity, null);
                vfx.name = vfxName;
                vfx.transform.localScale = scale;
                if (parent != null) vfx.transform.SetParent(parent, true);
            }
            else
            {
                vfx = new GameObject(vfxName);
                vfx.transform.position = pos;
                vfx.transform.localScale = scale;
                if (parent != null) vfx.transform.SetParent(parent);
                vfx.AddComponent<SpriteRenderer>();
            }

            var sr = vfx.GetComponent<SpriteRenderer>();
            sr.sortingOrder = (animationMode == Visuals.SpriteSheetAnimator.AnimationMode.Floor) ? 50 : 55; 

            var animator = vfx.GetComponent<Visuals.SpriteSheetAnimator>();
            if (animator == null) animator = vfx.AddComponent<Visuals.SpriteSheetAnimator>();
            
            animator.tintColor = color;
            animator.useSpawnScale = useSpawnScale;
            animator.usePulse = pulse;
            animator.useFadeOut = !loop; 
            animator.fadeStartTime = fadeStart;
            animator.scaleMultiplier = Vector3.one * scaleMult;
            animator.randomRotationRange = rotationRange;
            animator.loop = loop;

            animator.Initialize(frames, fps, animationMode);
            
            return vfx;
        }
    }
}
