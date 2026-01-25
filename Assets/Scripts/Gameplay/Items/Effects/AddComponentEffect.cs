using UnityEngine;
using System;
using System.Reflection;

namespace Geneforge.Gameplay.Items.Effects
{
    /// <summary>
    /// Adds a specific MonoBehaviour component to the player when the item is collected.
    /// Useful for granting passive abilities that run in Update().
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/Effects/Add Component", fileName = "GrantAbilityEffect")]
    public class AddComponentEffect : RewardEffect
    {
        [Tooltip("The exact class name of the component to add (e.g., 'DoubleJumpAbility'). Case sensitive.")]
        public string componentName;

        [Tooltip("If true, it will try to find the component in common namespaces if not found directly.")]
        public bool searchCommonNamespaces = true;

        public override void Apply(GameObject player)
        {
            if (string.IsNullOrEmpty(componentName)) return;

            Type type = FindType(componentName);

            if (type != null)
            {
                // Check if already exists to avoid duplicates
                if (player.GetComponent(type) == null)
                {
                    player.AddComponent(type);
                    Debug.Log($"[AddComponentEffect] Granted ability: {type.Name}");
                }
                else
                {
                    Debug.Log($"[AddComponentEffect] Player already has ability: {type.Name}");
                }
            }
            else
            {
                Debug.LogWarning($"[AddComponentEffect] Could not find component class with name '{componentName}'. Check spelling or assembly definitions.");
            }
        }

        private Type FindType(string name)
        {
            // 1. Try direct lookup
            Type t = Type.GetType(name);
            if (t != null) return t;

            // 2. Search in all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name);
                if (t != null) return t;

                // 3. Try partial matches in known namespaces
                if (searchCommonNamespaces)
                {
                    t = asm.GetType($"Geneforge.Gameplay.Abilities.{name}");
                    if (t != null) return t;
                    
                    t = asm.GetType($"Geneforge.Gameplay.Characters.Player.{name}");
                    if (t != null) return t;
                }
            }
            return null;
        }
    }
}
