using UnityEngine;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Map;
using Geneforge.Gameplay.Weapons.Slots;
using Geneforge.Gameplay.Items;
using System.Text;

namespace Geneforge.UI.DebugTools
{
    public class DebugStatsOverlay : MonoBehaviour
    {
        private bool _isVisible = false;
        private Vector2 _scrollPosition;
        private Rect _windowRect = new Rect(20, 20, 400, 600);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var go = new GameObject("DebugStatsOverlay");
            go.AddComponent<DebugStatsOverlay>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.pKey.wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
            }
            
            // Allow reset with 'R' only when panel is visible
            if (_isVisible && kb.rKey.wasPressedThisFrame)
            {
                ResetRunAndReload();
            }
#else
            if (Input.GetKeyDown(KeyCode.P))
            {
                _isVisible = !_isVisible;
            }

            if (_isVisible && Input.GetKeyDown(KeyCode.R))
            {
                ResetRunAndReload();
            }
#endif
        }

        private void ResetRunAndReload()
        {
            // 0. Manual Cleanup of stubborn Singletons (Pooling, MetaStats, etc)
            // This prevents "Duplicate Instance" errors or stale pools on reload.
            if (Geneforge.Core.Pooling.PoolManager.Instance != null)
            {
                Destroy(Geneforge.Core.Pooling.PoolManager.Instance.gameObject);
            }

            var oldMeta = FindAnyObjectByType<Geneforge.Core.Stats.MetaStats>();
            if (oldMeta != null)
            {
                Destroy(oldMeta.gameObject);
            }

            // 1. Try to use the official Flow Controller (AAA way) - Restarts full run state
            if (Geneforge.Gameplay.Progression.RunFlowController.Instance != null)
            {
                Debug.Log("[DebugStats] Restarting run via RunFlowController.");
                Geneforge.Gameplay.Progression.RunFlowController.Instance.StartNewRun();
                return;
            }

            // 2. Fallback for testing/uninitialized scenes
            Debug.Log("[DebugStats] Fallback restart (Reload Scene + Clear Persistence).");
            
            // Clear items/timeline
            var persistence = Geneforge.Gameplay.Items.RunPersistenceManager.Instance;
            if (persistence != null)
            {
                persistence.ClearRun();
            }

            // Reset stats (HP, etc) if session exists
            if (Geneforge.Gameplay.Progression.RunSession.Instance != null)
            {
                var meta = FindAnyObjectByType<Geneforge.Core.Stats.MetaStats>();
                Geneforge.Gameplay.Progression.RunSession.Instance.BeginRun(meta);
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            // Scale UI for high DPI screens
            float scale = Screen.height / 1080f; // Baseline 1080p
            if (scale < 1f) scale = 1f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            // Increase font sizes
            GUI.skin.window.fontSize = 20;
            GUI.skin.label.fontSize = 18;
            GUI.skin.button.fontSize = 18;
            GUI.skin.label.richText = true;

            // Wider window for 4 columns
            float width = 1400;
            float height = 650;
            
            // Center the window horizontally at the top of the screen
            float xPos = ((Screen.width / scale) - width) / 2f;
            _windowRect = new Rect(xPos, 50, width, height);
            
            _windowRect = GUI.Window(9999, _windowRect, DrawWindow, "Debug Stats (Press P)");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Label("<color=yellow>Press 'R' to Reset Run & Reload</color>");
            GUILayout.Space(15);
            
            GUILayout.BeginHorizontal();

            // --- Column 1: Run Stats ---
            GUILayout.BeginVertical(GUILayout.Width(350)); // Fixed width for neatness
            DrawRunStats();
            GUILayout.EndVertical();

            GUILayout.Space(20);

            // --- Column 2: Weapon Stats ---
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawWeaponStats();
            GUILayout.EndVertical();

            GUILayout.Space(20);

            // --- Column 3: Inventory ---
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawInventory();
            GUILayout.EndVertical();

            GUILayout.Space(20);

            // --- Column 4: Meta Stats ---
            GUILayout.BeginVertical(); // Takes remaining space
            DrawMetaStats();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawRunStats()
        {
            GUILayout.Label("<size=22><b>Run Stats</b></size>");
            GUILayout.Space(10);
            
            // Access RunStats via RunSession (Global) or fallback to finding it
            RunStats stats = null;
            
            // 1. Try RunSession first
            if (Geneforge.Gameplay.Progression.RunSession.Instance != null)
            {
                stats = Geneforge.Gameplay.Progression.RunSession.Instance.Run;
            }
            
            // 2. If null, try finding on Player (assuming Player has StatsManager or similar)
            if (stats == null)
            {
                var player = GetPlayer();
                if (player != null)
                {
                    stats = player.GetComponentInChildren<RunStats>();
                }
            }

            // 3. Global Fallback
            if (stats == null)
            {
                stats = FindAnyObjectByType<RunStats>();
            }

            if (stats != null)
            {
                GUILayout.Label($"HP: <color=green>{stats.CurrentHP:F1} / {stats.MaxHP:F1}</color>");
                GUILayout.Label($"Lives: {stats.Lives}");
                GUILayout.Label($"Gold: <color=yellow>{stats.Currency}</color>");
                GUILayout.Label($"DNA: <color=cyan>{stats.DnaSplices}</color>");
                GUILayout.Label($"Rolls: {stats.Rolls}");
                GUILayout.Label($"Speed: <color=white>{stats.MoveSpeedMultiplier:F2}x</color>");
                GUILayout.Label($"Luck: <color=purple>{stats.Luck:F2}</color>");
            }
            else
            {
                GUILayout.Label("<color=red>RunStats not found</color>");
            }
        }

        private void DrawWeaponStats()
        {
            GUILayout.Label("<size=22><b>Weapon Stats</b></size>");
             GUILayout.Space(10);

            // Look for GunSlots on Player first, then globally
            GunSlots gunSlots = null;
            
            Transform player = GetPlayer();
            if (player != null)
                gunSlots = player.GetComponentInChildren<GunSlots>();
            
            // Global Fallback
            if (gunSlots == null)
            {
                gunSlots = FindAnyObjectByType<GunSlots>();
            }

            if (gunSlots != null && gunSlots.ActiveStats != null)
            {
                var s = gunSlots.ActiveStats;
                GUILayout.Label($"Damage: {s.Damage:F1}");
                GUILayout.Label($"Fire Rate: {s.FireRate:F2}s");
                GUILayout.Label($"Speed: {s.ProjectileSpeed:F1}");
                GUILayout.Label($"Size: {s.ProjectileSize:F2}");
                GUILayout.Label($"Multishot: {s.ProjectilesPerShot}");
                GUILayout.Label($"Spread: {s.SpreadAngle:F1}");
                GUILayout.Label($"Knockback: {s.KnockbackForce:F1}");
                GUILayout.Label($"Crit: {s.CritChance*100:F1}% (x{s.CritMultiplier:F1})");
                GUILayout.Label($"Pierce: {s.PierceCount}");
                GUILayout.Label($"Bounce: {s.BounceCount}");
                GUILayout.Label($"Homing: {s.HomingStrength:F2}");
            }
            else
            {
                GUILayout.Label($"<color=grey>No GunSlots Found {(player!=null?"(Checked Player & Global)":"(Global Search)")}</color>");
            }
        }

        private void DrawInventory()
        {
            GUILayout.Label("<size=22><b>Inventory</b></size>");
             GUILayout.Space(10);

            Transform player = GetPlayer();
            if (player == null) return;

            var inventory = player.GetComponent<RunInventory>();
            if (inventory != null)
            {
                GUILayout.Label($"Total Items: {inventory.CollectedItems.Count}");
                foreach (var item in inventory.CollectedItems)
                {
                    if (item != null)
                        GUILayout.Label($"- <color=white>{item.ItemName}</color> <size=14><color=grey>({item.Rarity})</color></size>");
                }
            }
            else
            {
                GUILayout.Label("<color=grey>No RunInventory</color>");
            }
        }

        private void DrawMetaStats()
        {
            GUILayout.Label("<size=22><b>Meta Stats</b></size>");
            GUILayout.Space(10);

            var meta = MetaStats.Instance != null ? MetaStats.Instance : FindAnyObjectByType<MetaStats>();

            if (meta != null)
            {
                GUILayout.Label($"Banked DNA: <color=cyan>{meta.TotalDnaSplices}</color>");
                GUILayout.Label($"Banked Essence: <color=yellow>{meta.Essence}</color>");
                GUILayout.Label($"Starting Lives: {meta.StartingLives}");
                
                GUILayout.Space(10);
                if (GUILayout.Button("Add 10 DNA")) meta.AddDnaSplices(10);
                if (GUILayout.Button("Add 100 Essence")) meta.AddEssence(100);
            }
            else
            {
                GUILayout.Label("<color=red>MetaStats not found</color>");
            }
        }

        private Transform GetPlayer()
        {
            if (DungeonMapManager.Instance != null && DungeonMapManager.Instance.Player != null)
                return DungeonMapManager.Instance.Player;
            
            // Fallback
            var p = GameObject.FindGameObjectWithTag("Player");
            return p ? p.transform : null;
        }
    }
}
