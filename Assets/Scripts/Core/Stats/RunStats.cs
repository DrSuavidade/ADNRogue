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
            set => lives = Mathf.Max(0, value);
        }
        public int Currency { get; private set; }
        public int DnaSplices { get; private set; }
        public int Rolls { get; private set; }

        public event Action OnPlayerDeath;

        int lives;

        void Awake()
        {
            ResetRunStats();
        }

        public void ResetRunStats()
        {
            CurrentHP = maxHP;
            Lives = startingLives;
            Currency = startingCurrency;
            DnaSplices = startingDnaSplices;
            Rolls = startingRolls;
        }

        public bool TakeDamage(float dmg)
        {
            if (dmg <= 0f || CurrentHP <= 0f) return false;

            CurrentHP = Mathf.Max(0f, CurrentHP - dmg);

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
            CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        }

        public bool SpendCurrency(int amount)
        {
            if (amount <= 0) return true;
            if (Currency < amount) return false;

            Currency -= amount;
            return true;
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0) return;
            Currency += amount;
        }

        public bool SpendDnaSplices(int amount)
        {
            if (amount <= 0) return true;
            if (DnaSplices < amount) return false;

            DnaSplices -= amount;
            return true;
        }


        public void AddDnaSplices(int amount)
        {
            if (amount <= 0) return;
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
