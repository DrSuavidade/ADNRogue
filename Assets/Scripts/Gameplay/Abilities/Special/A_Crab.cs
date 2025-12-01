using UnityEngine;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;

namespace Geneforge.Gameplay.Abilities.Special
{
    [CreateAssetMenu(menuName = "Geneforge/Abilities/Crab - Bubble Burst")]
    public class A_CrabBubbleBurst : EssenceAbility
    {
        [Header("Forced Weapon Accuracy")]
        [Range(0f, 1f)] public float forcedAccuracy = 0.10f;
        [Range(0f, 90f)] public float forcedInaccuracyHalf = 25f;

        [Header("Weapon feel (applied pre-fire)")]
        [Range(0.05f, 1f)] public float damageMult = 0.60f;
        [Range(0.1f, 20f)] public float fireRateMult = 4.0f;

        [Header("Bubble feel (per projectile)")]
        public float bubbleDrag = 1.2f;
        public float sizeMult = 0.9f;
        public bool sphereVisual = true;
        static float _prevFireRateMult = 1f;
        static float _prevDamageMult = 1f;

        public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
        {
            _prevFireRateMult = 1f;
            _prevDamageMult = 1f;
        }

        public override void OnPrimaryUnequipped(GameObject owner)
        {
            _prevFireRateMult = 1f;
            _prevDamageMult = 1f;
        }

        public override void OnAboutToFire(WeaponStats activeStats)
        {
            if (activeStats == null) return;

            activeStats.UpgradeAccuracy(forcedAccuracy - activeStats.Accuracy);
            activeStats.UpgradeInaccuracyHalfAngle(forcedInaccuracyHalf - activeStats.InaccuracyHalfAngle);

            float safePrevFR = Mathf.Max(0.01f, _prevFireRateMult);
            float baselineInterval = Mathf.Max(0.001f, activeStats.FireRate * safePrevFR);

            float safeNewFR = Mathf.Max(0.01f, fireRateMult);
            float newInterval = Mathf.Max(0.02f, baselineInterval / safeNewFR);
            float fireRateDelta = activeStats.FireRate - newInterval;
            activeStats.UpgradeFireRate(fireRateDelta);
            _prevFireRateMult = safeNewFR;

            float safePrevDMG = Mathf.Max(0.0001f, _prevDamageMult);
            float baselineDamage = Mathf.Max(0f, activeStats.Damage / safePrevDMG);
            float safeNewDMG = Mathf.Max(0.0001f, damageMult);
            float newDamage = baselineDamage * safeNewDMG;
            float damageDelta = newDamage - activeStats.Damage;
            activeStats.UpgradeDamage(damageDelta);
            _prevDamageMult = safeNewDMG;
        }

        public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
        {
            if (!bullet) return;

            var rb = bullet.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            if (rb) rb.linearDamping = bubbleDrag;
#else
        if (rb) rb.drag = bubbleDrag;
#endif
            bullet.transform.localScale *= sizeMult;

            if (sphereVisual) MakeSphereVisual(bullet);
        }

        static void MakeSphereVisual(Bullet bullet)
        {
            var existing = bullet.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < existing.Length; i++) existing[i].enabled = false;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "CrabBubble_Sphere";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(bullet.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one;

            var r = sphere.GetComponent<Renderer>();
            if (r)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat == null) mat = new Material(Shader.Find("Standard"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.8f, 0.95f, 1f, 1f));
                else if (mat.HasProperty("_Color")) mat.color = new Color(0.8f, 0.95f, 1f, 1f);
                r.material = mat;
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
                    case "Bubble/ForcedAccuracy":
                        forcedAccuracy = Mathf.Clamp01(ApplyNumeric(forcedAccuracy, u));
                        break;

                    case "Bubble/ForcedInaccuracyHalf":
                        forcedInaccuracyHalf = Mathf.Clamp(ApplyNumeric(forcedInaccuracyHalf, u), 0f, 90f);
                        break;

                    case "Bubble/DamageMult":
                        damageMult = Mathf.Max(0.01f, ApplyNumeric(damageMult, u));
                        break;

                    case "Bubble/FireRateMult":
                        fireRateMult = Mathf.Max(0.01f, ApplyNumeric(fireRateMult, u));
                        break;

                    case "Bubble/BubbleDrag":
                        bubbleDrag = Mathf.Max(0f, ApplyNumeric(bubbleDrag, u));
                        break;

                    case "Bubble/SizeMult":
                        sizeMult = Mathf.Max(0.1f, ApplyNumeric(sizeMult, u));
                        break;
                }
            }
        }
    }
}