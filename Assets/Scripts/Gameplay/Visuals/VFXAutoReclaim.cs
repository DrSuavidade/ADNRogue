using UnityEngine;
using Geneforge.Core.Pooling;
using System.Collections;

namespace Geneforge.Gameplay.Visuals
{
    /// <summary>
    /// Script simples para garantir que GameObjects (VFX, modelos temporários, etc.)
    /// sejam devolvidos ao pool automaticamente após um tempo definido.
    /// Limpa ParticleSystems e Trails no OnEnable para evitar efeitos "esticados" (Pooling artifacts).
    /// </summary>
    public class VFXAutoReclaim : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Tempo em segundos antes de desaparecer / voltar ao pool.")]
        public float duration = 2.0f;
        
        private PoolIdentifier _poolId;
        private Coroutine _reclaimCo;
        private TrailRenderer[] _trails;
        private ParticleSystem[] _particles;

        private void Awake()
        {
            _poolId = GetComponent<PoolIdentifier>();
            // Cache components to be efficient
            _trails = GetComponentsInChildren<TrailRenderer>();
            _particles = GetComponentsInChildren<ParticleSystem>();
        }

        private void OnEnable()
        {
            // CRITICAL: Clear previous data to avoid the "stretched" look from previous pool usage
            ClearVisuals();
            
            // Inicia sempre a contagem ao ativar o objeto
            StartReclaimTimer(duration);
        }

        private void ClearVisuals()
        {
            if (_trails != null)
            {
                foreach (var tr in _trails) tr.Clear();
            }
            if (_particles != null)
            {
                foreach (var ps in _particles) ps.Clear();
            }
        }

        /// <summary>
        /// Reinicia o temporizador de reclaim. Pode ser chamado via código para mudar o tempo via spawn.
        /// </summary>
        public void StartReclaimTimer(float time)
        {
            if (_reclaimCo != null) StopCoroutine(_reclaimCo);
            _reclaimCo = StartCoroutine(ReclaimRoutine(time));
        }

        private IEnumerator ReclaimRoutine(float delay)
        {
            yield return Geneforge.Core.Utils.WaitCache.Get(delay);
            
            if (PoolManager.Instance != null && _poolId != null)
            {
                PoolManager.Instance.Reclaim(gameObject);
            }
            else
            {
                // Se não estiver no sistema de pooling, apenas destrói
                if (gameObject.activeInHierarchy)
                    Destroy(gameObject);
            }
        }
    }
}
