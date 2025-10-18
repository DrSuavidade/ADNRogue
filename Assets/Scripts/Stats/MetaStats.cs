// Assets/Scripts/MetaStats.cs
using UnityEngine;

public class MetaStats : MonoBehaviour
{
    public static MetaStats I { get; private set; }

    [Header("Progression")]
    [Tooltip("Lives carried into each new run")]
    public int startingLives = 3;
    [Tooltip("Earned between runs")]
    public int essence = 0;
    [Tooltip("Total DNA Fragments banked")]
    public int totalDnaSplices = 0;

    void Awake()
    {
        if (I != null) Destroy(gameObject);
        else
        {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void OnRunStart(RunStats run)
    {
        // initialize run lives
        run.lives = startingLives;
    }

    public void OnRunEnd(RunStats run, bool survived)
    {
        if (survived)
        {
            essence += run.currency;            // reward for completing dive
            totalDnaSplices += run.dnaSplices;  // bank your fragments
        }
        else
        {
            // maybe penalty: lose some essence?
        }
    }

    public bool SpendEssence(int amount)
    {
        if (essence < amount) return false;
        essence -= amount;
        return true;
    }
}
