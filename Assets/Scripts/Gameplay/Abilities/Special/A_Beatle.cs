using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Beetle - Dung Roller")]
public class A_BeetleDungRoller : EssenceAbility
{
    [Header("Growth")]
    [Tooltip("Size added per meter traveled. 0.15 -> +15% size each meter.")]
    public float growthPerMeter    = 0.15f;

    [Tooltip("Max size multiplier cap (relative to initial bullet scale).")]
    public float maxSizeMult       = 2.5f;

    [Header("Damage")]
    [Tooltip("Extra damage per size multiplier above 1. Example: at 2.0x size and 0.6 -> +60% base damage.")]
    public float damagePerSizeMult = 0.6f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        var rt = bullet.gameObject.AddComponent<DungRuntime>();
        rt.baseDamage     = stats.damage;
        rt.growthPerMeter = growthPerMeter;
        rt.maxSizeMult    = maxSizeMult;
        rt.damagePerSizeMult = damagePerSizeMult;
    }

    class DungRuntime : MonoBehaviour
    {
        public float baseDamage;
        public float growthPerMeter;
        public float maxSizeMult;
        public float damagePerSizeMult;

        Bullet b;
        Vector3 lastPos;
        float traveled;
        float sizeMult = 1f;
        Vector3 initialLocalScale;

        void Awake()
        {
            b = GetComponent<Bullet>();
            lastPos = transform.position;
            initialLocalScale = transform.localScale; // respect projectileSize set at spawn
        }

        void Update()
        {
            // accumulate distance
            Vector3 p = transform.position;
            traveled += (p - lastPos).magnitude;
            lastPos = p;

            // compute desired size multiplier from distance, clamp to cap
            float targetSize = Mathf.Clamp(1f + traveled * Mathf.Max(0f, growthPerMeter), 1f, Mathf.Max(1f, maxSizeMult));
            if (!Mathf.Approximately(targetSize, sizeMult))
            {
                sizeMult = targetSize;

                // scale visuals relative to the initial local scale
                transform.localScale = initialLocalScale * sizeMult;

                // scale damage from base damage
                if (b != null)
                {
                    float bonus = (sizeMult - 1f) * Mathf.Max(0f, damagePerSizeMult);
                    b.damage = baseDamage * (1f + bonus);
                }
            }
        }
    }

    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Dung/GrowthPerMeter":
                    growthPerMeter = Mathf.Max(0f, ApplyNumeric(growthPerMeter, u));
                    break;

                case "Dung/MaxSizeMult":
                    maxSizeMult = Mathf.Max(1f, ApplyNumeric(maxSizeMult, u));
                    break;

                case "Dung/DamagePerSizeMult":
                    damagePerSizeMult = Mathf.Max(0f, ApplyNumeric(damagePerSizeMult, u));
                    break;
            }
        }
    }
}
