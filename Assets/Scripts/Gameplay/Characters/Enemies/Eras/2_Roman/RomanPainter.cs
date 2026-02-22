using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanPainter : RomanEnemyAbilityBase
    {
        [Header("References")]
        public Transform firePoint;
        public GameObject inkSplashPrefab;    // O JATO ARCO-ÍRIS
        public GameObject paintBucketPrefab;  // O BALDE COLORIDO

        [Header("Tactical Decision")]
        public float closeRangeThreshold = 8f; 

        [Header("Attack Settings")]
        public float inkDamage = 10f;     // Dano do jato
        public float bucketDamage = 25f;  // Dano do balde

        [Header("Visuals - Paint Colors")]
        public Color[] paintColors = new Color[] { 
            Color.cyan, 
            new Color(1f, 0f, 1f), // Magenta
            Color.yellow, 
            new Color(0.1f, 0.1f, 0.1f) // Black/Ink
        };

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, closeRangeThreshold);
        }

        public void AnimEvent_PainterLogic()
        {
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            Color randomColor = paintColors[Random.Range(0, paintColors.Length)];

            if (distance <= closeRangeThreshold)
            {
                LaunchInkSplash(); 
            }
            else
            {
                LaunchPaintBucket(randomColor);
            }
        }

        private void LaunchInkSplash()
        {
            if (inkSplashPrefab == null || firePoint == null) return;

            GameObject projectile = Instantiate(inkSplashPrefab, firePoint.position, firePoint.rotation);
            ApplyEffects(projectile, Color.white, true);

            // Passar o dano
            var projScript = projectile.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = projectile.AddComponent<PaintProjectile>();
            projScript.damage = inkDamage;

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 direction = (target.position + Vector3.up - firePoint.position).normalized;
                rb.linearVelocity = direction * 15f;
            }
        }

        private void LaunchPaintBucket(Color paintColor)
        {
            if (paintBucketPrefab == null || firePoint == null) return;

            GameObject bucket = Instantiate(paintBucketPrefab, firePoint.position, Quaternion.identity);
            ApplyEffects(bucket, paintColor, false);

            // Passar o dano
            var projScript = bucket.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = bucket.AddComponent<PaintProjectile>();
            projScript.damage = bucketDamage;

            Rigidbody rb = bucket.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 toTarget = (target.position - firePoint.position);
                Vector3 horizontalDir = new Vector3(toTarget.x, 0, toTarget.z).normalized;
                Vector3 launchForce = (horizontalDir * 12f) + (Vector3.up * 5f);
                rb.AddForce(launchForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            }
        }

        private void ApplyEffects(GameObject obj, Color color, bool isRainbow)
        {
            var trail = obj.GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = isRainbow ? Color.white : color;
                trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
            }

            var renderers = obj.GetComponentsInChildren<Renderer>();
            var propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", color);
            propBlock.SetColor("_BaseColor", color);

            foreach (var r in renderers)
            {
                r.SetPropertyBlock(propBlock);
            }
        }
    }
}
