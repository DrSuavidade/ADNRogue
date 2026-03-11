using UnityEngine;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Visuals
{
    /// <summary>
    /// Detecta quando um ParticleSystem termina de rodar e o devolve ao pool automaticamente.
    /// Funciona com prefabs 3D e sistemas de partículas complexos.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class VFXParticleAutoReclaim : MonoBehaviour
    {
        private ParticleSystem _ps;
        private PoolIdentifier _poolId;
        private bool _isInitialized;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _poolId = GetComponent<PoolIdentifier>();
        }

        private void OnEnable()
        {
            _isInitialized = true;
        }

        private void Update()
        {
            // Se o sistema de partículas parou de emitir e não tem mais partículas vivas
            if (_isInitialized && _ps != null && !_ps.IsAlive(true))
            {
                Reclaim();
            }
        }

        private void Reclaim()
        {
            _isInitialized = false;
            if (PoolManager.Instance != null && _poolId != null)
            {
                PoolManager.Instance.Reclaim(gameObject);
            }
            else
            {
                // Fallback caso não esteja no PoolManager
                if (gameObject.activeInHierarchy)
                    Destroy(gameObject);
            }
        }
    }
}
