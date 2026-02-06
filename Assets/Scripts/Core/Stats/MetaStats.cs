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

        public int StartingLives 
        { 
            get => startingLives;
            set 
            {
                if (startingLives == value) return;
                startingLives = value;
                OnStartingLivesChanged?.Invoke(startingLives);
            }
        }
        public int Essence => essence;
        public int TotalDnaSplices => totalDnaSplices;
        
        public event Action<int> OnEssenceChanged;
        public event Action<int> OnTotalDnaSplicesChanged;
        public event Action<int> OnStartingLivesChanged;

        public int BankedDnaSplices => TotalDnaSplices;


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
            
            // Sync values to run: Start the run with the current meta-stats as copies
            run.Lives = StartingLives;
            run.DnaSplices = TotalDnaSplices;
            run.Essence = Essence;
        }

        public void OnRunEnd(RunStats run, bool survived)
        {
            if (run == null) return;

            if (survived)
            {
                int oldEssence = essence;
                int oldTotal = totalDnaSplices;

                // Gold (run.Currency) is converted to essence at the end of the run
                // We overwrite with the run values to persist any changes (spending/earning) made during the run
                essence = run.Essence + Mathf.Max(0, run.Currency);
                totalDnaSplices = run.DnaSplices;

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

        public bool SpendDnaSplices(int amount)
        {
            if (amount <= 0) return false;
            if (TotalDnaSplices < amount) return false;

            int oldTotal = totalDnaSplices;
            totalDnaSplices -= amount;

            if (totalDnaSplices != oldTotal)
                OnTotalDnaSplicesChanged?.Invoke(TotalDnaSplices);
            
            return true;
        }

        public void AddEssence(int amount)
        {
            if (amount <= 0) return;
            essence += amount;
            OnEssenceChanged?.Invoke(essence);
        }

        public void AddDnaSplices(int amount)
        {
            if (amount <= 0) return;
            totalDnaSplices += amount;
            OnTotalDnaSplicesChanged?.Invoke(totalDnaSplices);
        }
    }
}
