using UnityEngine;
using Geneforge.Core.Stats;

namespace Geneforge.Gameplay.Progression
{
    /// <summary>
    /// Persistent run container. This is the single source of truth for run-level state.
    /// Lives/Gold/DNA/Rolls should live here (via RunStats), not on scene-instantiated player objects.
    /// </summary>
    public class RunSession : MonoBehaviour
    {
        public static RunSession Instance { get; private set; }

        public RunStats Run { get; private set; }
        public EssenceProgression EssenceProgression { get; private set; }

        public bool IsRunActive { get; private set; }

        public static RunSession Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("RunSession");
            Instance = go.AddComponent<RunSession>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure required components exist ON THIS persistent object.
            Run = GetComponent<RunStats>();
            if (Run == null) Run = gameObject.AddComponent<RunStats>();

            EssenceProgression = GetComponent<EssenceProgression>();
            if (EssenceProgression == null) EssenceProgression = gameObject.AddComponent<EssenceProgression>();
        }

        public void BeginRun(MetaStats meta)
        {
            IsRunActive = true;

            // 1) reset run-level stats/resources
            Run.ResetRunStats();

            // 2) reset run-level progression unlocks (spent DNA nodes etc)
            EssenceProgression.ResetAll();

            // 3) apply meta effects (starting lives, etc)
            meta?.OnRunStart(Run);
        }

        public void EndRun(MetaStats meta, bool survived)
        {
            if (!IsRunActive) return;

            IsRunActive = false;
            meta?.OnRunEnd(Run, survived);
        }
    }
}
