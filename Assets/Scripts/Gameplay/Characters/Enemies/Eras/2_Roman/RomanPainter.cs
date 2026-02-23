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
            Color.red, 
            Color.yellow, 
            Color.green, 
            Color.blue,
            Color.cyan,
            new Color(1f, 0f, 1f) // Magenta
        };

        [Header("Ink Animation (Attack 1)")]
        public Sprite[] inkAnimationFrames; 
        public float inkFPS = 12f;
        public float inkScale = 1.0f;

        [Header("Puddle Animation (Ground)")]
        public Sprite[] puddleAnimationFrames;
        public float puddleFPS = 10f;
        public float puddleScale = 2.0f;

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

            // --- LÓGICA DE SPAWN HORIZONTAL (Igual ao Disco) ---
            Vector3 targetCenter = target.position + Vector3.up * 1.2f;
            Vector3 to = targetCenter - firePoint.position;
            
            Vector3 flatDir = to;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = transform.forward;

            // Pegamos a rotação original do prefab (para respeitar o "deitado")
            Vector3 prefabEuler = inkSplashPrefab.transform.eulerAngles;
            float targetYaw = Quaternion.LookRotation(flatDir).eulerAngles.y;
            // Combinamos X e Z do prefab com o Yaw para o player
            Quaternion spawnRot = Quaternion.Euler(prefabEuler.x, targetYaw, prefabEuler.z);

            GameObject projectile = Instantiate(inkSplashPrefab, firePoint.position, spawnRot);
            
            // Lógica da Animação por Frames
            if (inkAnimationFrames != null && inkAnimationFrames.Length > 0)
            {
                foreach (var r in projectile.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is SpriteRenderer)) r.enabled = false;
                }

                var animator = projectile.GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = projectile.AddComponent<SpriteSheetAnimator>();
                
                animator.Initialize(inkAnimationFrames, inkFPS, SpriteSheetAnimator.AnimationMode.Horizontal);
                
                animator.transform.localScale = Vector3.one * inkScale;
            }

            Color randomColor = paintColors[Random.Range(0, paintColors.Length)];
            
            var projScript = projectile.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = projectile.AddComponent<PaintProjectile>();
            projScript.damage = inkDamage;
            projScript.myColor = randomColor; 
            
            // Passamos a animação da poça
            projScript.puddleFrames = puddleAnimationFrames;
            projScript.puddleFPS = puddleFPS;
            projScript.puddleScale = puddleScale;

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 direction = (targetCenter - firePoint.position).normalized;
                rb.linearVelocity = direction * 15f;
            }
        }

        private void LaunchPaintBucket(Color paintColor)
        {
            if (paintBucketPrefab == null || target == null) return;

            Vector3 spawnPos = target.position + Vector3.up * 3.5f;
            
            GameObject bucket = Instantiate(paintBucketPrefab, spawnPos, Quaternion.Euler(180, Random.Range(0, 360), 0));
            
            ApplyEffects(bucket, paintColor, false);

            var projScript = bucket.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = bucket.AddComponent<PaintProjectile>();
            projScript.damage = bucketDamage;
            projScript.myColor = paintColor;

            // Passamos a animação da poça
            projScript.puddleFrames = puddleAnimationFrames;
            projScript.puddleFPS = puddleFPS;
            projScript.puddleScale = puddleScale;

            Rigidbody rb = bucket.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.down * 5f;
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
