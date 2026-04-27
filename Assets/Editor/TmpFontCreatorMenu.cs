using UnityEditor;

namespace HiddenCats.Editor
{
    /// <summary>
    /// Shortcut to TMP Font Asset Creator. Use with Characters from File:
    /// Assets/Font/Source/Noto/CharSets/Char.txt
    /// </summary>
    public static class TmpFontCreatorMenu
    {
        private const string MenuPath = "Tools/Hidden Cats/TextMeshPro/Font Asset Creator";

        [MenuItem(MenuPath, priority = 10)]
        public static void OpenFontAssetCreator()
        {
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
        }
    }
}
