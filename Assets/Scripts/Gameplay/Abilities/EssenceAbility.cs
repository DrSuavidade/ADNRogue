using UnityEngine;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Abilities
{
    public abstract class EssenceAbility : ScriptableObject
    {
        public virtual void OnBulletSpawn(Bullet bullet, WeaponStats activeStats) {}
        public virtual void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats activeStats) { }
        public virtual void ApplyUpgrades(AbilityUpgrade[] upgrades) { }
        public virtual void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats) {}
        public virtual void OnPrimaryUnequipped(GameObject owner) {}

    }
}
