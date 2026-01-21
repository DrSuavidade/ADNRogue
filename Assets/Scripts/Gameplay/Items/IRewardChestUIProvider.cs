using System;
using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Interface that decouples the RewardChestPickup from the UI implementation.
    /// The UI layer registers itself as the provider.
    /// </summary>
    public interface IRewardChestUIProvider
    {
        /// <summary>
        /// Display the reward selection panel.
        /// </summary>
        /// <param name="items">Items to offer.</param>
        /// <param name="player">Player reference.</param>
        /// <param name="onSelection">Callback when selection is made.</param>
        void ShowRewardSelection(List<RewardItemData> items, GameObject player, Action<RewardItemData, GameObject> onSelection);
    }

    /// <summary>
    /// Static service locator for the reward chest UI.
    /// The UI registers itself on Awake/OnEnable.
    /// </summary>
    public static class RewardChestUIService
    {
        private static IRewardChestUIProvider _provider;

        public static IRewardChestUIProvider Provider => _provider;

        public static void Register(IRewardChestUIProvider provider)
        {
            _provider = provider;
        }

        public static void Unregister(IRewardChestUIProvider provider)
        {
            if (_provider == provider)
            {
                _provider = null;
            }
        }

        public static bool HasProvider => _provider != null;
    }
}
