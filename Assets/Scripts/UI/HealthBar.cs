using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class HealthBar : MonoBehaviour
{
    public Enemy enemy;       // optional: auto‐found if left null
    public Image fillImage;   // assign in inspector
    public Vector3 offset = Vector3.up * 1.2f;

    Camera mainCam;
    Canvas canvas;

    void Awake()
    {
        mainCam = Camera.main;
        canvas = GetComponent<Canvas>();
        canvas.enabled = false;
    }

    void Start()
    {
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();

        if (enemy != null)
            enemy.OnFirstHit += OnEnemyFirstHit;
        else
            Debug.LogError($"HealthBar on {name} couldn’t find an Enemy.", this);
    }

    void OnDestroy()
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
        if (enemy == null) return;

        // 1) Position above head and face camera
        transform.position = enemy.transform.position + offset;
        transform.rotation = Quaternion.LookRotation(transform.position - mainCam.transform.position);

        // 2) Update fill
        float pct = enemy.CurrentHealth / enemy.MaxHealth;
        fillImage.fillAmount = pct;

        // 3) Change color by thresholds
        if (pct <= 0.15f)
        {
            fillImage.color = Color.red;
        }
        else if (pct <= 0.45f)
        {
            fillImage.color = Color.yellow;
        }
        else
        {
            fillImage.color = Color.green; // or your default
        }
    }
}
