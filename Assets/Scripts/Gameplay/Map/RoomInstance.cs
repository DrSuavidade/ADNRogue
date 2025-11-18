using System;
using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class RoomInstance : MonoBehaviour
    {
        [Header("Static (per prefab)")]
        public RoomType roomType = RoomType.Combat;

        [Tooltip("Optional: reward spawner in this room (must exist in combat rooms).")]
        public RewardSpawner rewardSpawner;

        [Tooltip("Optional: at least one enemy spawner in combat rooms.")]
        public MonoBehaviour[] enemySpawners;

        [Header("Runtime (filled by DungeonMapManager)")]
        public TimelineId timelineId;
        public int floorIndex;
        public RoomDirection directionFromHub;
        public int visitOrderGlobal = -1;
        public int visitOrderAmongDiagonals = -1;
        public bool isKeyRoom;

        public Guid RoomGuid { get; private set; }

        private void Awake()
        {
            RoomGuid = Guid.NewGuid();
        }

        public void Initialize(TimelineId timeline, int floor, RoomDirection dir, RoomType type)
        {
            timelineId = timeline;
            floorIndex = floor;
            directionFromHub = dir;
            roomType = type;
        }

        public void MarkAsKeyRoom()
        {
            isKeyRoom = true;
            if (rewardSpawner != null)
                rewardSpawner.ConfigureKeySpawn(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (DungeonMapManager.Instance != null)
                DungeonMapManager.Instance.HandleRoomEntered(this);
        }
    }
}
