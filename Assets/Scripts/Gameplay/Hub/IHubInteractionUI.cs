using UnityEngine;

namespace Geneforge.Gameplay.Hub
{
    public interface IHubInteractionUI
    {
        void StartInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen);
        void StartShopInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen);
        void StartIncubatorInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen, Hub.IncubatorMachine machine);
        void StartLibraryInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen);
    }
}
