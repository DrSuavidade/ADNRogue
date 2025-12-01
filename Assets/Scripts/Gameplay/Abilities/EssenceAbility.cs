using UnityEngine;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Abilities
{
    public abstract class EssenceAbility : ScriptableObject
    {
        public virtual void OnBulletSpawn(Bullet bullet, WeaponStats activeStats) { }
        public virtual void OnHitEnemy(Bullet bullet, EnemyCore enemy, WeaponStats activeStats) { }
        public virtual void ApplyUpgrades(AbilityUpgrade[] upgrades) { }
        public virtual void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats) { }
        public virtual void OnPrimaryUnequipped(GameObject owner) { }
        public virtual void OnAboutToFire(WeaponStats activeStats) { }
        public virtual void OnFireHeldStart() { }
        public virtual void OnFireHeldStop() { }

        protected static float ApplyNumeric(float current, AbilityUpgrade u)
        {
            switch (u.kind)
            {
                case ModifierKind.Add: return current + u.value;
                case ModifierKind.Multiply: return current * u.value;
                default: return current;
            }
        }
    }
}
