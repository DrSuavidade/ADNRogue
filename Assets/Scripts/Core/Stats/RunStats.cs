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
        [SerializeField] private int startingGold = 0;
        [Tooltip("DNA Fragments collected this run")]
        [SerializeField] private int startingDnaSplices = 0;
        [Tooltip("Number of rerolls/reshuffles you have this run")]
        [SerializeField] private int startingRolls = 1;
        [Tooltip("Essence carried into the run")]
        [SerializeField] private int startingEssence = 0;

        public float MaxHP => maxHP;
        public int BaseStartingLives => startingLives;
        public float CurrentHP { get; private set; }

        public void IncreaseMaxHP(float amount)
        {
            if (amount == 0) return;
            maxHP += amount;
            // Optionally heal the amount increased so current HP stays proportional or just adds the buffer
            CurrentHP += amount; 
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

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
            set
            {
                int clamped = Mathf.Max(0, value);
                if (dnaSplices == clamped) return;
                dnaSplices = clamped;
                OnDnaSplicesChanged?.Invoke(dnaSplices);
            }
        }

        public int Essence
        {
            get => essence;
            set
            {
                int clamped = Mathf.Max(0, value);
                if (essence == clamped) return;
                essence = clamped;
                OnEssenceChanged?.Invoke(essence);
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

        public int Gold => Currency;
        public void AddGold(int amount) => AddCurrency(amount);
        public bool SpendGold(int amount) => SpendCurrency(amount);


        public event Action<float, float> OnHealthChanged;
        public event Action OnPlayerDeath;
        public event Action<int> OnLivesChanged;
        public event Action<int> OnCurrencyChanged;
        public event Action<int> OnDnaSplicesChanged;
        public event Action<int> OnEssenceChanged;
        public event Action<int> OnRollsChanged;

        [Header("Movement & Luck")]
        [Tooltip("Multiplies base player speed. 1.0 = normal.")]
        [SerializeField] private float runSpeedMultiplier = 1f;
        [Tooltip("Luck factor from -1 to 1. 0 = Neutral.")]
        [Range(-1f, 1f)]
        [SerializeField] private float startingLuck = 0f;

        public float MoveSpeedMultiplier { get => moveSpeedMultiplier; private set => moveSpeedMultiplier = value; }
        public float Luck { get => luck; private set => luck = Mathf.Clamp(value, -1f, 1f); }

        public event Action<float> OnSpeedChanged;
        public event Action<float> OnLuckChanged;

        int lives;
        int currency;
        int dnaSplices;
        int essence;
        int rolls;
        float moveSpeedMultiplier;
        float luck;

        float _defaultMaxHP;
        float _defaultSpeed;
        float _defaultLuck;

        void Awake()
        {
            _defaultMaxHP = maxHP;
            _defaultSpeed = runSpeedMultiplier;
            _defaultLuck = startingLuck;
            ResetRunStats();
        }

        public void ResetRunStats()
        {
            maxHP = _defaultMaxHP;
            CurrentHP = Mathf.Max(1f, maxHP);
            
            // Priority: Use MetaStats as the source of truth if available
            var meta = MetaStats.Instance;
            if (meta != null)
            {
                lives = meta.StartingLives;
                dnaSplices = meta.TotalDnaSplices;
                essence = meta.Essence;
            }
            else
            {
                lives = startingLives;
                dnaSplices = startingDnaSplices;
                essence = startingEssence;
            }

            currency = startingGold;
            rolls = startingRolls;
            moveSpeedMultiplier = _defaultSpeed;
            luck = _defaultLuck;

            OnHealthChanged?.Invoke(CurrentHP, MaxHP);
            OnLivesChanged?.Invoke(lives);
            OnCurrencyChanged?.Invoke(currency);
            OnDnaSplicesChanged?.Invoke(dnaSplices);
            OnEssenceChanged?.Invoke(essence);
            OnRollsChanged?.Invoke(rolls);
            OnSpeedChanged?.Invoke(moveSpeedMultiplier);
            OnLuckChanged?.Invoke(luck);
        }

        public void ModifySpeed(float percent)
        {
            // adds raw value to multiplier. e.g. +0.1 for 10% faster
            moveSpeedMultiplier += percent;
            if (moveSpeedMultiplier < 0.1f) moveSpeedMultiplier = 0.1f; // min speed cap
            OnSpeedChanged?.Invoke(moveSpeedMultiplier);
        }

        public void ModifyLuck(float amount)
        {
            luck += amount;
            luck = Mathf.Clamp(luck, -1f, 1f);
            OnLuckChanged?.Invoke(luck);
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

        public void AddRolls(int amount)
        {
            if (amount <= 0) return;
            Rolls += amount;
        }

        public bool UseRoll()
        {
            if (Rolls <= 0) return false;
            Rolls--;
            return true;
        }

        public bool SpendEssence(int amount)
        {
            if (amount == 0) return true;
            if (amount < 0)
            {
                Debug.LogError($"SpendEssence called with negative amount {amount}.", this);
                return false;
            }
            if (Essence < amount) return false;

            Essence -= amount;
            return true;
        }

        public void AddEssence(int amount)
        {
            if (amount == 0) return;
            if (amount < 0)
            {
                Debug.LogError($"AddEssence called with negative amount {amount}. Use SpendEssence instead.", this);
                return;
            }
            Essence += amount;
        }
    }
}
