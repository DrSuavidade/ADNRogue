using UnityEngine;

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
    [SerializeField] Transform firePoint;

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
    static readonly int AnimMoveX    = Animator.StringToHash("MoveX");
    static readonly int AnimMoveY    = Animator.StringToHash("MoveY");
    static readonly int AnimIsFiring = Animator.StringToHash("IsFiring");
    static readonly int AnimRoll     = Animator.StringToHash("Roll");
    static readonly int AnimSpeed    = Animator.StringToHash("Speed");

    // -------------------- Unity --------------------
    void Awake()
    {
        cc = GetComponent<CharacterController>();
        mainCam = Camera.main;
        if (movementCamera == null) movementCamera = mainCam;
        playerHealth = GetComponent<PlayerHealth>();

        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer = LayerMask.NameToLayer("Enemy");

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

        float interval = (stats != null) ? stats.fireRate : 0.25f;
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + interval;

        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("BulletPrefab or FirePoint not assigned.", this);
            return;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        if (stats != null)
            bullet.transform.localScale = Vector3.one * stats.projectileSize;

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float speed = (stats != null) ? stats.projectileSpeed : 20f;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = firePoint.forward * speed;
#else
            rb.velocity = firePoint.forward * speed;
#endif
        }
        else Debug.LogWarning("Bullet prefab needs a Rigidbody.", bullet);

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            float dmg = (stats != null) ? stats.damage : 1f;
            bool crit = false;
            if (stats != null && Random.value <= stats.critChance)
            {
                dmg *= stats.critMultiplier;
                crit = true;
            }
            b.damage = dmg;
            b.knockbackForce = (stats != null) ? stats.knockbackForce : 0f;
            b.isCrit = crit;
        }
        else Debug.LogWarning("Bullet prefab missing Bullet component.", bullet);
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
        Vector3 t = new Vector3(to.x,   0f, to.z).normalized;
        if (f.sqrMagnitude < 1e-6f || t.sqrMagnitude < 1e-6f) return 0f;
        float angle = Vector3.SignedAngle(f, t, Vector3.up);
        return angle;
    }
}
