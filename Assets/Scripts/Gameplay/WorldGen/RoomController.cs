// Assets/Scripts/Gameplay/WorldGen/RoomController.cs
using UnityEngine;
using System;

namespace Geneforge.Gameplay.WorldGen
{
    public enum RoomKind { Hub, Combat, Treasure, Shop, Event, Boss } // shared with templates

    public class RoomController : MonoBehaviour
    {
        public RoomKind kind;

        public bool visited { get; private set; }
        public bool completed { get; private set; }

        public RewardKind reward { get; private set; }
        public bool rewardAssigned { get; private set; }

        public event Action<RoomController> OnVisited;
        public event Action<RoomController> OnCompleted;

        public void AssignReward(RewardKind k) { reward = k; rewardAssigned = true; }

        // Call when player crosses the entrance trigger
        public void MarkVisited()
        {
            if (visited) return;
            visited = true;
            OnVisited?.Invoke(this);
        }

        // Call when all enemies/waves are cleared
        public void MarkCompleted()
        {
            if (completed) return;
            completed = true;
            OnCompleted?.Invoke(this);
        }
    }
}
