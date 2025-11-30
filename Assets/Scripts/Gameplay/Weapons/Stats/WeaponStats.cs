using UnityEngine;

namespace Geneforge.Gameplay.Weapons.Stats
{
    [CreateAssetMenu(menuName = "Geneforge/WeaponStats")]
    public class WeaponStats : ScriptableObject
    {
        [Header("Combat")]
        [Tooltip("Seconds between shots (smaller = faster)")]
        [SerializeField] private float fireRate = 0.25f;

        [Tooltip("Units per second")]
        [SerializeField] private float projectileSpeed = 20f;

        [Tooltip("Hit points dealt on impact")]
        [SerializeField] private float damage = 1f;

        [Tooltip("Scale multiplier for the projectile mesh")]
        [SerializeField] private float projectileSize = 1f;

        [Tooltip("Impulse force applied to enemies on hit")]
        [SerializeField] private float knockbackForce = 5f;

        [Tooltip("Chance to land a critical hit (0 to 1)")]
        [Range(0f, 1f)]
        [SerializeField] private float critChance = 0f;

        [Tooltip("Damage multiplier applied on a critical hit")]
        [SerializeField] private float critMultiplier = 2f;

        [Header("Pattern / Behavior")]
        [Min(1)]
        [SerializeField] private int projectilesPerShot = 1;

        [Range(0f, 180f)]
        [SerializeField] private float spreadAngle = 0f;

        [Range(0.05f, 60f)]
        [SerializeField] private float projectileLifetime = 5f;

        [Min(0)]
        [SerializeField] private int pierceCount = 0;

        [Min(0)]
        [SerializeField] private int bounceCount = 0;

        [Range(0f, 1f)]
        [SerializeField] private float homingStrength = 0f;

        [Min(0f)]
        [SerializeField] private float aoeRadius = 0f;

        [Range(0f, 1f)]
        [SerializeField] private float accuracy = 1f;

        [Range(0f, 90f)]
        [SerializeField] private float inaccuracyHalfAngle = 45f;


        // --- Read-only public API (use these everywhere else) ---

        public float FireRate => fireRate;
        public float ProjectileSpeed => projectileSpeed;
        public float Damage => damage;
        public float ProjectileSize => projectileSize;
        public float KnockbackForce => knockbackForce;
        public float CritChance => critChance;
        public float CritMultiplier => critMultiplier;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadAngle => spreadAngle;
        public float ProjectileLifetime => projectileLifetime;
        public int PierceCount => pierceCount;
        public int BounceCount => bounceCount;
        public float HomingStrength => homingStrength;
        public float AoeRadius => aoeRadius;
        public float Accuracy => accuracy;
        public float InaccuracyHalfAngle => inaccuracyHalfAngle;


        // --- Upgrade methods (typically used on runtime clones) ---

        public void UpgradeFireRate(float delta)
        {
            fireRate = Mathf.Max(0.05f, fireRate - delta);
        }

        public void UpgradeProjectileSpeed(float delta)
        {
            projectileSpeed = Mathf.Max(0f, projectileSpeed + delta);
        }

        public void UpgradeDamage(float delta)
        {
            damage = Mathf.Max(0f, damage + delta);
        }

        public void UpgradeProjectileSize(float delta)
        {
            projectileSize = Mathf.Max(0.1f, projectileSize + delta);
        }

        public void UpgradeKnockback(float delta)
        {
            knockbackForce = Mathf.Max(0f, knockbackForce + delta);
        }

        public void UpgradeCritChance(float delta)
        {
            critChance = Mathf.Clamp01(critChance + delta);
        }

        public void UpgradeCritMultiplier(float delta)
        {
            critMultiplier = Mathf.Max(1f, critMultiplier + delta);
        }

        public void UpgradeProjectilesPerShot(int delta)
        {
            projectilesPerShot = Mathf.Max(1, projectilesPerShot + delta);
        }

        public void UpgradeSpreadAngle(float delta)
        {
            spreadAngle = Mathf.Clamp(spreadAngle + delta, 0f, 180f);
        }

        public void UpgradeProjectileLifetime(float delta)
        {
            projectileLifetime = Mathf.Clamp(projectileLifetime + delta, 0.05f, 60f);
        }

        public void UpgradePierce(int delta)
        {
            pierceCount = Mathf.Max(0, pierceCount + delta);
        }

        public void UpgradeBounce(int delta)
        {
            bounceCount = Mathf.Max(0, bounceCount + delta);
        }

        public void UpgradeHoming(float delta)
        {
            homingStrength = Mathf.Clamp01(homingStrength + delta);
        }

        public void UpgradeAoeRadius(float delta)
        {
            aoeRadius = Mathf.Max(0f, aoeRadius + delta);
        }

        public void UpgradeAccuracy(float delta)
        {
            accuracy = Mathf.Clamp01(accuracy + delta);
        }
        public void UpgradeInaccuracyHalfAngle(float delta)
        {
            inaccuracyHalfAngle = Mathf.Clamp(inaccuracyHalfAngle + delta, 0f, 90f);
        }


        // --- Runtime-safe cloning (avoid mutating the asset at runtime) ---

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            fireRate = Mathf.Max(0.01f, fireRate);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            damage = Mathf.Max(0f, damage);
            projectileSize = Mathf.Max(0.01f, projectileSize);
            knockbackForce = Mathf.Max(0f, knockbackForce);

            critChance = Mathf.Clamp01(critChance);
            critMultiplier = Mathf.Max(1f, critMultiplier);

            projectilesPerShot = Mathf.Max(1, projectilesPerShot);
            spreadAngle = Mathf.Clamp(spreadAngle, 0f, 180f);
            projectileLifetime = Mathf.Clamp(projectileLifetime, 0.05f, 60f);
            pierceCount = Mathf.Max(0, pierceCount);
            bounceCount = Mathf.Max(0, bounceCount);

            homingStrength = Mathf.Clamp01(homingStrength);
            aoeRadius = Mathf.Max(0f, aoeRadius);
            accuracy = Mathf.Clamp01(accuracy);
            inaccuracyHalfAngle = Mathf.Clamp(inaccuracyHalfAngle, 0f, 90f);
        }
#endif
    }
}
