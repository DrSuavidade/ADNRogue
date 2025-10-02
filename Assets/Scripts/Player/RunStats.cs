// Assets/Scripts/RunStats.cs
using UnityEngine;
using System;

[RequireComponent(typeof(CharacterController))]
public class RunStats : MonoBehaviour
{
    [Header("Health & Lives")]
    public float maxHP = 100f;
    public float currentHP;
    public int lives = 3;
    [HideInInspector] public int maxLives;

    [Header("Run Currency & Resources")]
    [Tooltip("Spendable during this dive")]
    public int currency = 0;
    [Tooltip("DNA Fragments collected this run")]
    public int dnaSplices = 0;
    [Tooltip("Number of rerolls/reshuffles you have this run")]
    public int rolls = 1;
    public event Action OnPlayerDeath;

    void Awake()
    {
        ResetRunStats();
    }

    public void ResetRunStats()
    {
        currentHP = maxHP;
        maxLives  = lives;
        currency  = 0;
        dnaSplices = 0;
        rolls      = 1;
        // lives carried over from MetaStats
    }

    public bool TakeDamage(float dmg)
    {
        currentHP -= dmg;
        currentHP = Mathf.Max(0f, currentHP);
        if (currentHP <= 0f)
        {
            OnPlayerDeath?.Invoke();
            return true;
        }
        return false;
    }

    public void Heal(float amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }

    public bool SpendCurrency(int amount)
    {
        if (currency < amount) return false;
        currency -= amount;
        return true;
    }

    public void AddCurrency(int amount) => currency += amount;
    public void AddDnaSplices(int amount) => dnaSplices += amount;
    public bool UseRoll() 
    {
        if (rolls <= 0) return false;
        rolls--;
        return true;
    }
}
