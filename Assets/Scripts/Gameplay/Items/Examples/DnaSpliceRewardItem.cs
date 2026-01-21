using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Items.Examples
{
    /// <summary>
    /// Example item that gives the player DNA splices.
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/Examples/DnaSpliceRewardItem", fileName = "DnaSpliceRewardItem")]
    public class DnaSpliceRewardItem : RewardItemData
    {
        [Header("DNA Splice Settings")]
        [Tooltip("Amount of DNA splices to add.")]
        [SerializeField] private int dnaSpliceAmount = 10;

        public override void Apply(GameObject player)
        {
            base.Apply(player);

            var runSession = RunSession.Instance;
            if (runSession != null && runSession.Run != null)
            {
                runSession.Run.AddDnaSplices(dnaSpliceAmount);
                Debug.Log($"[DnaSpliceRewardItem] Added {dnaSpliceAmount} DNA splices. Total: {runSession.Run.DnaSplices}");
            }
            else
            {
                Debug.LogWarning("[DnaSpliceRewardItem] RunSession not found.");
            }
        }
    }
}
