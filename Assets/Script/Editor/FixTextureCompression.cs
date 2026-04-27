using UnityEngine;
using UnityEditor;

public class FixTextureCompression : MonoBehaviour
{
    [MenuItem("Tools/Fix Sprite Compression")]
    static void FixCompression()
    {
        // 需要修复的文件夹
        string[] folders = new string[]
        {
            "Assets/Texture/ChallengeRewardIcon",
            "Assets/Texture/RewardWnd",
            "Assets/Texture/IngameWnd",
            "Assets/Texture/SettingWnd"
        };

        int fixedCount = 0;
        foreach (string folder in folders)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    // 设置为无压缩或高质量
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                    fixedCount++;
                    Debug.Log($"Fixed: {path}");
                }
            }
        }
        Debug.Log($"Fixed {fixedCount} textures");
    }
}
