using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.AI;
using Geneforge.Gameplay.Characters.UI;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(Collider))]
    public class BossTriggerZone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform spawnPoint;

        [Header("Door Animation")]
        [SerializeField] private Transform door1;
        [SerializeField] private float door1TargetY = -78f;
        [SerializeField] private Transform door2;
        [SerializeField] private float door2TargetY = 84f;
        [SerializeField] private float doorAnimateDuration = 1.0f;

        [Header("Cinematic Cameras")]
        [Tooltip("Assign 3 cameras for: 0-33%, 33-66%, 66-100% of spawn animation.")]
        [SerializeField] private GameObject[] cinematicCameras;

        [Header("UI Controls")]
        [SerializeField] private CanvasGroup hudGroup;
        [SerializeField] private HealthBar bossHealthBar;
        [SerializeField] private Image bossFlashImage;
        [SerializeField] private float uiFadeDuration = 1.0f;

        [Header("Settings")]


        [SerializeField] private bool lockPlayerControl = true;
        [SerializeField] private bool ignoreFirstFrame = true;
        [SerializeField] private float triggerDisableDelay = 2.0f;
        [SerializeField] private float targetZ = -5f;
        [SerializeField] private float playerWalkSpeed = 3f;

        private bool _hasTriggered = false;

        private float _startTime;

        private void Start()
        {
            _startTime = Time.time;
        }


        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Ensure cinematic cameras are off at start
            SwitchStatusCamera(-1);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;
            if (ignoreFirstFrame && Time.time <= _startTime + 0.1f) return;

            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                Debug.Log($"[BossTriggerZone] {name} detected Player: {other.name}. Triggering boss.");
                _hasTriggered = true;
                StartCoroutine(TriggerSequence(player));
            }
        }


        private IEnumerator TriggerSequence(PlayerController player)
        {
            Debug.Log("[BossTriggerZone] Player entered zone. Animating doors and spawning boss.");

            // 1. Optionally lock player control
            if (lockPlayerControl)
            {
                player.IsInCutscene = true;
            }

            // 1.5 Hide UI
            if (hudGroup != null) hudGroup.alpha = 0f;

            // 2. Start delayed entrance blocking

            StartCoroutine(DisableTriggerAfterDelay());

            // 3. Move player to target Z
            yield return StartCoroutine(MovePlayerToTarget(player));

            // 4. Animate doors
            StartCoroutine(AnimateDoors());



            // 2. Spawn boss
            GameObject bossInstance = null;
            if (bossPrefab != null && spawnPoint != null)
            {
                bossInstance = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
                BossBrain bossBrain = bossInstance.GetComponent<BossBrain>();
                
                // If the prefab has delaySpawn = true, we still need to trigger it
                if (bossBrain != null)
                {
                    bossBrain.TriggerSpawn();
                }

                // Link the boss to the HUD Health Bar
                EnemyCore bossCore = bossInstance.GetComponent<EnemyCore>();
                if (bossHealthBar != null && bossCore != null)
                {
                    bossHealthBar.Initialize(bossCore);
                }

                // Link the flash image
                PrehistoricBoss prehistoricBoss = bossInstance.GetComponent<PrehistoricBoss>();
                if (prehistoricBoss != null && bossFlashImage != null)
                {
                    prehistoricBoss.InitializeFlash(bossFlashImage);
                }
            }

            else
            {
                Debug.LogWarning("[BossTriggerZone] BossPrefab or SpawnPoint reference missing!");
            }

            // 5. Wait for spawn to finish and handle camera sequence
            yield return StartCoroutine(CameraSequenceRoutine(bossInstance, player));

            // Notify the boss (and its linked UI) that the intro is done
            if (bossInstance != null)
            {
                EnemyCore bossCore = bossInstance.GetComponent<EnemyCore>();
                if (bossCore != null) bossCore.NotifyIntroFinished();
            }

            // 6. Fade UI back in

            if (hudGroup != null)
            {
                yield return StartCoroutine(FadeUIRoutine(1f));
            }

            // 7. Restore control
            if (lockPlayerControl)

            {
                player.IsInCutscene = false;
            }
            
            Debug.Log("[BossTriggerZone] Boss spawn finished. Control restored.");
        }

        private IEnumerator CameraSequenceRoutine(GameObject bossInstance, PlayerController player)
        {
            if (bossInstance == null) yield break;
            Animator bossAnimator = bossInstance.GetComponentInChildren<Animator>();
            if (bossAnimator == null) yield break;

            // When using Cinemachine, we DON'T disable the Main Camera 
            // because it holds the Cinemachine Brain. Instead, we toggle 
            // Virtual Cameras with higher priority.

            int currentCamIndex = -1;

            // Failsafe to ensure we don't wait forever if the animation doesn't play
            float timeout = 10f;
            float elapsed = 0f;

            while (BossBrain.IsAnyBossSpawning && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                var state = bossAnimator.GetCurrentAnimatorStateInfo(0);
                
                if (state.IsName("Spawn"))
                {
                    float progress = state.normalizedTime;
                    int targetCamIndex = progress < 0.33f ? 0 : (progress < 0.66f ? 1 : 2);
                    
                    if (targetCamIndex != currentCamIndex)
                    {
                        SwitchStatusCamera(targetCamIndex);
                        currentCamIndex = targetCamIndex;
                    }
                }
                yield return null;
            }

            // Deactivate all cinematic cameras to return focus to the player's CM camera
            SwitchStatusCamera(-1);
        }


        private void SwitchStatusCamera(int index)
        {
            if (cinematicCameras == null) return;
            for (int i = 0; i < cinematicCameras.Length; i++)
            {
                if (cinematicCameras[i] != null)
                {
                    cinematicCameras[i].SetActive(i == index);
                }
            }
        }


        private IEnumerator AnimateDoors()
        {
            float elapsed = 0f;
            Quaternion startRot1 = door1 != null ? door1.localRotation : Quaternion.identity;
            Quaternion startRot2 = door2 != null ? door2.localRotation : Quaternion.identity;
            
            Quaternion endRot1 = door1 != null ? Quaternion.Euler(0, door1TargetY, 0) : Quaternion.identity;
            Quaternion endRot2 = door2 != null ? Quaternion.Euler(0, door2TargetY, 0) : Quaternion.identity;

            while (elapsed < doorAnimateDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / doorAnimateDuration;
                // Use SmoothStep for a nice professional feel
                float smoothT = t * t * (3f - 2f * t);

                if (door1 != null) door1.localRotation = Quaternion.Slerp(startRot1, endRot1, smoothT);
                if (door2 != null) door2.localRotation = Quaternion.Slerp(startRot2, endRot2, smoothT);

                yield return null;
            }

            if (door1 != null) door1.localRotation = endRot1;
            if (door2 != null) door2.localRotation = endRot2;
        }

        private IEnumerator DisableTriggerAfterDelay()
        {
            yield return new WaitForSeconds(triggerDisableDelay);
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
                Debug.Log("[BossTriggerZone] Entrance blocked.");
            }
        }

        private IEnumerator MovePlayerToTarget(PlayerController player)
        {
            if (player == null) yield break;

            float margin = 0.1f;
            float speed = (player != null && player.CurrentMovementSpeed > 0) ? player.CurrentMovementSpeed : playerWalkSpeed;
            
            while (Mathf.Abs(player.transform.position.z - targetZ) > margin)
            {
                float zDiff = targetZ - player.transform.position.z;
                Vector3 walkDir = new Vector3(0, 0, Mathf.Sign(zDiff)).normalized * speed;
                
                player.SetCutsceneMovement(walkDir);
                yield return null;
            }

            player.SetCutsceneMovement(Vector3.zero);
            Debug.Log("[BossTriggerZone] Player reached target position.");
        }

        private IEnumerator FadeUIRoutine(float targetAlpha)
        {
            if (hudGroup == null) yield break;

            float startAlpha = hudGroup.alpha;
            float elapsed = 0f;

            while (elapsed < uiFadeDuration)
            {
                elapsed += Time.deltaTime;
                hudGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / uiFadeDuration);
                yield return null;
            }

            hudGroup.alpha = targetAlpha;
        }
    }
}



