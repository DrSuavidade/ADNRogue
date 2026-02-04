using UnityEngine;

namespace Geneforge.Gameplay.Hub
{
    public interface IHubInteractionUI
    {
        void StartInteraction(GameObject player, string text, string npcName, Sprite portrait);
        void StartShopInteraction(GameObject player, string text, string npcName, Sprite portrait);
    }
}
