using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class HubRoom : RoomInstance
    {
        [Header("Hub Anchors")]
        [Tooltip("Where the player is spawned when coming from the south entrance.")]
        [SerializeField] private Transform southEntrySpawn;

        [Tooltip("Anchor for the north exit / stairs.")]
        [SerializeField] private Transform northExitAnchor;

        public Transform SouthEntrySpawn => southEntrySpawn;
        public Transform NorthExitAnchor => northExitAnchor;

    }
}
