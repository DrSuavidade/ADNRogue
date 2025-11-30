using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.Gameplay.Weapons.Stats
{
    public static class WeaponStatApplier
    {
        public static void ApplyAll(WeaponStats ws, IEnumerable<StatModifier> mods)
        {
            if (ws == null || mods == null) return;
            foreach (var m in mods) Apply(ws, m);
        }

        public static void Apply(WeaponStats ws, StatModifier m)
        {
            switch (m.stat)
            {
                case WeaponStatId.FireRate:
                    {
                        float cur = ws.FireRate;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        target = Mathf.Max(0.05f, target);
                        float delta = cur - target;
                        ws.UpgradeFireRate(delta);
                        break;
                    }

                case WeaponStatId.ProjectileSpeed:
                    {
                        float cur = ws.ProjectileSpeed;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeProjectileSpeed(delta);
                        break;
                    }

                case WeaponStatId.Damage:
                    {
                        float cur = ws.Damage;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeDamage(delta);
                        break;
                    }

                case WeaponStatId.ProjectileSize:
                    {
                        float cur = ws.ProjectileSize;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeProjectileSize(delta);
                        break;
                    }

                case WeaponStatId.KnockbackForce:
                    {
                        float cur = ws.KnockbackForce;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeKnockback(delta);
                        break;
                    }

                case WeaponStatId.CritChance:
                    {
                        float cur = ws.CritChance;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeCritChance(delta);
                        break;
                    }

                case WeaponStatId.CritMultiplier:
                    {
                        float cur = ws.CritMultiplier;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * Mathf.Max(0f, m.value)
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeCritMultiplier(delta);
                        break;
                    }

                case WeaponStatId.ProjectilesPerShot:
                    {
                        int cur = ws.ProjectilesPerShot;
                        int delta = (m.kind == ModifierKind.Multiply)
                            ? Mathf.RoundToInt(cur * (m.value - 1f))
                            : Mathf.RoundToInt(m.value);
                        ws.UpgradeProjectilesPerShot(delta);
                        break;
                    }

                case WeaponStatId.SpreadAngle:
                    {
                        float cur = ws.SpreadAngle;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeSpreadAngle(delta);
                        break;
                    }

                case WeaponStatId.ProjectileLifetime:
                    {
                        float cur = ws.ProjectileLifetime;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeProjectileLifetime(delta);
                        break;
                    }

                case WeaponStatId.PierceCount:
                    {
                        int cur = ws.PierceCount;
                        int delta = (m.kind == ModifierKind.Multiply)
                            ? Mathf.RoundToInt(cur * (m.value - 1f))
                            : Mathf.RoundToInt(m.value);
                        ws.UpgradePierce(delta);
                        break;
                    }

                case WeaponStatId.BounceCount:
                    {
                        int cur = ws.BounceCount;
                        int delta = (m.kind == ModifierKind.Multiply)
                            ? Mathf.RoundToInt(cur * (m.value - 1f))
                            : Mathf.RoundToInt(m.value);
                        ws.UpgradeBounce(delta);
                        break;
                    }

                case WeaponStatId.HomingStrength:
                    {
                        float cur = ws.HomingStrength;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeHoming(delta);
                        break;
                    }

                case WeaponStatId.AoeRadius:
                    {
                        float cur = ws.AoeRadius;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * m.value
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeAoeRadius(delta);
                        break;
                    }

                case WeaponStatId.Accuracy:
                    {
                        float cur = ws.Accuracy;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * Mathf.Max(0f, m.value)
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeAccuracy(delta);
                        break;
                    }

                case WeaponStatId.InaccuracyHalfAngle:
                    {
                        float cur = ws.InaccuracyHalfAngle;
                        float target = (m.kind == ModifierKind.Multiply)
                            ? cur * Mathf.Max(0f, m.value)
                            : cur + m.value;
                        float delta = target - cur;
                        ws.UpgradeInaccuracyHalfAngle(delta);
                        break;
                    }
            }
        }
    }
}
