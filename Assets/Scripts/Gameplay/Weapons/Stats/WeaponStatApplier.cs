using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Weapons.Stats
{
    public static class WeaponStatApplier
    {
        public static void ApplyAll(WeaponStats ws, IEnumerable<Geneforge.Gameplay.Abilities.StatModifier> mods)
        {
            if (ws == null || mods == null) return;
            foreach (var m in mods) Apply(ws, m);
        }

        public static void Apply(WeaponStats ws, Abilities.StatModifier m)
        {
            switch (m.stat)
            {
                case Abilities.WeaponStatId.FireRate:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.fireRate *= m.value;
                    else ws.fireRate = Mathf.Max(0.05f, ws.fireRate + m.value);
                    break;

                case Abilities.WeaponStatId.ProjectileSpeed:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.projectileSpeed *= m.value;
                    else ws.projectileSpeed = Mathf.Max(0f, ws.projectileSpeed + m.value);
                    break;

                case Abilities.WeaponStatId.Damage:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.damage *= m.value;
                    else ws.damage = Mathf.Max(0f, ws.damage + m.value);
                    break;

                case Abilities.WeaponStatId.ProjectileSize:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.projectileSize *= m.value;
                    else ws.projectileSize = Mathf.Max(0.1f, ws.projectileSize + m.value);
                    break;

                case Abilities.WeaponStatId.KnockbackForce:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.knockbackForce *= m.value;
                    else ws.knockbackForce = Mathf.Max(0f, ws.knockbackForce + m.value);
                    break;

                case Abilities.WeaponStatId.CritChance:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.critChance *= m.value;
                    else ws.critChance += m.value;
                    ws.critChance = Mathf.Clamp01(ws.critChance);
                    break;

                case Abilities.WeaponStatId.CritMultiplier:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.critMultiplier *= Mathf.Max(0f, m.value);
                    else ws.critMultiplier = Mathf.Max(1f, ws.critMultiplier + m.value);
                    break;

                case Abilities.WeaponStatId.ProjectilesPerShot:
                    if (m.kind == Abilities.ModifierKind.Multiply)
                        ws.projectilesPerShot = Mathf.Max(1, Mathf.RoundToInt(ws.projectilesPerShot * m.value));
                    else
                        ws.projectilesPerShot = Mathf.Max(1, ws.projectilesPerShot + Mathf.RoundToInt(m.value));
                    break;

                case Abilities.WeaponStatId.SpreadAngle:
                    ws.spreadAngle = Mathf.Clamp(
                        ws.spreadAngle + (m.kind == Abilities.ModifierKind.Multiply ? (ws.spreadAngle * (m.value - 1f)) : m.value),
                        0f, 180f);
                    break;

                case Abilities.WeaponStatId.ProjectileLifetime:
                    ws.projectileLifetime = Mathf.Clamp(
                        ws.projectileLifetime + (m.kind == Abilities.ModifierKind.Multiply ? (ws.projectileLifetime * (m.value - 1f)) : m.value),
                        0.05f, 60f);
                    break;

                case Abilities.WeaponStatId.PierceCount:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.pierceCount = Mathf.Max(0, Mathf.RoundToInt(ws.pierceCount * m.value));
                    else ws.pierceCount = Mathf.Max(0, ws.pierceCount + Mathf.RoundToInt(m.value));
                    break;

                case Abilities.WeaponStatId.BounceCount:
                    if (m.kind == Abilities.ModifierKind.Multiply) ws.bounceCount = Mathf.Max(0, Mathf.RoundToInt(ws.bounceCount * m.value));
                    else ws.bounceCount = Mathf.Max(0, ws.bounceCount + Mathf.RoundToInt(m.value));
                    break;

                case Abilities.WeaponStatId.HomingStrength:
                    ws.homingStrength = Mathf.Clamp01(
                        ws.homingStrength + (m.kind == Abilities.ModifierKind.Multiply ? (ws.homingStrength * (m.value - 1f)) : m.value));
                    break;

                case Abilities.WeaponStatId.AoeRadius:
                    ws.aoeRadius = Mathf.Max(0f,
                        ws.aoeRadius + (m.kind == Abilities.ModifierKind.Multiply ? (ws.aoeRadius * (m.value - 1f)) : m.value));
                    break;

                // NEW: Accuracy knobs
                case Abilities.WeaponStatId.Accuracy:
                    if (m.kind == Abilities.ModifierKind.Multiply)
                        ws.accuracy = Mathf.Clamp01(ws.accuracy * Mathf.Max(0f, m.value));
                    else
                        ws.accuracy = Mathf.Clamp01(ws.accuracy + m.value);
                    break;

                case Abilities.WeaponStatId.InaccuracyHalfAngle:
                    if (m.kind == Abilities.ModifierKind.Multiply)
                        ws.inaccuracyHalfAngle = Mathf.Clamp(ws.inaccuracyHalfAngle * Mathf.Max(0f, m.value), 0f, 90f);
                    else
                        ws.inaccuracyHalfAngle = Mathf.Clamp(ws.inaccuracyHalfAngle + m.value, 0f, 90f);
                    break;
            }
        }
    }
}
