using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public enum TimelineId
    {
        Prehistoric,
        Roman,
        Present,
        Future
    }

    public enum RoomType
    {
        Hub,
        Combat,
        Shop,
        Event
    }

    public enum RoomDirection
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    public static class RoomDirectionExtensions
    {
        /// <summary>
        /// Grid offset around the hub. N = (0,1), E = (1,0), etc.
        /// </summary>
        public static Vector2Int ToGridOffset(this RoomDirection dir)
        {
            switch (dir)
            {
                case RoomDirection.North: return new Vector2Int(0, 1);
                case RoomDirection.NorthEast: return new Vector2Int(1, 1);
                case RoomDirection.East: return new Vector2Int(1, 0);
                case RoomDirection.SouthEast: return new Vector2Int(1, -1);
                case RoomDirection.South: return new Vector2Int(0, -1);
                case RoomDirection.SouthWest: return new Vector2Int(-1, -1);
                case RoomDirection.West: return new Vector2Int(-1, 0);
                case RoomDirection.NorthWest: return new Vector2Int(-1, 1);
                default: return Vector2Int.zero;
            }
        }

        /// <summary>
        /// World-space yaw in degrees if North looks down +Z.
        /// </summary>
        public static float AngleFromNorth(this RoomDirection dir)
        {
            switch (dir)
            {
                case RoomDirection.North: return 0f;
                case RoomDirection.NorthEast: return 45f;
                case RoomDirection.East: return 90f;
                case RoomDirection.SouthEast: return 135f;
                case RoomDirection.South: return 180f;
                case RoomDirection.SouthWest: return 225f;
                case RoomDirection.West: return 270f;
                case RoomDirection.NorthWest: return 315f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Rotation that takes a prefab oriented to SouthEast and aligns it to the given direction.
        /// If your combat rooms are not actually facing SE by default, tweak baseDir.
        /// </summary>
        public static Quaternion RotationFromSE(this RoomDirection targetDir)
        {
            const RoomDirection baseDir = RoomDirection.SouthEast;
            float baseAngle = baseDir.AngleFromNorth();
            float targetAngle = targetDir.AngleFromNorth();
            float delta = targetAngle - baseAngle;
            return Quaternion.Euler(0f, delta + 180f, 0f);
        }

        public static bool IsDiagonal(this RoomDirection dir)
        {
            return dir == RoomDirection.NorthEast
                || dir == RoomDirection.SouthEast
                || dir == RoomDirection.SouthWest
                || dir == RoomDirection.NorthWest;
        }
    }
}
