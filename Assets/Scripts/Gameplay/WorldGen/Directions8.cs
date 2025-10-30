// Assets/Scripts/Gameplay/WorldGen/Directions8.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    public enum Dir8 { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

    public static class Dir8Util
    {
        public static Vector2Int ToGrid(this Dir8 d) => d switch
        {
            Dir8.North     => new Vector2Int(0, 1),
            Dir8.NorthEast => new Vector2Int(1, 1),
            Dir8.East      => new Vector2Int(1, 0),
            Dir8.SouthEast => new Vector2Int(1,-1),
            Dir8.South     => new Vector2Int(0,-1),
            Dir8.SouthWest => new Vector2Int(-1,-1),
            Dir8.West      => new Vector2Int(-1,0),
            _              => new Vector2Int(-1,1),
        };

        public static Dir8 Opposite(this Dir8 d) => d switch
        {
            Dir8.North     => Dir8.South,
            Dir8.NorthEast => Dir8.SouthWest,
            Dir8.East      => Dir8.West,
            Dir8.SouthEast => Dir8.NorthWest,
            Dir8.South     => Dir8.North,
            Dir8.SouthWest => Dir8.NorthEast,
            Dir8.West      => Dir8.East,
            _              => Dir8.SouthEast,
        };

        public static Quaternion ToRotation(this Dir8 d)
        {
            float deg = d switch
            {
                Dir8.North     => 0f,
                Dir8.NorthEast => 45f,
                Dir8.East      => 90f,
                Dir8.SouthEast => 135f,
                Dir8.South     => 180f,
                Dir8.SouthWest => 225f,
                Dir8.West      => 270f,
                _              => 315f,
            };
            return Quaternion.Euler(0f, deg, 0f);
        }
    }
}
