using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    /// <summary>
    /// Lightweight bridge: whenever this enemy is destroyed, it notifies its spawner.
    /// </summary>
    public class EnemyDeathNotifier : MonoBehaviour
    {
        [HideInInspector] public EnemySpawner ownerSpawner;

        private void OnDestroy()
        {
            if (ownerSpawner != null)
            {
                ownerSpawner.NotifyEnemyDied();
            }
        }
    }
}
