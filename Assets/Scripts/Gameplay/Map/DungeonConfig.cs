using System;
using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    [Serializable]
    public class TimelineRoomSet
    {
        public TimelineId timelineId;
        [Tooltip("How many floors this timeline has (Prehistoric=1, Roman=1, Present=2, Future=3).")]
        public int floors = 1;

        [Header("Prefabs (SE-oriented where applicable)")]
        public GameObject hubPrefab;

        [Tooltip("Combat rooms used for the four diagonal slots (NE, SE, SW, NW). Oriented to SE by default.")]
        public List<GameObject> combatRoomsSE = new List<GameObject>();

        [Tooltip("Reserved for shops on East/West openings.")]
        public List<GameObject> shopRoomsSE = new List<GameObject>();

        [Tooltip("Reserved for special events on East/West openings.")]
        public List<GameObject> eventRoomsSE = new List<GameObject>();
    }

    [CreateAssetMenu(menuName = "Geneforge/Map/DungeonConfig", fileName = "DungeonConfig")]
    public class DungeonConfig : ScriptableObject
    {
        [SerializeField] private List<TimelineRoomSet> timelines = new List<TimelineRoomSet>();

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
