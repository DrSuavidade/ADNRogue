using System.Collections.Generic;
using UnityEngine;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    /// <summary>
    /// Manages a spiral/ring of orbs around the Boss.
    /// Handles rotation and provides functionality to launch orbs as projectiles.
    /// </summary>
    public class BossOrbiter : MonoBehaviour
    {
        [Header("Orbit Config")]
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private int orbCount = 6;
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private float rotationSpeed = 60f;
        [SerializeField] private float verticalOffset = 1.3f;
        [SerializeField] private float lerpSpeed = 8f;
        [SerializeField] private float bobIntensity = 0.2f;

        [Header("Projectile Stats")]
        [SerializeField] private float defaultDamage = 15f;
        [SerializeField] private float projectileSpeed = 18f;
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private bool defaultHoming = false;

        private List<BossOrb> _activeOrbs = new List<BossOrb>();
        private float _angleOffset;
        private EnemyCore _core;

        private bool _initializedThisLife = false;

        private void Awake()
        {
            _core = GetComponentInParent<EnemyCore>();
        }

        private void OnEnable()
        {
            _initializedThisLife = false; // Reset quando o boss sai do pool
            if (_core != null)
            {
                _core.OnIntroFinished -= HandleIntroFinished;
                _core.OnIntroFinished += HandleIntroFinished;
            }
        }

        private void OnDisable()
        {
            if (_core != null)
            {
                _core.OnIntroFinished -= HandleIntroFinished;
            }
        }

        private void HandleIntroFinished()
        {
            if (_initializedThisLife) return;
            
            Debug.Log($"[BossOrbiter] Intro finished on {_core.name}. First time initialization.");
            InitializeOrbs();
        }

        private void Start()
        {
            // Vazio
        }

        public void InitializeOrbs()
        {
            if (_initializedThisLife) return;
            _initializedThisLife = true;

            // Limpeza de segurança
            foreach (var orb in _activeOrbs)
            {
                if (orb != null && orb.gameObject.activeInHierarchy)
                    PoolManager.Instance.Reclaim(orb.gameObject);
            }
            _activeOrbs.Clear();

            for (int i = 0; i < orbCount; i++)
            {
                SpawnAndAddOrb();
            }
        }

        private void SpawnAndAddOrb()
        {
            if (orbPrefab == null || PoolManager.Instance == null) return;

            var obj = PoolManager.Instance.Spawn(orbPrefab, transform.position, Quaternion.identity);
            var orb = obj.GetComponent<BossOrb>();
            if (orb == null) orb = obj.AddComponent<BossOrb>();

            orb.SetOrbiting();
            _activeOrbs.Add(orb);
        }

        private void Update()
        {
            _angleOffset += rotationSpeed * Time.deltaTime;

            int count = _activeOrbs.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                // Calculate position in the spiral/ring
                float angle = _angleOffset + (i * (360f / count));
                float rad = angle * Mathf.Deg2Rad;

                // Vertical bobbing based on angle for a "floating" look
                float bob = Mathf.Sin((_angleOffset * 1.5f + i * 30f) * Mathf.Deg2Rad) * bobIntensity;

                Vector3 targetLocalPos = new Vector3(
                    Mathf.Cos(rad) * radius,
                    verticalOffset + bob,
                    Mathf.Sin(rad) * radius
                );

                Vector3 targetWorldPos = transform.position + targetLocalPos;

                // Smoothly lerp to position to follow boss movements without jitters
                _activeOrbs[i].transform.position = Vector3.Lerp(_activeOrbs[i].transform.position, targetWorldPos, Time.deltaTime * lerpSpeed);
                
                // Rotate the orb itself for flair
                _activeOrbs[i].transform.Rotate(Vector3.up, 120f * Time.deltaTime);
            }
        }

        /// <summary>
        /// Launches the closest orb to the target or just the first available one.
        /// Automatically spawns a replacement orb in the orbit.
        /// </summary>
        public void LaunchOrb(Transform target, float? customDamage = null, float? customSpeed = null, bool? homing = null)
        {
            if (_activeOrbs.Count == 0 || target == null) return;

            // Pick an orb (could be refined to pick the one most facing the target)
            BossOrb orbToLaunch = _activeOrbs[0];
            _activeOrbs.RemoveAt(0);

            float finalDamage = customDamage ?? defaultDamage;
            float finalSpeed = customSpeed ?? projectileSpeed;
            bool finalHoming = homing ?? defaultHoming;

            orbToLaunch.Launch(finalDamage, hitMask, target, finalSpeed, finalHoming, 5f);

            // Immediately spawn a replacement so the ring stays full
            SpawnAndAddOrb();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * verticalOffset, radius);
        }
    }
}
