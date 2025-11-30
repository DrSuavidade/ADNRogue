using UnityEngine;
using System;

namespace Geneforge.Core.Stats
{
    public class RunStats : MonoBehaviour
    {
        [Header("Health & Lives")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private int startingLives = 3;

        [Header("Run Currency & Resources")]
        [Tooltip("Spendable during this dive")]
        [SerializeField] private int startingCurrency = 0;
        [Tooltip("DNA Fragments collected this run")]
        [SerializeField] private int startingDnaSplices = 0;
        [Tooltip("Number of rerolls/reshuffles you have this run")]
        [SerializeField] private int startingRolls = 1;

        public float MaxHP => maxHP;
        public int BaseStartingLives => startingLives;
        public float CurrentHP { get; private set; }

        public int Lives
        {
            get => lives;
            set
            {
                int clamped = Mathf.Max(0, value);
                if (lives == clamped) return;
                lives = clamped;
                OnLivesChanged?.Invoke(lives);
            }
        }

        public int Currency
        {
            get => currency;
            private set
            {
                int clamped = Mathf.Max(0, value);
                if (currency == clamped) return;
                currency = clamped;
                OnCurrencyChanged?.Invoke(currency);
            }
        }

        public int DnaSplices
        {
            get => dnaSplices;
            private set
            {
                int clamped = Mathf.Max(0, value);
                if (dnaSplices == clamped) return;
                dnaSplices = clamped;
                OnDnaSplicesChanged?.Invoke(dnaSplices);
            }
        }

        public int Rolls
        {
            get => rolls;
            private set
            {
                int clamped = Mathf.Max(0, value);
                if (rolls == clamped) return;
                rolls = clamped;
                OnRollsChanged?.Invoke(rolls);
            }
        }

        public event Action<float, float> OnHealthChanged;
        public event Action OnPlayerDeath;
        public event Action<int> OnLivesChanged;
        public event Action<int> OnCurrencyChanged;
        public event Action<int> OnDnaSplicesChanged;
        public event Action<int> OnRollsChanged;

        int lives;
        int currency;
        int dnaSplices;
        int rolls;

        void Awake()
        {
            ResetRunStats();
        }

        public void ResetRunStats()
        {
            CurrentHP = Mathf.Max(1f, maxHP);
            lives = startingLives;
            currency = startingCurrency;
            dnaSplices = startingDnaSplices;
            rolls = startingRolls;

            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            OnLivesChanged?.Invoke(lives);
            OnCurrencyChanged?.Invoke(currency);
            OnDnaSplicesChanged?.Invoke(dnaSplices);
            OnRollsChanged?.Invoke(rolls);
        }

        public bool TakeDamage(float dmg)
        {
            if (dmg <= 0f || CurrentHP <= 0f) return false;

            float prev = CurrentHP;
            CurrentHP = Mathf.Max(0f, CurrentHP - dmg);

            if (!Mathf.Approximately(prev, CurrentHP))
                OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0f)
            {
                OnPlayerDeath?.Invoke();
                return true;
            }
            return false;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || CurrentHP <= 0f) return;

            float prev = CurrentHP;
            CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);

            if (!Mathf.Approximately(prev, CurrentHP))
                OnHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

        public bool SpendCurrency(int amount)
        {
            if (amount == 0) return true;
            if (amount < 0)
            {
                Debug.LogError($"SpendCurrency called with negative amount {amount}.", this);
                return false;
            }
            if (Currency < amount) return false;

            Currency -= amount;
            return true;
        }

        public void AddCurrency(int amount)
        {
            if (amount == 0) return;
            if (amount < 0)
            {
                Debug.LogError($"AddCurrency called with negative amount {amount}. Use SpendCurrency instead.", this);
                return;
            }
            Currency += amount;
        }

        public bool SpendDnaSplices(int amount)
        {
            if (amount == 0) return true;
            if (amount < 0)
            {
                Debug.LogError($"SpendDnaSplices called with negative amount {amount}.", this);
                return false;
            }
            if (DnaSplices < amount) return false;

            DnaSplices -= amount;
            return true;
        }

        public void AddDnaSplices(int amount)
        {
            if (amount == 0) return;
            if (amount < 0)
            {
                Debug.LogError($"AddDnaSplices called with negative amount {amount}. Use SpendDnaSplices instead.", this);
                return;
            }
            DnaSplices += amount;
        }

        public bool UseRoll()
        {
            if (Rolls <= 0) return false;
            Rolls--;
            return true;
        }
    }
}
