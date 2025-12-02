using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.UI
{
    [RequireComponent(typeof(Canvas))]
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private EnemyCore enemy;
        [SerializeField] private Image fillImage;
        [SerializeField] private Vector3 offset = Vector3.up * 1.2f;
        [SerializeField] private bool autoHeightFromEnemy = true;
        [SerializeField] private float extraHeight = 0.15f; // how far above the top of the enemy
        float cachedHeight = -1f;

        public EnemyCore Enemy
        {
            get => enemy;
            set
            {
                if (enemy == value) return;

                // Unsubscribe from old enemy
                if (_subscribed && enemy != null)
                {
                    enemy.OnFirstHit -= OnEnemyFirstHit;
                    _subscribed = false;
                }

                enemy = value;

                // Subscribe to new enemy if we’re active
                if (isActiveAndEnabled && enemy != null)
                {
                    enemy.OnFirstHit += OnEnemyFirstHit;
                    _subscribed = true;

                    // If enemy was already hit before we attached, show bar immediately
                    if (enemy.HasBeenHit)
                        canvas.enabled = true;
                }
            }
        }

        Camera mainCam;
        Canvas canvas;
        bool _subscribed;

        void Awake()
        {
            mainCam = Camera.main;
            canvas = GetComponent<Canvas>();
            canvas.enabled = false;

            if (fillImage == null)
                Debug.LogWarning($"HealthBar on {name} has no fillImage assigned.", this);
        }
        public void Initialize(EnemyCore owner)
        {
            if (enemy == owner) return;

            // Unsubscribe from previous enemy if any
            if (_subscribed && enemy != null)
            {
                enemy.OnFirstHit -= OnEnemyFirstHit;
                _subscribed = false;
            }

            enemy = owner;
            RecomputeOffset();

            // If we're already enabled, hook the event immediately
            if (isActiveAndEnabled && enemy != null && !_subscribed)
            {
                enemy.OnFirstHit += OnEnemyFirstHit;
                _subscribed = true;
            }
        }

        void OnEnable()
        {
            if (enemy == null)
                enemy = GetComponentInParent<EnemyCore>();

            if (enemy != null && !_subscribed)
            {
                enemy.OnFirstHit += OnEnemyFirstHit;
                _subscribed = true;
                RecomputeOffset();

                if (enemy.HasBeenHit)
                    canvas.enabled = true;
            }
        }

        void OnDisable()
        {
            if (_subscribed && enemy != null)
            {
                enemy.OnFirstHit -= OnEnemyFirstHit;
                _subscribed = false;
            }

            if (canvas != null)
                canvas.enabled = false;
        }


        void OnEnemyFirstHit()
        {
            canvas.enabled = true;
        }

        void LateUpdate()
        {
            if (enemy == null || fillImage == null) return;

            if (mainCam == null)
                mainCam = Camera.main;
            if (mainCam == null) return;

            // 1) Position above head and face camera
            transform.position = enemy.transform.position + offset;
            transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);

            // 2) Update fill
            float maxHp = Mathf.Max(enemy.MaxHealth, 0.0001f);
            float pct = enemy.CurrentHealth / maxHp;
            fillImage.fillAmount = pct;

            // 3) Change color by thresholds
            if (pct <= 0.15f)
                fillImage.color = Color.red;
            else if (pct <= 0.45f)
                fillImage.color = Color.yellow;
            else
                fillImage.color = Color.green;
        }

        void RecomputeOffset()
        {
            if (!autoHeightFromEnemy || enemy == null)
                return;

            float h = EstimateHeight(enemy);
            cachedHeight = h;
            offset = Vector3.up * (h + extraHeight);
        }

        float EstimateHeight(EnemyCore e)
        {
            if (e == null) return 1.2f;

            // 1) explicit override per enemy (designer control)
            if (e.HealthBarHeightOverride > 0f)
                return e.HealthBarHeightOverride;

            // 2) auto from CharacterController / Collider
            float h = 1.6f; // a reasonable human-ish default
            var cc = e.GetComponent<CharacterController>();
            if (cc)
            {
                h = cc.height;
            }
            else
            {
                var col = e.GetComponent<Collider>();
                if (col)
                    h = Mathf.Max(1f, col.bounds.size.y);
            }

            return h;
        }

    }
}
