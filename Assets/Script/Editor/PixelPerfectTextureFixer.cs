using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using HiddenCats.Core;

namespace HiddenCats.Core.Editor
{
    /// <summary>
    /// Editor tool to automatically fix texture import settings for textures used by PixelPerfectClickDetector.
    /// Enables 'Read/Write Enabled' for all textures that need pixel-perfect click detection.
    /// </summary>
    public class PixelPerfectTextureFixer
    {
        [MenuItem("Tools/Fix Pixel Perfect Click Detector Textures")]
        private static void FixPixelPerfectTextures()
        {
            List<string> texturePaths = new List<string>();
            HashSet<string> processedTextures = new HashSet<string>();

            // Find all PixelPerfectClickDetector components in currently loaded scenes
            Debug.Log("[PixelPerfectTextureFixer] Scanning loaded scenes for PixelPerfectClickDetector components...");
            PixelPerfectClickDetector[] sceneDetectors = Object.FindObjectsByType<PixelPerfectClickDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[PixelPerfectTextureFixer] Found {sceneDetectors.Length} PixelPerfectClickDetector component(s) in loaded scenes.");
            foreach (PixelPerfectClickDetector detector in sceneDetectors)
            {
                CollectTextureFromDetector(detector, texturePaths, processedTextures);
            }

            // Find all PixelPerfectClickDetector components in prefabs
            Debug.Log("[PixelPerfectTextureFixer] Scanning prefabs for PixelPerfectClickDetector components...");
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            Debug.Log($"[PixelPerfectTextureFixer] Found {prefabGuids.Length} prefab(s) to scan.");
            int totalPrefabDetectors = 0;
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab != null)
                {
                    PixelPerfectClickDetector[] detectors = prefab.GetComponentsInChildren<PixelPerfectClickDetector>(true);
                    totalPrefabDetectors += detectors.Length;
                    foreach (PixelPerfectClickDetector detector in detectors)
                    {
                        CollectTextureFromDetector(detector, texturePaths, processedTextures);
                    }
                }
            }
            Debug.Log($"[PixelPerfectTextureFixer] Found {totalPrefabDetectors} PixelPerfectClickDetector component(s) in prefabs.");

            if (texturePaths.Count == 0)
            {
                Debug.LogWarning("[PixelPerfectTextureFixer] No textures found that need fixing. Make sure you have PixelPerfectClickDetector components in your scenes or prefabs.");
                EditorUtility.DisplayDialog("No Textures Found", 
                    "No textures used by PixelPerfectClickDetector were found.\n\nMake sure you have PixelPerfectClickDetector components in your scenes or prefabs.", 
                    "OK");
                return;
            }

            Debug.Log($"[PixelPerfectTextureFixer] Found {texturePaths.Count} unique texture(s) to check.");

            // Fix each texture
            int fixedCount = 0;
            int alreadyFixedCount = 0;
            int errorCount = 0;

