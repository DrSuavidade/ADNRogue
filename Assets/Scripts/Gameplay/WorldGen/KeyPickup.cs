// Assets/Scripts/Gameplay/WorldGen/KeyPickup.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    [RequireComponent(typeof(Collider))]
    public class KeyPickup : MonoBehaviour
    {
        void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (KeyManager.I) KeyManager.I.PickupKey();
            Destroy(gameObject);
        }
    }
}
