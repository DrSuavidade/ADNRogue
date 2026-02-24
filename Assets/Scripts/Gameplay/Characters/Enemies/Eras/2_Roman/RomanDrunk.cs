using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.Habilidades;

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

        [Header("Puddle Animation (Ground)")]
        public Color puddleColor = new Color(0.5f, 0f, 0f, 0.8f); // Vinho tinto por padrão
        public Sprite[] puddleAnimationFrames;
        public float puddleFPS = 10f;
        public float puddleScale = 2.5f;

        [Header("Poison Settings")]
        public float poisonDps = 2.0f;
        public float poisonDuration = 3.0f;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            _config = GetComponent<EnemyConfigurator>();
        }

        /// <summary>
        /// Ataque 1: Trajetória Ébria. A garrafa faz um movimento em S no ar.
        /// Chamado pelo Animation Event.
        /// </summary>
        public void AnimEvent_AttackWobbly()
        {
            LaunchBottle(RomanWineBottleProjectile.BottleType.Wobbly);
        }

        /// <summary>
        /// Ataque 2: Garrafa de Vinho com Poça. Cria uma área de Slow no impacto.
        /// Chamado pelo Animation Event.
        /// </summary>
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
            
            GameObject obj = Instantiate(settings.projectilePrefab, origin.position, origin.rotation);
            
            var proj = obj.GetComponent<RomanWineBottleProjectile>();
            if (!proj) proj = obj.AddComponent<RomanWineBottleProjectile>();

            // Calculamos a direção apenas no plano horizontal para o arco funcionar melhor
            Vector3 startPos = origin.position;
            Vector3 targetPos = target.position;
            Vector3 dir = (targetPos - startPos);
            dir.y = 0; // Ignora altura na direção base
            dir.Normalize();
            
            proj.Init(type, impactDamage, splashRadius, hitMask, dir, throwSpeed, arcHeight, startPos);
            
            if (type == RomanWineBottleProjectile.BottleType.Wobbly)
            {
                proj.SetWobble(wobbleAmplitude, wobbleFrequency);
            }
            
            if (type == RomanWineBottleProjectile.BottleType.Puddle)
            {
                proj.puddlePrefab = puddlePrefab;
                proj.puddleColor = puddleColor;
                proj.puddleFrames = puddleAnimationFrames;
                proj.puddleFPS = puddleFPS;
                proj.puddleScale = puddleScale;
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
        
        [HideInInspector] public Color puddleColor;
        [HideInInspector] public Sprite[] puddleFrames;
        [HideInInspector] public float puddleFPS = 10f;
        [HideInInspector] public float puddleScale = 2.0f;
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

        public void Init(BottleType t, float dmg, float r, LayerMask mask, Vector3 dir, float s, float h, Vector3 start)
        {
            type = t;
            damage = dmg;
            radius = r;
            hitMask = mask;
            direction = dir;
            speed = s;
            arc = h;
            startPos = start;
            startTime = Time.time;
            
            // Define o eixo horizontal para o ziguezague
            horizontalAxis = Vector3.Cross(direction, Vector3.up).normalized;
            
            // Garante que o Rigidbody não interfere com o nosso script de movimento
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Destroy(gameObject, 6f);
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
            if (other.GetComponentInParent<EnemyCore>() != null) return;

            if ((hitMask.value & (1 << other.gameObject.layer)) != 0)
            {
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
                // Garante que a poça spawn no chão, ignorando o player (como no Painter)
                Vector3 rayStart = transform.position + Vector3.up * 1.5f;
                Vector3 spawnPos = transform.position;
                
                // Ignora Player (3), Triggers (2) e IgnoreRaycast (2)
                int floorMask = ~((1 << 3) | (1 << 6) | (1 << 2)); 

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 15f, floorMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + new Vector3(0, 0.05f, 0);
                }
                else
                {
                    // Fallback para o nível do chão se o raycast falhar
                    spawnPos = new Vector3(transform.position.x, 0.1f, transform.position.z);
                }
                
                GameObject p = Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                
                // Inicializamos como PaintPuddle (o script do Painter)
                var paintPuddle = p.GetComponent<PaintPuddle>();
                if (paintPuddle != null)
                {
                    // No Drunk, passamos o poison. O Painter continuará a passar apenas os defaults (poison=0)
                    paintPuddle.Init(puddleColor, puddleFrames, puddleFPS, puddleScale, poisonDps, poisonDuration);
                    
                    // Como queremos apenas Poison no Drunk, zeramos o Slow Amount
                    paintPuddle.slowAmount = 0f; 
                }
            }

            Destroy(gameObject);
        }
    }
}
