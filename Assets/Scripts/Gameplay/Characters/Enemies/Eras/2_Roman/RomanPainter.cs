using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies.Habilidades;
using Geneforge.Gameplay.Visuals;

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

        public Color[] paintColors = new Color[] { 
            Color.cyan, 
            new Color(1f, 0f, 1f), // Magenta
            Color.yellow, 
            new Color(0.1f, 0.1f, 0.1f) // Black/Ink
        };

        [Header("Ink Animation (Attack 1)")]
        public Sprite[] inkAnimationFrames; // Os teus frames de tinta
        public float inkFPS = 12f;
        public float inkScale = 1.0f;

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
            
            // Lógica da Animação por Frames
            if (inkAnimationFrames != null && inkAnimationFrames.Length > 0)
            {
                // Desativa modelos 3D originais para não sobrepor
                foreach (var r in projectile.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is SpriteRenderer)) r.enabled = false;
                }

                var animator = projectile.GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = projectile.AddComponent<SpriteSheetAnimator>();
                
                animator.Initialize(inkAnimationFrames, inkFPS, SpriteSheetAnimator.AnimationMode.Horizontal);
                
                // Aplicamos a escala no componente que contém o SpriteRenderer (que agora pode ser um filho)
                animator.transform.localScale = Vector3.one * inkScale;
            }

            // Sorteamos uma cor base para a poça se o jato bater em algo
            Color randomColor = paintColors[Random.Range(0, paintColors.Length)];
            // Passar o dano
            var projScript = projectile.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = projectile.AddComponent<PaintProjectile>();
            projScript.damage = inkDamage;
            projScript.myColor = randomColor; 

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
            if (paintBucketPrefab == null || target == null) return;

            // SPAWN EM CIMA DO PLAYER (3.5 metros acima para ser rápido e visível)
            Vector3 spawnPos = target.position + Vector3.up * 3.5f;
            
            // Instancia o balde virado para baixo e com rotação aleatória
            GameObject bucket = Instantiate(paintBucketPrefab, spawnPos, Quaternion.Euler(180, Random.Range(0, 360), 0));
            
            ApplyEffects(bucket, paintColor, false);

            var projScript = bucket.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = bucket.AddComponent<PaintProjectile>();
            projScript.damage = bucketDamage;
            projScript.myColor = paintColor;

            Rigidbody rb = bucket.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                
                // Forçar a queda vertical
                rb.linearVelocity = Vector3.down * 5f;
                // Adicionar um pouco de rotação caótica
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
            }

            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(paintColor)}><b>[PAINTER]</b> Balde invocado sobre o player!</color>");
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
