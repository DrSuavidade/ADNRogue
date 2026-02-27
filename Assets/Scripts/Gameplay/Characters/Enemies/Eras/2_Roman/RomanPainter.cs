using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies.Habilidades;
using Geneforge.Gameplay.Visuals;
using Geneforge.Core.Pooling;

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
        public float inkFPS = 6f;
        public float inkScale = 2.0f;

        [Header("Projectile Setting")]
        [Tooltip("Se a tinta voar de lado, mude o Y para 90 ou -90 aqui.")]
        public float yawOffset = 90f;
        public float inkSpeed = 25f; // Aumentado para ser profissional como o Archer
        public bool inkUseGravity = false; // Falso para voar em linha reta perfeita
        [Tooltip("Que layers o projetil pode atingir.")]
        public LayerMask hitMask = ~0;

        [Header("Aiming")]
        [Tooltip("Velocidade com que o pintor gira para encarar o player.")]
        public float turnSpeed = 12f;
        [Tooltip("Se ativado, o pintor vira instantaneamente para o player no momento da pintura.")]
        public bool snapToTargetOnFire = true;

        [Header("Puddle Animation (Ground)")]
        public Sprite[] puddleAnimationFrames;
        public float puddleFPS = 1.2f;
        public float puddleLifetime = 15f;
        public Vector3 puddleScale = new Vector3(2.0f, 2.0f, 1f);
        [Range(0, 360)] public float puddleRotationY = 0f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, closeRangeThreshold);
        }

        protected virtual void Update()
        {
            if (enemy != null && enemy.IsDead) return;

            if (target != null)
            {
                // Rotação suave em direção ao player (apenas no eixo Y)
                Vector3 lookPos = target.position - transform.position;
                lookPos.y = 0; // Mantém o inimigo em pé
                
                if (lookPos.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookPos);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }
        }

        public void AnimEvent_PainterLogic()
        {
            if (target == null) return;

            // SNAP: Ajuste instantâneo para não pintar de lado
            if (snapToTargetOnFire)
            {
                Vector3 finalLook = target.position - transform.position;
                finalLook.y = 0;
                if (finalLook.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(finalLook);
                }
            }

            float distance = Vector3.Distance(transform.position, target.position);
            Color randomColor = paintColors[Random.Range(0, paintColors.Length)];

            if (distance <= closeRangeThreshold)
            {
                LaunchInkSplash(randomColor); 
            }
            else
            {
                LaunchPaintBucket(randomColor);
            }
        }



        private void LaunchInkSplash(Color randomColor)
        {
            if (inkSplashPrefab == null || !target) return;

            // --- LÓGICA IDÊNTICA AO ARCHER ---
            // 1. Alvo na altura do peito/centro
            Vector3 targetPos = target.position + Vector3.up * 1.2f;
            Vector3 toTarget = targetPos - firePoint.position;
            
            // 2. Direção horizontal para orientação visual do prefab
            Vector3 flatDir = toTarget;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.001f) flatDir = transform.forward;
            
            // 3. Calculamos o ângulo Y (Yaw) para o player e somamos o offset do modelo
            float angleY = Quaternion.LookRotation(flatDir).eulerAngles.y + yawOffset;
            Quaternion spawnRot = Quaternion.Euler(0, angleY, 0);

            GameObject projectile = PoolManager.Instance != null 
                ? PoolManager.Instance.Spawn(inkSplashPrefab, firePoint.position, spawnRot)
                : Instantiate(inkSplashPrefab, firePoint.position, spawnRot);
            
            // Ignorar colisão com o próprio atirador
            var shooterCols = GetComponentsInChildren<Collider>();
            var projCols = projectile.GetComponentsInChildren<Collider>();
            foreach (var sCol in shooterCols)
                foreach (var pCol in projCols)
                    Physics.IgnoreCollision(sCol, pCol);

            var rb = projectile.GetComponent<Rigidbody>();
            if (rb == null) rb = projectile.AddComponent<Rigidbody>();

            if (rb != null)
            {
                rb.useGravity = inkUseGravity;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // --- AJUSTE: Movimento Estritamente Horizontal ---
                Vector3 horizontalToTarget = toTarget;
                if (!inkUseGravity) horizontalToTarget.y = 0; // Se não tem gravidade, não sobe nem desce
                
                Vector3 velocity = horizontalToTarget.normalized * inkSpeed;
                if (inkUseGravity) velocity.y += 1.5f;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = velocity;
#else
                rb.velocity = velocity;
#endif
            }

            // Lógica da Animação por Frames
            if (inkAnimationFrames != null && inkAnimationFrames.Length > 0)
            {
                foreach (var r in projectile.GetComponentsInChildren<Renderer>())
                    if (!(r is SpriteRenderer)) r.enabled = false;

                var animator = projectile.GetComponent<SpriteSheetAnimator>();
                if (animator == null) animator = projectile.AddComponent<SpriteSheetAnimator>();
                
                // --- AJUSTE: Definir escala ANTES da inicialização ---
                animator.transform.localScale = Vector3.one * inkScale;

                animator.tintColor = randomColor * 2f; // Dobramos a intensidade para o Bloom (HDR)
                animator.useSpawnScale = true; 
                animator.useFadeOut = false; // Não some enquanto voa
                animator.loop = true; // Repete os frames de tinta enquanto voa
                animator.Initialize(inkAnimationFrames, inkFPS, SpriteSheetAnimator.AnimationMode.Billboard);
            }

            var projScript = projectile.GetComponent<PaintProjectile>();
            if (!projScript) projScript = projectile.AddComponent<PaintProjectile>();
            
            // Inicializa com o Yaw fixo para não girar/tremer (igual ao Archer)
            projScript.Init(inkDamage, hitMask, spawnRot.eulerAngles.y, paintColors[Random.Range(0, paintColors.Length)], inkUseGravity, vfxGenericPrefab);
            
            projScript.puddleFrames = puddleAnimationFrames;
            projScript.puddleFPS = puddleFPS;
            projScript.puddleScale = puddleScale;
            projScript.puddleRotationY = puddleRotationY;
            projScript.puddleLifetime = puddleLifetime;
        }

        private void LaunchPaintBucket(Color paintColor)
        {
            if (paintBucketPrefab == null || target == null) return;

            Vector3 spawnPos = target.position + Vector3.up * 3.5f;
            
            GameObject bucket = PoolManager.Instance != null
                ? PoolManager.Instance.Spawn(paintBucketPrefab, spawnPos, Quaternion.Euler(180, Random.Range(0, 360), 0))
                : Instantiate(paintBucketPrefab, spawnPos, Quaternion.Euler(180, Random.Range(0, 360), 0));
            
            ApplyEffects(bucket, paintColor, false);

            var projScript = bucket.GetComponent<PaintProjectile>();
            if (projScript == null) projScript = bucket.AddComponent<PaintProjectile>();
            
            // Usamos a mesma lógica de Init, mandando 'true' para a gravidade do balde
            projScript.Init(bucketDamage, hitMask, bucket.transform.eulerAngles.y, paintColor, true, vfxGenericPrefab);

            // Passamos a animação da poça
            projScript.puddleFrames = puddleAnimationFrames;
            projScript.puddleFPS = puddleFPS;
            projScript.puddleScale = puddleScale;
            projScript.puddleRotationY = puddleRotationY;
            projScript.puddleLifetime = puddleLifetime;

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
