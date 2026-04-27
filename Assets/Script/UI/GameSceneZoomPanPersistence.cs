using System.Collections.Generic;
using UnityEngine;
using HiddenCats.Core;

namespace HiddenCats.UI
{
    /// <summary>
    /// 在会话内记住 RoomWnd 的缩放与平移，切换窗口后再进入可恢复。
    /// 「重置游戏进度」会清空全部记忆。
    /// </summary>
    internal static class GameSceneZoomPanPersistence
    {
        private struct Entry
        {
            public Vector3 LocalScale;
            public Vector2 AnchoredPosition;
        }

        private static readonly Dictionary<string, Entry> ByWindowKey = new Dictionary<string, Entry>();

        static GameSceneZoomPanPersistence()
        {
            GameProgressResetService.OnGameProgressReset += ClearAll;
        }

        public static void Save(string windowKey, Vector3 localScale, Vector2 anchoredPosition)
        {
            if (string.IsNullOrEmpty(windowKey))
            {
                return;
            }

            ByWindowKey[windowKey] = new Entry
            {
                LocalScale = localScale,
                AnchoredPosition = anchoredPosition
            };
        }

        public static bool TryGet(string windowKey, out Vector3 localScale, out Vector2 anchoredPosition)
        {
            localScale = Vector3.one;
            anchoredPosition = Vector2.zero;
            if (string.IsNullOrEmpty(windowKey) || !ByWindowKey.TryGetValue(windowKey, out Entry e))
            {
                return false;
            }

            localScale = e.LocalScale;
            anchoredPosition = e.AnchoredPosition;
            return true;
        }

        public static void Clear(string windowKey)
        {
            if (!string.IsNullOrEmpty(windowKey))
            {
                ByWindowKey.Remove(windowKey);
            }
        }

        public static void ClearAll()
        {
            ByWindowKey.Clear();
        }
    }
}
