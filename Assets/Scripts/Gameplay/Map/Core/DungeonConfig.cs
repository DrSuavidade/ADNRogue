using System;
using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Items;

namespace Geneforge.Gameplay.Map
{
    [Serializable]
    public class WeightedPrefab
    {
        public GameObject prefab;
        [Tooltip("Relative chance; values are normalized at runtime.")]
        public float weight = 1f;
    }

    [Serializable]
    public class TimelineRoomSet
    {
        public TimelineId timelineId;
        [Tooltip("How many floors this timeline has (Prehistoric=1, Roman=1, Present=2, Future=3).")]
        public int floors = 1;

        [Header("Prefabs (SE-oriented where applicable)")]
        public GameObject hubPrefab;

        [Tooltip("Weighted combat rooms for NE/SE/SW/NW (prefabs oriented to SE).")]
        public List<WeightedPrefab> combatRoomsSE = new List<WeightedPrefab>();

        [Tooltip("Reserved for shops on East/West openings.")]
        public List<WeightedPrefab> shopRoomsSE = new List<WeightedPrefab>();

        [Tooltip("Reserved for special events on East/West openings.")]
        public List<WeightedPrefab> eventRoomsSE = new List<WeightedPrefab>();

        [Header("Rewards (per timeline/floor)")]
        [Tooltip("Key pickup prefab used on every floor of this timeline (must have KeyPickup).")]
        public GameObject keyPickupPrefab;

        [Tooltip("Weighted non-key reward pool used on all floors of this timeline.")]
        public List<WeightedPrefab> floorRewardPrefabs = new List<WeightedPrefab>();

        [Header("Enemies")]
        [Tooltip("Weighted enemy prefabs used in combat rooms for this timeline.")]
        public List<WeightedPrefab> enemyPrefabs = new List<WeightedPrefab>();

        [Header("Reward Rarity Rates (%)")]
        [Range(0, 100)] public float commonRate = 70f;
        [Range(0, 100)] public float rareRate = 30f;
        [Range(0, 100)] public float epicRate = 0f;
        [Range(0, 100)] public float legendaryRate = 0f;
        [Range(0, 100)] public float mythicRate = 0f;

        [Header("Economy")]
        [Tooltip("Multiplier applied to stat pickups (Gold, Health, etc) in this timeline.")]
        public float statPickupMultiplier = 1f;
    }

    [CreateAssetMenu(menuName = "Geneforge/Map/DungeonConfig", fileName = "DungeonConfig")]
    public class DungeonConfig : ScriptableObject
    {
        [Header("Timelines")]
        [SerializeField] private List<TimelineRoomSet> timelines = new List<TimelineRoomSet>();

        [Header("Global Item Pool")]
        [Tooltip("All possible reward items in the game. These will be filtered by world/rarity.")]
        [SerializeField] private List<RewardItemData> globalRewardItemPool = new List<RewardItemData>();

        public List<RewardItemData> GlobalRewardItemPool => globalRewardItemPool;

        public TimelineRoomSet GetTimeline(TimelineId id)
        {
            for (int i = 0; i < timelines.Count; i++)
            {
                if (timelines[i].timelineId == id)
                    return timelines[i];
            }

            Debug.LogError($"DungeonConfig: No TimelineRoomSet configured for {id}");
            return null;
        }
    }
}
