using System;
using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class MinimapManager : MonoBehaviour
    {
        public static MinimapManager Instance { get; private set; }

        public event Action<RoomInstance> RoomDiscovered;
        public event Action<RoomInstance> RoomVisited;

        private HashSet<Guid> discoveredRooms = new HashSet<Guid>();
        private HashSet<Guid> visitedRooms = new HashSet<Guid>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ReportRoomDiscovery(RoomInstance room)
        {
            if (room == null) return;
            if (discoveredRooms.Add(room.RoomGuid))
            {
                RoomDiscovered?.Invoke(room);
            }
        }

        public void ReportRoomVisit(RoomInstance room)
        {
            if (room == null) return;
            
            // Re-ensure it is discovered
            ReportRoomDiscovery(room);

            if (visitedRooms.Add(room.RoomGuid))
            {
                RoomVisited?.Invoke(room);
            }
        }

        public bool IsRoomDiscovered(Guid guid) => discoveredRooms.Contains(guid);
        public bool IsRoomVisited(Guid guid) => visitedRooms.Contains(guid);

        public void ClearData()
        {
            discoveredRooms.Clear();
            visitedRooms.Clear();
        }
    }
}
