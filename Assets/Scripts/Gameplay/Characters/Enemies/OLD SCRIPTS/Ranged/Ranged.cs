using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Abilities.Special;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class Ranged : MonoBehaviour
    {
        [Header("Referências")]
        public Animator animator;                // usa "Attack" e "Speed" apenas
        public PlayerHealth playerHealth;        // opcional (projetil trata do dano)

        [Header("Comportamento")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Ataque")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 12f;
        public float attackRate = 1.25f;

        [Header("Direção do lançamento")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Lança (embutida)")]
        public GameObject spearPrefab;
        public Transform spearSocket;
        public Transform throwOrigin;
        public float throwForce = 30f;
        public Vector3 aimOffset = Vector3.zero;

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Lock / Motion Control -------
        [Header("Motion Control (Lock de Movimento)")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        [Header("Attack Lock / Detection")]
        public bool freezeRotationWhileAttacking = true;
        // igual ao Melee (caso tenhas mais do que um tipo de ataque)
        public string[] attackStateNames = { "Attack", "AttackB", "AttackC" };
        public string[] attackStateTags  = { "Attack" };

        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        // igual ao Melee → garante que apanha "Damaged", "Hit" ou "Hurt"
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" };
        public string[] hitStateTags  = { "Hurt", "Damaged" };

        [Header("Animator – Estado de Morte (nome do clip/estado)")]
        public string deathStateName = "Death";

        // ----------------- estado interno -----------------
        Vector3 spawnPos, wanderTarget;
        float wanderTimer, lastAttackTime;
        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        // Damage pause
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        GameObject heldSpear;
        Collider[] ownerCols;

        Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        // --- Translation Lock (anti-deslize) ---
        bool _translationLocked = false;
        Vector3 _pinnedXZ;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // --- Death / freeze ---
        bool _deadLatched = false;
        bool _deathFrozen = false;
        int _deathHash = 0;
        Coroutine _freezeDeathCo;

        // --- Pin de rotação durante ataque/hit ---
        bool _attackFacingPinned = false;
        Quaternion _attackFacing;

        void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _cc  = GetComponent<CharacterController>();
            enemy = GetComponent<Enemy>();

            if (!string.IsNullOrEmpty(deathStateName))
                _deathHash = Animator.StringToHash(deathStateName);
        }

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;
            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

            if (enemy != null) enemy.OnDamaged += HandleOnDamaged;

            ownerCols = GetComponentsInChildren<Collider>(true);
            SpawnHeldSpear();
        }

        void OnDestroy()
        {
            if (enemy != null) enemy.OnDamaged -= HandleOnDamaged;
            CancelInvoke(nameof(SpawnHeldSpear));
        }

        // igual em espírito ao Melee: ativa só o timer
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
            // --- MORTE: parar já; Enemy destrói aos ~5s ---
            if (!_deadLatched && enemy != null && enemy.CurrentHealth <= 0f)
            {
                LatchDeathStop();
            }

            if (_deadLatched)
            {
                PinXZ();
                return;
            }
            // ------------------------------------------------

            // Estados de animação "ocupados"
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // -------- Lock de Translação ----------
            // EXACTO ao Melee: dano (timer) OU enquanto o clip de hit/ataque está ativo
            bool wantsTranslationLock = isDamagePaused || inBusyAnim;

            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();
            // --------------------------------------

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

            // Dano: avançar timer (não fazemos return — mantém lógica viva,
            // mas o lock + HardStopNow impedem movimento real)
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);

                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            // -------- BLOQUEIO TOTAL ENQUANTO ESTÁ EM HIT --------
            // Tal e qual o comportamento que queres: enquanto o clip de Hit
            // estiver ativo, ele não anda nem roda (fica “pregado”).
            if (inHitAnim)
            {
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);
                return;
            }
            // ------------------------------------------------------

            // Invisibilidade → como se o player não existisse
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: false);
                return;
            }

            if (!player) return;

            // 2) estado (com LOS)
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= attackRange && (!requireLineOfSight || HasLineOfSight()))
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // 3) movimento
            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = player.position; targetSpeed = chaseSpeed; }
                    break;
                case State.Wandering:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = wanderTarget; targetSpeed = isIdleWaiting ? 0f : wanderSpeed; }
                    break;
                default: // Attacking
                    targetPos = transform.position; targetSpeed = 0f;
                    break;
            }

            currentSpeed = targetSpeed;
            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    // Não rodar se a rotação estiver pinada (ataque ou hit)
                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                HardStopNow(alsoFreezeRotationY: false);
            }

            // 4) olhar para o alvo (attack / rotateInPlace), mas não durante pin
            if ((state == State.Attacking || rotateInPlace) && player != null)
            {
                if (!_attackFacingPinned)
                {
                    Vector3 face = player.position - transform.position; face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(face.normalized);
                }
            }

            // Reaplica rotação pinada (garante que nada a altera)
            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            // 5) locomotion -> "Speed"
            if (animator)
            {
                float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                AnimatorSpeed(normSpeed);
            }

            // 6) ataque → Trigger "Attack"
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                if (heldSpear != null && animator)
                    animator.SetTrigger("Attack"); // a clip chamará OnThrowRelease()
            }

            // 7) wander
            if (!stayStationary && state == State.Wandering)
            {
                if (!isIdleWaiting)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderInterval ||
                        Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                    {
                        isIdleWaiting = true; idleWaitTimer = 0f;
                    }
                }
                else
                {
                    idleWaitTimer += Time.deltaTime;
                    if (idleWaitTimer >= idleWaitDuration)
                    {
                        isIdleWaiting = false; wanderTimer = 0f; PickWanderTarget();
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (_translationLocked || _deadLatched) PinXZ();
        }

        void LateUpdate()
        {
            if (_translationLocked || _deadLatched) PinXZ();
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
        //    Lança (embutida)
        // ============================
        void SpawnHeldSpear()
        {
            if (!spearPrefab || !spearSocket) return;

            for (int i = spearSocket.childCount - 1; i >= 0; i--)
                Destroy(spearSocket.GetChild(i).gameObject);

            if (heldSpear != null) return;

            heldSpear = Instantiate(spearPrefab, spearSocket);
            heldSpear.name = "Spear_Held";
            heldSpear.transform.localPosition = Vector3.zero;
            heldSpear.transform.localRotation = Quaternion.identity;

            var rb = heldSpear.GetComponent<Rigidbody>();
            var col = heldSpear.GetComponent<Collider>();
            var proj = heldSpear.GetComponent<SpearProjectile>();
            if (!rb || !col || !proj) return;

            rb.isKinematic = true;
            col.enabled = false;
        }

        public void OnThrowRelease() => ThrowNow();
        public void OnAttackHit() { /* sem efeito no ranged */ }

        void ThrowNow()
        {
            if (_deadLatched || heldSpear == null) return;

            var proj = heldSpear.GetComponent<SpearProjectile>();
            var rb = heldSpear.GetComponent<Rigidbody>();
            var col = heldSpear.GetComponent<Collider>();
            if (!proj || !rb || !col) return;

            heldSpear.transform.SetParent(null, true);

            Vector3 originPos = (throwOrigin ? throwOrigin.position :
                                (spearSocket ? spearSocket.position : transform.position));
            Vector3 targetPos = (player ? player.position : transform.position + transform.forward * 5f) + aimOffset;
            Vector3 dir = (targetPos - originPos).normalized;

            if (ownerCols == null || ownerCols.Length == 0)
                ownerCols = GetComponentsInChildren<Collider>(true);

            proj.Launch(dir * throwForce, ownerCols);

            heldSpear = null;
            Invoke(nameof(SpawnHeldSpear), 0.4f);
        }

        // ============================
        //    Auxiliares
        // ============================
        bool HasLineOfSight()
        {
            if (!requireLineOfSight || player == null) return true;

            Vector3 origin = (throwOrigin ? throwOrigin.position :
                             (spearSocket ? spearSocket.position : transform.position));
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

        // -------- Helpers locais (morte/pin) --------
        void LatchDeathStop()
        {
            _deadLatched = true;

            // parar animações/ataques após a morte
            if (animator)
            {
                animator.ResetTrigger("Attack");
                animator.SetFloat("Speed", 0f);
                animator.applyRootMotion = false;
            }

            CancelInvoke(nameof(SpawnHeldSpear));

            if (heldSpear != null)
            {
                Destroy(heldSpear);
                heldSpear = null;
            }

            HardStopNow();
            StartTranslationLock();

            // Congelar o Animator após a 1ª reprodução do estado "Death"
            if (animator && !_deathFrozen && _freezeDeathCo == null)
                _freezeDeathCo = StartCoroutine(FreezeAfterDeathOnce());
        }

        IEnumerator FreezeAfterDeathOnce()
        {
            yield return null;
            int safety = 0;
            while (animator && !_IsInDeathState() && safety++ < 300)
                yield return null;

            safety = 0;
            while (animator && _IsInDeathState() &&
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f &&
                   safety++ < 600)
                yield return null;

            if (animator)
            {
                animator.speed = 0f;
                // opcional: animator.enabled = false;
            }
            _deathFrozen = true;
            _freezeDeathCo = null;
        }

        bool _IsInDeathState()
        {
            if (!animator) return false;
            if (_deathHash != 0)
                return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _deathHash;
            var st = animator.GetCurrentAnimatorStateInfo(0);
            return st.IsName(deathStateName);
        }

        // ---------- Translation Lock ----------
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
            // CharacterController: basta não chamar Move
        }

        void ReleaseRotationFreeze()
        {
            if (_rb && hardStopFreezeRotationY)
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        }
    }
}
