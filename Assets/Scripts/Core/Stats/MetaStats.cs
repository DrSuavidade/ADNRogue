using System;
using UnityEngine;

namespace Geneforge.Core.Stats
{
    public interface IMetaStats
    {
        int StartingLives { get; }
        int Essence { get; }
        int TotalDnaSplices { get; }

        bool SpendEssence(int amount);
        void OnRunStart(RunStats run);
        void OnRunEnd(RunStats run, bool survived);
    }


    public class MetaStats : MonoBehaviour, IMetaStats
    {

        public static MetaStats Instance { get; private set; }
        [Header("Starting values")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int startingEssence = 0;
        [SerializeField] private int startingTotalDnaSplices = 0;

        int essence;
        int totalDnaSplices;

        public int StartingLives => startingLives;
        public int Essence => essence;
        public int TotalDnaSplices => totalDnaSplices;
        public event Action<int> OnEssenceChanged;
        public event Action<int> OnTotalDnaSplicesChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MetaStats] Duplicate instance detected, destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            essence = Mathf.Max(0, startingEssence);
            totalDnaSplices = Mathf.Max(0, startingTotalDnaSplices);

            OnEssenceChanged?.Invoke(Essence);
            OnTotalDnaSplicesChanged?.Invoke(TotalDnaSplices);
        }

        public void OnRunStart(RunStats run)
        {
            if (run == null) return;
            run.Lives = Mathf.Max(run.Lives, StartingLives);
        }

        public void OnRunEnd(RunStats run, bool survived)
        {
            if (run == null) return;

            if (survived)
            {
                int oldEssence = essence;
                int oldTotal = totalDnaSplices;

                essence += Mathf.Max(0, run.Currency);
                totalDnaSplices += Mathf.Max(0, run.DnaSplices);

                if (essence != oldEssence)
                    OnEssenceChanged?.Invoke(Essence);
                if (totalDnaSplices != oldTotal)
                    OnTotalDnaSplicesChanged?.Invoke(TotalDnaSplices);
            }
        }

        public bool SpendEssence(int amount)
        {
            if (amount <= 0) return false;
            if (Essence < amount) return false;

            int oldEssence = essence;
            essence -= amount;

            if (essence != oldEssence)
                OnEssenceChanged?.Invoke(Essence);

            return true;
        }
    }
}
