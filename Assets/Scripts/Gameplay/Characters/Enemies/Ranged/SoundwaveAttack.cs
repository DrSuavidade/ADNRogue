using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Player;

public class SoundwaveAttack : MonoBehaviour
{
    [Header("Ondas")]
    public int waveCount = 3;
    public float waveInterval = 0.25f;
    public float waveDuration = 0.45f;
    public float maxRadius = 6f;

    [Header("Visual (tipo Nautilus)")]
    public float lineWidth = 0.08f;
    public int segments = 64;
    public Color ringColor = new Color(0.5f, 0.9f, 1f, 0.9f);
    public float yOffset = 0.05f;

    [Header("Dano")]
    public float damage = 10f;

    public void Fire(Vector3 center)
    {
        StartCoroutine(SpawnWaves(center));
    }

    IEnumerator SpawnWaves(Vector3 center)
    {
        for (int i = 0; i < waveCount; i++)
        {
            SpawnRing(center);
            yield return new WaitForSeconds(waveInterval);
        }
    }

    void SpawnRing(Vector3 center)
    {
        var go = new GameObject("SoundWaveRing_VFX");
        go.transform.position = center + new Vector3(0, yOffset, 0);

        // LineRenderer igual ao Nautilus
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Mathf.Max(16, segments);
        lr.widthMultiplier = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingOrder = 5000;
        lr.startColor = ringColor;
        lr.endColor = ringColor;

        // Collider trigger para dano
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.01f;

        // Expansor / animador (tipo Nautilus)
        go.AddComponent<RingExpander>()
          .Init(center + new Vector3(0, yOffset, 0), maxRadius, waveDuration, lr, col, ringColor, damage);
    }

    // ------------------------------------------------------------------
    // RingExpander: animação igual ao exemplo Nautilus
    // ------------------------------------------------------------------
    class RingExpander : MonoBehaviour
    {
        Vector3 center;
        float targetR, life, t;
        LineRenderer lr;
        SphereCollider col;
        Color baseColor;
        float damage;

        HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

        public void Init(Vector3 c, float r, float l,
                         LineRenderer line, SphereCollider collider,
                         Color color, float dmg)
        {
            center = c;
            targetR = Mathf.Max(0.01f, r);
            life = Mathf.Max(0.05f, l);
            lr = line;
            col = collider;
            baseColor = color;
            damage = dmg;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            float r = Mathf.Lerp(0f, targetR, k);

            // expande collider
            col.radius = r;

            // desenha círculo
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }

            // fade alpha igual Nautilus
            var ccol = baseColor;
            ccol.a = (1f - k) * baseColor.a;
            lr.startColor = ccol;
            lr.endColor = ccol;

            if (k >= 1f)
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            var ph = other.GetComponentInParent<PlayerHealth>();
            if (ph != null && !hitPlayers.Contains(ph))
            {
                hitPlayers.Add(ph);
                ph.ApplyDamage(damage);
            }
        }
    }
}
