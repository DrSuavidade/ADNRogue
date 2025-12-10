using System.Collections;
using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    /// <summary>
    /// Controlador de bombardeamento aéreo da era Romana.
    /// De X em X segundos, um veículo passa por cima do player,
    /// desenha um círculo no chão (zona de dano) e passado o tempo
    /// de telegráfico aplica dano se o jogador ainda lá estiver.
    /// </summary>
    public class PresentPrivateJet: MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Intervalo entre bombardeamentos (segundos). 600 = 10 minutos.")]
        public float intervalSeconds = 600f;

        [Tooltip("Tempo que o círculo fica visível antes de causar dano.")]
        public float telegraphDuration = 2f;

        [Header("Dano")]
        [Tooltip("Raio do círculo de dano no chão.")]
        public float damageRadius = 3f;

        [Tooltip("Quantidade de dano aplicada ao player se estiver na área.")]
        public float damageAmount = 30f;

        [Tooltip("Layers que podem ser atingidas (normalmente só Player).")]
        public LayerMask hitMask = ~0;

        [Header("Veículo (portador da bomba)")]
        [Tooltip("Prefab do veículo que sobrevoa a zona (ave romana, biga voadora, etc).")]
        public GameObject vehiclePrefab;

        [Tooltip("Altura a que o veículo voa.")]
        public float vehicleHeight = 20f;

        [Tooltip("Distância total que o veículo percorre por cima da zona de impacto.")]
        public float vehicleTravelDistance = 40f;

        [Tooltip("Tempo que o veículo demora a atravessar essa distância.")]
        public float vehicleTravelTime = 3f;

        [Tooltip("Se verdadeiro, o veículo roda sempre para olhar para o player.")]
        public bool vehicleAlwaysLookAtPlayer = true;

        

        [Header("Visual da bomba (opcional, só estético)")]
        [Tooltip("Prefab da bomba a aparecer no centro do círculo (não precisa de collider).")]
        public GameObject bombVisualPrefab;

        Transform _player;
        PlayerHealth _playerHealth;
        Coroutine _loopCo;

        void Awake()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
                _playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        void OnEnable()
        {
            if (_loopCo == null && intervalSeconds > 0f)
                _loopCo = StartCoroutine(StrikeLoop());
        }

        void OnDisable()
        {
            if (_loopCo != null)
            {
                StopCoroutine(_loopCo);
                _loopCo = null;
            }
        }

        IEnumerator StrikeLoop()
        {
            // pequeno offset aleatório para não sincronizar tudo no mesmo frame
            yield return new WaitForSeconds(Random.Range(0f, 1f));

            while (true)
            {
                yield return new WaitForSeconds(intervalSeconds);

                if (_player == null)
                {
                    var playerObj = GameObject.FindWithTag("Player");
                    if (playerObj != null)
                    {
                        _player = playerObj.transform;
                        _playerHealth = playerObj.GetComponent<PlayerHealth>();
                    }
                }

                if (_player != null)
                    yield return ExecuteStrike();
            }
        }

        IEnumerator ExecuteStrike()
{
    if (_player == null)
        yield break;

    // 1) calcula posição de impacto no chão por baixo do player
    Vector3 impactCenter = GetImpactCenter(_player.position);

    // 2) instancia a BOMBA visual no chão (sem círculo)
    GameObject bombInstance = null;
    if (bombVisualPrefab != null)
    {
        bombInstance = Instantiate(
            bombVisualPrefab,
            impactCenter + Vector3.up * 0.1f, // ligeiramente acima do chão
            Quaternion.identity
        );
    }

    // 3) veículo a passar por cima (se quiseres manter)
    if (vehiclePrefab != null)
        StartCoroutine(SpawnAndMoveVehicle(impactCenter));

    // 4) espera o “fuse” da bomba (antes da explosão)
    // usa telegraphDuration como tempo até explodir
    float t = 0f;
    float fuse = Mathf.Max(0f, telegraphDuration);
    while (t < fuse)
    {
        t += Time.deltaTime;
        yield return null;
    }

    // 5) aplica dano à volta da bomba
    ApplyDamage(impactCenter);

    // 6) opcional: destruir a bomba visual depois da explosão
    if (bombInstance != null)
        Destroy(bombInstance);
}


        Vector3 GetImpactCenter(Vector3 playerPos)
        {
            // raycast de cima para baixo para encontrar o chão
            Vector3 origin = playerPos + Vector3.up * 40f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return playerPos;
        }

        


        IEnumerator SpawnAndMoveVehicle(Vector3 impactCenter)
        {
            if (vehiclePrefab == null || _player == null)
                yield break;

            // direcção do player (para o veículo atravessar “por cima” dele)
            Vector3 forward = _player.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
                forward = _player.right;

            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            forward.Normalize();

            float halfDist = vehicleTravelDistance * 0.5f;
            Vector3 start = impactCenter - forward * halfDist;
            Vector3 end = impactCenter + forward * halfDist;
            start.y = end.y = vehicleHeight;

            GameObject obj = Instantiate(vehiclePrefab, start, Quaternion.identity);

            float t = 0f;
            float duration = Mathf.Max(0.01f, vehicleTravelTime);

            while (t < 1f && obj != null)
            {
                t += Time.deltaTime / duration;
                obj.transform.position = Vector3.Lerp(start, end, t);

                if (vehicleAlwaysLookAtPlayer && _player != null)
                {
                    Vector3 lookTarget = _player.position;
                    lookTarget.y = obj.transform.position.y;
                    Vector3 dir = lookTarget - obj.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                        obj.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }

                yield return null;
            }

            if (obj != null)
                Destroy(obj);
        }

        void ApplyDamage(Vector3 center)
        {
            Collider[] hits = Physics.OverlapSphere(center, damageRadius, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var hp = hits[i].GetComponentInParent<PlayerHealth>();
                if (hp != null)
                {
                    hp.ApplyDamage(damageAmount);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }
#endif
    }
}
