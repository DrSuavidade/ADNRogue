using System;
using UnityEngine;
using Geneforge.Gameplay.Weapons.Stats;

namespace Geneforge.Gameplay.Abilities
{
    public enum WeaponStatId
    {
        FireRate,
        ProjectileSpeed,
        Damage,
        ProjectileSize,
        KnockbackForce,
        CritChance,
        CritMultiplier,
        // new stats
        ProjectilesPerShot,
        SpreadAngle,
        ProjectileLifetime,
        PierceCount,
        BounceCount,
        HomingStrength,
        AoeRadius,
        Accuracy,
        InaccuracyHalfAngle,
    }

    public enum ModifierKind { Add, Multiply }

    [Serializable]
    public struct StatModifier
    {
        public WeaponStatId stat;
        public ModifierKind kind;

        [Tooltip("Multiply: use 1.2 for +20%, 0.8 for -20%. Add: value is added directly (can be negative).")]
        public float value;
    }

    [CreateAssetMenu(menuName = "Geneforge/Animal Essence", fileName = "NewAnimalEssence")]
    public class AnimalEssence : ScriptableObject
    {
        [Header("Presentation")]
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Stat Modifiers")]
        public StatModifier[] modifiers;

        [Header("Special")]
        [Tooltip("Optional active/on-hit behavior. Implemented later.")]
        public EssenceAbility specialAbility;

        [Header("Progression")]
        public EssenceSkillTree skillTree;


        /// <summary>
        /// Apply this essence's modifiers to a runtime WeaponStats instance.
        /// NOTE: This mutates the given instance. Pass a clone, not your asset.
        /// </summary>
        public void ApplyTo(WeaponStats stats)
        {
            if (stats == null || modifiers == null) return;
            Geneforge.Gameplay.Weapons.Stats.WeaponStatApplier.ApplyAll(stats, modifiers);
        }

    }
}
