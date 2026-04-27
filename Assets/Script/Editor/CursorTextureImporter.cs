using UnityEngine;
using UnityEditor;
using System.IO;

namespace HiddenCats.Core.Editor
{
    /// <summary>
    /// Automatically configures cursor textures with correct import settings.
    /// This ensures cursor textures are readable for Cursor.SetCursor().
    /// </summary>
    public class CursorTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            TextureImporter textureImporter = (TextureImporter)assetImporter;
            
            // Check if this is a cursor texture in the Resources/Cursor folder
            if (assetPath.Contains("Resources/Cursor/") && 
                (assetPath.Contains("MouseX1") || assetPath.Contains("MouseX2")))
            {
                ConfigureCursorTexture(textureImporter, assetPath);
            }
        }

        private static void ConfigureCursorTexture(TextureImporter textureImporter, string assetPath)
        {
            // Enable Read/Write for cursor textures (required for Cursor.SetCursor)
            textureImporter.isReadable = true;
            
            // Set texture type to Default (not Sprite) for cursor usage
            textureImporter.textureType = TextureImporterType.Default;
            
            // Disable mipmaps for cursor textures (required for cursor)
            textureImporter.mipmapEnabled = false;
            
            // Enable alpha transparency
            textureImporter.alphaIsTransparency = true;
            
            // Set texture format to RGBA32 for all platforms (required for cursor)
            // This ensures the texture is in the correct format for Cursor.SetCursor()
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            
            // Configure platform-specific settings to use RGBA32
            var platformSettings = textureImporter.GetDefaultPlatformTextureSettings();
            platformSettings.format = TextureImporterFormat.RGBA32;
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            
            // Apply settings for all platforms
            textureImporter.SetPlatformTextureSettings(platformSettings);
            
            Debug.Log($"[CursorTextureImporter] Configured cursor texture: {assetPath} (RGBA32, Readable, No Mipmaps)");
        }

        [MenuItem("Tools/Fix Cursor Textures Import Settings")]
        private static void FixCursorTextures()
        {
            string cursorFolder = "Assets/Resources/Cursor";
            
            if (!Directory.Exists(cursorFolder))
            {
                Debug.LogError($"[CursorTextureImporter] Cursor folder not found: {cursorFolder}");
                return;
            }

            string[] cursorFiles = { "MouseX1.png", "MouseX2.png" };
            int fixedCount = 0;

            foreach (string fileName in cursorFiles)
            {
                string assetPath = Path.Combine(cursorFolder, fileName).Replace('\\', '/');
                
                if (File.Exists(assetPath))
                {
                    TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                    if (textureImporter != null)
                    {
                        bool needsReimport = false;
                        
                        // Check if any settings need to be changed
                        if (!textureImporter.isReadable)
                        {
                            needsReimport = true;
                        }
                        
                        if (textureImporter.textureType != TextureImporterType.Default)
                        {
                            needsReimport = true;
                        }
                        
                        if (textureImporter.mipmapEnabled)
                        {
                            needsReimport = true;
                        }
                        
                        if (!textureImporter.alphaIsTransparency)
                        {
                            needsReimport = true;
                        }
                        
                        // Check texture format
                        var platformSettings = textureImporter.GetDefaultPlatformTextureSettings();
                        if (platformSettings.format != TextureImporterFormat.RGBA32)
                        {
                            needsReimport = true;
                        }
                        
                        if (needsReimport)
                        {
                            ConfigureCursorTexture(textureImporter, assetPath);
                            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                            fixedCount++;
                            Debug.Log($"[CursorTextureImporter] Fixed: {assetPath}");
                        }
                        else
                        {
                            Debug.Log($"[CursorTextureImporter] Already configured: {assetPath}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[CursorTextureImporter] File not found: {assetPath}");
                }
            }

            AssetDatabase.Refresh();
            
            if (fixedCount > 0)
            {
                Debug.Log($"[CursorTextureImporter] Fixed {fixedCount} cursor texture(s). Please restart the game to see the changes.");
                EditorUtility.DisplayDialog("Cursor Textures Fixed", 
                    $"Fixed {fixedCount} cursor texture(s).\n\nPlease restart the game to see the changes.", 
                    "OK");
            }
            else
            {
                Debug.Log("[CursorTextureImporter] All cursor textures are already properly configured.");
            }
        }
    }
}
