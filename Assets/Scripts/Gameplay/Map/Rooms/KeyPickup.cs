using UnityEngine;
using UnityEngine.Events;

namespace Geneforge.Gameplay.Map
{
    public class KeyPickup : MonoBehaviour
    {
        [Header("Optional FX hooks")]
        [SerializeField] private UnityEvent onPickedUp;
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (DungeonMapManager.Instance != null)
                DungeonMapManager.Instance.NotifyPlayerPickedUpKey();

            onPickedUp?.Invoke();

            Destroy(gameObject);
        }
    }
}
