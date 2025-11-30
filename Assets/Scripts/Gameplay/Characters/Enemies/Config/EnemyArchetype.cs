using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.AI;

namespace Geneforge.Gameplay.Characters.Enemies.Config
{
    [CreateAssetMenu(menuName = "Geneforge/Enemies/Enemy Archetype", fileName = "EnemyArchetype_")]
    public class EnemyArchetype : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name for designers / debugging.")]
        public string displayName;

        [Header("Core Stats")]
        public float maxHealth = 10f;

        [Tooltip("Base move speed used by brains for normalization.")]
        public float baseMoveSpeed = 3f;

        [Header("Visuals")]
        [Tooltip("Optional: animator controller override for this enemy.")]
        public RuntimeAnimatorController animatorController;

        // --------------------------------------------------------------------
        // Brain-specific configs
        // Only the relevant section is used depending on which brain is attached
        // --------------------------------------------------------------------

        [System.Serializable]
        public struct MeleeConfig
        {
            public float detectionRadius;
            public float chaseSpeed;
            public float wanderRadius;
            public float wanderInterval;
            public float wanderSpeed;
            public float attackRange;
            public float attackRate;
            public float damagePerHit;
            [Range(1, 3)] public int attackVariants;
        }

        [System.Serializable]
        public struct RangedConfig
        {
            public float detectionRadius;
            public float wanderRadius;
            public float wanderInterval;
            public float wanderSpeed;
            public float preferredRange;
            public float minRange;
            public float strafeSpeed;
            public float attackRate;
        }

        [System.Serializable]
        public struct SupportConfig
        {
            public float followRadiusFromSpawn;
            public float repositionSpeed;
            public float detectionRadius;
            public float supportInterval;
        }

        [System.Serializable]
        public struct AnimalConfig
        {
            public float roamRadius;
            public float roamInterval;
            public float roamSpeed;
            public float detectionRadius;
            public float pounceRange;
            public float chaseSpeed;
        }

        [System.Serializable]
        public struct FlyingConfig
        {
            public float hoverHeight;
            public float hoverLerpSpeed;
            public float orbitRadius;
            public float orbitAngularSpeedDeg;
            public float attackRate;
            public float preferredRange;
        }

        [System.Serializable]
        public struct BossConfig
        {
            [Range(0f, 1f)] public float phase2Threshold;
            [Range(0f, 1f)] public float phase3Threshold;

            public float phase1AttackRate;
            public float phase2AttackRate;
            public float phase3AttackRate;
        }

        [Header("Brains")]
        public MeleeConfig melee;
        public RangedConfig ranged;
        public SupportConfig support;
        public AnimalConfig animal;
        public FlyingConfig flying;
        public BossConfig boss;

        // --------------------------------------------------------------------
        // Ability knobs (generic)
        // You *can* put era-specific ability data here, but I’d start small.
        // For Prehistoric we’ll keep those on the ability components directly.
        // --------------------------------------------------------------------
    }
}
