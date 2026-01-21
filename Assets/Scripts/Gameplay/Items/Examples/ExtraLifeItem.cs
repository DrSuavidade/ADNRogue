using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Items.Examples
{
    /// <summary>
    /// Example item that gives the player an extra life.
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/Examples/ExtraLifeItem", fileName = "ExtraLifeItem")]
    public class ExtraLifeItem : RewardItemData
    {
        [Header("Extra Life Settings")]
        [Tooltip("Number of lives to add.")]
        [SerializeField] private int livesToAdd = 1;

        public override void Apply(GameObject player)
        {
            base.Apply(player);

            var runSession = RunSession.Instance;
            if (runSession != null && runSession.Run != null)
            {
                runSession.Run.Lives += livesToAdd;
                Debug.Log($"[ExtraLifeItem] Added {livesToAdd} life. Total lives: {runSession.Run.Lives}");
            }
            else
            {
                Debug.LogWarning("[ExtraLifeItem] RunSession not found.");
            }
        }
    }
}
