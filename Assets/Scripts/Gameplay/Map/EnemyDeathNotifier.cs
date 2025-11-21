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

        public void ReportDeath()
        {
            if (ownerSpawner != null)
            {
                ownerSpawner.NotifyEnemyDied();
            }
        }

        private void OnDestroy()
        {
            // Still supports the simple "Destroy(enemy)" case.
            ReportDeath();
        }
    }
}
