using UnityEngine;
using UnityEngine.SceneManagement;
using Geneforge.Gameplay.Map;

namespace Geneforge.Gameplay.Progression
{
    public class RunFlowController : MonoBehaviour
    {
        public static RunFlowController Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string dungeonSceneName = "Dungeon";

        [SerializeField] private string prehistoricBossSceneName = "Boss_Prehistoric";
        [SerializeField] private string romanBossSceneName = "Boss_Roman";
        [SerializeField] private string presentBossSceneName = "Boss_Present";
        [SerializeField] private string futureBossSceneName = "Boss_Future";

        public string DungeonSceneName => dungeonSceneName;
        public string PrehistoricBossSceneName => prehistoricBossSceneName;
        public string RomanBossSceneName => romanBossSceneName;
        public string PresentBossSceneName => presentBossSceneName;
        public string FutureBossSceneName => futureBossSceneName;


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

        public void StartNewRun()
        {
            RunState.CurrentTimeline = TimelineId.Prehistoric;
            RunState.HasTimelineOverride = true;
            SafeLoadScene(dungeonSceneName);
        }

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

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"RunFlowController: Scene '{sceneName}' is not in build settings or cannot be loaded.");
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneName));
        }

        System.Collections.IEnumerator LoadSceneAsync(string sceneName)
        {
            // TODO: show loading screen UI here if you want
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone)
                yield return null;
            // TODO: hide loading UI here
        }

        private string GetBossSceneName(TimelineId t)
        {
            switch (t)
            {
                case TimelineId.Prehistoric: return prehistoricBossSceneName;
                case TimelineId.Roman: return romanBossSceneName;
                case TimelineId.Present: return presentBossSceneName;
                case TimelineId.Future: return futureBossSceneName;
                default: return null;
            }
        }

        private TimelineId GetNextTimeline(TimelineId t)
        {
            switch (t)
            {
                case TimelineId.Prehistoric: return TimelineId.Roman;
                case TimelineId.Roman: return TimelineId.Present;
                case TimelineId.Present: return TimelineId.Future;
                case TimelineId.Future: return TimelineId.Future;
                default: return t;
            }
        }
    }
}
