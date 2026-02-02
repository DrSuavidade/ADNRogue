using UnityEngine;
using UnityEditor;
using Geneforge.Gameplay.Map;
using System.IO;

namespace Geneforge.Editor
{
    /// <summary>
    /// Automatically assigns minimap icons to room prefabs.
    /// Menu: Tools > Assign Minimap Icons to Room Prefabs
    /// </summary>
    public class MinimapIconAssigner : UnityEditor.Editor
    {
        [MenuItem("Tools/Assign Minimap Icons to Room Prefabs")]
        public static void AssignMinimapIcons()
        {
            Debug.Log("[MinimapIconAssigner] Starting assignment process...");

            string iconsFolderPath = "Assets/Resources/Prefabs/MiniMap/Icones";
            string roomPrefabsFolderPath = "Assets/Resources/WorldGenAssets/Prefabs/RoomS";

            // Find all room prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { roomPrefabsFolderPath });
            int assignedCount = 0;
            int skippedCount = 0;

            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null) continue;

                // Get the RoomInstance component
                RoomInstance roomInstance = prefab.GetComponent<RoomInstance>();
                if (roomInstance == null)
                {
                    Debug.LogWarning($"[MinimapIconAssigner] Prefab '{prefab.name}' has no RoomInstance component. Skipping.");
                    skippedCount++;
                    continue;
                }

                // Try to find a matching sprite
                string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                string spritePath = $"{iconsFolderPath}/{prefabName}.png";

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                if (sprite == null)
                {
                    Debug.LogWarning($"[MinimapIconAssigner] No sprite found at '{spritePath}' for prefab '{prefabName}'. Skipping.");
                    skippedCount++;
                    continue;
                }

                // Assign the sprite using SerializedObject to ensure it's saved
                SerializedObject serializedPrefab = new SerializedObject(roomInstance);
                SerializedProperty minimapIconProperty = serializedPrefab.FindProperty("minimapIcon");

                if (minimapIconProperty != null)
                {
                    minimapIconProperty.objectReferenceValue = sprite;
                    serializedPrefab.ApplyModifiedProperties();

                    Debug.Log($"[MinimapIconAssigner] ✅ Assigned '{sprite.name}' to '{prefab.name}'");
                    assignedCount++;

                    // Mark prefab as dirty and save
                    EditorUtility.SetDirty(prefab);
                }
                else
                {
                    Debug.LogError($"[MinimapIconAssigner] Could not find 'minimapIcon' property on '{prefab.name}'");
                    skippedCount++;
                }
            }

            // Save all changes
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MinimapIconAssigner] ✅ DONE! Assigned {assignedCount} icons, skipped {skippedCount} prefabs.");
            EditorUtility.DisplayDialog(
                "Minimap Icon Assignment Complete",
                $"Successfully assigned {assignedCount} minimap icons to room prefabs.\n\nSkipped: {skippedCount}",
                "OK"
            );
        }

        [MenuItem("Tools/Verify Minimap Sprite Import Settings")]
        public static void VerifySpriteSettings()
        {
            Debug.Log("[MinimapIconAssigner] Verifying sprite import settings...");

            string iconsFolderPath = "Assets/Resources/Prefabs/MiniMap/Icones";
            string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { iconsFolderPath });

            int fixedCount = 0;

            foreach (string guid in spriteGuids)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;

                if (importer == null) continue;

                bool needsUpdate = false;

                // Check and fix Texture Type
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    needsUpdate = true;
                    Debug.Log($"[MinimapIconAssigner] Fixed texture type for '{Path.GetFileName(spritePath)}'");
                }

                // Check and fix Sprite Mode
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    needsUpdate = true;
                }

                // Ensure readable (for runtime use)
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
                    fixedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fixedCount > 0)
            {
                Debug.Log($"[MinimapIconAssigner] ✅ Fixed {fixedCount} sprite import settings.");
                EditorUtility.DisplayDialog(
                    "Sprite Import Settings Fixed",
                    $"Fixed import settings for {fixedCount} sprites.\n\nThey are now properly configured as UI Sprites.",
                    "OK"
                );
            }
            else
            {
                Debug.Log("[MinimapIconAssigner] ✅ All sprites are already correctly configured.");
                EditorUtility.DisplayDialog(
                    "Sprite Import Settings OK",
                    "All minimap icon sprites are already correctly configured.",
                    "OK"
                );
            }
        }
    }
}
