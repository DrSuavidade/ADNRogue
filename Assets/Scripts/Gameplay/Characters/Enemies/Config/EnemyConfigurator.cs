using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.AI;
using Geneforge.Gameplay.Characters.Enemies.Abilities;

namespace Geneforge.Gameplay.Characters.Enemies.Config
{
    /// <summary>
    /// Applies an EnemyArchetype to the attached Enemy and its Brains/Abilities at runtime.
    /// This makes enemies fully data-driven.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCore))]
    public class EnemyConfigurator : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;
        public EnemyArchetype Archetype => archetype;

        EnemyCore enemy;
        Animator animator;

        void Awake()
        {
            enemy = GetComponent<EnemyCore>();
            animator = GetComponentInChildren<Animator>();
            ApplyArchetype();
        }

        public void ApplyArchetype()
        {
            if (archetype == null)
            {
                Debug.LogWarning($"[EnemyConfigurator] No archetype assigned on {name}", this);
                return;
            }

            if (enemy != null)
                enemy.MaxHealth = archetype.maxHealth;

            if (animator != null && archetype.animatorController != null)
                animator.runtimeAnimatorController = archetype.animatorController;

            // generic base-move-speed to all brains present
            var brainBase = GetComponent<EnemyBrainBase>();
            if (brainBase != null)
                brainBase.DefaultMoveSpeed = archetype.baseMoveSpeed;

            ApplyMeleeConfig();
            ApplyRangedConfig();
            ApplySupportConfig();
            ApplyAnimalConfig();
            ApplyFlyingConfig();
            ApplyBossConfig();
            ApplyProjectileConfig();
        }

        void ApplyMeleeConfig()
        {
            var brain = GetComponent<MeleeBrain>();
            if (brain == null) return;

            var c = archetype.melee;
            brain.detectionRadius = c.detectionRadius;
            brain.chaseSpeed      = c.chaseSpeed;
            brain.wanderRadius    = c.wanderRadius;
            brain.wanderInterval  = c.wanderInterval;
            brain.wanderSpeed     = c.wanderSpeed;
            brain.attackRange     = c.attackRange;
            brain.attackRate      = c.attackRate;
            brain.damagePerHit    = c.damagePerHit;
            brain.attackVariants  = c.attackVariants;
        }

        void ApplyRangedConfig()
        {
            var brain = GetComponent<RangedBrain>();
            if (brain == null) return;

            var c = archetype.ranged;
            brain.detectionRadius = c.detectionRadius;
            brain.wanderRadius    = c.wanderRadius;
            brain.wanderInterval  = c.wanderInterval;
            brain.wanderSpeed     = c.wanderSpeed;
            brain.preferredRange  = c.preferredRange;
            brain.minRange        = c.minRange;
            brain.strafeSpeed     = c.strafeSpeed;
            brain.attackRate      = c.attackRate;
            brain.attackVariants  = c.attackVariants;
        }

        void ApplySupportConfig()
        {
            var brain = GetComponent<SupportBrain>();
            if (brain == null) return;

            var c = archetype.support;
            brain.followRadiusFromSpawn = c.followRadiusFromSpawn;
            brain.repositionSpeed       = c.repositionSpeed;
            brain.detectionRadius       = c.detectionRadius;
            brain.supportInterval       = c.supportInterval;
            brain.attackVariants        = c.attackVariants;
        }

        void ApplyAnimalConfig()
        {
            var brain = GetComponent<AnimalBrain>();
            if (brain == null) return;

            var c = archetype.animal;
            brain.roamRadius      = c.roamRadius;
            brain.roamInterval    = c.roamInterval;
            brain.roamSpeed       = c.roamSpeed;
            brain.detectionRadius = c.detectionRadius;
            brain.pounceRange     = c.pounceRange;
            brain.chaseSpeed      = c.chaseSpeed;
            brain.attackVariants  = c.attackVariants;
        }

        void ApplyFlyingConfig()
        {
            var brain = GetComponent<FlyingBrain>();
            if (brain == null) return;

            var c = archetype.flying;
            brain.hoverHeight          = c.hoverHeight;
            brain.hoverLerpSpeed       = c.hoverLerpSpeed;
            brain.orbitRadius          = c.orbitRadius;
            brain.orbitAngularSpeedDeg = c.orbitAngularSpeedDeg;
            brain.attackRate           = c.attackRate;
            brain.preferredRange       = c.preferredRange;
            brain.attackVariants       = c.attackVariants;
        }

        void ApplyBossConfig()
        {
            var brain = GetComponent<BossBrain>();
            if (brain == null) return;

            var c = archetype.boss;
            brain.phase2Threshold   = c.phase2Threshold;
            brain.phase3Threshold   = c.phase3Threshold;
            brain.phase1AttackRate  = c.phase1AttackRate;
            brain.phase2AttackRate  = c.phase2AttackRate;
            brain.phase3AttackRate  = c.phase3AttackRate;
            brain.attackVariants    = c.attackVariants;
        }

        void ApplyProjectileConfig()
        {
            // If the archetype defines a projectile ability, enable/add it
            if (!archetype.projectile.enabled) return;

            var ability = GetComponent<ProjectileAttackAbility>();
            if (ability == null)
            {
                ability = gameObject.AddComponent<ProjectileAttackAbility>();
            }

            // Create a dedicated spawn point if offset is non-zero and standard point missing
            // For now, we just pass the transform + offset concept to the ability or create a child object
            Transform spawnPoint = transform.Find("ProjectileSpawnPoint");
            if (spawnPoint == null)
            {
                var go = new GameObject("ProjectileSpawnPoint");
                go.transform.SetParent(transform);
                go.transform.localPosition = archetype.projectile.spawnOffset;
                go.transform.localRotation = Quaternion.identity;
                spawnPoint = go.transform;
            }

            ability.Configure(
                archetype.projectile.projectilePrefab,
                spawnPoint,
                archetype.projectile.damage,
                archetype.projectile.speed,
                archetype.projectile.hitMask,
                archetype.projectile.arcHeight // <--- Added this
            );
        }
    }
}


