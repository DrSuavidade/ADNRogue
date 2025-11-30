using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.UI
{
    [RequireComponent(typeof(Canvas))]
    public class HealthBar : MonoBehaviour
    {
        public Enemy enemy;
        public Image fillImage;
        public Vector3 offset = Vector3.up * 1.2f;

        Camera mainCam;
        Canvas canvas;

        void Awake()
        {
            mainCam = Camera.main;
            canvas = GetComponent<Canvas>();
            canvas.enabled = false;

            if (fillImage == null)
                Debug.LogWarning($"HealthBar on {name} has no fillImage assigned.", this);
        }

        void OnEnable()
        {
            if (enemy == null)
                enemy = GetComponentInParent<Enemy>();

            if (enemy != null)
                enemy.OnFirstHit += OnEnemyFirstHit;
            else
                Debug.LogError($"HealthBar on {name} couldn’t find an Enemy.", this);
        }

        void OnDisable()
        {
            if (enemy != null)
                enemy.OnFirstHit -= OnEnemyFirstHit;
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
    }
}
