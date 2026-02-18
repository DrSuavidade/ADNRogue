using UnityEngine;
using UnityEngine.SceneManagement;
using Geneforge.Gameplay.Map;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Items;

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

        [Header("Run End Scenes")]
        [SerializeField] private string runCompleteSceneName = "MainMenu";
        [SerializeField] private string runFailedSceneName = "MainMenu";


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            RunSession.Ensure();
            Geneforge.Core.UI.SceneTransitionManager.Ensure();
            DontDestroyOnLoad(gameObject);
        }

        public void StartNewRun()
        {
            // Ensure persistent systems exist
            RunSession.Ensure();

            // Clear any saved items from previous runs
            if (RunPersistenceManager.Instance != null)
            {
                RunPersistenceManager.Instance.ClearRun();
                RunPersistenceManager.Instance.CurrentTimeline = TimelineId.Prehistoric;
            }

            // Begin run BEFORE loading gameplay scenes
            var meta = MetaStats.Instance != null
                ? MetaStats.Instance
                : FindAnyObjectByType<MetaStats>();

            RunSession.Instance.BeginRun(meta);

            SafeLoadScene(dungeonSceneName);
        }


        public void OnBossStairsUsed()
        {
            TimelineId timeline = RunPersistenceManager.Instance.CurrentTimeline;

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
            TimelineId current = RunPersistenceManager.Instance.CurrentTimeline;

            // Final boss (Future) completes the run
            if (current == TimelineId.Future)
            {
                EndRun(survived: true);
                return;
            }

            GoToNextTimeline();
        }


        private void GoToNextTimeline()
        {
            TimelineId current = RunPersistenceManager.Instance.CurrentTimeline;
            TimelineId next = GetNextTimeline(current);

            Debug.Log($"[RunFlow] Advancing timeline from {current} to {next}");
            RunPersistenceManager.Instance.CurrentTimeline = next;

            SafeLoadScene(dungeonSceneName);
        }

        private AsyncOperation preloadedOperation;
        private string preloadedSceneName;

        public void PreloadScene(string sceneName)
        {
            if (preloadedSceneName == sceneName && preloadedOperation != null) return;
            
            preloadedSceneName = sceneName;
            preloadedOperation = SceneManager.LoadSceneAsync(sceneName);
            preloadedOperation.allowSceneActivation = false;
            Debug.Log($"[RunFlow] Preloading scene: {sceneName}");
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

            StartCoroutine(LoadSceneSequence(sceneName));
        }

        private System.Collections.IEnumerator LoadSceneSequence(string sceneName)
        {
            if (Geneforge.Core.UI.SceneTransitionManager.Instance != null)
            {
                yield return Geneforge.Core.UI.SceneTransitionManager.Instance.FadeOut(0.2f);
            }

            if (preloadedOperation != null && preloadedSceneName == sceneName)
            {
                preloadedOperation.allowSceneActivation = true;
                while (!preloadedOperation.isDone)
                    yield return null;
                
                preloadedOperation = null;
                preloadedSceneName = null;
            }
            else
            {
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (!op.isDone)
                    yield return null;
            }

            // Small delay to allow new scene's Start() and Awake() to run or initialize
            yield return null;

            if (Geneforge.Core.UI.SceneTransitionManager.Instance != null)
            {
                yield return Geneforge.Core.UI.SceneTransitionManager.Instance.FadeIn(0.2f);
            }
        }

        public void EndRun(bool survived)
        {
            var meta = Geneforge.Core.Stats.MetaStats.Instance != null
                ? Geneforge.Core.Stats.MetaStats.Instance
                : FindAnyObjectByType<Geneforge.Core.Stats.MetaStats>();

            if (RunSession.Instance != null)
                RunSession.Instance.EndRun(meta, survived);
            
            // Clear items so they don't carry over to next run
            if (RunPersistenceManager.Instance != null)
                RunPersistenceManager.Instance.ClearRun();

            SafeLoadScene(survived ? runCompleteSceneName : runFailedSceneName);
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
