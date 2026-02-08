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

        [Header("Hub Tunnels")]
        [Tooltip("Tunnel on the South-West diagonal.")]
        [SerializeField] private Transform tunnelSW;
        [Tooltip("Tunnel on the South-East diagonal.")]
        [SerializeField] private Transform tunnelSE;
        [Tooltip("Tunnel on the North-East diagonal.")]
        [SerializeField] private Transform tunnelNE;
        [Tooltip("Tunnel on the North-West diagonal.")]
        [SerializeField] private Transform tunnelNW;

        public Transform SouthEntrySpawn => southEntrySpawn;
        public Transform NorthExitAnchor => northExitAnchor;

        /// <summary>
        /// Returns the hub tunnel transform for the given diagonal direction.
        /// Returns null for non-diagonal directions.
        /// </summary>
        public Transform GetTunnelForDirection(RoomDirection dir)
        {
            switch (dir)
            {
                case RoomDirection.SouthWest: return tunnelSW;
                case RoomDirection.SouthEast: return tunnelSE;
                case RoomDirection.NorthEast: return tunnelNE;
                case RoomDirection.NorthWest: return tunnelNW;
                default: return null;
            }
        }
    }
}
