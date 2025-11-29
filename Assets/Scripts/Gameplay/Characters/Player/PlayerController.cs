using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Slots;
using Geneforge.Core.Pooling;


namespace Geneforge.Gameplay.Characters.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))] // << recomendamos ter o componente
    public class PlayerController : MonoBehaviour
    {
        // -------------------- Inspector --------------------
        [Header("Movement")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] Camera movementCamera;            // defaults to Camera.main if null

        [Header("Facing")]
        [SerializeField] bool faceToCameraForward = true;  // keep aim forward (strafe)
        [SerializeField] float faceCameraTurnSpeed = 12f;  // slerp speed

        [Header("Shooting")]
        [SerializeField] WeaponStats stats;                // ScriptableObject com fireRate, damage, etc.
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] public Transform firePoint;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [Tooltip("Transform visual do personagem (raiz do Animator/malha). Será rodado no roll para a direção do roll.")]
        [SerializeField] Transform modelRoot;

        [Header("Dodge Roll")]
        [Tooltip("A tecla do roll agora é definida nas Input Actions (ação 'roll'). Este campo já não é usado.")]
        [SerializeField] KeyCode rollKey = KeyCode.Space; // mantido só para compat, não é usado
        [SerializeField] float rollDuration = 2f;
        [SerializeField] float rollCooldown = 1f;
        [SerializeField] float rollDistance = 5f;
        [Tooltip("Arredonda a direção do roll para 8 direções (múltiplos de 45°) relativas à câmara.")]
        [SerializeField] bool snapRollToEightWay = true;

        [Header("Gravity")]
        [SerializeField] float gravityY = -35f;
        [SerializeField] float groundedGravityY = -2f;

        float verticalVelocityY = 0f;

        [SerializeField] GunSlots gunSlots;

        // -------------------- Runtime state --------------------
        CharacterController cc;
        Camera mainCam;
        PlayerHealth playerHealth;
        readonly List<Collider> volleyColliders = new List<Collider>(32);

        Vector3 currentMoveWorld;   // movimento (mundo) deste frame
        Vector3 rollDirection;      // direção escolhida no início do roll (mundo)
        float rollSpeed;

        bool isRolling = false;
        bool canRoll = true;
        float rollTimer = 0f;
        float cooldownTimer = 0f;

        int playerLayer;
        int enemyLayer;

        float nextFireTime = 0f;
        float baseAnimatorSpeed = 1f;

        [Header("Animation – Diagonal Boost")]
        [SerializeField] bool diagonalBoostEnabled = true;
        [SerializeField] float diagonalBoostMax = 1.7f;
        [SerializeField] float diagonalBoostPower = 1.25f;
        [SerializeField] float animAxisDeadzone = 0.05f;

        Quaternion modelPreRollRotation;

        // Animator parameter IDs
        static readonly int AnimMoveX = Animator.StringToHash("MoveX");
        static readonly int AnimMoveY = Animator.StringToHash("MoveY");
        static readonly int AnimIsFiring = Animator.StringToHash("IsFiring");
        static readonly int AnimRoll = Animator.StringToHash("Roll");
        static readonly int AnimSpeed = Animator.StringToHash("Speed");

        // --------- Input System references ---------
        PlayerInput playerInput;
        InputAction moveAction;   // Vector2
        InputAction rollAction;   // Button
        InputAction attackAction; // Button (IsPressed)

        // -------------------- Unity --------------------
        void Awake()
        {
            cc = GetComponent<CharacterController>();
            mainCam = Camera.main;
            if (movementCamera == null) movementCamera = mainCam;
            playerHealth = GetComponent<PlayerHealth>();

            playerLayer = LayerMask.NameToLayer("Player");
            enemyLayer = LayerMask.NameToLayer("Enemy");

            if (gunSlots == null) gunSlots = GetComponent<GunSlots>();

            if (modelRoot == null && animator != null) modelRoot = animator.transform;
            if (animator != null) baseAnimatorSpeed = animator.speed;

            // ---- Input System wiring ----
            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                // O teu mapa chama-se "Player"
                var map = playerInput.actions.FindActionMap("Player", true);
                moveAction   = map.FindAction("Move", true);
                rollAction   = map.FindAction("roll", true);    // 'roll' em minúsculas, como no teu asset
                attackAction = map.FindAction("Attack", false); // pode não existir; é opcional
            }
            else
            {
                Debug.LogError("PlayerInput/Actions não encontrado. Adiciona o componente PlayerInput e associa o .inputactions.", this);
            }
        }

        void OnEnable()
        {
            // Garantir enable/disable correto das actions
            moveAction?.Enable();
            rollAction?.Enable();
            attackAction?.Enable();
        }

        void OnDisable()
        {
            attackAction?.Disable();
            rollAction?.Disable();
            moveAction?.Disable();
        }

        void Update()
        {
            UpdateRollTimers();

            if (isRolling)
            {
                HandleRollingMovement();
                return; // não processa movimento/disparo normal durante o roll
            }

            HandleMovement();
            HandleShooting();
            UpdateAnimator();
            TryStartRoll();

            // Sinais para o teu sistema de armas (mantidos)
            if (attackAction != null && attackAction.WasPressedThisFrame()) gunSlots?.OnFireHeldStart();
            if (attackAction != null && attackAction.WasReleasedThisFrame()) gunSlots?.OnFireHeldStop();
        }

        // -------------------- Movement --------------------
        void HandleMovement()
        {
            // Lê o Vector2 do Input System
            Vector2 move2D = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            float ix = Mathf.Clamp(move2D.x, -1f, 1f);
            float iz = Mathf.Clamp(move2D.y, -1f, 1f);

            Vector3 moveInput = new Vector3(ix, 0f, iz);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            Vector3 camF = GetCamForward();
            Vector3 camR = GetCamRight();
            Vector3 moveWorld = camF * moveInput.z + camR * moveInput.x;

            currentMoveWorld = moveWorld;

            if (moveWorld.sqrMagnitude > 0f)
                cc.Move(moveWorld * moveSpeed * Time.deltaTime);

            if (cc.isGrounded)
            {
                if (verticalVelocityY < 0f) verticalVelocityY = groundedGravityY;
            }
            else
            {
                verticalVelocityY += gravityY * Time.deltaTime;
            }

            cc.Move(new Vector3(0f, verticalVelocityY, 0f) * Time.deltaTime);

            if (!isRolling)
            {
                if (faceToCameraForward)
                {
                    Vector3 faceDir = camF;
                    if (faceDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, faceCameraTurnSpeed * Time.deltaTime);
                    }
                }
                else if (moveWorld.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveWorld, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, faceCameraTurnSpeed * Time.deltaTime);
                }
            }
        }

        // -------------------- Shooting --------------------
        void HandleShooting()
        {
            if (attackAction == null || !attackAction.IsPressed()) return; // contínuo
            if (Time.time < nextFireTime) return;

            gunSlots?.OnAboutToFire();
            var active = (gunSlots != null && gunSlots.ActiveStats != null) ? gunSlots.ActiveStats : stats;

            if (bulletPrefab == null || firePoint == null)
            {
                Debug.LogWarning("BulletPrefab ou FirePoint não atribuídos.", this);
                return;
            }

            float interval = (active != null) ? active.fireRate : 0.25f;
            nextFireTime = Time.time + interval;

            int shots = Mathf.Max(1, (active != null ? active.projectilesPerShot : 1));
            float spread = (active != null ? active.spreadAngle : 0f);
            float acc = (active != null) ? Mathf.Clamp01(active.accuracy) : 1f;
            float inaccuracyHalf = (active != null) ? (1f - acc) * Mathf.Max(0f, active.inaccuracyHalfAngle) : 0f;
            float spacing = ((active != null ? active.projectileSize : 1f) * 0.35f);

            volleyColliders.Clear();

            Vector3 forward = firePoint.forward;
            Vector3 axis = firePoint.up;
            Vector3 right = firePoint.right;

            for (int i = 0; i < shots; i++)
            {
                float t = (shots == 1) ? 0f : i / (shots - 1f);
                float angle = (shots == 1) ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Quaternion rot = Quaternion.AngleAxis(angle, axis);
                Vector3 dir = rot * forward;

                if (inaccuracyHalf > 0f)
                {
                    float jitter = Random.Range(-inaccuracyHalf, inaccuracyHalf);
                    dir = Quaternion.AngleAxis(jitter, axis) * dir;
                }

                float lane = (i - (shots - 1) * 0.5f);
                Vector3 spawnPos = firePoint.position + right * (lane * spacing);

                GameObject bulletGO;
                if (PoolManager.Instance != null)
                    bulletGO = PoolManager.Instance.Spawn(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));
                else
                    bulletGO = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));

                if (active != null)
                    bulletGO.transform.localScale = Vector3.one * active.projectileSize;

                Bullet b = bulletGO.GetComponent<Bullet>();
                if (b != null)
                {
                    float dmg = (active != null) ? active.damage : 1f;
                    bool crit = (active != null && Random.value <= active.critChance);
                    if (crit && active != null) dmg *= active.critMultiplier;

                    b.damage = dmg;
                    b.knockbackForce = (active != null) ? active.knockbackForce : 0f;
                    b.isCrit = crit;

                    if (gunSlots != null)
                    {
                        gunSlots.ApplyToBullet(b);
                    }
                    else if (active != null)
                    {
                        b.ApplyRuntimeStats(active);
                    }

                    float speed = (active != null) ? active.projectileSpeed : 20f;
                    b.Launch(dir, speed);

                    var cols = bulletGO.GetComponentsInChildren<Collider>();
                    if (cols != null && cols.Length > 0) volleyColliders.AddRange(cols);
                }
                else
                {
                    Debug.LogWarning("Bullet prefab sem componente Bullet.", bulletGO);
                }
            }

            // Ignore self-collisions inside this volley
            for (int a = 0; a < volleyColliders.Count; a++)
            {
                var ca = volleyColliders[a];
                if (!ca) continue;

                for (int bIdx = a + 1; bIdx < volleyColliders.Count; bIdx++)
                {
                    var cb = volleyColliders[bIdx];
                    if (cb)
                        Physics.IgnoreCollision(ca, cb, true);
                }
            }

            // Fire event once per volley, not once per bullet
            if (OnFired != null)
                OnFired(active);
        }


        // -------------------- Roll --------------------
        void TryStartRoll()
        {
            // Usar o gatilho do Input System
            if (!canRoll || isRolling || rollAction == null || !rollAction.triggered) return;
            StartRoll();
        }

        void StartRoll()
        {
            Vector3 camF = GetCamForward();
            Vector3 inputDir = GetCameraRelativeInputDirRaw(); // usa Move do Input System

            if (inputDir == Vector3.zero)
            {
                rollDirection = camF; // forward
            }
            else
            {
                if (snapRollToEightWay)
                {
                    float signedAngle = SignedAngleOnY(camF, inputDir);
                    float snapped = Mathf.Round(signedAngle / 45f) * 45f;
                    rollDirection = Quaternion.AngleAxis(snapped, Vector3.up) * camF;
                }
                else
                {
                    rollDirection = inputDir.normalized;
                }
            }

            if (modelRoot != null)
            {
                modelPreRollRotation = modelRoot.rotation;
                modelRoot.rotation = Quaternion.LookRotation(rollDirection, Vector3.up);
            }

            rollSpeed = rollDistance / Mathf.Max(0.0001f, rollDuration);
            rollTimer = rollDuration;
            cooldownTimer = rollCooldown;
            isRolling = true;
            canRoll = false;

            currentMoveWorld = Vector3.zero;

            if (playerLayer >= 0 && enemyLayer >= 0)
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);

            playerHealth?.BeginInvulnerability(rollDuration);
            animator?.SetTrigger(AnimRoll);
        }

        void HandleRollingMovement()
        {
            if (cc.isGrounded)
            {
                if (verticalVelocityY < 0f) verticalVelocityY = groundedGravityY;
            }
            else
            {
                verticalVelocityY += gravityY * Time.deltaTime;
            }

            cc.Move(rollDirection * rollSpeed * Time.deltaTime);

            if (faceToCameraForward)
            {
                Vector3 camF = GetCamForward();
                if (camF.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camF, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, faceCameraTurnSpeed * Time.deltaTime);
                }
            }
        }

        void UpdateRollTimers()
        {
            if (isRolling)
            {
                rollTimer -= Time.deltaTime;
                if (rollTimer <= 0f)
                    EndRoll();
            }
            else if (!canRoll)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                    canRoll = true;
            }
        }

        void EndRoll()
        {
            isRolling = false;

            if (modelRoot != null)
            {
                Vector3 camF = GetCamForward();
                modelRoot.rotation = Quaternion.LookRotation(camF, Vector3.up);
            }

            if (playerLayer >= 0 && enemyLayer >= 0)
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        // -------------------- Animator --------------------
        void UpdateAnimator()
        {
            if (animator == null) return;

            Vector3 camF = GetCamForward();
            Vector3 camR = GetCamRight();

            float animX = 0f;
            float animY = 0f;

            if (currentMoveWorld.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = currentMoveWorld.normalized;
                animX = Mathf.Clamp(Vector3.Dot(dir, camR), -1f, 1f);
                animY = Mathf.Clamp(Vector3.Dot(dir, camF), -1f, 1f);
            }

            if (Mathf.Abs(animX) < 0.05f) animX = 0f;
            if (Mathf.Abs(animY) < 0.05f) animY = 0f;

            animator.SetFloat(AnimMoveX, animX);
            animator.SetFloat(AnimMoveY, animY);

            bool isFiringNow = (attackAction != null && attackAction.IsPressed());
            animator.SetBool(AnimIsFiring, isFiringNow);

            float speed01 = Mathf.Clamp01(currentMoveWorld.magnitude / Mathf.Max(0.0001f, moveSpeed));
            animator.SetFloat(AnimSpeed, speed01);

            if (!isRolling && diagonalBoostEnabled && (animX != 0f || animY != 0f))
            {
                float ax = Mathf.Abs(animX);
                float ay = Mathf.Abs(animY);

                if (ax < animAxisDeadzone || ay < animAxisDeadzone)
                {
                    animator.speed = baseAnimatorSpeed;
                }
                else
                {
                    float axisMax = Mathf.Max(ax, ay);
                    float baseBoost = 1f / Mathf.Max(axisMax, 0.0001f);
                    float boosted = Mathf.Pow(baseBoost, diagonalBoostPower);
                    animator.speed = baseAnimatorSpeed * Mathf.Min(boosted, diagonalBoostMax);
                }
            }
            else
            {
                animator.speed = baseAnimatorSpeed;
            }
        }

        // -------------------- Helpers --------------------
        Vector3 GetCamForward()
        {
            Transform ct = (movementCamera != null) ? movementCamera.transform : null;
            Vector3 fwd = (ct != null) ? ct.forward : Vector3.forward;
            return Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
        }

        Vector3 GetCamRight()
        {
            Transform ct = (movementCamera != null) ? movementCamera.transform : null;
            Vector3 right = (ct != null) ? ct.right : Vector3.right;
            return Vector3.ProjectOnPlane(right, Vector3.up).normalized;
        }

        // Direção da entrada (WASD/analógico) relativa à câmara, usando o Input System
        Vector3 GetCameraRelativeInputDirRaw()
        {
            Vector2 move2D = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (Mathf.Approximately(move2D.x, 0f) && Mathf.Approximately(move2D.y, 0f))
                return Vector3.zero;

            Vector3 dir = GetCamForward() * move2D.y + GetCamRight() * move2D.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            return dir;
        }

        static float SignedAngleOnY(Vector3 from, Vector3 to)
        {
            Vector3 f = new Vector3(from.x, 0f, from.z).normalized;
            Vector3 t = new Vector3(to.x, 0f, to.z).normalized;
            if (f.sqrMagnitude < 1e-6f || t.sqrMagnitude < 1e-6f) return 0f;
            float angle = Vector3.SignedAngle(f, t, Vector3.up);
            return angle;
        }

        // Eventos externos
        public event System.Action<WeaponStats> OnFired;

        // API p/ clones (mantido)
        public void FireOnceFrom(Transform origin, WeaponStats overrideStats = null)
        {
            var active = overrideStats ?? (gunSlots != null && gunSlots.ActiveStats != null ? gunSlots.ActiveStats : stats);
            if (origin == null || bulletPrefab == null || active == null) return;
            SpawnVolleyFrom(origin, active);
        }

        void SpawnVolleyFrom(Transform origin, WeaponStats active)
        {
            int shots = Mathf.Max(1, active.projectilesPerShot);
            float spread = active.spreadAngle;
            float inaccuracyHalf = (1f - Mathf.Clamp01(active.accuracy)) * active.inaccuracyHalfAngle;
            float spacing = active.projectileSize * 0.35f;

            var volleyCols = new List<Collider>();

            Vector3 forward = origin.forward;
            Vector3 axis = origin.up;
            Vector3 right = origin.right;

            for (int i = 0; i < shots; i++)
            {
                float t = (shots == 1) ? 0f : i / (shots - 1f);
                float angle = (shots == 1) ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Quaternion rot = Quaternion.AngleAxis(angle, axis);
                Vector3 dir = rot * forward;

                if (inaccuracyHalf > 0f)
                    dir = Quaternion.AngleAxis(Random.Range(-inaccuracyHalf, inaccuracyHalf), axis) * dir;

                float lane = (i - (shots - 1) * 0.5f);
                Vector3 spawnPos = origin.position + right * (lane * spacing);

                GameObject go;
                if (PoolManager.Instance != null)
                    go = PoolManager.Instance.Spawn(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));
                else
                    go = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));

                go.transform.localScale = Vector3.one * active.projectileSize;

                var b = go.GetComponent<Bullet>();
                if (b != null)
                {
                    float dmg = active.damage;
                    bool crit = false;
                    if (Random.value <= active.critChance) { dmg *= active.critMultiplier; crit = true; }
                    b.damage = dmg; b.isCrit = crit; b.knockbackForce = active.knockbackForce;
                    if (gunSlots != null)
                    {
                        gunSlots.ApplyToBullet(b);
                    }
                    else if (active != null)
                    {
                        b.ApplyRuntimeStats(active);
                    }   
                    b.Launch(dir, active.projectileSpeed);
                }

                var cols = go.GetComponentsInChildren<Collider>();
                if (cols != null && cols.Length > 0) volleyCols.AddRange(cols);
            }

            for (int a = 0; a < volleyCols.Count; a++)
            {
                var ca = volleyCols[a];
                if (!ca) continue;

                for (int b = a + 1; b < volleyCols.Count; b++)
                {
                    var cb = volleyCols[b];
                    if (cb)
                        Physics.IgnoreCollision(ca, cb, true);
                }
            }
        }
    }
}
