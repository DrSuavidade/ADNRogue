// Assets/Scripts/Gameplay/WorldGen/TimelineConfig.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    public enum Era { BaseHub, Prehistoric, Romanic, Present, Future }

    [CreateAssetMenu(menuName = "Geneforge/WorldGen/Timeline Config")]
    public class TimelineConfig : ScriptableObject
    {
        public Era era;

        [Header("Prefabs & Pools")]
        public RoomTemplate8 hubTemplate;   // spherical hub prefab for this era (kind = Hub)
        public RoomPool8 combatPool;
        public RoomTemplate8 bossTemplate;

        [Header("Layout Distances")]
        public float hubToDiagonal = 25f;   // NE/NW/SE/SW offset
        public float hubToBoss = 30f;       // North offset

        [Header("Floors")]
        public bool secondFloor = false;    // (future) enable a second floor
    }
}
