using UnityEngine;
using UnityEngine.AI;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class BossBrain : EnemyBrainBase
    {
        [Header("Phases")]
        [Tooltip("HP fraction where boss enters phase 2 (e.g. 0.66).")]
        public float phase2Threshold = 0.66f;
        [Tooltip("HP fraction where boss enters phase 3 (e.g. 0.33).")]
        public float phase3Threshold = 0.33f;

        [Header("Phase Timings")]
        public float phase1AttackRate = 1.2f;
        public float phase2AttackRate = 0.9f;
        public float phase3AttackRate = 0.6f;

        [Header("Distance")]
        public float meleeRange = 4f;

        [Header("Animation Triggers")]
        public string[] meleeTriggers = { "Melee1", "Melee2" };
        public string[] rangedTriggers = { "Range1", "Range2" };
        public string teleportTrigger = "Teleport";
        public string danceTrigger = "Dance";
        public string phaseChangeTrigger = "PhaseChange";
        [Range(1, 4)] public int attackVariants = 1; // Kept for struct compatibility

        int currentPhase = 1;
        public int CurrentPhase => currentPhase;
        float lastAttackTime;
        
        [Header("Intro")]
        public bool delaySpawn = false;
        [Tooltip("At what % of the Spawn animation the Rigidbody should stop being kinematic.")]
        [Range(0f, 1f)] public float spawnPhysicsRestoreThreshold = 0.8f;

        [Header("Movement Logic")]
        public float repositionChance = 0.7f;
        public float repositionRadius = 6f;
        
        private Vector3 _repositionTarget;
        private bool _isRepositioning = false;
        private float _repositionStartTime = 0f;
        private bool _attackInProgress = false;

        public static bool IsAnyBossSpawning = false;

        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private bool _hasSpawned = false;
        private Coroutine spawningCooldownCoroutine;
        private bool isTransforming = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (delaySpawn && !_hasSpawned)
            {
                Debug.Log($"[BossBrain] {name} is waiting for trigger...");
                if (animator != null) animator.speed = 0f;
            }
            else if (!delaySpawn)
            {
                Debug.Log($"[BossBrain] {name} TriggerSpawn() automatically because delaySpawn is false.");
                TriggerSpawn();
            }
        }


        public void TriggerSpawn()
        {
            if (_hasSpawned) return;
            
            Debug.Log($"[BossBrain] {name} TriggerSpawn() called!");
            _hasSpawned = true;
            if (animator != null) animator.speed = 1f;

            if (spawningCooldownCoroutine != null) StopCoroutine(spawningCooldownCoroutine);
            spawningCooldownCoroutine = StartCoroutine(SpawnLockRoutine());
        }




        private System.Collections.IEnumerator SpawnLockRoutine()
        {
            IsAnyBossSpawning = true;
            if (enemy != null) enemy.IsInvulnerable = true;
            
            // Handle Rigidbody to prevent collision issues with the floor during spawn
            Rigidbody rb = GetComponent<Rigidbody>();
            bool wasKinematic = rb != null && rb.isKinematic;
            if (rb != null) rb.isKinematic = true;

            // Give animator time to initialize and enter Spawn state
            yield return new WaitForSeconds(0.5f);
            
            bool rbRestored = false;
            while (animator != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Spawn"))
                {
                    // Restore Rigidbody at the specified threshold of the animation
                    if (!rbRestored && state.normalizedTime >= spawnPhysicsRestoreThreshold)
                    {
                        if (rb != null) rb.isKinematic = wasKinematic;
                        rbRestored = true;
                    }

                    // Keep facing player during intro
                    FaceTarget();
                    yield return null;
                }

                else break;
            }
            
            // Failsafe: Ensure RB is restored if we exit the loop early
            if (!rbRestored && rb != null) rb.isKinematic = wasKinematic;

            if (enemy != null) enemy.IsInvulnerable = false;

            
            // Wait 1 second before players can shoot
            yield return new WaitForSeconds(1f);
            IsAnyBossSpawning = false;
            spawningCooldownCoroutine = null;
            
            if (enemy != null) enemy.NotifyIntroFinished();
        }

        protected override void TickBrain(float dt)
        {
            if (!_hasSpawned || enemy == null || target == null) return;

            // Block movement and combat while transforming phases

            if (isTransforming)
            {
                if (animator != null) animator.SetFloat(MoveYHash, 0f);
                FaceTarget();
                return;
            }

            // Check if we just finished an attack
            if (_attackInProgress)
            {
                _attackInProgress = false;
                if (Random.value < repositionChance)
                {
                    _repositionTarget = GetRepositionTarget(repositionRadius);
                    _isRepositioning = true;
                    _repositionStartTime = Time.time;
                    Debug.Log($"[BossBrain] {name} decided to reposition to {_repositionTarget}");
                }
            }

            if (_isRepositioning)
            {
                float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                    new Vector3(_repositionTarget.x, 0, _repositionTarget.z));
                
                bool timeout = (Time.time - _repositionStartTime) > 4f;

                if (distToTarget > 0.5f && !timeout)
                {
                    float moveSpeed = currentPhase >= 2 ? defaultMoveSpeed * 1.5f : defaultMoveSpeed;
                    MoveTowards(_repositionTarget, moveSpeed);
                    if (animator != null) animator.SetFloat(MoveYHash, 1f);
                    return; // Don't look for more attacks while repositioning
                }
                else
                {
                    if (timeout) Debug.LogWarning($"[BossBrain] {name} repositioning timed out (stuck?)");
                    _isRepositioning = false;
                    if (animator != null) animator.SetFloat(MoveYHash, 0f);
                }
            }

            FaceTarget();

            float dist = DistanceToTargetXZ();
            if (dist > meleeRange)
            {
                float moveSpeed = currentPhase >= 2 ? defaultMoveSpeed * 1.5f : defaultMoveSpeed;
                MoveTowards(target.position, moveSpeed);
                if (animator != null) animator.SetFloat(MoveYHash, 1f);
            }
            else
            {
                if (animator != null) animator.SetFloat(MoveYHash, 0f); // Halt walking
            }

            // Anti-backfire check: Wait until boss is facing the player
            Vector3 toTarget = (target.position - transform.position);
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toTarget.normalized);
                if (angle > 40f) return;
            }

            float attackRate = GetCurrentAttackRate();
            if (IsAttackReady(ref lastAttackTime, attackRate))
            {
                PerformPhaseAttack(dist);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (enemy != null)
            {
                enemy.OnDeathIntercept = HandleDeathIntercept;
                enemy.DeathDespawnTime = 10f; // Bosses stay longer on the ground
            }
        }

        private bool HandleDeathIntercept()
        {
            // Max 2 phases configured so far (from Phase 1 to Phase 2). 
            // If you want Phase 3, change `currentPhase < 2` to `< 3`.
            if (currentPhase < 2) 
            {
                currentPhase++;
                if (enemy != null) enemy.Heal(1f, false); // Give it exactly 1 HP so it doesn't try to Die again, we refill the rest smoothly later!
                OnPhaseChanged(currentPhase);
                return true; // Prevent Death
            }
            // Ensure flag is cleaned up if they die
            IsAnyBossSpawning = false;
            return false; // Actually die
        }

        void OnPhaseChanged(int newPhase)
        {
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(phaseChangeTrigger))
                    animator.SetTrigger(phaseChangeTrigger);
                
                // Forcibly play the BreakPhase animation bypassing complex web transitions
                animator.Play("BreakPhase"); 
            }

            if (newPhase == 2)
            {
                if (enemy != null) enemy.IsInvulnerable = true;
                isTransforming = true;
                StartCoroutine(ScaleBossRoutine());
            }
        }

        private System.Collections.IEnumerator ScaleBossRoutine()
        {
            // Wait a tiny bit for the animator transition
            yield return new WaitForSeconds(0.15f);

            while (animator != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("BreakPhase")) break;
                yield return null;
            }

            if (animator == null) yield break;

            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 2f;
            
            while (animator != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("BreakPhase")) break;

                float normTime = state.normalizedTime;
                
                // Scale between 0.65 and 0.90
                if (normTime >= 0.65f && normTime <= 0.9f)
                {
                    float t = (normTime - 0.65f) / (0.9f - 0.65f);
                    transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                    
                    if (enemy != null)
                    {
                        float expectedHP = Mathf.Lerp(1f, enemy.MaxHealth, t);
                        if (expectedHP > enemy.CurrentHealth)
                        {
                            enemy.Heal(expectedHP - enemy.CurrentHealth, false);
                        }
                    }
                }
                else if (normTime > 0.9f)
                {
                    transform.localScale = targetScale;
                    if (enemy != null) enemy.Heal(enemy.MaxHealth, false);
                }
                
                yield return null;
                FaceTarget(); // Keep facing player during enrage
            }
            
            // Ensure snap to final size when animation exits
            transform.localScale = targetScale;
            if (enemy != null)
            {
                enemy.Heal(enemy.MaxHealth, false);
                enemy.IsInvulnerable = false;
            }
            
            isTransforming = false; // Release the combat lock!
        }

        float GetCurrentAttackRate()
        {
            float rate;
            switch (currentPhase)
            {
                default:
                case 1: rate = phase1AttackRate; break;
                case 2: rate = phase2AttackRate; break;
                case 3: rate = phase3AttackRate; break;
            }

            if (currentPhase >= 2) rate *= 1.5f;
            return rate;
        }

        void PerformPhaseAttack(float distanceToTarget)
        {
            if (animator == null) return;

            _attackInProgress = true;
            _isRepositioning = false; // Cancel repositioning if we are somehow starting a new attack

            bool doMelee = distanceToTarget <= meleeRange;

            // Optional Utility (Dance/Teleport based on phase and RNG)
            if (currentPhase >= 2 && UnityEngine.Random.value < 0.25f)
            {
                string special = currentPhase == 2 ? teleportTrigger : danceTrigger;
                // For Phase 3, we alternate between Teleport and Dance unpredictably
                if (currentPhase == 3 && UnityEngine.Random.value < 0.5f) special = teleportTrigger;

                if (!string.IsNullOrEmpty(special))
                {
                    animator.SetTrigger(special);
                    return;
                }
            }

            // Normal combat
            if (doMelee && meleeTriggers != null && meleeTriggers.Length > 0)
            {
                int r = UnityEngine.Random.Range(0, meleeTriggers.Length);
                animator.SetTrigger(meleeTriggers[r]);
            }
            else if (!doMelee && rangedTriggers != null && rangedTriggers.Length > 0)
            {
                int r = UnityEngine.Random.Range(0, rangedTriggers.Length);
                animator.SetTrigger(rangedTriggers[r]);
            }
        }

        public override void OnOwnerDied()
        {
            base.OnOwnerDied();
            // Extra clean-up for boss
        }

        protected new Vector3 GetRandomPointAroundSpawn(float radius)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            Vector3 targetPoint = spawnPosition + new Vector3(rnd.x, 0f, rnd.y);
            
            // Try to snap to NavMesh to avoid going outside boundaries
            if (NavMesh.SamplePosition(targetPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return targetPoint; // Fallback
        }

        protected Vector3 GetRepositionTarget(float radius)
        {
            if (target == null) return GetRandomPointAroundSpawn(radius);

            // Calculate direction to player
            Vector3 dirToPlayer = (target.position - transform.position).normalized;
            dirToPlayer.y = 0;

            // Target a point that is roughly 70% towards the player and 30% random jitter
            Vector3 sideDir = Vector3.Cross(dirToPlayer, Vector3.up).normalized;
            
            // Move partially towards the player
            Vector3 push = dirToPlayer * (radius * 0.7f);
            // Add side jitter for unpredictability
            Vector3 jitter = sideDir * Random.Range(-radius * 0.5f, radius * 0.5f);

            Vector3 rawTarget = transform.position + push + jitter;

            // Always snap to NavMesh to stay in bounds
            if (NavMesh.SamplePosition(rawTarget, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return rawTarget;
        }
    }
}
