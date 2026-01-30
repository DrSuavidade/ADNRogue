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
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] Camera movementCamera;

        [Header("Facing")]
        [SerializeField] bool faceToCameraForward = true;
        [SerializeField] float faceCameraTurnSpeed = 12f;

        [Header("Shooting")]
        [SerializeField] WeaponStats stats;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] public Transform firePoint;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [Tooltip("Transform visual do personagem (raiz do Animator/malha). Será rodado no roll para a direção do roll.")]
        [SerializeField] Transform modelRoot;

        [Header("Dodge Roll")]
        [Tooltip("A tecla do roll agora é definida nas Input Actions (ação 'roll'). Este campo já não é usado.")]
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

        CharacterController cc;
        Camera mainCam;
        PlayerHealth playerHealth;
        readonly List<Collider> volleyColliders = new List<Collider>(32);

        Vector3 currentMoveWorld;
        Vector3 rollDirection;
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

        static readonly int AnimMoveX = Animator.StringToHash("MoveX");
        static readonly int AnimMoveY = Animator.StringToHash("MoveY");
        static readonly int AnimIsFiring = Animator.StringToHash("IsFiring");
        static readonly int AnimRoll = Animator.StringToHash("Roll");
        static readonly int AnimSpeed = Animator.StringToHash("Speed");

        PlayerInput playerInput;
        InputAction moveAction;
        InputAction rollAction;
        InputAction attackAction;


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

            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                var map = playerInput.actions.FindActionMap("Player", true);
                moveAction   = map.FindAction("Move", true);
                rollAction   = map.FindAction("roll", true);
                attackAction = map.FindAction("Attack", false);
            }
            else
            {
                Debug.LogError("PlayerInput/Actions não encontrado. Adiciona o componente PlayerInput e associa o .inputactions.", this);
            }
        }

        void OnEnable()
        {
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
                return;
            }

            HandleMovement();
            HandleShooting();
            UpdateAnimator();
            TryStartRoll();

            if (attackAction != null && attackAction.WasPressedThisFrame()) gunSlots?.OnFireHeldStart();
            if (attackAction != null && attackAction.WasReleasedThisFrame()) gunSlots?.OnFireHeldStop();
        }


        // -------------------- Movement --------------------
        void HandleMovement()
        {
            Vector2 move2D = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            float ix = Mathf.Clamp(move2D.x, -1f, 1f);
            float iz = Mathf.Clamp(move2D.y, -1f, 1f);

            Vector3 moveInput = new Vector3(ix, 0f, iz);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            Vector3 camF = GetCamForward();
            Vector3 camR = GetCamRight();
            Vector3 moveWorld = camF * moveInput.z + camR * moveInput.x;

            currentMoveWorld = moveWorld;

            float speedMult = 1f;
            if (Geneforge.Gameplay.Progression.RunSession.Instance != null && Geneforge.Gameplay.Progression.RunSession.Instance.Run != null)
            {
                speedMult = Geneforge.Gameplay.Progression.RunSession.Instance.Run.MoveSpeedMultiplier;
            }

            if (moveWorld.sqrMagnitude > 0f)
                cc.Move(moveWorld * (moveSpeed * speedMult) * Time.deltaTime);

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

            float interval = (active != null) ? active.FireRate : 0.25f;
            nextFireTime = Time.time + interval;

            int shots = Mathf.Max(1, (active != null ? active.ProjectilesPerShot : 1));
            float spread = (active != null ? active.SpreadAngle : 0f);
            float acc = (active != null) ? Mathf.Clamp01(active.Accuracy) : 1f;
            float inaccuracyHalf = (active != null) ? (1f - acc) * Mathf.Max(0f, active.InaccuracyHalfAngle) : 0f;
            float spacing = ((active != null ? active.ProjectileSize : 1f) * 0.35f);

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
                    bulletGO.transform.localScale = Vector3.one * active.ProjectileSize;

                Bullet b = bulletGO.GetComponent<Bullet>();
                if (b != null)
                {
                    float dmg = (active != null) ? active.Damage : 1f;
                    bool crit = (active != null && Random.value <= active.CritChance);
                    if (crit && active != null) dmg *= active.CritMultiplier;

                    b.Damage = dmg;
                    b.KnockbackForce = (active != null) ? active.KnockbackForce : 0f;
                    b.IsCrit = crit;

                    if (gunSlots != null)
                    {
                        gunSlots.ApplyToBullet(b);
                    }
                    else if (active != null)
                    {
                        b.ApplyRuntimeStats(active);
                    }

                    float speed = (active != null) ? active.ProjectileSpeed : 20f;
                    b.Launch(dir, speed);

                    var cols = bulletGO.GetComponentsInChildren<Collider>();
                    if (cols != null && cols.Length > 0) volleyColliders.AddRange(cols);
                }
                else
                {
                    Debug.LogWarning("Bullet prefab sem componente Bullet.", bulletGO);
                }
            }

            IgnoreSelfCollisions(volleyColliders);

            if (OnFired != null)
                OnFired(active);
        }


        // -------------------- Roll --------------------
        void TryStartRoll()
        {
            if (!canRoll || isRolling || rollAction == null || !rollAction.triggered) return;
            StartRoll();
        }

        void StartRoll()
        {
            Vector3 camF = GetCamForward();
            Vector3 inputDir = GetCameraRelativeInputDirRaw();

            if (inputDir == Vector3.zero)
            {
                rollDirection = camF;
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

        static void IgnoreSelfCollisions(List<Collider> colliders)
        {
            int count = colliders == null ? 0 : colliders.Count;
            for (int a = 0; a < count; a++)
            {
                var ca = colliders[a];
                if (!ca) continue;

                for (int b = a + 1; b < count; b++)
                {
                    var cb = colliders[b];
                    if (cb)
                        Physics.IgnoreCollision(ca, cb, true);
                }
            }
        }


        // -------------------- External Events --------------------
        public event System.Action<WeaponStats> OnFired;

        public void FireOnceFrom(Transform origin, WeaponStats overrideStats = null)
        {
            var active = overrideStats ?? (gunSlots != null && gunSlots.ActiveStats != null ? gunSlots.ActiveStats : stats);
            if (origin == null || bulletPrefab == null || active == null) return;
            SpawnVolleyFrom(origin, active);
        }

        void SpawnVolleyFrom(Transform origin, WeaponStats active)
        {
            int shots = Mathf.Max(1, active.ProjectilesPerShot);
            float spread = active.SpreadAngle;
            float inaccuracyHalf = (1f - Mathf.Clamp01(active.Accuracy)) * active.InaccuracyHalfAngle;
            float spacing = active.ProjectileSize * 0.35f;

            volleyColliders.Clear();

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

                go.transform.localScale = Vector3.one * active.ProjectileSize;

                var b = go.GetComponent<Bullet>();
                if (b != null)
                {
                    float dmg = active.Damage;
                    bool crit = false;
                    if (Random.value <= active.CritChance) { dmg *= active.CritMultiplier; crit = true; }
                    b.Damage = dmg; b.IsCrit = crit; b.KnockbackForce = active.KnockbackForce;
                    if (gunSlots != null)
                    {
                        gunSlots.ApplyToBullet(b);
                    }
                    else if (active != null)
                    {
                        b.ApplyRuntimeStats(active);
                    }   
                    b.Launch(dir, active.ProjectileSpeed);
                }

                var cols = go.GetComponentsInChildren<Collider>();
                if (cols != null && cols.Length > 0) volleyColliders.AddRange(cols);
            }

            IgnoreSelfCollisions(volleyColliders);

        }
    }
}
