using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class RangedShooter : MonoBehaviour
    {
        public enum WeaponType
        {
            Projectile,   // dispara balas / lanças
            Flamethrower  // instância um prefab de fogo
        }

        [Header("Referências")]
        public Animator animator;
        public PlayerHealth playerHealth;

        [Header("Animator")]
        [Tooltip("Nome do Trigger de ataque no Animator (ex: 'Attack')")]
        public string attackTriggerName = "Attack";

        [Header("Comportamento")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Ataque")]
        public float detectionRadius = 20f;   // até onde ele vê / persegue
        public float chaseSpeed = 4f;
        public float attackRange = 10f;       // distância a que pode atacar
        public float attackRate = 1.25f;

        [Header("Direção do disparo")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Arma de fogo (projétil)")]
        [Tooltip("Só é usado se WeaponType == Projectile")]
        public GameObject bulletPrefab;
        public Transform firePoint;
        public float bulletSpeed = 30f;
        public Vector3 aimOffset = Vector3.zero;

        [Header("Tipo de arma")]
        public WeaponType weaponType = WeaponType.Flamethrower;

        [Header("Fogo (Prefab de lança-chamas)")]
        [Tooltip("Prefab com ParticleSystem que se destrói sozinho")]
        public GameObject flamePrefab;
        public float flameDamage = 10f;   // por agora não usado
        public float flameRange = 8f;     // por agora não usado

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Lock / Motion Control (como no Melee) -------
        [Header("Motion Control")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        [Header("Attack Lock / Detection")]
        public bool freezeRotationWhileAttacking = true;
        public string[] attackStateNames = { "Attack", "AttackB", "AttackC" };
        public string[] attackStateTags  = { "Attack" };

        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" };
        public string[] hitStateTags  = { "Hurt", "Damaged" };
        // -----------------------------------------------------

        // ----------------- estado interno -----------------
        Vector3 spawnPos, wanderTarget;
        float wanderTimer, lastAttackTime;
        enum State { Wandering, Chasing }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        // Damage pause
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        // Despawn após morte
        bool _deathDespawnScheduled = false;

        Collider[] ownerCols;

        Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        // Lock de Translação (anti-deslize)
        bool _translationLocked = false;
        Vector3 _pinnedXZ; // XZ ancorados durante lock
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // Pin de rotação durante ataque/hit
        bool _attackFacingPinned = false;
        Quaternion _attackFacing;

        void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _cc  = GetComponent<CharacterController>();
            enemy = GetComponent<Enemy>();
        }

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;
            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

            if (enemy != null)
                enemy.OnDamaged += HandleOnDamaged;

            ownerCols = GetComponentsInChildren<Collider>(true);
        }

        void OnDestroy()
        {
            if (enemy != null)
                enemy.OnDamaged -= HandleOnDamaged;
        }

        void HandleOnDamaged(float dmg)
        {
            if (!isDamagePaused)
            {
                isDamagePaused = true;
                damagePauseTimer = 0f;
            }
        }

        void Update()
        {
            // --- MORTE: parar já e marcar para desaparecer ---
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                AnimatorSpeed(0f);
                HardStopNow();

                if (!_translationLocked)
                    StartTranslationLock();

                if (!_deathDespawnScheduled)
                {
                    _deathDespawnScheduled = true;
                    Destroy(gameObject, 5f); // desaparece ao fim de 5 segundos
                }

                return; // não segue mais o player, nem faz AI
            }

            // Estados de animação "ocupados"
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // Lock de Translação durante:
            //  - dano (timer) OU
            //  - enquanto o clip de hit/ataque está ativo
            bool wantsTranslationLock = isDamagePaused || inBusyAnim;

            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();

            // --- Pin de rotação enquanto a animação "ocupada" decorre ---
            bool shouldPinFacing =
                (inAttackAnim && freezeRotationWhileAttacking) ||
                (inHitAnim    && freezeRotationWhileHit);

            if (inBusyAnim && !_attackFacingPinned && shouldPinFacing)
            {
                _attackFacingPinned = true;
                _attackFacing = transform.rotation;
            }
            else if (!inBusyAnim && _attackFacingPinned)
            {
                _attackFacingPinned = false;
            }

            // Dano: avançar timer (não fazemos return — mantém lógica viva)
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);

                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            // Invisibilidade → como se o player não existisse
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: false);
                return;
            }

            if (player == null) return;

            // distância ao player
            float dist = Vector3.Distance(transform.position, player.position);

            // ---- decidir movimento (estado) ----
            if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // ---- se pode atacar (independente do estado) ----
            bool canAttack = dist <= attackRange &&
                             (!requireLineOfSight || HasLineOfSight());

            // movimento
            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    if (stayStationary)
                    {
                        targetPos = transform.position;
                        targetSpeed = 0f;
                    }
                    else
                    {
                        targetPos = player.position;

                        // 👉 só anda enquanto estiver fora do alcance de ataque
                        if (dist > attackRange)
                            targetSpeed = chaseSpeed;
                        else
                            targetSpeed = 0f;       // em range → pára de andar
                    }
                    break;

                default: // Wandering
                    if (stayStationary)
                    {
                        targetPos = transform.position;
                        targetSpeed = 0f;
                    }
                    else
                    {
                        targetPos = wanderTarget;
                        targetSpeed = isIdleWaiting ? 0f : wanderSpeed;
                    }
                    break;
            }

            currentSpeed = targetSpeed;

            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;  // sem subir/descer, só plano XZ

                if (dir.sqrMagnitude > 0.01f)
                {
                    // não mexe na posição se estiver em lock (hit/ataque)
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    // não rodar se a rotação estiver pinada (ataque ou hit)
                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                // garante que não fica a “escorregar”
                HardStopNow(alsoFreezeRotationY: false);
            }

            // Rodar para o jogador (mas não durante pin)
            if (player != null)
            {
                bool wantRotateToPlayer =
                    (rotateInPlace && currentSpeed <= 0.01f) || canAttack;

                if (wantRotateToPlayer && !_attackFacingPinned)
                {
                    Vector3 face = player.position - transform.position;
                    face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(face.normalized);
                }
            }

            // Reaplica rotação pinada (garante que nada a altera)
            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            // animação de movimento (se não estiver em pausa de dano)
            if (!isDamagePaused)
            {
                float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                AnimatorSpeed(normSpeed);
            }

            // ataque (independente do estado, mas NÃO ataca se estiver em pausa de dano)
            if (!isDamagePaused && canAttack && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;

                if (animator && !string.IsNullOrEmpty(attackTriggerName))
                {
                    animator.ResetTrigger(attackTriggerName);
                    animator.SetTrigger(attackTriggerName);
                }

                if (weaponType == WeaponType.Projectile)
                    ShootProjectile();
                else if (weaponType == WeaponType.Flamethrower)
                    DoFlameAttack();
            }

            // wander
            if (!stayStationary && state == State.Wandering)
            {
                if (!isIdleWaiting)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderInterval ||
                        Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                    {
                        isIdleWaiting = true;
                        idleWaitTimer = 0f;
                    }
                }
                else
                {
                    idleWaitTimer += Time.deltaTime;
                    if (idleWaitTimer >= idleWaitDuration)
                    {
                        isIdleWaiting = false;
                        wanderTimer = 0f;
                        PickWanderTarget();
                    }
                }
            }

            if (_translationLocked)
                PinXZ();
        }

        void FixedUpdate()
        {
            if (_translationLocked)
                PinXZ();
        }

        // ============================
        //   Arma de fogo / Disparo
        // ============================
        public void OnThrowRelease() { }
        public void OnAttackHit() { }

        void ShootProjectile()
        {
            if (bulletPrefab == null) return;

            if (ownerCols == null || ownerCols.Length == 0)
                ownerCols = GetComponentsInChildren<Collider>(true);

            Vector3 originPos = firePoint ? firePoint.position : transform.position;
            Vector3 targetPos = (player ? player.position : transform.position + transform.forward * 5f) + aimOffset;
            Vector3 dir = (targetPos - originPos).normalized;

            GameObject bullet = Instantiate(bulletPrefab, originPos, Quaternion.LookRotation(dir));

            var proj = bullet.GetComponent<SpearProjectile>();
            var rb   = bullet.GetComponent<Rigidbody>();
            var col  = bullet.GetComponent<Collider>();
            if (!proj || !rb || !col) return;

            proj.Launch(dir * bulletSpeed, ownerCols);
        }

        // ---------- ATAQUE DE FOGO (só visual) ----------
        void DoFlameAttack()
        {
            if (!firePoint) return;
            SpawnFlamePrefab();
        }

        void SpawnFlamePrefab()
        {
            if (!firePoint || !flamePrefab) return;

            Vector3 dir;
            if (player != null)
                dir = (player.position - firePoint.position).normalized;
            else
                dir = firePoint.forward;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = firePoint.forward;

            Quaternion rot = Quaternion.LookRotation(dir);

            GameObject flame = Instantiate(flamePrefab, firePoint.position, rot);
            flame.transform.SetParent(firePoint, true);
        }

        public void flamefx()
        {
            if (weaponType == WeaponType.Flamethrower && !isDamagePaused)
                DoFlameAttack();
        }

        // ============================
        //    Auxiliares
        // ============================
        bool HasLineOfSight()
        {
            if (!requireLineOfSight || player == null) return true;

            Vector3 origin = (firePoint ? firePoint.position : transform.position);
            Vector3 target = player.position + aimOffset;

            Vector3 diff = target - origin;
            float dist = Mathf.Max(0f, diff.magnitude - lineOfSightPadding);
            if (dist <= 0.001f) return true;

            Vector3 dir = diff / (diff.magnitude > 0.0001f ? diff.magnitude : 1f);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider && hit.collider.transform != player && !hit.collider.transform.IsChildOf(player))
                    return false;
            }
            return true;
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void OnDrawGizmosSelected()
        {
            if (!stayStationary)
            {
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wanderRadius);
                Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // ----------------- Helpers de lock / animação -----------------

        void StartTranslationLock()
        {
            _translationLocked = true;
            var p = transform.position;
            _pinnedXZ = new Vector3(p.x, 0f, p.z);

            if (_rb)
            {
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                _rbPrevConstraints = _rb.constraints;
                _rbHadConstraints = true;

                _rb.constraints = _rbPrevConstraints
                                  | RigidbodyConstraints.FreezePositionX
                                  | RigidbodyConstraints.FreezePositionZ;

                if (hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }

            ApplyRootMotionLock(true);
        }

        void EndTranslationLock()
        {
            _translationLocked = false;

            if (_rb && _rbHadConstraints)
            {
                _rb.constraints = _rbPrevConstraints;
                _rbHadConstraints = false;
            }

            ApplyRootMotionLock(false);
            ReleaseRotationFreeze();
        }

        void PinXZ()
        {
            var p = transform.position;
            transform.position = new Vector3(_pinnedXZ.x, p.y, _pinnedXZ.z);

            if (_rb && !_rb.isKinematic)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                _rb.angularVelocity = Vector3.zero;
            }
        }

        bool IsInAttackAnim()
        {
            if (animator == null) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (var n in attackStateNames)
                if (!string.IsNullOrEmpty(n) && st.IsName(n))
                    return true;

            foreach (var t in attackStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;

            return false;
        }

        bool IsInHitAnim()
        {
            if (animator == null) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (var n in hitStateNames)
                if (!string.IsNullOrEmpty(n) && st.IsName(n))
                    return true;

            foreach (var t in hitStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;

            return false;
        }

        void AnimatorSpeed(float v)
        {
            if (animator != null)
                animator.SetFloat("Speed", v);
        }

        void ApplyRootMotionLock(bool locked)
        {
            if (animator == null || !disableRootMotionWhenLocked) return;
            animator.applyRootMotion = !locked;
        }

        void HardStopNow(bool alsoFreezeRotationY = true)
        {
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.SetFloat("Speed", 0f);
            }

            if (hardStopRigidbodyWhenLocked && _rb)
            {
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                if (alsoFreezeRotationY && hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }
        }

        void ReleaseRotationFreeze()
        {
            if (_rb && hardStopFreezeRotationY)
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        }
    }
}
