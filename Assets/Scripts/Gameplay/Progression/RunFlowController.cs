using UnityEngine;
using UnityEngine.SceneManagement;
using Geneforge.Gameplay.Map;

namespace Geneforge.Gameplay.Progression
{
    public class RunFlowController : MonoBehaviour
    {
        public static RunFlowController Instance { get; private set; }

        [Header("Scene Names")]
        public string dungeonSceneName = "Dungeon";

        public string prehistoricBossSceneName = "Boss_Prehistoric";
        public string romanBossSceneName      = "Boss_Roman";
        public string presentBossSceneName    = "Boss_Present";
        public string futureBossSceneName     = "Boss_Future";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Optional: call this from your main menu button.
        public void StartNewRun()
        {
            RunState.CurrentTimeline = TimelineId.Prehistoric;
            RunState.HasTimelineOverride = true;
            SafeLoadScene(dungeonSceneName);
        }

        // Hook this to DungeonMapManager.onBossStairsUsed in the dungeon scene.
        public void OnBossStairsUsed()
        {
            var mgr = DungeonMapManager.Instance;
            TimelineId timeline = mgr != null ? mgr.CurrentTimeline : RunState.CurrentTimeline;

            string bossScene = GetBossSceneName(timeline);
            if (string.IsNullOrEmpty(bossScene))
            {
                GoToNextTimeline();
                return;
            }

            SafeLoadScene(bossScene);
        }

        // Call this from the boss when it dies.
        public void OnBossDefeated()
        {
            GoToNextTimeline();
        }

        private void GoToNextTimeline()
        {
            var mgr = DungeonMapManager.Instance;
            TimelineId current = mgr != null ? mgr.CurrentTimeline : RunState.CurrentTimeline;
            TimelineId next = GetNextTimeline(current);

            RunState.CurrentTimeline = next;
            RunState.HasTimelineOverride = true;

            SafeLoadScene(dungeonSceneName);
        }

        private void SafeLoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("RunFlowController.SafeLoadScene called with null/empty sceneName.");
                return;
            }

            if (!SceneManager.GetSceneByName(sceneName).IsValid() &&
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"RunFlowController: Scene '{sceneName}' is not in build settings or cannot be loaded.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }


        private string GetBossSceneName(TimelineId t)
        {
            switch (t)
            {
                case TimelineId.Prehistoric: return prehistoricBossSceneName;
                case TimelineId.Roman:       return romanBossSceneName;
                case TimelineId.Present:     return presentBossSceneName;
                case TimelineId.Future:      return futureBossSceneName;
                default:                     return null;
            }
        }

        private TimelineId GetNextTimeline(TimelineId t)
        {
            switch (t)
            {
                case TimelineId.Prehistoric: return TimelineId.Roman;
                case TimelineId.Roman:       return TimelineId.Present;
                case TimelineId.Present:     return TimelineId.Future;
                case TimelineId.Future:      return TimelineId.Future; // or end run / credits
                default:                     return t;
            }
        }
    }
}
