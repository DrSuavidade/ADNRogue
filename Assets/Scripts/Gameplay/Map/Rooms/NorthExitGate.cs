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
        [SerializeField] private UnityEvent onUseDenied;
        [SerializeField] private UnityEvent onUseAcceptedNextFloor;
        [SerializeField] private UnityEvent onUseAcceptedBoss;

        public UnityEvent OnUseDeniedEvent => onUseDenied;
        public UnityEvent OnUseAcceptedNextFloorEvent => onUseAcceptedNextFloor;
        public UnityEvent OnUseAcceptedBossEvent => onUseAcceptedBoss;


        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (DungeonMapManager.Instance != null)
            {
                DungeonMapManager.Instance.TryUseNorthExit(this);
            }
        }

        public void OnUseDenied() { onUseDenied?.Invoke(); }
        public void OnUseAcceptedNextFloor() { onUseAcceptedNextFloor?.Invoke(); }
        public void OnUseAcceptedBoss() { onUseAcceptedBoss?.Invoke(); }

    }
}