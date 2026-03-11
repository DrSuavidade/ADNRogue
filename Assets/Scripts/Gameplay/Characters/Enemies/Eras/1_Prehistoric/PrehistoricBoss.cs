using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.AI;
using Geneforge.Gameplay.Characters.Enemies.Abilities;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PrehistoricBoss : PrehistoricEnemyAbilityBase
    {
        [Header("Visual Effects")]
        [SerializeField] private GameObject meleeAoEPrefab;
        [SerializeField] private GameObject meleeSlashPrefab;
        [SerializeField] private GameObject melee2VFXPrefab;
        [SerializeField] private GameObject range1ProjectilePrefab;
        [SerializeField] private GameObject range2ProjectilePrefab;
        [SerializeField] private GameObject teleportVFXPrefab;
        [SerializeField] private Image flashImage;
        [SerializeField] private float flashInDuration = 0.2f;
        [SerializeField] private float flashOutDuration = 1.0f;

        private EnemyConfigurator _config;
        private BossBrain _brain;
        private Coroutine _flashCoroutine;
        private Geneforge.Gameplay.Characters.Enemies.Abilities.BossOrbiter _orbiter;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            _brain = GetComponent<BossBrain>();
            _orbiter = GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossOrbiter>();
            
            if (flashImage != null)
            {
                var color = flashImage.color;
                color.a = 0f;
                flashImage.color = color;
            }
        }

        public void InitializeFlash(Image flash)
        {
            flashImage = flash;
            if (flashImage != null)
            {
                var color = flashImage.color;
                color.a = 0f;
                flashImage.color = color;
            }
        }

        // ==========================================
        // MELEE 1 (E.g. 3-hit combo)
        // ==========================================

        public void AnimEvent_Melee1_1() { ExecuteMelee1(1); }
        public void AnimEvent_Melee1_2() { ExecuteMelee1(2); }
        public void AnimEvent_Melee1_3() { ExecuteMelee1(3); }

        private void ExecuteMelee1(int strikeIndex)
        {
            if (_config == null || _config.Archetype == null) return;
            var bossConfig = _config.Archetype.boss;
            
            bool isThirdStrike = strikeIndex == 3;
            float radius = bossConfig.melee1HitRadius;
            if (isThirdStrike && _brain != null && _brain.CurrentPhase >= 2) radius *= 2f;

            float damage = _brain != null && _brain.CurrentPhase >= 2 ? bossConfig.melee1Damage * 1.3f : bossConfig.melee1Damage;
            
            // --- Directional Scythe Hitbox (120 degree cone) ---
            if (target != null && playerHealth != null)
            {
                // 1. Center the check 0.5m in front of the boss
                Vector3 checkCenter = transform.position + transform.forward * 0.5f;
                Vector3 toPlayer = target.position - checkCenter;
                toPlayer.y = 0;

                // 2. Distance check from the new center
                if (toPlayer.magnitude <= radius)
                {
                    // 3. Dot product check for 120 degree cone (cos(120/2) = cos(60) = 0.5)
                    float dot = Vector3.Dot(transform.forward, toPlayer.normalized);
                    if (dot >= 0.5f) // Player is within 60 degrees of center-forward (total 120)
                    {
                        playerHealth.ApplyDamage(damage);
                    }
                }
            }

            SpawnMeleeVisual(radius, true, strikeIndex);
        }

        // ==========================================
        // MELEE 2 (E.g. heavy slam, 1 hit or multiple)
        // ==========================================

        public void AnimEvent_Melee2_1() { ExecuteMelee2(1); }
        public void AnimEvent_Melee2_2() { ExecuteMelee2(2); }
        public void AnimEvent_Melee2_3() { ExecuteMelee2(3); }

        private void ExecuteMelee2(int strikeIndex)
        {
            if (_config == null || _config.Archetype == null) return;
            var bossConfig = _config.Archetype.boss;

            float radius = bossConfig.melee2HitRadius;
            // Strike 3 has increased radius in phase 2
            if (strikeIndex == 3 && _brain != null && _brain.CurrentPhase >= 2) radius *= 2f;

            // Always a wave for Melee 2
            float damage = _brain != null && _brain.CurrentPhase >= 2 ? bossConfig.melee2Damage * 1.3f : bossConfig.melee2Damage;

            DealDamageToPlayer(damage, radius);

            // Spawn visual
            GameObject prefab = melee2VFXPrefab != null ? melee2VFXPrefab : meleeAoEPrefab;
            if (prefab != null)
            {
                // Position at feet
                Vector3 spawnPos = transform.position + Vector3.up * 0.05f;

                // Find player if reference is lost
                if (target == null)
                {
                    var player = Object.FindAnyObjectByType<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
                    if (player != null) target = player.transform;
                }

                // Direction towards the player
                Vector3 direction = transform.forward;
                if (target != null)
                {
                    direction = (target.position - transform.position);
                    direction.y = 0;
                    if (direction.sqrMagnitude > 0.001f) direction.Normalize();
                    else direction = transform.forward;
                }
                else
                {
                    Debug.LogWarning("Boss Melee2: Target (Player) not found! Using Boss forward.");
                }

                // Instantiate with the PREFAB's default rotation first.
                var obj = Instantiate(prefab, spawnPos, prefab.transform.rotation);
                
                var mover = obj.GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMovingEffect>();
                if (mover == null) mover = obj.AddComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMovingEffect>();
                
                mover.flatOnGround = false;
                
                // Se o teu prefab precisa de estar rodado (ex: -180 ou 90) para estar "de frente", 
                // passamos essa rotação original como offset para o mover.
                mover.rotationOffset = prefab.transform.rotation.eulerAngles;
                
                // Prevent the Boss from being pushed/dragged by the VFX physics
                var bossColliders = GetComponentsInChildren<Collider>();
                var vfxColliders = obj.GetComponentsInChildren<Collider>();
                foreach (var bc in bossColliders)
                {
                    foreach (var vc in vfxColliders)
                    {
                        vc.isTrigger = true; // Prevents player from climbing/walking on crystals
                        Physics.IgnoreCollision(bc, vc);
                    }
                }
                
                mover.Init(direction, 12f, 0.7f);
            }
        }

        // ==========================================
        // RANGE 1 (Sequential Orbs)
        // ==========================================

        public void AnimEvent_Range1_1() { ExecuteRange1(); }
        public void AnimEvent_Range1_2() { ExecuteRange1(); }
        public void AnimEvent_Range1_3() { ExecuteRange1(); }

        private void ExecuteRange1()
        {
            if (_config == null || _config.Archetype == null || !target || _orbiter == null) return;
            var bossConfig = _config.Archetype.boss;
            
            float damage = _brain != null && _brain.CurrentPhase >= 2 ? bossConfig.range1Damage * 1.3f : bossConfig.range1Damage;

            // Launches 1 orb from the spiral. If in Phase 2, we could launch more, but let's keep it clean
            // since the AnimEvents handle the sequence.
            _orbiter.LaunchOrb(target, damage);
        }

        // ==========================================
        // RANGE 2 (Fan/Rapid Orbs)
        // ==========================================

        public void AnimEvent_Range2_1() { ExecuteRange2(); }

        private void ExecuteRange2()
        {
            if (_config == null || _config.Archetype == null || !target || _orbiter == null) return;
            var bossConfig = _config.Archetype.boss;
            
            // Trigger Flash
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());

            float damage = _brain != null && _brain.CurrentPhase >= 2 ? bossConfig.range2Damage * 1.3f : bossConfig.range2Damage;
            bool isHoming = _brain != null && _brain.CurrentPhase >= 2;

            // Phase 2: Rapid burst of 3 orbs from the spiral
            if (_brain != null && _brain.CurrentPhase >= 2)
            {
                StartCoroutine(BurstLaunchOrbs(3, damage, isHoming));
            }
            else
            {
                _orbiter.LaunchOrb(target, damage, null, isHoming);
            }
        }

        private System.Collections.IEnumerator BurstLaunchOrbs(int count, float damage, bool homing)
        {
            for (int i = 0; i < count; i++)
            {
                if (target == null) break;
                _orbiter.LaunchOrb(target, damage, null, homing);
                yield return new WaitForSeconds(0.15f);
            }
        }


        // ==========================================
        // UTILITY MOVES
        // ==========================================

        public void AnimEvent_Teleport()
        {
            if (!target) return;

            // Spawn VFX at old position
            if (teleportVFXPrefab != null) Instantiate(teleportVFXPrefab, transform.position, transform.rotation);

            // Finds a spot randomly nearby
            Vector2 randomCircle = Random.insideUnitCircle * 5f;
            Vector3 teleportSpot = target.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // Actually teleport
            transform.position = teleportSpot;

            // Spawn VFX at new position
            if (teleportVFXPrefab != null) Instantiate(teleportVFXPrefab, transform.position, transform.rotation);
            
            // Face the player immediately
            Vector3 lookData = target.position - transform.position;
            lookData.y = 0;
            if (lookData.sqrMagnitude > 0)
            {
                transform.rotation = Quaternion.LookRotation(lookData);
            }
        }

        public void AnimEvent_Dance()
        {
            if (_config == null || _config.Archetype == null) return;
            var bossConfig = _config.Archetype.boss;
            
            Debug.Log("Boss used Dance! Stun mechanics go here.");
            
            if (IsPlayerInRange(bossConfig.danceRadius)) 
            {
                DealDamageToPlayer(bossConfig.danceDamage, bossConfig.danceRadius);
            }
        }

        // ==========================================
        // UTILITIES
        // ==========================================

        private void SpawnBossBullet(Transform firePoint, GameObject bulletPrefab, float speed, float damage, LayerMask hitMask, float yOffsetAngle = 0f, bool homing = false)
        {
            if (bulletPrefab == null || firePoint == null) return;
            
            // Failsafe: if the origin point is practically on the floor, raise it to chest/hand height!
            Vector3 originPos = firePoint.position;
            if (originPos.y <= 0.5f) 
            {
                originPos.y += 1.5f; 
            }

            Vector3 toTarget = target.position - originPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion baseRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Quaternion finalRot = baseRot * Quaternion.Euler(0f, yOffsetAngle, 0f);

            var obj = Object.Instantiate(bulletPrefab, originPos, finalRot);

            // Applying Velocity - Adding a Rigidbody if the user forgot it!
            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = obj.AddComponent<Rigidbody>();
                rb.useGravity = false; // Don't let magic boss projectiles drop to the floor
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = finalRot * Vector3.forward * speed;
#else
            rb.velocity = finalRot * Vector3.forward * speed;
#endif
            
            // Attach hit logic
            var proj = obj.GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossProjectile>();
            if (proj == null) proj = obj.AddComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossProjectile>();
            
            // Phase 2 Homing
            proj.Init(damage, hitMask, homing, 1.0f, speed, target);
        }

        private void SpawnMeleeVisual(float radius, bool isSlash, int strikeIndex = 1)
        {
            GameObject prefab = isSlash ? meleeSlashPrefab : meleeAoEPrefab;
            if (prefab == null) return;
            
            // Spawn at feet with slight Y offset to avoid Z-fighting
            Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
            
            // If it's a slash, move it forward 0.5m to match the scythe reach
            if (isSlash) spawnPos += transform.forward * 0.5f;

            var obj = Instantiate(prefab, spawnPos, transform.rotation);
            
            if (isSlash)
            {
                var slash = obj.GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMeleeSlash>();
                if (slash == null) slash = obj.AddComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMeleeSlash>();
                
                // Strike 2 flips direction to sweep Left to Right
                // Strike 1 & 3 are Right to Left
                bool flip = strikeIndex == 2;
                slash.Init(radius, flip);
            }
            else
            {
                var wave = obj.GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMeleeWave>();
                if (wave == null) wave = obj.AddComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.BossMeleeWave>();
                wave.Init(radius, strikeIndex);
            }
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            if (flashImage == null) yield break;

            Color color = flashImage.color;

            // Flash IN (0.2s to 255)
            float elapsed = 0f;
            while (elapsed < flashInDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(0f, 1f, elapsed / flashInDuration);
                flashImage.color = color;
                yield return null;
            }
            color.a = 1f;
            flashImage.color = color;

            // Flash OUT (1.0s to 0)
            elapsed = 0f;
            while (elapsed < flashOutDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(1f, 0f, elapsed / flashOutDuration);
                flashImage.color = color;
                yield return null;
            }
            color.a = 0f;
            flashImage.color = color;
            _flashCoroutine = null;
        }
    }
}
