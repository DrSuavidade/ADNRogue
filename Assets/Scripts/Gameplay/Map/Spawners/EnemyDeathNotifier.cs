using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    /// <summary>
    /// Lightweight bridge: call ReportDeath() from your enemy's death logic,
    /// and it will notify the owning EnemySpawner/RoomInstance.
    /// </summary>
    public class EnemyDeathNotifier : MonoBehaviour
    {
        [HideInInspector, SerializeField] private EnemySpawner ownerSpawner;
        bool _reported;
        private static bool _isQuitting = false;

        public EnemySpawner OwnerSpawner
        {
            get => ownerSpawner;
            set => ownerSpawner = value;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        public void ReportDeath()
        {
            if (_reported || _isQuitting) return;
            _reported = true;

            Debug.Log($"[EnemyDeathNotifier] Death reported from {gameObject.name}", this);

            if (ownerSpawner != null)
            {
                ownerSpawner.NotifyEnemyDied();
            }
        }

        private void OnDestroy()
        {
            // Only report if we aren't quitting the app/scene
            if (!_isQuitting)
            {
                ReportDeath();
            }
        }
    }
}

