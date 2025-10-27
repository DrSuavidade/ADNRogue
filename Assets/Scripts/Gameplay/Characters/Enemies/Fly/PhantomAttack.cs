using System.Collections;
using UnityEngine;
using Geneforge.Gameplay.Characters.Player; // para PlayerHealth

namespace Geneforge.Gameplay.Characters.Enemies.Fly
{
    public class PhantomAttack : MonoBehaviour
    {
        [Header("Agendamento")]
        [Tooltip("Intervalo fixo entre ataques (segundos).")]
        public float intervalSeconds = 10f;   // <<< agora 10 segundos

        [Tooltip("Atraso antes do PRIMEIRO ataque (0 = começa já).")]
        public float initialDelaySeconds = 0f;

        [Header("Telegrapho / Dano")]
        public float telegraphRadius = 3f;
        public float windupTime = 2.0f;
        public float damagePerHit = 10f;

        [Header("Avião (opcional)")]
        public GameObject planePrefab;
        public float planeHeight = 15f;
        public float planeDistance = 30f;

        [Header("Círculo (LineRenderer)")]
        public float lineWidth = 0.08f;
        public int segments = 64;
        public LayerMask groundMask = ~0;
        public float yOffset = 0.02f;

        [Header("Debug")]
        public bool debugLogs = false;

        // refs
        Transform player;
        PlayerHealth playerHealth;

        // internos
        LineRenderer lr;
        Vector3 impactCenter;
        GameObject activePlane;
        Material lrMat;

        void Start()
        {
            AutoFindPlayer();
            StartCoroutine(AttackLoop());
        }

        void AutoFindPlayer()
        {
            if (player == null)
            {
                var pObj = GameObject.FindWithTag("Player");
                if (pObj) player = pObj.transform;
            }
            if (player == null || playerHealth == null)
            {
                var ph = FindAnyObjectByType<PlayerHealth>();
                if (ph != null) { playerHealth = ph; player = ph.transform; }
            }
            if (player != null && playerHealth == null)
            {
                playerHealth = player.GetComponent<PlayerHealth>()
                            ?? player.GetComponentInChildren<PlayerHealth>(true)
                            ?? player.GetComponentInParent<PlayerHealth>();
            }
        }

        IEnumerator AttackLoop()
        {
            if (initialDelaySeconds > 0f)
                yield return new WaitForSeconds(initialDelaySeconds);

            var waitInterval = new WaitForSeconds(intervalSeconds);

            while (true)
            {
                if (player == null) AutoFindPlayer();
                if (player != null)
                    yield return DoOneAttack();
                else if (debugLogs) Debug.LogWarning("[PhantomAttack] Player não encontrado.");

                yield return waitInterval;
            }
        }

        IEnumerator DoOneAttack()
        {
            if (debugLogs) Debug.Log("[PhantomAttack] Ataque iniciado.");

            impactCenter = GetGroundPointUnder(player.position);

            SpawnPlaneForThisAttack();

            EnsureLR();
            DrawCircle(impactCenter, telegraphRadius);

            yield return new WaitForSeconds(windupTime);

            TryDamage();

            if (activePlane != null)
            {
                Destroy(activePlane);
                activePlane = null;
            }

            HideCircle();

            if (debugLogs) Debug.Log("[PhantomAttack] Ataque concluído.");
        }

        void SpawnPlaneForThisAttack()
        {
            Vector3 forward = player.forward.normalized;
            if (forward == Vector3.zero) forward = Vector3.forward;

            Vector3 startPos = player.position - forward * planeDistance;
            Vector3 endPos   = player.position + forward * planeDistance;

            startPos.y = impactCenter.y + planeHeight;
            endPos.y   = impactCenter.y + planeHeight;

            if (planePrefab != null)
            {
                activePlane = Instantiate(planePrefab, startPos, Quaternion.identity);
            }
            else
            {
                activePlane = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                activePlane.name = "PlaneFallback";
                var mr = activePlane.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material = new Material(Shader.Find("Standard"));
                    mr.material.color = new Color(1f, 0.85f, 0.2f, 1f);
                }
                var col = activePlane.GetComponent<Collider>();
                if (col != null) Destroy(col);
                activePlane.transform.localScale = new Vector3(1.2f, 1.2f, 6f);

                var trail = activePlane.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.startWidth = 0.6f;
                trail.endWidth = 0.0f;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.material.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            }

            Vector3 dir = (endPos - startPos).normalized;
            if (dir != Vector3.zero)
                activePlane.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            StartCoroutine(MovePlane(activePlane, startPos, endPos, windupTime));
        }

        IEnumerator MovePlane(GameObject plane, Vector3 start, Vector3 end, float duration)
        {
            float t = 0f;
            while (plane != null && t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                plane.transform.position = Vector3.Lerp(start, end, k);
                yield return null;
            }
        }

        void TryDamage()
        {
            if (player == null) return;
            Vector3 pos = player.position;
            Vector2 delta = new Vector2(pos.x - impactCenter.x, pos.z - impactCenter.z);
            if (delta.magnitude <= telegraphRadius + 0.01f && playerHealth != null)
            {
                playerHealth.ApplyDamage(damagePerHit);
                if (debugLogs) Debug.Log("[PhantomAttack] Dano aplicado ao player.");
            }
            else if (debugLogs)
            {
                Debug.Log("[PhantomAttack] Player esquivou-se, sem dano.");
            }
        }

        Vector3 GetGroundPointUnder(Vector3 worldPos)
        {
            Vector3 start = worldPos + Vector3.up * 50f;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * yOffset;

            return new Vector3(worldPos.x, worldPos.y + yOffset, worldPos.z);
        }

        // círculo
        void EnsureLR()
        {
            if (lr != null) { lr.enabled = true; return; }

            var go = new GameObject("PhantomTelegraph");
            lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.positionCount = segments;
            lr.widthMultiplier = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default");
            lrMat = shader ? new Material(shader) : new Material(Shader.Find("Unlit/Color"));
            lrMat.color = new Color(1f, 0f, 0f, 0.9f);
            lr.material = lrMat;

            lr.enabled = true;
        }

        void DrawCircle(Vector3 center, float radius)
        {
            if (lr == null) return;
            Vector3[] pts = new Vector3[segments];
            float step = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * step;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * radius,
                                     center.y,
                                     center.z + Mathf.Sin(a) * radius);
            }
            lr.SetPositions(pts);
        }

        void HideCircle()
        {
            if (lr != null) lr.enabled = false;
        }

        void OnDisable() => HideCircle();

        void OnDestroy()
        {
            if (lr != null) Destroy(lr.gameObject);
            if (lrMat != null) Destroy(lrMat);
            if (activePlane != null) Destroy(activePlane);
        }
    }
}
