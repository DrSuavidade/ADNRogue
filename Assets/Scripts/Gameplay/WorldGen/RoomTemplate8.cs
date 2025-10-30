// Assets/Scripts/Gameplay/WorldGen/RoomTemplate8.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    [CreateAssetMenu(menuName = "Geneforge/WorldGen/Room Template (8-way)")]
    public class RoomTemplate8 : ScriptableObject
    {
        public RoomKind kind;
        public GameObject prefab;

        [Header("Supported Doors (tick the ones this prefab contains)")]
        public bool N, NE, E, SE, S, SW, W, NW;

        public bool Supports(Dir8 d) => d switch
        {
            Dir8.North => N, Dir8.NorthEast => NE, Dir8.East => E, Dir8.SouthEast => SE,
            Dir8.South => S, Dir8.SouthWest => SW, Dir8.West => W, Dir8.NorthWest => NW, _ => false
        };
    }
}
