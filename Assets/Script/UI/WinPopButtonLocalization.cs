using HiddenCats.Core;
using TMPro;
using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Sets ButtonWin label under WinPop from <see cref="LocalizationManager"/> (key: <see cref="Key"/>).
    /// </summary>
    public static class WinPopButtonLocalization
    {
        public const string Key = "smallgame.win_button";

        public static void Apply(Transform winPopRoot)
        {
            if (winPopRoot == null || LocalizationManager.Instance == null)
            {
                return;
            }

            Transform btnTr = FindChildRecursive(winPopRoot, "ButtonWin");
            if (btnTr == null)
            {
                return;
            }

            TMP_Text tmp = btnTr.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null)
            {
                return;
            }

            tmp.text = LocalizationManager.Instance.GetText(Key);
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name || root.name.StartsWith(name + "("))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
