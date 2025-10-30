// Assets/Scripts/Gameplay/WorldGen/TimelineGenerator.cs
using UnityEngine;
using System.Collections.Generic;

namespace Geneforge.Gameplay.WorldGen
{
    public class TimelineGenerator : MonoBehaviour
    {
        [Header("Config")]
        public TimelineConfig config;
        public int seed = 0;                     // 0 = random

        [Header("Key")]
        public GameObject keyPrefab;             // prefab with KeyPickup (+ trigger collider)
        public Vector3   keyLocalOffset = new Vector3(0, 0.5f, 0);

        [Header("Rewards")]
        public RewardTable rewardTable;          // weights for this era
        public bool rollRewardAtStart = true;    // Hades-style telegraph
        public RewardSpawner rewardSpawner;      // scene object that knows how to spawn pickups
        public Vector3 rewardLocalOffset = new Vector3(0, 0.25f, 0);

        // runtime
        System.Random _rng;
        readonly Dictionary<Dir8, GameObject> _rooms = new();
        readonly Dictionary<Dir8, RoomController> _controllers = new();
        RoomController _firstCompleted = null;
        Dir8? _keyRoomDir = null;
        GameObject _hub;

        void Awake()
        {
            if (!KeyManager.I) new GameObject("KeyManager").AddComponent<KeyManager>();
            Generate(seed);
        }

        public void Generate(int useSeed)
        {
            ClearRuntime();

            seed = (useSeed == 0) ? Random.Range(int.MinValue, int.MaxValue) : useSeed;
            _rng = new System.Random(seed);

            // Hub
            if (!config || !config.hubTemplate || !config.hubTemplate.prefab)
            {
                Debug.LogError("TimelineGenerator: Missing hub template.");
                return;
            }

            _hub = Instantiate(config.hubTemplate.prefab, transform.position, Quaternion.identity, transform);
            _hub.name = $"Hub_{config.era}";

            // Four diagonals
            var diagonals = new[] { Dir8.NorthEast, Dir8.NorthWest, Dir8.SouthEast, Dir8.SouthWest };
            foreach (var d in diagonals)
            {
                var tmpl = PickDiagonalTemplate();
                var g = d.ToGrid();
                var pos = transform.position + new Vector3(g.x, 0f, g.y) * config.hubToDiagonal; // <-- fix: Vector2Int -> Vector3
                var rot = d.ToRotation();

                var inst = Instantiate(tmpl.prefab, pos, rot, transform);
                inst.name = $"{d}_Room";

                WireDoors(_hub, inst, d); // hub <-> diagonal
                RegisterRoom(inst, RoomKind.Combat, d);
            }

            // Boss (North)
            if (config.bossTemplate && config.bossTemplate.prefab)
            {
                var pos = transform.position + Vector3.forward * config.hubToBoss;
                var boss = Instantiate(config.bossTemplate.prefab, pos, Quaternion.identity, transform);
                boss.name = "Boss_Room";

                WireDoors(_hub, boss, Dir8.North); // hub <-> boss

                // Lock hub's North door until key
                var lockDoor = _hub.AddComponent<LockedDoor>();
                lockDoor.doorway = FindDoor(_hub, Dir8.North);

                RegisterRoom(boss, RoomKind.Boss, Dir8.North);
            }
            else
            {
                Debug.LogWarning("TimelineGenerator: Boss template not set.");
            }
        }

        RoomTemplate8 PickDiagonalTemplate()
        {
            // For now: purely combat rooms; later you can mix pools or weights here.
            if (!config.combatPool)
            {
                Debug.LogError("TimelineGenerator: Combat pool not assigned.");
                return config.hubTemplate;
            }
            var required = new HashSet<Dir8>(); // if you want to require a door facing the hub, add it here.
            var t = config.combatPool.Pick(_rng, required);
            return t ?? config.hubTemplate;
        }

        void RegisterRoom(GameObject inst, RoomKind kind, Dir8 slot)
        {
            var rc = inst.GetComponent<RoomController>() ?? inst.AddComponent<RoomController>();
            rc.kind = kind;
            rc.OnVisited += HandleVisited;
            rc.OnCompleted += HandleCompleted;

            if (rollRewardAtStart && kind == RoomKind.Combat && rewardTable)
            {
                var rolled = rewardTable.Roll(_rng);
                rc.AssignReward(rolled);
                // TODO: (optional) show door icon on hub for this slot based on rolled reward.
            }

            _rooms[slot] = inst;
            _controllers[slot] = rc;
        }

        void HandleVisited(RoomController rc) { /* optional analytics */ }

        void HandleCompleted(RoomController rc)
        {
            // Rewards
            if (!rollRewardAtStart && rc.kind == RoomKind.Combat && rewardTable && !rc.rewardAssigned)
                rc.AssignReward(rewardTable.Roll(_rng));

            if (rewardSpawner && rc.rewardAssigned)
                rewardSpawner.Spawn(rc.reward, rc.transform, rewardLocalOffset);

            // Key spawning logic (never in the first completed room)
            if (_firstCompleted == null)
            {
                _firstCompleted = rc;

                var candidates = new List<Dir8>();
                foreach (var kv in _controllers)
                {
                    if (kv.Value == rc) continue;
                    if (kv.Key == Dir8.North) continue; // exclude boss
                    candidates.Add(kv.Key);
                }
                if (candidates.Count > 0)
                    _keyRoomDir = candidates[_rng.Next(candidates.Count)];

                TrySpawnKeyNow();
            }
            else
            {
                TrySpawnKeyNow();
            }
        }

        void TrySpawnKeyNow()
        {
            if (_keyRoomDir == null || KeyManager.I == null || KeyManager.I.HasKey) return;
            var dir = _keyRoomDir.Value;

            if (!_rooms.TryGetValue(dir, out var room)) return;

            // Spawn a simple key pickup at the chosen room's center + offset.
            if (keyPrefab)
            {
                var key = Instantiate(keyPrefab, room.transform);
                key.transform.localPosition = keyLocalOffset;
                if (!key.GetComponent<KeyPickup>()) key.AddComponent<KeyPickup>();
            }
            else
            {
                // Fallback: visible sphere + trigger + KeyPickup
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(room.transform, false);
                sphere.transform.localPosition = keyLocalOffset;
                sphere.transform.localScale = Vector3.one * 0.5f;
                Destroy(sphere.GetComponent<Collider>());
                var trigger = sphere.AddComponent<SphereCollider>(); trigger.isTrigger = true;
                sphere.AddComponent<KeyPickup>();
            }

            _keyRoomDir = null; // prevent double spawn
        }

        void WireDoors(GameObject a, GameObject b, Dir8 fromHubToB)
        {
            var da = FindDoor(a, fromHubToB);
            var db = FindDoor(b, fromHubToB.Opposite());
            if (da) da.SetOpen(true);
            if (db) db.SetOpen(true);
        }

        Doorway8 FindDoor(GameObject root, Dir8 dir)
        {
            var doors = root.GetComponentsInChildren<Doorway8>(true);
            foreach (var d in doors) if (d.direction == dir) return d;
            return null;
        }

        void ClearRuntime()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _rooms.Clear();
            _controllers.Clear();
            _hub = null;
            _firstCompleted = null;
            _keyRoomDir = null;

            if (KeyManager.I) KeyManager.I.ResetKey();
        }
    }
}
