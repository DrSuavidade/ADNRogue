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
            Projectile,   // dispara projétil
            Flamethrower  // dano por área (retângulo no chão)
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

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Ataque")]
        public float detectionRadius = 20f;   // até onde vê / persegue
        public float chaseSpeed = 4f;
        public float attackRange = 10f;       // distância a que pode atacar
        public float attackRate = 1.25f;

        [Header("LOS / Raycasts")]
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

        // ---------- ATAQUE DE FOGO POR ÁREA ----------
        [Header("Flamethrower (Retângulo no chão)")]
        [Tooltip("Comprimento da área à frente do inimigo")]
        public float flameLength = 6f;
        [Tooltip("Largura da área (esquerda-direita)")]
        public float flameWidth = 3f;
        [Tooltip("Altura da área (em Y)")]
        public float flameHeight = 2f;
        [Tooltip("Dano aplicado por cada 'tic' de ataque (ligado ao attackRate)")]
        public float flameDamage = 10f;
        [Tooltip("Afasta a hitbox da origem do inimigo")]
        public float flameForwardOffset = 0.5f;
        [Tooltip("Offset vertical em relação ao transform (ex: -1 para encostar ao chão)")]
        public float flameVerticalOffset = 0f;

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Lock / Motion Control (igual Melee) -------
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
        // ---------------------------------------------------

        // ----------------- estado interno -----------------
        Vector3 spawnPos;
        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }
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

        Enemy enemy;

        // --- Lock de Translação (anti-deslize) ---
        bool _translationLocked = false;
        Vector3 _pinnedXZ; // XZ ancorados durante lock
        Rigidbody _rb;
        CharacterController _cc;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // --- Pin de rotação durante ataque/hit ---
        bool _attackFacingPinned = false;
        Quaternion _attackFacing;

        Collider[] ownerCols;

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

            if (animator)
                animator.applyRootMotion = false;
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

        // --------- LOCK DE TRANSLATION ---------
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

        void Update()
        {
            // --- MORTE ---
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                AnimatorSpeed(0f);
                HardStopNow();
                if (!_translationLocked) StartTranslationLock();

                if (!_deathDespawnScheduled)
                {
                    _deathDespawnScheduled = true;
                    Destroy(gameObject, 5f);
                }
                return;
            }

            // Estados de animação "ocupados"
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // Lock enquanto está em dano OU em clip de hit/ataque
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

            // Dano: avançar timer
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

            // -------- ESTADO --------
            float dist = Vector3.Distance(transform.position, player.position);
            bool hasLOS = !requireLineOfSight || HasLineOfSight();
            bool inRange = dist <= attackRange && hasLOS;

            if (inRange)
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // -------- MOVIMENTO --------
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
                        targetSpeed = chaseSpeed;
                    }
                    break;

                case State.Wandering:
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

                default: // Attacking
                    targetPos = transform.position;
                    targetSpeed = 0f; // parado a atacar
                    break;
            }

            currentSpeed = targetSpeed;

            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                HardStopNow(alsoFreezeRotationY: false);
            }

            // Rodar para o jogador
            if (player != null)
            {
                bool wantRotateToPlayer =
                    (state == State.Attacking || (rotateInPlace && currentSpeed <= 0.01f));

                if (wantRotateToPlayer && !_attackFacingPinned)
                {
                    Vector3 face = player.position - transform.position;
                    face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(face.normalized);
                }
            }

            // Reaplica rotação pinada
            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            // Animação de movimento
            float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
            AnimatorSpeed(normSpeed);

            // ---------- ATAQUE (trigger) ----------
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate && !isDamagePaused)
            {
                lastAttackTime = Time.time;

                if (animator && !string.IsNullOrEmpty(attackTriggerName))
                {
                    animator.ResetTrigger(attackTriggerName);
                    animator.SetTrigger(attackTriggerName);
                }
                // Dano é feito pelos Animation Events:
                // - OnThrowRelease()  -> projétil
                // - flamefx()         -> hitbox no chão
            }

            // ---------- WANDER ----------
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
        }

        void FixedUpdate()
        {
            if (_translationLocked) PinXZ();
        }

        void LateUpdate()
        {
            if (_translationLocked) PinXZ();
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

        // ============================
        //   Arma de fogo / Disparo
        // ============================

        // Animation Event no ataque de projétil
        public void OnThrowRelease()
        {
            if (weaponType == WeaponType.Projectile && !isDamagePaused)
                ShootProjectile();
        }

        // Compat por causa do Melee (não usado aqui)
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

        // ---------- ATAQUE DE FOGO (RETÂNGULO NO CHÃO) ----------
        // Animation Event no ataque de fogo
        public void flamefx()
        {
            if (weaponType == WeaponType.Flamethrower && !isDamagePaused)
                ApplyFlameDamageBox();
        }

        void ApplyFlameDamageBox()
        {
            if (player == null || playerHealth == null)
                return;

            if (A_ChameleonCamouflage.InvisibleActive)
                return;

            // Origem do inimigo
            Vector3 origin = transform.position;

            // Direção para a frente, no plano XZ
            Vector3 fwd = transform.forward;
            Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z);
            if (flatFwd.sqrMagnitude < 1e-4f)
                flatFwd = Vector3.forward;
            flatFwd.Normalize();

            // Tamanho da caixa
            Vector3 halfExtents = new Vector3(
                flameWidth  * 0.5f,
                flameHeight * 0.5f,
                flameLength * 0.5f
            );

            // Altura (no chão + offset)
            Vector3 ground = origin;
            ground.y = origin.y + flameVerticalOffset + halfExtents.y;

            // Centro totalmente à frente do inimigo
            float forwardDist = flameLength * 0.5f + flameForwardOffset;
            Vector3 center = ground + flatFwd * forwardDist;

            Quaternion rot = Quaternion.LookRotation(flatFwd, Vector3.up);

            Collider[] hits = Physics.OverlapBox(
                center,
                halfExtents,
                rot,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;

                if (h.transform == player || h.transform.IsChildOf(player))
                {
                    playerHealth.ApplyDamage(flameDamage);
                }
            }
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

            // Gizmo da área de flame (retângulo) para debug
            if (weaponType == WeaponType.Flamethrower)
            {
                Vector3 origin = transform.position;

                Vector3 fwd = transform.forward;
                Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z);
                if (flatFwd.sqrMagnitude < 1e-4f)
                    flatFwd = Vector3.forward;
                flatFwd.Normalize();

                Vector3 halfExtents = new Vector3(
                    flameWidth  * 0.5f,
                    flameHeight * 0.5f,
                    flameLength * 0.5f
                );

                Vector3 ground = origin;
                ground.y = origin.y + flameVerticalOffset + halfExtents.y;

                float forwardDist = flameLength * 0.5f + flameForwardOffset;
                Vector3 center = ground + flatFwd * forwardDist;

                Quaternion rot = Quaternion.LookRotation(flatFwd, Vector3.up);

                Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
                Gizmos.matrix = old;
            }
        }

        // ----------------- Helpers de animação / movimento -----------------

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
            if (animator != null) animator.SetFloat("Speed", v);
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
