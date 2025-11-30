using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    /// <summary>
    /// Lightweight bridge: call ReportDeath() from your enemy's death logic,
    /// and it will notify the owning EnemySpawner/RoomInstance.
    /// </summary>
    public class EnemyDeathNotifier : MonoBehaviour
    {
        [HideInInspector] public EnemySpawner ownerSpawner;
        bool _reported;

        public void ReportDeath()
        {
            if (_reported) return;
            _reported = true;
            if (ownerSpawner != null)
            {
                ownerSpawner.NotifyEnemyDied();
            }
        }

        private void OnDestroy()
        {
            ReportDeath();
        }
    }
}
