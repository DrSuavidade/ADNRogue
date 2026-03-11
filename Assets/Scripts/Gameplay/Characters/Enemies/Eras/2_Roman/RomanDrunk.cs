using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.Habilidades;
using Geneforge.Core.Pooling;
using Geneforge.Gameplay.Visuals;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class RomanDrunk : RomanEnemyAbilityBase
    {
        [Header("General Wine Bottle Settings")]
        public float throwSpeed = 15f;
        public float arcHeight = 1.0f;
        public float impactDamage = 8f;
        public float splashRadius = 2.0f;
        public LayerMask hitMask = ~0;

        [Header("Attack Variants")]
        public GameObject puddlePrefab;
        [Tooltip("Amplitude do balanço para a Trajetória Ébria")]
        public float wobbleAmplitude = 1.5f;
        [Tooltip("Velocidade do balanço para a Trajetória Ébria")]
        public float wobbleFrequency = 5f;

        [Header("VFX Prefabs")]
        [ColorUsage(true, true)] public Color puddleColor = new Color(0.5f, 0f, 0f, 0.8f);
        public GameObject impactPrefab;
        public float impactScaleMult = 1.0f;
        public float puddleLifetime = 15f;
        public float puddleScaleMult = 1.0f;

        [Header("Poison Settings")]
        public float poisonDps = 2.0f;
        public float poisonDuration = 3.0f;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            _config = GetComponent<EnemyConfigurator>();
        }

        public void AnimEvent_AttackWobbly()
        {
            LaunchBottle(RomanWineBottleProjectile.BottleType.Wobbly);
        }

        public void AnimEvent_AttackPuddle()
        {
            LaunchBottle(RomanWineBottleProjectile.BottleType.Puddle);
        }

        private void LaunchBottle(RomanWineBottleProjectile.BottleType type)
        {
            if (_config == null) _config = GetComponent<EnemyConfigurator>();
            if (_config == null || _config.Archetype == null) return;
            
            var settings = _config.Archetype.projectile;
            if (!settings.enabled || settings.projectilePrefab == null || !target) return;

            Transform origin = transform.Find("ProjectileSpawnPoint");
            if (origin == null) origin = transform;
            
            GameObject obj = PoolManager.Instance != null 
                ? PoolManager.Instance.Spawn(settings.projectilePrefab, origin.position, origin.rotation)
                : Instantiate(settings.projectilePrefab, origin.position, origin.rotation);

            // SEGURANÇA: Ignora colisão com o próprio Drunk
            var drunkCols = GetComponentsInChildren<Collider>();
            var bottleCols = obj.GetComponentsInChildren<Collider>();
            foreach (var dCol in drunkCols)
                foreach (var bCol in bottleCols)
                    Physics.IgnoreCollision(dCol, bCol);
            
            var proj = obj.GetComponent<RomanWineBottleProjectile>();
            if (!proj) proj = obj.AddComponent<RomanWineBottleProjectile>();

            Vector3 startPos = origin.position;
            Vector3 targetPos = target.position;
            Vector3 dir = (targetPos - startPos);
            dir.y = 0; 
            dir.Normalize();
            
            proj.Init(type, impactDamage, splashRadius, hitMask, dir, throwSpeed, arcHeight, startPos, impactPrefab);
            
            if (type == RomanWineBottleProjectile.BottleType.Wobbly)
            {
                proj.SetWobble(wobbleAmplitude, wobbleFrequency);
            }
            
            if (type == RomanWineBottleProjectile.BottleType.Puddle)
            {
                proj.puddlePrefab = puddlePrefab;
                proj.puddleColor = puddleColor;
                proj.puddleScaleMult = puddleScaleMult;
                proj.puddleLifetime = puddleLifetime;
                proj.poisonDps = poisonDps;
                proj.poisonDuration = poisonDuration;
            }
        }
    }

    public class RomanWineBottleProjectile : MonoBehaviour
    {
        public enum BottleType { Normal, Wobbly, Puddle }
        
        public BottleType type;
        public GameObject puddlePrefab;
        public GameObject vfxGenericPrefab;
        
        [HideInInspector] public Color puddleColor;
        [HideInInspector] public float puddleLifetime = 10f;
        [HideInInspector] public float puddleScaleMult = 1f;
        [HideInInspector] public float poisonDps;
        [HideInInspector] public float poisonDuration;

        private float damage;
        private float radius;
        private LayerMask hitMask;
        
        private Vector3 startPos;
        private Vector3 direction;
        private float speed;
        private float arc;
        private float startTime;
        
        private float wobbleAmp;
        private float wobbleFreq;
        private Vector3 horizontalAxis;
        private bool _hasExploded;

        public void Init(BottleType t, float dmg, float r, LayerMask mask, Vector3 dir, float s, float h, Vector3 start, GameObject vfxPrefab)
        {
            type = t;
            damage = dmg;
            radius = r;
            hitMask = mask;
            direction = dir;
            speed = s;
            arc = h;
            startPos = start;
            vfxGenericPrefab = vfxPrefab;
            startTime = Time.time;
            _hasExploded = false; // Reset flag
            
            horizontalAxis = Vector3.Cross(direction, Vector3.up).normalized;
            
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            StopAllCoroutines();
            StartCoroutine(LifetimeRoutine(6f));
        }

        private System.Collections.IEnumerator LifetimeRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null && GetComponent<PoolIdentifier>() != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }

        public void SetWobble(float amp, float freq)
        {
            wobbleAmp = amp;
            wobbleFreq = freq;
        }

        private void Update()
        {
            float elapsed = Time.time - startTime;
            
            // 1. Movimento linear base no chão
            Vector3 currentPos = startPos + (direction * speed * elapsed);
            
            // 2. Cálculo do Arco (Sobe e Desce)
            // Calculamos a duração baseada na distância para o arco ser consistente
            float totalDistance = Vector3.Distance(startPos, startPos + direction * 10f); // Referência
            float flightDuration = Mathf.Max(0.5f, Vector3.Distance(startPos, startPos + (direction * speed)) / speed);
            
            float arcPercent = Mathf.Clamp01(elapsed / flightDuration);
            float height = Mathf.Sin(arcPercent * Mathf.PI) * arc;
            
            // 3. Cálculo do Wobble (Ziguezague)
            float sideOffset = 0f;
            if (type == BottleType.Wobbly)
            {
                // Redução drástica do wobble para garantir acerto
                float precisionFactor = Mathf.Pow(1f - arcPercent, 2); 
                sideOffset = Mathf.Sin(elapsed * wobbleFreq) * (wobbleAmp * precisionFactor);
            }

            // Aplicar posição final
            transform.position = currentPos + (Vector3.up * height) + (horizontalAxis * sideOffset);
            
            // Rotação visual para parecer que está a voar/rodar no ar
            transform.Rotate(Vector3.right * 500f * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // SEGURANÇA: Só explode após 0.05s de voo para não bater no pé
            if (_hasExploded || (Time.time - startTime < 0.05f) || other.GetComponentInParent<EnemyCore>() != null) return;

            // Se for camada do hitMask OU uma colisão sólida (não trigger)
            if (((1 << other.gameObject.layer) & hitMask.value) != 0 || !other.isTrigger)
            {
                _hasExploded = true;
                Explode();
            }
        }

        private void Explode()
        {
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                var hp = col.GetComponentInParent<PlayerHealth>();
                if (hp != null) hp.ApplyDamage(damage);
            }

            if (type == BottleType.Puddle && puddlePrefab != null)
            {
                // 1. GARANTIR POSIÇÃO NO CHÃO (Raycast Robusto)
                Vector3 spawnPos = transform.position;
                int floorMask = ~((1 << 3) | (1 << 2) | (1 << 6)); // Ignora Player, IgnoreRaycast, Triggers

                if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 15f, floorMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + new Vector3(0, 0.05f, 0);
                }
                else
                {
                    // Fallback: força a altura do chão se o raycast falhar (assumindo Y=0 como base do cenário)
                    spawnPos.y = 0.05f; 
                }

                // 2. IMPACT LAYER
                if (vfxGenericPrefab != null)
                {
                    if (PoolManager.Instance != null)
                        PoolManager.Instance.Spawn(vfxGenericPrefab, spawnPos + Vector3.up * 0.1f, Quaternion.identity);
                    else
                        Instantiate(vfxGenericPrefab, spawnPos + Vector3.up * 0.1f, Quaternion.identity);
                }

                // 3. POÇA NORMAL
                GameObject p = PoolManager.Instance != null 
                    ? PoolManager.Instance.Spawn(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0))
                    : Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                
                var paintPuddle = p.GetComponent<PaintPuddle>();
                if (paintPuddle != null)
                {
                    paintPuddle.Init(puddleColor, null, 1f, Vector3.one * puddleScaleMult, 0f, poisonDps, poisonDuration, puddleLifetime);
                    paintPuddle.slowAmount = 0f; 
                }
            }

            ReturnToPool();
        }
    }
}
