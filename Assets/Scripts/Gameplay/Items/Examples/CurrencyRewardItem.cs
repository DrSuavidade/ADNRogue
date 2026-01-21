using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Items.Examples
{
    /// <summary>
    /// Example item that gives the player currency.
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/Examples/CurrencyRewardItem", fileName = "CurrencyRewardItem")]
    public class CurrencyRewardItem : RewardItemData
    {
        [Header("Currency Settings")]
        [Tooltip("Amount of currency to add.")]
        [SerializeField] private int currencyAmount = 50;

        public override void Apply(GameObject player)
        {
            base.Apply(player);

            var runSession = RunSession.Instance;
            if (runSession != null && runSession.Run != null)
            {
                runSession.Run.AddCurrency(currencyAmount);
                Debug.Log($"[CurrencyRewardItem] Added {currencyAmount} currency. Total: {runSession.Run.Currency}");
            }
            else
            {
                Debug.LogWarning("[CurrencyRewardItem] RunSession not found.");
            }
        }
    }
}
