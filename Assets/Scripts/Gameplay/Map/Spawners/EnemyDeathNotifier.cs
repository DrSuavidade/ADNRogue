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

        public EnemySpawner OwnerSpawner
        {
            get => ownerSpawner;
            set => ownerSpawner = value;
        }


        public void ReportDeath()
        {
            if (_reported) return;
            _reported = true;

            Debug.Log($"[EnemyDeathNotifier] Death reported from {gameObject.name}", this);

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