            foreach (string texturePath in texturePaths)
            {
                if (FixTextureReadable(texturePath))
                {
                    fixedCount++;
                }
                else
                {
                    TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer != null && importer.isReadable)
                    {
                        alreadyFixedCount++;
                    }
                    else
                    {
                        errorCount++;
                        Debug.LogError($"[PixelPerfectTextureFixer] Failed to fix texture: {texturePath}");
                    }
                }
            }

            AssetDatabase.Refresh();

            // Show results
            string message = $"Texture Fix Results:\n\n" +
                           $"✓ Fixed: {fixedCount}\n" +
                           $"✓ Already OK: {alreadyFixedCount}\n" +
                           $"✗ Errors: {errorCount}\n\n" +
                           $"Total: {texturePaths.Count} texture(s)";

            Debug.Log($"[PixelPerfectTextureFixer] {message}");

            if (fixedCount > 0)
            {
                EditorUtility.DisplayDialog("Pixel Perfect Textures Fixed", 
                    message + "\n\nTextures have been reimported. The game should now work correctly.", 
                    "OK");
            }
            else if (errorCount > 0)
            {
                EditorUtility.DisplayDialog("Pixel Perfect Texture Fix", 
                    message + "\n\nSome textures could not be fixed. Check the Console for details.", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Pixel Perfect Textures", 
                    "All textures are already properly configured!", 
                    "OK");
            }
        }

        /// <summary>
        /// Collect texture path from a PixelPerfectClickDetector component.
        /// </summary>
        private static void CollectTextureFromDetector(PixelPerfectClickDetector detector, List<string> texturePaths, HashSet<string> processedTextures)
        {
            if (detector == null)
            {
                Debug.LogWarning("[PixelPerfectTextureFixer] Detector is null");
                return;
            }

            Image image = detector.GetComponent<Image>();
            if (image == null)
            {
                Debug.LogWarning($"[PixelPerfectTextureFixer] Image component not found on {detector.gameObject.name}");
                return;
            }

            if (image.sprite == null)
            {
                Debug.LogWarning($"[PixelPerfectTextureFixer] Sprite is null on Image component of {detector.gameObject.name}");
                return;
            }

            // Get sprite asset path first
            string spritePath = AssetDatabase.GetAssetPath(image.sprite);
            if (string.IsNullOrEmpty(spritePath))
            {
                Debug.LogWarning($"[PixelPerfectTextureFixer] Could not get asset path for sprite on {detector.gameObject.name}");
                return;
            }

            // Get the texture importer from the sprite's asset path
            // For sprites, the texture is usually in the same file
            TextureImporter textureImporter = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (textureImporter == null)
            {
                Debug.LogWarning($"[PixelPerfectTextureFixer] Could not get TextureImporter for sprite at {spritePath}");
                return;
            }

            // Use the sprite path as the texture path (they're the same file)
            string texturePath = spritePath;
            
            if (!string.IsNullOrEmpty(texturePath) && !processedTextures.Contains(texturePath))
            {
                processedTextures.Add(texturePath);
                texturePaths.Add(texturePath);
                Debug.Log($"[PixelPerfectTextureFixer] Found texture: {texturePath} (used by {detector.gameObject.name}, sprite: {image.sprite.name})");
            }
        }

        /// <summary>
        /// Fix a texture to be readable.
        /// </summary>
        private static bool FixTextureReadable(string texturePath)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            
            if (textureImporter == null)
            {
                Debug.LogWarning($"[PixelPerfectTextureFixer] Could not get TextureImporter for: {texturePath}");
                return false;
            }

            // Check if already readable
            if (textureImporter.isReadable)
            {
                return false; // Already fixed, no need to reimport
            }

            // Enable Read/Write
            textureImporter.isReadable = true;
            
            // Reimport the texture
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            
            Debug.Log($"[PixelPerfectTextureFixer] Fixed texture: {texturePath} (enabled Read/Write)");
            return true;
        }

        /// <summary>
        /// Check if a specific texture is readable (for validation).
        /// </summary>
        [MenuItem("Tools/Check Pixel Perfect Texture Status")]
        private static void CheckTextureStatus()
        {
            List<string> texturePaths = new List<string>();
            HashSet<string> processedTextures = new HashSet<string>();

            // Find all PixelPerfectClickDetector components in currently loaded scenes
            Debug.Log("[PixelPerfectTextureFixer] Scanning loaded scenes for PixelPerfectClickDetector components...");
            PixelPerfectClickDetector[] sceneDetectors = Object.FindObjectsByType<PixelPerfectClickDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[PixelPerfectTextureFixer] Found {sceneDetectors.Length} PixelPerfectClickDetector component(s) in loaded scenes.");
            foreach (PixelPerfectClickDetector detector in sceneDetectors)
            {
                CollectTextureFromDetector(detector, texturePaths, processedTextures);
            }

            // Find all textures used by PixelPerfectClickDetector in prefabs
            Debug.Log("[PixelPerfectTextureFixer] Scanning prefabs for PixelPerfectClickDetector components...");
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            Debug.Log($"[PixelPerfectTextureFixer] Found {prefabGuids.Length} prefab(s) to scan.");
            int totalPrefabDetectors = 0;
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab != null)
                {
                    PixelPerfectClickDetector[] detectors = prefab.GetComponentsInChildren<PixelPerfectClickDetector>(true);
                    totalPrefabDetectors += detectors.Length;
                    foreach (PixelPerfectClickDetector detector in detectors)
                    {
                        CollectTextureFromDetector(detector, texturePaths, processedTextures);
                    }
                }
            }
            Debug.Log($"[PixelPerfectTextureFixer] Found {totalPrefabDetectors} PixelPerfectClickDetector component(s) in prefabs.");

            if (texturePaths.Count == 0)
            {
                Debug.LogWarning("[PixelPerfectTextureFixer] No textures found. Make sure you have PixelPerfectClickDetector components in your scenes or prefabs with Image components that have sprites assigned.");
                EditorUtility.DisplayDialog("No Textures Found", 
                    "No textures used by PixelPerfectClickDetector were found.\n\nMake sure:\n" +
                    "1. You have PixelPerfectClickDetector components in your scenes or prefabs\n" +
                    "2. The Image components have sprites assigned\n" +
                    "3. The scenes are loaded in the editor", 
                    "OK");
                return;
            }

            // Check status
            int readableCount = 0;
            int notReadableCount = 0;

            Debug.Log($"[PixelPerfectTextureFixer] Checking {texturePaths.Count} texture(s)...");
            foreach (string texturePath in texturePaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    if (importer.isReadable)
                    {
                        readableCount++;
                        Debug.Log($"[PixelPerfectTextureFixer] ✓ {texturePath} - Readable");
                    }
                    else
                    {
                        notReadableCount++;
                        Debug.LogWarning($"[PixelPerfectTextureFixer] ✗ {texturePath} - NOT Readable (needs fixing)");
                    }
                }
            }

            string statusMessage = $"Status: {readableCount} readable, {notReadableCount} need fixing (out of {texturePaths.Count} total)";
            Debug.Log($"[PixelPerfectTextureFixer] {statusMessage}");
            
            EditorUtility.DisplayDialog("Pixel Perfect Texture Status", statusMessage, "OK");
        }
    }
}
