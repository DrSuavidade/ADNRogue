using UnityEngine;

namespace Geneforge.Core.Stats
{
    public class MetaStats : MonoBehaviour
    {
        public static MetaStats I { get; private set; }

        [Header("Progression")]
        [Tooltip("Lives carried into each new run")]
        [SerializeField] private int startingLives = 3;
        [Tooltip("Earned between runs")]
        [SerializeField] private int essence = 0;
        [Tooltip("Total DNA Fragments banked")]
        [SerializeField] private int totalDnaSplices = 0;

        public int StartingLives => startingLives;
        public int Essence => essence;
        public int TotalDnaSplices => totalDnaSplices;

        void Awake()
        {
            if (I != null && I != this)
            {
                Debug.LogError($"Duplicate MetaStats on {name}, destroying this instance.");
                Destroy(gameObject);
                return;
            }

            I = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OnRunStart(RunStats run)
        {
            if (run == null)
            {
                Debug.LogWarning("MetaStats.OnRunStart called with null RunStats.");
                return;
            }

            // Ensure at least this many lives; respects any higher base from RunStats.
            run.Lives = Mathf.Max(run.Lives, startingLives);
        }

        public void OnRunEnd(RunStats run, bool survived)
        {
            if (run == null)
            {
                Debug.LogWarning("MetaStats.OnRunEnd called with null RunStats.");
                return;
            }

            if (survived)
            {
                essence += run.Currency;        // reward for completing dive
                totalDnaSplices += run.DnaSplices;  // bank your fragments
            }
            else
            {
                // maybe penalty: lose some essence?
            }
        }

        public bool SpendEssence(int amount)
        {
            if (amount == 0) return true;
            if (amount < 0)
            {
                Debug.LogError($"MetaStats.SpendEssence called with negative amount {amount}.");
                return false;
            }
            if (essence < amount) return false;

            essence -= amount;
            return true;
        }
    }
}
