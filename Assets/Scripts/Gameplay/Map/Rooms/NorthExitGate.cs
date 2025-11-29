using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach this to the north exit / stairs collider inside the hub prefab.
/// </summary>
namespace Geneforge.Gameplay.Map
{
    public class NorthExitGate : MonoBehaviour
    {
        [Header("Optional FX hooks")]
        public UnityEvent onUseDenied;
        public UnityEvent onUseAcceptedNextFloor;
        public UnityEvent onUseAcceptedBoss;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (DungeonMapManager.Instance != null)
            {
                DungeonMapManager.Instance.TryUseNorthExit(this);
            }
        }

        public void OnUseDenied()
        {
            onUseDenied?.Invoke();
        }

        public void OnUseAcceptedNextFloor()
        {
            onUseAcceptedNextFloor?.Invoke();
        }

        public void OnUseAcceptedBoss()
        {
            onUseAcceptedBoss?.Invoke();
        }
    }
}