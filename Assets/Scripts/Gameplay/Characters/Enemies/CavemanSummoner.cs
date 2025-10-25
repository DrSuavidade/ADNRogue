using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies
{
    [RequireComponent(typeof(Enemy))]
    public class CavemanSummoner : MonoBehaviour
    {
        [Header("Prefab do Dinossauro (tem Enemy + Animal)")]
        public GameObject dinosaurPrefab;

        [Header("Spawns")]
        public Transform[] spawnPoints;           // opcional
        public float spawnRadius = 6f;            // usado se não houver spawnPoints
        public float groundRaycastHeight = 5f;
        public LayerMask groundMask = ~0;

        [Header("Regra de população (cap fixo)")]
        [Tooltip("Número alvo de dinossauros ativos. O invocador enche até este valor e repõe quando morre algum.")]
        public int targetAlive = 5;

        [Tooltip("Repor baixas sem cooldown (recomendado).")]
        public bool refillWithoutCooldown = true;

        [Tooltip("Atraso opcional para a reposição (evita picos).")]
        public float refillDelay = 0.15f;

        [Header("Deteção do Player")]
        public string playerTag = "Player";
        public float viewDistance = 18f;
        [Range(0f, 180f)] public float viewHalfAngle = 60f; // cone (120º total)
        public float losRayRadius = 0.25f;                   // raio da LOS (SphereCast)
        public LayerMask losMask = ~0;                       // layers que bloqueiam visão
        public float visionCheckEvery = 0.1f;

        [Header("Debug/Manual")]
        public bool enableTestKey = false;
        public KeyCode testKey = KeyCode.T;

        // estado
        private readonly List<GameObject> _alive = new List<GameObject>();
        private Enemy _ownerEnemy;
        private Collider[] _ownerCols;
        private Transform _player;
        private bool _firstFillDone;

        void Awake()
        {
            _ownerEnemy = GetComponent<Enemy>();
            _ownerCols = GetComponentsInChildren<Collider>(true);
        }

        void Start()
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            _player = go ? go.transform : null;
            StartCoroutine(ControlLoop());
        }

        void Update()
        {
            // limpar referências nulas (mortos/destroyed)
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] == null) _alive.RemoveAt(i);

            if (enableTestKey && Input.GetKeyDown(testKey))
                StartCoroutine(FillToCap(immediate:true));
        }

        IEnumerator ControlLoop()
        {
            while (this != null && gameObject.activeInHierarchy)
            {
                bool sees = CanSeePlayer();

                if (sees && !_firstFillDone)
                {
                    // 1) primeira vez que vê o player -> enche até ao cap
                    yield return FillToCap(immediate:true);
                    _firstFillDone = true;
                }
                else if (sees && _firstFillDone)
                {
                    // 2) manutenção: repõe baixas se estiver abaixo do cap
                    if (_alive.Count < targetAlive)
                        yield return FillToCap(immediate:false); // repor com pequeno atraso (refillDelay)
                }

                yield return new WaitForSeconds(visionCheckEvery);
            }
        }

        IEnumerator FillToCap(bool immediate)
        {
            // enquanto houver vaga e o invocador estiver vivo
            while (_alive.Count < targetAlive && IsOwnerAlive())
            {
                // se não estamos em modo imediato, espera um bocadinho entre reposições
                if (!immediate && refillDelay > 0f)
                    yield return new WaitForSeconds(refillDelay);

                // criar 1 dino (sem cooldown de “ataque”)
                TrySummon(ignoreCooldown: refillWithoutCooldown);
                // a lista é atualizada em Update (remove nulls), mas acabámos de instanciar,
                // por isso incrementa já localmente:
                // (a própria adição é feita em TrySummon)
                yield return null; // um frame de folga para permitir inicializações
            }
        }

        bool IsOwnerAlive()
        {
            return _ownerEnemy != null && _ownerEnemy.CurrentHealth > 0f;
        }

        bool CanSeePlayer()
        {
            if (_player == null) return false;

            Vector3 to = _player.position - transform.position;
            float dist = to.magnitude;
            if (dist > viewDistance) return false;

            // cone
            Vector3 dir = to.normalized;
            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > viewHalfAngle) return false;

            // linha de visão (SphereCast)
            Vector3 origin = transform.position + Vector3.up * 1.6f;
            Vector3 target = _player.position + Vector3.up * 1.2f;
            Vector3 castDir = (target - origin).normalized;
            float castDist = Vector3.Distance(origin, target);

            if (Physics.SphereCast(origin, losRayRadius, castDir, out RaycastHit hit, castDist, losMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.CompareTag(playerTag)) return false;
            }

            return true;
        }

        public bool TrySummon(bool ignoreCooldown)
        {
            if (!IsOwnerAlive()) return false;
            if (_alive.Count >= targetAlive) return false;

            if (!PickSpawn(out Vector3 pos, out Quaternion rot)) return false;

            var go = Instantiate(dinosaurPrefab, pos, rot);

            // ignorar colisões iniciais
            if (_ownerCols != null && _ownerCols.Length > 0)
            {
                var dinoCols = go.GetComponentsInChildren<Collider>(true);
                foreach (var oc in _ownerCols)
                    foreach (var dc in dinoCols)
                        if (oc != null && dc != null)
                            Physics.IgnoreCollision(oc, dc, true);
                StartCoroutine(ReenableCollisionSoon(_ownerCols, dinoCols, 0.75f));
            }

            // orientar para o player, se existir
            if (_player != null)
                go.transform.forward = (_player.position - go.transform.position).normalized;
            else
                go.transform.forward = transform.forward;

            _alive.Add(go);
            return true;
        }

        IEnumerator ReenableCollisionSoon(Collider[] ownerCols, Collider[] dinoCols, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var oc in ownerCols)
                foreach (var dc in dinoCols)
                    if (oc != null && dc != null)
                        Physics.IgnoreCollision(oc, dc, false);
        }

        bool PickSpawn(out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.identity;

            // 1) pontos fixos
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int k = 0; k < spawnPoints.Length; k++)
                {
                    var t = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (t == null) continue;
                    if (TryProjectToGround(t.position, out pos))
                    {
                        rot = LookAtPlayerRotation(pos);
                        return true;
                    }
                }
            }

            // 2) círculo à volta
            const int tries = 12;
            for (int i = 0; i < tries; i++)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(spawnRadius * 0.5f, spawnRadius);
                Vector3 candidate = transform.position + new Vector3(c.x, 0f, c.y);
                if (TryProjectToGround(candidate, out pos))
                {
                    rot = LookAtPlayerRotation(pos);
                    return true;
                }
            }

            pos = Vector3.zero;
            return false;
        }

        Quaternion LookAtPlayerRotation(Vector3 spawnPos)
        {
            Vector3 look = transform.forward;
            if (_player != null) look = (_player.position - spawnPos).normalized;
            look.y = 0f;
            if (look.sqrMagnitude < 0.001f) look = transform.forward;
            return Quaternion.LookRotation(look, Vector3.up);
        }

        bool TryProjectToGround(Vector3 around, out Vector3 grounded)
        {
            Vector3 start = around + Vector3.up * groundRaycastHeight;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, groundRaycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                grounded = hit.point;
                return true;
            }
            grounded = around;
            return false;
        }
    }
}
