using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class RangedMagic : MonoBehaviour
    {
        [Header("Referências")]
        public Animator animator;
        public PlayerHealth playerHealth; // se não puseres, ele procura pelo tag Player

        [Header("Comportamento")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Movimento")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;

        [Header("Line of Sight")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Origem do ataque GLOBAL (mão direita / default)")]
        [Tooltip("Usado pelo Attack (spellA) e como fallback se a spell não tiver throwOrigins próprios.")]
        public Transform throwOrigin;
        public Vector3 defaultAimOffset = Vector3.zero;

        // =========================================================
        //                    2 ATAQUES MÁGICOS
        // =========================================================
        [System.Serializable]
        public class SpellAttack
        {
            public string name = "Spell";

            [Header("Ataque")]
            public float attackRange = 8f;
            public float attackRate = 1.2f;
            public float damage = 10f;               // ✅ dano direto

            [Header("Aim")]
            public Vector3 aimOffset = Vector3.zero;

            [Header("Animator Trigger")]
            public string animatorTrigger = "Attack";

            [Header("Throw Origins específicos (opcional)")]
            [Tooltip("Se estiver vazio, esta spell usa o throwOrigin global. Para AttackB podes pôr aqui as duas mãos.")]
            public Transform[] throwOrigins;
        }

        [Header("Ataque A (perto)")]
        public SpellAttack spellA = new SpellAttack()
        {
            name = "Spell A (Close)",
            attackRange = 6f,
            attackRate = 1.1f,
            damage = 8f,
            animatorTrigger = "Attack"
        };

        [Header("Ataque B (médio/longe)")]
        public SpellAttack spellB = new SpellAttack()
        {
            name = "Spell B (Mid)",
            attackRange = 10f,
            attackRate = 1.4f,
            damage = 12f,
            animatorTrigger = "AttackB"
        };

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        [Header("Precisão / Dodge")]
        [Tooltip("Se o player se afastar mais do que isto da posição onde o inimigo mirou, o feitiço falha.")]
        public float hitToleranceRadius = 1.0f;

        // ------- Lock / Motion Control -------
        [Header("Motion Control (Lock de Movimento)")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        [Header("Attack Lock / Detection")]
        public bool freezeRotationWhileAttacking = false; // ⚠ já não usamos isto para fixar a rotação em ataque
        public string[] attackStateNames = { "Attack", "AttackB" };
        public string[] attackStateTags = { "Attack" };

        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" };
        public string[] hitStateTags = { "Hurt", "Damaged" };

        [Header("Animator – Estado de Morte")]
        public string deathStateName = "Death";

        // ----------------- estado interno -----------------
        Vector3 spawnPos, wanderTarget;
        float wanderTimer;

        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        bool _translationLocked = false;
        Vector3 _pinnedXZ;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        bool _deadLatched = false;
        bool _deathFrozen = false;
        int _deathHash = 0;
        Coroutine _freezeDeathCo;

        bool _attackFacingPinned = false;
        Quaternion _attackFacing;

        float lastAttackA = -999f;
        float lastAttackB = -999f;

        SpellAttack queuedSpell = null;

        // posição onde o inimigo estava a mirar quando iniciou o ataque
        Vector3 _queuedAimPosition = Vector3.zero;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cc = GetComponent<CharacterController>();
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
        }

        void OnDestroy()
        {
            if (enemy != null) enemy.OnDamaged -= HandleOnDamaged;
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
            // --- MORTE ---
            if (!_deadLatched && enemy != null && enemy.CurrentHealth <= 0f)
                LatchDeathStop();

            if (_deadLatched)
            {
                PinXZ();
                return;
            }

            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim = IsInHitAnim();
            bool inBusyAnim = inAttackAnim || inHitAnim;

            bool wantsTranslationLock = isDamagePaused || inBusyAnim;
            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();

            // ⚠ Agora só fixamos rotação em animações de HIT, não em ataque
            bool shouldPinFacing =
                (inHitAnim && freezeRotationWhileHit);

            if (inBusyAnim && !_attackFacingPinned && shouldPinFacing)
            {
                _attackFacingPinned = true;
                _attackFacing = transform.rotation;
            }
            else if (!inBusyAnim && _attackFacingPinned)
                _attackFacingPinned = false;

            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(true);

                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            if (inHitAnim)
            {
                AnimatorSpeed(0f);
                HardStopNow(true);
                return;
            }

            if (!player) return;

            // Invisível = ignora player
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                AnimatorSpeed(0f);
                HardStopNow(false);
                return;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            SpellAttack chosen = ChooseSpell(dist);

            if (chosen != null && dist <= chosen.attackRange &&
                (!requireLineOfSight || HasLineOfSight())) // ← aqui usamos a LOS genérica (global)
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // Movimento base
            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    targetPos = stayStationary ? transform.position : player.position;
                    targetSpeed = stayStationary ? 0f : chaseSpeed;
                    break;

                case State.Wandering:
                    targetPos = stayStationary ? transform.position : wanderTarget;
                    targetSpeed = (stayStationary || isIdleWaiting) ? 0f : wanderSpeed;
                    break;

                default:
                    targetPos = transform.position;
                    targetSpeed = 0f;
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

                    // ⚠ já não fixamos rotação em ataque, por isso ele continua a virar para o alvo
                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else HardStopNow(false);

            // Olha para o player
            if ((state == State.Attacking || rotateInPlace) && !_attackFacingPinned)
            {
                Vector3 face = player.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            if (animator)
            {
                float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                AnimatorSpeed(normSpeed);
            }

            // Disparar trigger correto
            if (state == State.Attacking && chosen != null)
            {
                MarkCast(chosen);
                queuedSpell = chosen;

                // Guardar posição onde ele está a mirar no momento do cast
                _queuedAimPosition = GetAimTargetPosition(chosen);

                animator.SetTrigger(chosen.animatorTrigger);
            }

            // Wander loop
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

        // =========================================================
        //   EVENTO DA ANIMAÇÃO → aplica dano DIRETO, mas dodgeable
        // =========================================================
        public void OnThrowRelease()
        {
            if (_deadLatched || queuedSpell == null || playerHealth == null) return;

            // Distância atual ao player (ainda precisa estar no range da spell)
            float distToPlayerNow = Vector3.Distance(transform.position, player.position);
            if (distToPlayerNow > queuedSpell.attackRange)
            {
                // Player saiu do alcance → falha
                queuedSpell = null;
                return;
            }

            // Ver se o player se desviou o suficiente da posição que foi mirado
            float distFromSavedAim = Vector3.Distance(player.position, _queuedAimPosition);
            if (distFromSavedAim > hitToleranceRadius)
            {
                // Player dodgeou → falha
                queuedSpell = null;
                return;
            }

            // ✅ LOS específico da spell (Attack usa 1 origin global, AttackB pode usar 2 mãos)
            if (!requireLineOfSight || HasLineOfSight(queuedSpell))
            {
                playerHealth.ApplyDamage(queuedSpell.damage);
            }

            queuedSpell = null;
        }

        // --------- Calcula a posição de mira no momento do cast ----------
        Vector3 GetAimTargetPosition(SpellAttack spell)
        {
            if (player == null) return transform.position;

            Vector3 baseTarget = player.position;

            // aplica offset local se houver origem de lançamento
            Vector3 offsetWorld = spell.aimOffset;
            if (throwOrigin != null)
                offsetWorld = throwOrigin.TransformVector(spell.aimOffset);

            return baseTarget + offsetWorld;
        }

        // --------- Escolha de magia ----------
        SpellAttack ChooseSpell(float dist)
        {
            // Perto -> Attack (spellA)
            if (dist <= spellA.attackRange && CanCast(spellA))
                return spellA;

            // Mais longe, mas ainda dentro do alcance -> AttackB (spellB)
            if (dist > spellA.attackRange && dist <= spellB.attackRange && CanCast(spellB))
                return spellB;

            // Fora de alcance
            return null;
        }

        bool CanCast(SpellAttack s)
        {
            float t = Time.time;
            if (s == spellA) return t >= lastAttackA + spellA.attackRate;
            if (s == spellB) return t >= lastAttackB + spellB.attackRate;
            return false;
        }

        void MarkCast(SpellAttack s)
        {
            float t = Time.time;
            if (s == spellA)      lastAttackA = t;
            else if (s == spellB) lastAttackB = t;
        }

        // --------- Throw Origins helpers ----------
        Transform[] GetThrowOrigins(SpellAttack spell)
        {
            // 1) Se a spell tiver origins próprios definidos, usa esses
            if (spell != null && spell.throwOrigins != null && spell.throwOrigins.Length > 0)
                return spell.throwOrigins;

            // 2) Caso contrário, usa o throwOrigin global (mão direita)
            if (throwOrigin != null)
                return new Transform[] { throwOrigin };

            // 3) Fallback: posição do inimigo
            return new Transform[] { transform };
        }

        // --------- LOS genérico (para lógica de estado) ----------
        bool HasLineOfSight()
        {
            // usa throwOrigin global
            return HasLineOfSight(null);
        }

        // --------- LOS específico por spell ----------
        bool HasLineOfSight(SpellAttack spell)
        {
            if (!requireLineOfSight || player == null) return true;

            Transform[] origins = GetThrowOrigins(spell);
            Vector3 target = player.position + defaultAimOffset;

            foreach (var o in origins)
            {
                if (o == null) continue;

                Vector3 originPos = o.position;
                Vector3 diff = target - originPos;
                float dist = Mathf.Max(0f, diff.magnitude - lineOfSightPadding);
                if (dist <= 0.001f)
                    return true;

                Vector3 dir = diff.normalized;

                if (Physics.Raycast(originPos, dir, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider && hit.collider.transform != player && !hit.collider.transform.IsChildOf(player))
                    {
                        // este origin não tem LOS → tenta o próximo
                        continue;
                    }
                }

                // Se QUALQUER origin tiver LOS, a spell pode acertar
                return true;
            }

            // Nenhum origin tem LOS
            return false;
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void FixedUpdate() { if (_translationLocked || _deadLatched) PinXZ(); }
        void LateUpdate() { if (_translationLocked || _deadLatched) PinXZ(); }

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
                if (!string.IsNullOrEmpty(n) && st.IsName(n)) return true;

            foreach (var t in attackStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t)) return true;

            return false;
        }

        bool IsInHitAnim()
        {
            if (animator == null) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (var n in hitStateNames)
                if (!string.IsNullOrEmpty(n) && st.IsName(n)) return true;

            foreach (var t in hitStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t)) return true;

            return false;
        }

        void AnimatorSpeed(float v)
        {
            if (animator != null) animator.SetFloat("Speed", v);
        }

        // --------- Morte / Lock ----------
        void LatchDeathStop()
        {
            _deadLatched = true;

            if (animator)
            {
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("AttackB");
                animator.SetFloat("Speed", 0f);
                animator.applyRootMotion = false;
            }

            HardStopNow(true);
            StartTranslationLock();

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

            if (animator) animator.speed = 0f;
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

        void StartTranslationLock()
        {
            _translationLocked = true;
            var p = transform.position;
            _pinnedXZ = new Vector3(p.x, 0f, p.z);

            if (_rb)
            {
                _rbPrevConstraints = _rb.constraints;
                _rbHadConstraints = true;

                _rb.constraints = _rbPrevConstraints
                                  | RigidbodyConstraints.FreezePositionX
                                  | RigidbodyConstraints.FreezePositionZ;

                if (hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }

            if (animator && disableRootMotionWhenLocked)
                animator.applyRootMotion = false;
        }

        void EndTranslationLock()
        {
            _translationLocked = false;

            if (_rb && _rbHadConstraints)
            {
                _rb.constraints = _rbPrevConstraints;
                _rbHadConstraints = false;
            }

            if (_rb && hardStopFreezeRotationY)
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        }

        void HardStopNow(bool alsoFreezeRotationY)
        {
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.SetFloat("Speed", 0f);
            }

            if (hardStopRigidbodyWhenLocked && _rb && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                if (alsoFreezeRotationY && hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!stayStationary)
            {
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wanderRadius);
                Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }

            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, spellA.attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, spellB.attackRange);
        }
    }
}
