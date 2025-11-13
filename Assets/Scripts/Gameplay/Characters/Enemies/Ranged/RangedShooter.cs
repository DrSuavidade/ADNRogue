using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class RangedShooter : MonoBehaviour
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

        [Header("Direção do disparo")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Arma de fogo")]
        public GameObject bulletPrefab;      // prefab da bala (usa o mesmo script SpearProjectile)
        public Transform firePoint;          // saída da arma (cano)
        public float bulletSpeed = 30f;      // força / velocidade do disparo
        public Vector3 aimOffset = Vector3.zero;

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        [Header("Motion Control (Death Lock)")]
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

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

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        Collider[] ownerCols;

        Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        bool _translationLocked = false;
        Vector3 _pinnedXZ;

        bool _deadLatched = false;
        bool _deathFrozen = false;   // já congelei o animator após 1ª morte?
        int _deathHash = 0;
        Coroutine _freezeDeathCo;

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
        }

        void OnDestroy()
        {
            if (enemy != null) enemy.OnDamaged -= HandleOnDamaged;
        }

        void HandleOnDamaged(float dmg)
        {
            if (!_deadLatched && !isDamagePaused)
            {
                isDamagePaused = true;
                damagePauseTimer = 0f;
            }
        }

        void Update()
        {
            // --- MORTE: parar tudo aqui, sem tocar no Enemy ---
            if (!_deadLatched && enemy != null && enemy.CurrentHealth <= 0f)
            {
                LatchDeathStop();
            }
            if (_deadLatched)
            {
                // manter fixo até o Enemy destruir o GO (~5s)
                PinXZ();
                return;
            }
            // ---------------------------------------------------

            // 1) pausa por dano
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                if (animator) animator.SetFloat("Speed", 0f);
                if (damagePauseTimer >= damagePauseDuration) isDamagePaused = false;
                return;
            }

            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            if (!player) return;

            // 2) estado
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
            if (currentSpeed > 0f && !_translationLocked)
            {
                Vector3 dir = targetPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * currentSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            // 4) olhar para o alvo
            if ((state == State.Attacking || rotateInPlace) && player != null)
            {
                Vector3 face = player.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            // 5) locomotion -> "Speed"
            if (animator)
            {
                float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                animator.SetFloat("Speed", normSpeed);
            }

            // 6) ataque → Trigger "Attack"
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                if (animator)
                    animator.SetTrigger("Attack"); // a clip chamará OnThrowRelease() para disparar
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

            if (_translationLocked) PinXZ();
        }

        void FixedUpdate()
        {
            if (_translationLocked || _deadLatched) PinXZ();
        }

        // ============================
        //   Arma de fogo / Disparo
        // ============================
        public void OnThrowRelease() => ShootNow();   // reutiliza o mesmo evento da animação
        public void OnAttackHit() { /* sem efeito no ranged */ }

        void ShootNow()
        {
            if (_deadLatched || bulletPrefab == null) return;

            if (ownerCols == null || ownerCols.Length == 0)
                ownerCols = GetComponentsInChildren<Collider>(true);

            // origem do disparo
            Vector3 originPos = firePoint ? firePoint.position : transform.position;

            // alvo (posição do player com offset)
            Vector3 targetPos = (player ? player.position : transform.position + transform.forward * 5f) + aimOffset;
            Vector3 dir = (targetPos - originPos).normalized;

            // instanciar projétil
            GameObject bullet = Instantiate(bulletPrefab, originPos, Quaternion.LookRotation(dir));

            var proj = bullet.GetComponent<SpearProjectile>();   // usa o MESMO script de projétil da lança
            var rb   = bullet.GetComponent<Rigidbody>();
            var col  = bullet.GetComponent<Collider>();
            if (!proj || !rb || !col) return;

            // lançar na direção calculada
            proj.Launch(dir * bulletSpeed, ownerCols);
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

            StartTranslationLock();

            // Congelar o Animator após a 1ª reprodução do estado "Death"
            if (animator && !_deathFrozen && _freezeDeathCo == null)
                _freezeDeathCo = StartCoroutine(FreezeAfterDeathOnce());
        }

        IEnumerator FreezeAfterDeathOnce()
        {
            // esperar o Animator entrar no estado de morte
            yield return null;
            int safety = 0;
            while (animator && !_IsInDeathState() && safety++ < 300)
                yield return null;

            // esperar o fim da 1ª volta do clip (normalizedTime >= 1)
            safety = 0;
            while (animator && _IsInDeathState() && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && safety++ < 600)
                yield return null;

            if (animator)
            {
                // congela na última pose → impede repetir
                animator.speed = 0f;
                // (opcional) animator.enabled = false;
            }
            _deathFrozen = true;
            _freezeDeathCo = null;
        }

        bool _IsInDeathState()
        {
            if (!animator) return false;
            if (_deathHash != 0)
                return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _deathHash;
            // fallback por nome (menos eficiente)
            var st = animator.GetCurrentAnimatorStateInfo(0);
            return st.IsName(deathStateName);
        }

        void StartTranslationLock()
        {
            _translationLocked = true;
            var p = transform.position;
            _pinnedXZ = new Vector3(p.x, 0f, p.z);

            if (_rb && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            if (_rb)
            {
                _rb.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                if (hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }
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
    }
}
