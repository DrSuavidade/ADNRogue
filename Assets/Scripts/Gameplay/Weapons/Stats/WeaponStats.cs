using UnityEngine;

namespace Geneforge.Gameplay.Weapons.Stats 
{
    [CreateAssetMenu(menuName = "Geneforge/WeaponStats")]
    public class WeaponStats : ScriptableObject
    {
        [Header("Combat")]
        [Tooltip("Seconds between shots")]
        public float fireRate = 0.25f; // smaller = faster
        [Tooltip("Units per second")]
        public float projectileSpeed = 20f;
        [Tooltip("Hit points dealt on impact")]
        public float damage = 1f;
        [Tooltip("Scale multiplier for the projectile mesh")]
        public float projectileSize = 1f;
        [Tooltip("Impulse force applied to enemies on hit")]
        public float knockbackForce = 5f;

        [Tooltip("Chance to land a critical hit (0 to 1)")]
        [Range(0f, 1f)] public float critChance = 0f;

        [Tooltip("Damage multiplier applied on a critical hit")]
        public float critMultiplier = 2f;

        // --- NEW: bullet-hell friendly controls ---
        [Header("Pattern / Behavior")]
        [Min(1)] public int projectilesPerShot = 1;
        [Range(0f, 180f)] public float spreadAngle = 0f;  // degrees
        [Range(0.05f, 60f)] public float projectileLifetime = 5f; // seconds before despawn
        [Min(0)] public int pierceCount = 0;
        [Min(0)] public int bounceCount = 0;
        [Range(0f, 1f)] public float homingStrength = 0f; // 0 = none, 1 = very strong
        [Min(0f)] public float aoeRadius = 0f;            // 0 = no splash
        [Range(0f, 1f)] public float accuracy = 1f;         // 1 = perfect aim, 0 = max spread
        [Range(0f, 90f)] public float inaccuracyHalfAngle = 45f; // degrees (±half-angle)

        // --- Basic upgrade methods (kept from your version) ---
        public void UpgradeFireRate(float delta) => fireRate = Mathf.Max(0.05f, fireRate - delta);
        public void UpgradeProjectileSpeed(float delta) => projectileSpeed += delta;
        public void UpgradeDamage(float delta) => damage += delta;
        public void UpgradeProjectileSize(float delta) => projectileSize = Mathf.Max(0.1f, projectileSize + delta);
        public void UpgradeKnockback(float delta) => knockbackForce = Mathf.Max(0f, knockbackForce + delta);
        public void UpgradeCritChance(float delta) => critChance = Mathf.Clamp01(critChance + delta);
        public void UpgradeCritMultiplier(float delta) => critMultiplier = Mathf.Max(1f, critMultiplier + delta);

        // --- NEW: simple helpers for added stats ---
        public void UpgradeProjectilesPerShot(int delta) => projectilesPerShot = Mathf.Max(1, projectilesPerShot + delta);
        public void UpgradeSpreadAngle(float delta) => spreadAngle = Mathf.Clamp(spreadAngle + delta, 0f, 180f);
        public void UpgradeProjectileLifetime(float delta) => projectileLifetime = Mathf.Clamp(projectileLifetime + delta, 0.05f, 60f);
        public void UpgradePierce(int delta) => pierceCount = Mathf.Max(0, pierceCount + delta);
        public void UpgradeBounce(int delta) => bounceCount = Mathf.Max(0, bounceCount + delta);
        public void UpgradeHoming(float delta) => homingStrength = Mathf.Clamp01(homingStrength + delta);
        public void UpgradeAoeRadius(float delta) => aoeRadius = Mathf.Max(0f, aoeRadius + delta);
        public void UpgradeAccuracy(float delta) => accuracy = Mathf.Clamp01(accuracy + delta);

        // --- NEW: runtime-safe cloning (avoid mutating the asset at runtime) ---
        public WeaponStats CloneRuntime()
        {
            var clone = CreateInstance<WeaponStats>();
            clone.fireRate = fireRate;
            clone.projectileSpeed = projectileSpeed;
            clone.damage = damage;
            clone.projectileSize = projectileSize;
            clone.knockbackForce = knockbackForce;
            clone.critChance = critChance;
            clone.critMultiplier = critMultiplier;

            clone.projectilesPerShot = projectilesPerShot;
            clone.spreadAngle = spreadAngle;
            clone.projectileLifetime = projectileLifetime;
            clone.pierceCount = pierceCount;
            clone.bounceCount = bounceCount;
            clone.homingStrength = homingStrength;
            clone.aoeRadius = aoeRadius;
            clone.accuracy = accuracy;
            clone.inaccuracyHalfAngle = inaccuracyHalfAngle;


            return clone;
        }
    }
}
