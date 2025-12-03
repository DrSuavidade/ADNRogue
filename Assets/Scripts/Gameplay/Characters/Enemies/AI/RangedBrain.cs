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
                if (sqr <= 0.25f)
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

                return; // muito importante: nada de ataques aqui
            }

            // 2) Está demasiado perto → fugir para trás / reposicionamento
            if (dist < minRange)
            {
                StartReposition();
                return;
            }

            // 3) Está demasiado longe da distância ideal → aproximar
            if (dist > preferredRange)
            {
                MoveTowards(target.position, wanderSpeed * 1.2f);
                return;
            }

            // 4) Estamos na “zona ideal” → parar, virar e atacar
            FaceTarget();

            // Não chamamos Strafe(dt); -> deixamos de andar às voltas ao jogador

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

            // Disparar animação
            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            // A parte de lançar a lança / disparar projétil
            // fica a cargo do Animation Event + RangedAttackExecutor
        }

        // ---------- Reposicionar quando o player está demasiado perto ----------

        void StartReposition()
        {
            if (target == null)
            {
                isRepositioning = false;
                return;
            }

            // Direção do player -> inimigo (para fugir nessa direção)
            Vector3 toEnemy = transform.position - target.position;
            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude < 0.01f)
            {
                // Estamos demasiado em cima do player, escolhe uma direção qualquer
                Vector2 rnd2D = Random.insideUnitCircle.normalized;
                toEnemy = new Vector3(rnd2D.x, 0f, rnd2D.y);
            }
            else
            {
                toEnemy.Normalize();
            }

            float desiredRadius = Mathf.Max(minRange, preferredRange);

            repositionTarget = target.position + toEnemy * desiredRadius;

            isRepositioning = true;

            if (animator != null)
                animator.SetFloat("Speed", 1f); // se usares isto para blend de run
        }
    }
}
