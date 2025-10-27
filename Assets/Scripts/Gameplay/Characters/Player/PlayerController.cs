using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Slots;

namespace Geneforge.Gameplay.Characters.Player
{
    [RequireComponent(typeof(CharacterController))]
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
        [SerializeField] WeaponStats stats;                // ScriptableObject with fireRate, damage, etc.
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] public Transform firePoint;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [Tooltip("Transform that visually represents the character (Animator/mesh root). This will rotate during roll so the animation faces the roll direction.")]
        [SerializeField] Transform modelRoot;

        [Header("Dodge Roll")]
        [SerializeField] KeyCode rollKey = KeyCode.Space;
        [SerializeField] float rollDuration = 2f;
        [SerializeField] float rollCooldown = 1f;
        [SerializeField] float rollDistance = 5f;
        [Tooltip("Snap roll direction to 8-way (45° increments) relative to camera.")]
        [SerializeField] bool snapRollToEightWay = true;

        [Header("Gravity")]
        [SerializeField] float gravityY = -35f;       // tune to taste
        [SerializeField] float groundedGravityY = -2f; // small downward to keep grounded contact

        float verticalVelocityY = 0f;

        [SerializeField] GunSlots gunSlots;


        // -------------------- Runtime state --------------------
        CharacterController cc;
        Camera mainCam;
        PlayerHealth playerHealth;

        Vector3 currentMoveWorld;   // camera-relative move vector used this frame
        Vector3 rollDirection;      // roll direction chosen at start (world space, ground-projected)
        float rollSpeed;            // computed from distance/duration

        bool isRolling = false;
        bool canRoll = true;
        float rollTimer = 0f;
        float cooldownTimer = 0f;

        int playerLayer;
        int enemyLayer;

        float nextFireTime = 0f;
        float baseAnimatorSpeed = 1f;

        [Header("Animation – Diagonal Boost")]
        [SerializeField, Tooltip("Enable extra playback speed ONLY on diagonals.")]
        bool diagonalBoostEnabled = true;

        [SerializeField, Tooltip("Max animator speed multiplier at a perfect diagonal (e.g., 1.6–1.8).")]
        float diagonalBoostMax = 1.7f;

        [SerializeField, Tooltip("Response curve exponent (>1 boosts diagonals more aggressively).")]
        float diagonalBoostPower = 1.25f;

        [SerializeField, Tooltip("Deadzone for axes before considering movement.")]
        float animAxisDeadzone = 0.05f;


        // cache for restoring the model orientation after roll
        Quaternion modelPreRollRotation;

        // Animator parameter IDs
        static readonly int AnimMoveX = Animator.StringToHash("MoveX");
        static readonly int AnimMoveY = Animator.StringToHash("MoveY");
        static readonly int AnimIsFiring = Animator.StringToHash("IsFiring");
        static readonly int AnimRoll = Animator.StringToHash("Roll");
        static readonly int AnimSpeed = Animator.StringToHash("Speed");

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
        }

        void Update()
        {
            UpdateRollTimers();

            if (isRolling)
            {
                HandleRollingMovement();
                return; // skip regular movement/shooting while rolling
            }

            HandleMovement();
            HandleShooting();
            UpdateAnimator();
            TryStartRoll();

            if (Input.GetMouseButtonDown(0)) gunSlots?.OnFireHeldStart();
            if (Input.GetMouseButtonUp(0)) gunSlots?.OnFireHeldStop();

        }

        // -------------------- Movement --------------------
        void HandleMovement()
        {
            // Use RAW axes for snappy direction changes (avoids "old direction" after roll)
            float ix = Input.GetAxisRaw("Horizontal");
            float iz = Input.GetAxisRaw("Vertical");

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
                // keep slight downward pull so CC stays snapped to ground
                if (verticalVelocityY < 0f) verticalVelocityY = groundedGravityY;
            }
            else
            {
                verticalVelocityY += gravityY * Time.deltaTime;
            }

            // Apply vertical after horizontal (CC sums multiple Move calls per frame)
            cc.Move(new Vector3(0f, verticalVelocityY, 0f) * Time.deltaTime);

            // Face rules (keep aim forward unless disabled)
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
            if (!Input.GetMouseButton(0)) return;
            if (Time.time < nextFireTime) return;

            // Let the primary ability mutate the live snapshot BEFORE we read fireRate etc.
            gunSlots?.OnAboutToFire();
            var active = (gunSlots != null && gunSlots.ActiveStats != null) ? gunSlots.ActiveStats : stats;

            if (bulletPrefab == null || firePoint == null)
            {
                Debug.LogWarning("BulletPrefab or FirePoint not assigned.", this);
                return;
            }

            float interval = (active != null) ? active.fireRate : 0.25f;
            nextFireTime = Time.time + interval;

            // --- Active stat snapshot (use ACTIVE, not stats) ---
            int shots = Mathf.Max(1, (active != null ? active.projectilesPerShot : 1));
            float spread = (active != null ? active.spreadAngle : 0f);

            // Accuracy: 0 -> max jitter; 1 -> no jitter (yaw jitter scaled by inaccuracyHalfAngle)
            float acc = (active != null) ? Mathf.Clamp01(active.accuracy) : 1f;
            float inaccuracyHalf = (active != null) ? (1f - acc) * Mathf.Max(0f, active.inaccuracyHalfAngle) : 0f;

            // Lateral spacing so they don't spawn on top of each other.
            float spacing = ((active != null ? active.projectileSize : 1f) * 0.35f);

            List<Collider> volleyColliders = new List<Collider>();

            Vector3 forward = firePoint.forward;
            Vector3 axis = firePoint.up;     // yaw axis
            Vector3 right = firePoint.right;  // lateral lanes

            for (int i = 0; i < shots; i++)
            {
                float t = (shots == 1) ? 0f : i / (shots - 1f);
                float angle = (shots == 1) ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Quaternion rot = Quaternion.AngleAxis(angle, axis);
                Vector3 dir = rot * forward;

                // [Accuracy] random yaw jitter per projectile from ACTIVE
                if (inaccuracyHalf > 0f)
                {
                    float jitter = Random.Range(-inaccuracyHalf, inaccuracyHalf);
                    dir = Quaternion.AngleAxis(jitter, axis) * dir;
                }

                // Centered lateral lanes: -N..+N along 'right'
                float lane = (i - (shots - 1) * 0.5f);
                Vector3 spawnPos = firePoint.position + right * (lane * spacing);

                GameObject bulletGO = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));

                // Scale from ACTIVE
                if (active != null)
                    bulletGO.transform.localScale = Vector3.one * active.projectileSize;

                Bullet b = bulletGO.GetComponent<Bullet>();
                if (b != null)
                {
                    // Damage/crit from ACTIVE
                    float dmg = (active != null) ? active.damage : 1f;
                    bool crit = (active != null && Random.value <= active.critChance);
                    if (crit && active != null) dmg *= active.critMultiplier;

                    b.damage = dmg;
                    b.knockbackForce = (active != null) ? active.knockbackForce : 0f;
                    b.isCrit = crit;

                    // Push runtime knobs (lifetime, pierce, bounce, homing, aoe) from ACTIVE
                    if (active != null) b.ApplyRuntimeStats(active);

                    // Launch speed from ACTIVE
                    float speed = (active != null) ? active.projectileSpeed : 20f;
                    b.Launch(dir, speed);

                    // Ability hooks (e.g., Crab bubble visual etc.)
                    gunSlots?.ApplyToBullet(b);

                    var cols = bulletGO.GetComponentsInChildren<Collider>();
                    if (cols != null && cols.Length > 0) volleyColliders.AddRange(cols);
                    OnFired?.Invoke((gunSlots != null && gunSlots.ActiveStats != null) ? gunSlots.ActiveStats : stats);
                }
                else Debug.LogWarning("Bullet prefab missing Bullet component.", bulletGO);
            }

            // Disable collisions between bullets spawned in the same volley
            for (int a = 0; a < volleyColliders.Count; a++)
                for (int bIdx = a + 1; bIdx < volleyColliders.Count; bIdx++)
                    if (volleyColliders[a] && volleyColliders[bIdx])
                        Physics.IgnoreCollision(volleyColliders[a], volleyColliders[bIdx], true);
        }





        // -------------------- Roll --------------------
        void TryStartRoll()
        {
            if (!canRoll || isRolling || !Input.GetKeyDown(rollKey)) return;
            StartRoll();
        }

        void StartRoll()
        {
            // 1) Get camera-relative input direction (or camera forward if no input)
            Vector3 camF = GetCamForward();
            Vector3 inputDir = GetCameraRelativeInputDirRaw(); // unit length or zero

            if (inputDir == Vector3.zero)
            {
                rollDirection = camF; // forward roll
            }
            else
            {
                // 2) Optionally snap to nearest 45° relative to camera forward
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

            // 3) Rotate only the visual model to face the roll direction for animation
            if (modelRoot != null)
            {
                modelPreRollRotation = modelRoot.rotation;
                modelRoot.rotation = Quaternion.LookRotation(rollDirection, Vector3.up);
            }

            // 4) Prepare timers/flags
            rollSpeed = rollDistance / Mathf.Max(0.0001f, rollDuration);
            rollTimer = rollDuration;
            cooldownTimer = rollCooldown;
            isRolling = true;
            canRoll = false;

            // Clear stale move to avoid "old direction" artifacts in the first post-roll frame
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
            // Move strictly along the chosen direction
            cc.Move(rollDirection * rollSpeed * Time.deltaTime);

            // Keep the root aligned with camera forward while rolling (aim stays forward)
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

            // Restore model rotation to forward aim
            if (modelRoot != null)
            {
                // Option A: restore to pre-roll absolute rotation
                // modelRoot.rotation = modelPreRollRotation;

                // Option B (preferred for aiming): face current camera forward
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

            // Camera-relative axes for consistent W/A/S/D animation selection
            Vector3 camF = GetCamForward();
            Vector3 camR = GetCamRight();

            float animX = 0f;
            float animY = 0f;

            if (currentMoveWorld.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = currentMoveWorld.normalized;
                animX = Mathf.Clamp(Vector3.Dot(dir, camR), -1f, 1f); // left(-1) .. right(+1)
                animY = Mathf.Clamp(Vector3.Dot(dir, camF), -1f, 1f); // back(-1) .. forward(+1)
            }

            // Small deadzone to stabilize idle blends
            if (Mathf.Abs(animX) < 0.05f) animX = 0f;
            if (Mathf.Abs(animY) < 0.05f) animY = 0f;

            animator.SetFloat(AnimMoveX, animX);
            animator.SetFloat(AnimMoveY, animY);
            animator.SetBool(AnimIsFiring, Input.GetMouseButton(0));

            // Optional scalar (safe even if not used in the controller)
            float speed01 = Mathf.Clamp01(currentMoveWorld.magnitude / Mathf.Max(0.0001f, moveSpeed));
            animator.SetFloat(AnimSpeed, speed01);

            if (!isRolling && diagonalBoostEnabled && (animX != 0f || animY != 0f))
            {
                float ax = Mathf.Abs(animX);
                float ay = Mathf.Abs(animY);

                // If one axis is basically zero -> straight, no boost
                if (ax < animAxisDeadzone || ay < animAxisDeadzone)
                {
                    animator.speed = baseAnimatorSpeed;
                }
                else
                {
                    // axisMax is ~0.707 at a perfect 45° diagonal and approaches 1.0 near straight
                    float axisMax = Mathf.Max(ax, ay);
                    // Base boost = 1 / axisMax (≈1.414 at perfect diagonal)
                    float baseBoost = 1f / Mathf.Max(axisMax, 0.0001f);
                    // Exponent to push diagonals harder without affecting near-straights much
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

        // Camera-relative input direction using RAW axes (snappy)
        Vector3 GetCameraRelativeInputDirRaw()
        {
            float ix = Input.GetAxisRaw("Horizontal");
            float iz = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(ix, 0f) && Mathf.Approximately(iz, 0f))
                return Vector3.zero;

            Vector3 dir = GetCamForward() * iz + GetCamRight() * ix;
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

        // 1) Let others mirror your shot timing and active stats.
        public event System.Action<WeaponStats> OnFired;

        // 2) Fire a volley from a custom origin (for clones). Does not touch nextFireTime.
        public void FireOnceFrom(Transform origin, WeaponStats overrideStats = null)
        {
            var active = overrideStats ?? (gunSlots != null && gunSlots.ActiveStats != null ? gunSlots.ActiveStats : stats);
            if (origin == null || bulletPrefab == null || active == null) return;
            SpawnVolleyFrom(origin, active);
        }

        // Shared inner logic extracted from your shooting; same rules, just uses `origin`.
        void SpawnVolleyFrom(Transform origin, WeaponStats active)
        {
            int shots = Mathf.Max(1, active.projectilesPerShot);
            float spread = active.spreadAngle;
            float inaccuracyHalf = (1f - Mathf.Clamp01(active.accuracy)) * active.inaccuracyHalfAngle;
            float spacing = active.projectileSize * 0.35f;

            var volleyCols = new System.Collections.Generic.List<Collider>();

            Vector3 forward = origin.forward;
            Vector3 axis    = origin.up;
            Vector3 right   = origin.right;

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

                var go = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir, axis));
                go.transform.localScale = Vector3.one * active.projectileSize;

                var b = go.GetComponent<Bullet>();
                if (b != null)
                {
                    // crit + damage
                    float dmg = active.damage;
                    bool crit = false;
                    if (Random.value <= active.critChance) { dmg *= active.critMultiplier; crit = true; }
                    b.damage = dmg; b.isCrit = crit; b.knockbackForce = active.knockbackForce;
                    b.ApplyRuntimeStats(active);
                    b.Launch(dir, active.projectileSpeed);

                    // abilities
                    gunSlots?.ApplyToBullet(b);
                }

                var cols = go.GetComponentsInChildren<Collider>();
                if (cols != null && cols.Length > 0) volleyCols.AddRange(cols);
            }

            // ignore bullet-bullet collisions in same volley
            for (int a = 0; a < volleyCols.Count; a++)
                for (int b = a + 1; b < volleyCols.Count; b++)
                    if (volleyCols[a] && volleyCols[b])
                        Physics.IgnoreCollision(volleyCols[a], volleyCols[b], true);
        }

    }
}
