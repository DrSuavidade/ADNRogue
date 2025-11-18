using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class KeyPickup : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (DungeonMapManager.Instance != null)
                DungeonMapManager.Instance.NotifyPlayerPickedUpKey();

            // TODO: play SFX/VFX here
            Destroy(gameObject);
        }
    }
}
