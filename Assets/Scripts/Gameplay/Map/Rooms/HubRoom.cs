using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class HubRoom : RoomInstance
    {
        [Header("Hub Anchors")]
        [Tooltip("Where the player appears when entering this hub from the south stairs.")]
        public Transform southEntrySpawn;

        [Tooltip("Anchor used for positioning/locating the north exit (stairs).")]
        public Transform northExitAnchor;
    }
}
