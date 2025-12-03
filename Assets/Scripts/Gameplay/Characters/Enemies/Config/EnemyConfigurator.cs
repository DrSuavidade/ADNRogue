using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.AI;

namespace Geneforge.Gameplay.Characters.Enemies.Config
{
    // ================== NOVO: TIPOS PARA O RANGED ==================

    public enum RangedAttackMode
    {
        RangedThrow,
        RangedShooter
    }

    [System.Serializable]
    public class ThrowAttackSettings
    {
        [Tooltip("Onde a lança está \"presa\" na mão/corpo do inimigo.")]
        public Transform spearSocket;

        [Tooltip("Prefab da lança que será lançada.")]
        public GameObject spearPrefab;

        [Tooltip("Ponto exato de onde a lança é lançada (pode ser igual ao spearSocket).")]
        public Transform throwOrigin;
    }

    [System.Serializable]
    public class ShooterAttackSettings
    {
        [Tooltip("Ponto da arma de onde sai o projétil.")]
        public Transform firePoint;

        [Tooltip("Prefab do projétil/bala que é disparado.")]
        public GameObject bulletPrefab;
    }

    // ================== SCRIPT ORIGINAL + CAMPOS NOVOS ==================

    /// <summary>
    /// Applies an EnemyArchetype to the attached Enemy + Brain(s) at runtime.
    /// This makes enemies fully data-driven.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCore))]
    public class EnemyConfigurator : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;

        // ----------- NOVO BLOCO, NÃO MEXE NA LÓGICA EXISTENTE -----------

        [Header("Ranged Visual/Spawn Setup")]
        [Tooltip("Tipo de ataque à distância que este inimigo usa.")]
        [SerializeField] private RangedAttackMode rangedAttackMode = RangedAttackMode.RangedThrow;

        [Tooltip("Definições para inimigos que lançam a arma que têm na mão.")]
        [SerializeField] private ThrowAttackSettings throwSettings = new ThrowAttackSettings();

        [Tooltip("Definições para inimigos que disparam projéteis/balas.")]
        [SerializeField] private ShooterAttackSettings shooterSettings = new ShooterAttackSettings();

        // Getters públicos (para animation events / abilities irem buscar isto)
        public RangedAttackMode RangedMode => rangedAttackMode;
        public ThrowAttackSettings ThrowSettings => throwSettings;
        public ShooterAttackSettings ShooterSettings => shooterSettings;

        // ----------------------------------------------------------------

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

            // Nota: NÃO alterei nada da lógica aqui.
            // O tipo Throw/Shooter é só configuração visual/spawn,
            // usado depois pelos teus scripts / animation events.
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
        }
    }
}
