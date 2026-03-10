using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.AI;

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
        
        [Header("UI Mode")]
        [Tooltip("Check this if you placed this prefab directly on your HUD/Canvas overlay. It will skip world-space tracking.")]
        public bool isScreenSpaceUI = false;
        [SerializeField] private GameObject visualRoot;
        
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
                    enemy.OnIntroFinished -= OnIntroFinished;
                    _subscribed = false;
                }

                enemy = value;

                // Subscribe to new enemy if we’re active
                if (isActiveAndEnabled && enemy != null)
                {
                    enemy.OnFirstHit += OnEnemyFirstHit;
                    enemy.OnIntroFinished += OnIntroFinished;
                    _subscribed = true;

                    // If enemy was already hit before we attached, show bar immediately
                    if (enemy.HasBeenHit || isScreenSpaceUI)
                        SetVisible(true);
                }
            }
        }

        Camera mainCam;
        Canvas canvas;
        CanvasGroup group;
        bool _subscribed;

        void Awake()
        {
            mainCam = Camera.main;
            canvas = GetComponent<Canvas>();
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            if (visualRoot == null && transform.childCount > 0)
                visualRoot = transform.GetChild(0).gameObject;

            SetVisible(false);

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
                enemy.OnIntroFinished += OnIntroFinished;
                _subscribed = true;
            }
        }

        void OnEnable()
        {
            if (enemy == null)
                enemy = GetComponentInParent<EnemyCore>();

            if (enemy != null)
            {
                if (isScreenSpaceUI)
                {
                    // For Boss HUD, start hidden and wait for the intro event
                    SetVisible(false);
                }

                if (!_subscribed)
                {
                    enemy.OnFirstHit += OnEnemyFirstHit;
                    enemy.OnIntroFinished += OnIntroFinished;
                    _subscribed = true;
                    RecomputeOffset();

                    // Auto-show logic
                    if (!isScreenSpaceUI && enemy.HasBeenHit)
                    {
                        SetVisible(true);
                    }
                }
            }
        }

        void OnDisable()
        {
            if (_subscribed && enemy != null)
            {
                enemy.OnFirstHit -= OnEnemyFirstHit;
                enemy.OnIntroFinished -= OnIntroFinished;
                _subscribed = false;
            }

            SetVisible(false);
        }


        void OnEnemyFirstHit()
        {
            if (!isScreenSpaceUI) SetVisible(true);
            else 
            {
                // Backup: if they hit the boss before intro finishes, fade in anyway
                if (visualRoot != null && !visualRoot.activeInHierarchy)
                    OnIntroFinished();
            }
        }

        void OnIntroFinished()
        {
            if (isScreenSpaceUI)
            {
                StopAllCoroutines();
                StartCoroutine(FadeInRoutine());
            }
        }

        private void SetVisible(bool visible)
        {
            if (visualRoot != null) visualRoot.SetActive(visible);
            
            // If we are world space, we might still want to enable/disable canvas for performance 
            // but for ScreenSpace HUD we keep the canvas ON so it doesn't kill the whole HUD.
            if (!isScreenSpaceUI && canvas != null) canvas.enabled = visible;
            
            if (group != null) group.alpha = visible ? 1f : 0f;
        }

        private IEnumerator FadeInRoutine()
        {
            if (visualRoot == null) yield break;
            
            if (group == null) group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            
            visualRoot.SetActive(true);
            group.alpha = 0f;
            
            float duration = 1f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            
            group.alpha = 1f;
        }

        void LateUpdate()
        {
            if (enemy == null || fillImage == null) return;

            if (!isScreenSpaceUI)
            {
                if (mainCam == null)
                    mainCam = Camera.main;
                if (mainCam == null) return;

                // 1) Position above head and face camera
                transform.position = enemy.transform.position + offset;
                transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);
            }

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
