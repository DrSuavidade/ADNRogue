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

        /// <summary>
        /// Apply this essence's modifiers to a runtime WeaponStats instance.
        /// NOTE: This mutates the given instance. Pass a clone, not your asset.
        /// </summary>
        public void ApplyTo(WeaponStats stats)
        {
            if (stats == null || modifiers == null) return;

            foreach (var m in modifiers)
            {
                switch (m.stat)
                {
                    case WeaponStatId.FireRate:
                        if (m.kind == ModifierKind.Multiply) stats.fireRate *= m.value;
                        else stats.fireRate = Mathf.Max(0.05f, stats.fireRate + m.value);
                        break;

                    case WeaponStatId.ProjectileSpeed:
                        if (m.kind == ModifierKind.Multiply) stats.projectileSpeed *= m.value;
                        else stats.projectileSpeed = Mathf.Max(0f, stats.projectileSpeed + m.value);
                        break;

                    case WeaponStatId.Damage:
                        if (m.kind == ModifierKind.Multiply) stats.damage *= m.value;
                        else stats.damage = Mathf.Max(0f, stats.damage + m.value);
                        break;

                    case WeaponStatId.ProjectileSize:
                        if (m.kind == ModifierKind.Multiply) stats.projectileSize *= m.value;
                        else stats.projectileSize = Mathf.Max(0.1f, stats.projectileSize + m.value);
                        break;

                    case WeaponStatId.KnockbackForce:
                        if (m.kind == ModifierKind.Multiply) stats.knockbackForce *= m.value;
                        else stats.knockbackForce = Mathf.Max(0f, stats.knockbackForce + m.value);
                        break;

                    case WeaponStatId.CritChance:
                        if (m.kind == ModifierKind.Multiply) stats.critChance *= m.value;
                        else stats.critChance = Mathf.Clamp01(stats.critChance + m.value);
                        stats.critChance = Mathf.Clamp01(stats.critChance);
                        break;

                    case WeaponStatId.CritMultiplier:
                        if (m.kind == ModifierKind.Multiply) stats.critMultiplier *= Mathf.Max(0f, m.value);
                        else stats.critMultiplier = Mathf.Max(1f, stats.critMultiplier + m.value);
                        break;

                    case WeaponStatId.ProjectilesPerShot:
                        if (m.kind == ModifierKind.Multiply)
                            stats.projectilesPerShot = Mathf.Max(1, Mathf.RoundToInt(stats.projectilesPerShot * m.value));
                        else
                            stats.projectilesPerShot = Mathf.Max(1, stats.projectilesPerShot + Mathf.RoundToInt(m.value));
                        break;

                    case WeaponStatId.SpreadAngle:
                        if (m.kind == ModifierKind.Multiply) stats.spreadAngle *= m.value;
                        else stats.spreadAngle += m.value;
                        stats.spreadAngle = Mathf.Clamp(stats.spreadAngle, 0f, 180f);
                        break;

                    case WeaponStatId.ProjectileLifetime:
                        if (m.kind == ModifierKind.Multiply) stats.projectileLifetime *= m.value;
                        else stats.projectileLifetime += m.value;
                        stats.projectileLifetime = Mathf.Clamp(stats.projectileLifetime, 0.05f, 60f);
                        break;

                    case WeaponStatId.PierceCount:
                        if (m.kind == ModifierKind.Multiply)
                            stats.pierceCount = Mathf.Max(0, Mathf.RoundToInt(stats.pierceCount * m.value));
                        else
                            stats.pierceCount = Mathf.Max(0, stats.pierceCount + Mathf.RoundToInt(m.value));
                        break;

                    case WeaponStatId.BounceCount:
                        if (m.kind == ModifierKind.Multiply)
                            stats.bounceCount = Mathf.Max(0, Mathf.RoundToInt(stats.bounceCount * m.value));
                        else
                            stats.bounceCount = Mathf.Max(0, stats.bounceCount + Mathf.RoundToInt(m.value));
                        break;

                    case WeaponStatId.HomingStrength:
                        if (m.kind == ModifierKind.Multiply) stats.homingStrength *= m.value;
                        else stats.homingStrength += m.value;
                        stats.homingStrength = Mathf.Clamp01(stats.homingStrength);
                        break;

                    case WeaponStatId.AoeRadius:
                        if (m.kind == ModifierKind.Multiply) stats.aoeRadius *= Mathf.Max(0f, m.value);
                        else stats.aoeRadius = Mathf.Max(0f, stats.aoeRadius + m.value);
                        break;
                }
            }
        }
    }
}
