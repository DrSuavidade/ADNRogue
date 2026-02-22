using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class RangedBrain : EnemyBrainBase
    {
        [Header("Wander")]
        public float wanderRadius = 6f;
        public float wanderInterval = 4f;
        public float wanderSpeed = 2f;

        [Header("Engagement")]
        public float detectionRadius = 25f;

        // Distância ideal para atacar (zona de “sniper”)
        public float preferredRange = 12f;

        // Se o player entrar abaixo disto, o inimigo foge para trás
        public float minRange = 6f;

        // Ainda existe por causa do EnemyArchetype, mas já não é usado
        public float strafeSpeed = 3f;

        [Header("Attack")]
        public float attackRate = 1.25f;
        public string attackTrigger = "Attack";
        [Range(1, 3)] public int attackVariants = 1;

        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        // Reposicionamento (fugir quando o player está demasiado perto)
        Vector3 repositionTarget;
        bool isRepositioning;

        // Para outros scripts poderem saber quem é o alvo
        public Transform CurrentTarget => target;

        protected override void Awake()
        {
            base.Awake();
            PickWanderTarget();
        }

        protected override void TickBrain(float dt)
        {
            // Sem alvo → vaguear
            if (target == null)
            {
                TickWander(dt);
                return;
            }

            float dist = DistanceToTargetXZ();

            // Está demasiado longe → volta ao comportamento de wander
            if (dist > detectionRadius)
            {
                isRepositioning = false;
                TickWander(dt);
                return;
            }

            // 1) Se estamos num “dash” de reposicionamento, só corremos até ao ponto
            if (isRepositioning)
            {
                float sqr = (transform.position - repositionTarget).sqrMagnitude;
                if (sqr <= 0.4f)
                {
                    // Chegou ao ponto pretendido
                    isRepositioning = false;
                    if (animator != null)
                        animator.SetFloat("Speed", 0f);
                }
                else
                {
                    // Continua a correr para o novo spot (e NÃO ataca neste estado)
                    MoveTowards(repositionTarget, wanderSpeed * 1.5f);
                }

                return; // importante: nada de ataques durante a corrida
            }

            // 2) Se o player está demasiado perto, fugir
            if (dist < minRange)
            {
                StartReposition();
                return;
            }

            // 3) Está demasiado longe da distância ideal → aproximar
            if (dist > preferredRange + 1.5f)
            {
                MoveTowards(target.position, wanderSpeed * 1.2f);
                return;
            }

            // 4) Estamos na “zona ideal” → parar, virar e atacar
            if (animator != null) animator.SetFloat("Speed", 0f);
            FaceTarget();

            // Só tenta atacar se estiver "zona ideal" E estiver minimamente virado para o player
            TryAttack();
        }

        void TickWander(float dt)
        {
            wanderTimer -= dt;
            if (wanderTimer <= 0f)
                PickWanderTarget();

            MoveTowards(wanderTarget, wanderSpeed);
        }

        void PickWanderTarget()
        {
            wanderTimer = wanderInterval;
            wanderTarget = GetRandomPointAroundSpawn(wanderRadius);
        }

        void TryAttack()
        {
            // Cooldown
            if (!IsAttackReady(ref lastAttackTime, attackRate))
                return;

            // --- PROTEÇÃO CONTRA TIRO DE COSTAS ---
            if (target != null)
            {
                Vector3 toTarget = (target.position - transform.position);
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(transform.forward, toTarget.normalized);
                    // Se o ângulo for maior que 40 graus, ainda está a rodar. 
                    if (angle > 40f)
                        return;
                }
            }

            // Disparar animação
            TriggerAttackAnim();
        }

        void TriggerAttackAnim()
        {
            if (animator == null) return;

            if (attackVariants <= 1)
            {
                if (!string.IsNullOrEmpty(attackTrigger))
                    animator.SetTrigger(attackTrigger);
            }
            else
            {
                int idx = UnityEngine.Random.Range(0, attackVariants);
                string suffix = idx == 0 ? "" : (idx + 1).ToString(); // Attack, Attack2, Attack3
                string triggerName = attackTrigger + suffix;
                animator.SetTrigger(triggerName);
            }
        }

        // ---------- Reposicionar (IA Inteligente para procurar espaço) ----------

        void StartReposition()
        {
            if (target == null)
            {
                isRepositioning = false;
                return;
            }

            float avoidRadius = preferredRange;
            Vector3 bestPoint = transform.position;
            float bestScore = -1000f;

            // Vamos testar 8 direções para encontrar um "safe spot"
            Vector3 toEnemy = (transform.position - target.position).normalized;

            for (int i = 0; i < 8; i++)
            {
                float angleDegrees = i * 45f;
                Vector3 directionFromPlayer = Quaternion.Euler(0, angleDegrees, 0) * Vector3.forward;
                Vector3 candidatePoint = target.position + directionFromPlayer * avoidRadius;

                float score = 0f;

                // 1. Queremos fugir do sítio atual, mas PRIORIZAR manter o player à frente
                score += Vector3.Distance(transform.position, candidatePoint) * 0.4f;

                // 2. PENALIZAÇÃO CRÍTICA: Se o ponto estiver do "outro lado" do player, 
                // o inimigo teria de passar por cima dele.
                // Direção do player para o ponto candidato
                Vector3 toCandidate = (candidatePoint - target.position).normalized;
                float alignment = Vector3.Dot(toEnemy, toCandidate);
                
                // Se alignment < 0, o ponto está do lado oposto do player em relação ao inimigo
                // Se alignment for baixo, o inimigo vai passar demasiado perto do player
                if (alignment < 0.2f) 
                {
                    score -= 100f; // Penalização pesada para não vir "para cima" do player
                }

                // 3. Verificar Line of Sight
                if (HasLineOfSightFrom(candidatePoint, target.position))
                    score += 25f;
                else
                    score -= 15f;

                // 4. Evitar pontos dentro de obstáculos
                if (Physics.CheckSphere(candidatePoint + Vector3.up * 1f, 0.7f, lineOfSightMask, QueryTriggerInteraction.Ignore))
                    score -= 60f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = candidatePoint;
                }
            }

            repositionTarget = bestPoint;
            isRepositioning = true;

            if (animator != null)
                animator.SetFloat("Speed", 1f); 
        }

        private bool HasLineOfSightFrom(Vector3 from, Vector3 to)
        {
            Vector3 origin = from + Vector3.up * 1f;
            Vector3 dest = to + Vector3.up * 1f;
            Vector3 dir = dest - origin;
            float dist = dir.magnitude;
            
            if (dist <= 0.1f) return true;

            return !Physics.Raycast(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
        }
    }
}
